using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Photon.Realtime;

namespace FlipFriends.Network
{
    public static class QuantumMapAgreement
    {
        private const string Property = "ff.quantum.map.v1";

        public static void RequireMatch(string local, string remote)
        {
            if (string.IsNullOrEmpty(local) || !string.Equals(local, remote, StringComparison.Ordinal))
                throw new InvalidOperationException("맵 해시·변환 버전·팔레트 버전이 다릅니다.");
        }

        public static async Task WaitAsync(RealtimeClient client, string agreement, int playerCount, CancellationToken cancellation,
            bool closeRoom = false, string[] expectedUsers = null)
        {
            if (string.IsNullOrEmpty(agreement)) throw new InvalidOperationException("로컬 맵 확인이 끝나지 않았습니다.");
            client.LocalPlayer.SetCustomProperties(new Photon.Client.PhotonHashtable { [Property] = agreement });
            using var service = new ConnectionServiceScope(client);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(15000);
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                if (timeout.IsCancellationRequested) throw new InvalidOperationException("참가자 맵 합의 시간이 초과됐습니다.");
                if (!client.IsConnectedAndReady || client.CurrentRoom == null || client.CurrentRoom.PlayerCount > playerCount)
                    throw new InvalidOperationException("맵 확인 중 연결 또는 참가 인원이 유지되지 않았습니다.");
                // 마지막 입장 이벤트가 아직 전달되지 않은 경우에는 시작하지 않고 기다린다.
                bool isReady = client.CurrentRoom.PlayerCount == playerCount;
                foreach (var player in client.CurrentRoom.Players.Values)
                {
                    if (expectedUsers != null && !expectedUsers.Contains(player.UserId, StringComparer.Ordinal))
                        throw new InvalidOperationException("확정된 Steam 참가자와 Photon 참가자가 다릅니다.");
                    if (!player.CustomProperties.TryGetValue(Property, out var value)) { isReady = false; continue; }
                    RequireMatch(agreement, value as string);
                }
                if (isReady)
                {
                    if (!closeRoom) return;
                    if (client.LocalPlayer.IsMasterClient && client.CurrentRoom.IsOpen) client.CurrentRoom.IsOpen = false;
                    if (!client.CurrentRoom.IsOpen) return;
                }
                await Task.Delay(50, cancellation);
            }
        }
    }
}
