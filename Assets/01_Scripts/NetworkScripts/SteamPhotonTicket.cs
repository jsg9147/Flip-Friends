using System;
using System.Threading;
using System.Threading.Tasks;
using Photon.Realtime;
using Steamworks;

namespace FlipFriends.Network
{
    public sealed class SteamPhotonTicket : IDisposable
    {
        public const string Identity = "photon";
        private const int TimeoutMilliseconds = 15000;
        private Callback<GetTicketForWebApiResponse_t> callback;
        private HAuthTicket handle = HAuthTicket.Invalid;
        private TaskCompletionSource<AuthenticationValues> completion;
        private bool isDisposed;

        public async Task<AuthenticationValues> AcquireAsync(uint expectedAppId, CancellationToken cancellationToken)
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(SteamPhotonTicket));
            if (completion != null) throw new InvalidOperationException("인증 티켓을 이미 요청했습니다.");
            if (expectedAppId == 0 || expectedAppId == 480)
                throw new InvalidOperationException("온라인 인증에는 실제 출시 Steam App ID가 필요합니다.");
            if (!SteamManager.Initialized || !SteamUser.BLoggedOn() || SteamUtils.GetAppID().m_AppId != expectedAppId)
                throw new InvalidOperationException("Steam 로그인과 대상 앱 실행 상태를 확인하세요.");

            cancellationToken.ThrowIfCancellationRequested();
            completion = new TaskCompletionSource<AuthenticationValues>();
            callback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicket);
            handle = SteamUser.GetAuthTicketForWebApi(Identity);
            try
            {
                if (handle == HAuthTicket.Invalid) throw new InvalidOperationException("Steam 인증 티켓 발급 요청이 실패했습니다.");
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeoutMilliseconds);
                    using (timeout.Token.Register(() => completion.TrySetCanceled()))
                        return await completion.Task;
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void OnTicket(GetTicketForWebApiResponse_t response)
        {
            if (isDisposed || response.m_hAuthTicket != handle) return;
            if (response.m_eResult != EResult.k_EResultOK || response.m_rgubTicket == null ||
                response.m_cubTicket <= 0 || response.m_cubTicket > response.m_rgubTicket.Length)
            {
                completion.TrySetException(new InvalidOperationException("Steam 인증 티켓 발급이 거부됐습니다."));
                return;
            }

            var authentication = new AuthenticationValues { AuthType = CustomAuthenticationType.Steam };
            // 계정 식별은 Photon 공급자가 검증한 결과를 사용하며 티켓을 로그에 남기지 않는다.
            authentication.AddAuthParameter("ticket", BitConverter.ToString(response.m_rgubTicket, 0, response.m_cubTicket).Replace("-", "").ToLowerInvariant());
            completion.TrySetResult(authentication);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            callback?.Dispose();
            callback = null;
            if (handle != HAuthTicket.Invalid && SteamManager.Initialized)
                SteamUser.CancelAuthTicket(handle);
            handle = HAuthTicket.Invalid;
            completion?.TrySetCanceled();
        }
    }
}
