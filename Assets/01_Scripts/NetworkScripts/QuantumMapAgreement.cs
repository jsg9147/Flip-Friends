using System;
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

        public static async Task WaitAsync(RealtimeClient client, string agreement, CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(agreement)) throw new InvalidOperationException("로컬 맵 확인이 끝나지 않았습니다.");
            client.LocalPlayer.SetCustomProperties(new Photon.Client.PhotonHashtable { [Property] = agreement });
            using var service = new ConnectionServiceScope(client);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(15000);
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (!client.IsConnectedAndReady || client.CurrentRoom == null || client.CurrentRoom.PlayerCount > 2)
                    throw new InvalidOperationException("맵 확인 중 연결 또는 두 참가자가 유지되지 않았습니다.");
                // 두 번째 입장 이벤트가 아직 전달되지 않은 경우에는 시작하지 않고 기다린다.
                bool isReady = client.CurrentRoom.PlayerCount == 2;
                foreach (var player in client.CurrentRoom.Players.Values)
                {
                    if (!player.CustomProperties.TryGetValue(Property, out var value)) { isReady = false; continue; }
                    RequireMatch(agreement, value as string);
                }
                if (isReady) return;
                await Task.Delay(50, timeout.Token);
            }
        }
    }
}
