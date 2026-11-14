using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlipFriends.Maps;
using Photon.Client;
using Photon.Realtime;

namespace FlipFriends.Network
{
    internal sealed class QuantumSavedMapTransfer
    {
        private const string Protocol = "ff-saved-map-1";
        private const int ChunkBytes = 8192;
        private const int TimeoutMilliseconds = 30000;
        private const byte Hello = 71;
        private const byte Manifest = 72;
        private const byte Available = 73;
        private const byte Missing = 74;
        private const byte Chunk = 75;

        private readonly RealtimeClient client;
        private readonly MapEditorPalette palette;
        private readonly MapSessionSnapshot source;
        private readonly int host;
        private readonly Dictionary<int, int> offsets = new();
        private string mapId;
        private string hash;
        private byte[] received;
        private int receivedLength;
        private QuantumMapConversion conversion;
        private Exception failure;

        public QuantumSavedMapTransfer(RealtimeClient client, MapEditorPalette palette, MapSessionSnapshot source)
        {
            this.client = client;
            this.palette = palette;
            this.source = source;
            host = client.CurrentRoom.MasterClientId;
        }

        public static QuantumMapConversion Validate(string id, string hash, byte[] bytes, MapEditorPalette palette)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MapSessionSnapshot.MaximumContentBytes)
                throw new InvalidOperationException("수신 맵 크기가 허용 범위를 벗어났습니다.");
            if (!MapSessionContentValidator.TryValidate(id, hash, bytes, palette, out var data, out string error))
                throw new InvalidOperationException(error);
            // 정규 바이트 비교로 전송 중 구버전 마이그레이션이나 ID 정규화가 일어나지 않게 한다.
            if (!bytes.SequenceEqual(System.Text.Encoding.UTF8.GetBytes(MapDataRepository.ToJson(data))))
                throw new InvalidOperationException("맵이 정규 MapData 2.1 형식이 아닙니다.");
            return QuantumMapConverter.Convert(data, palette, enableRespawn: true);
        }

        public async Task<(QuantumMapConversion Conversion, string Hash)> RunAsync(
            QuantumMapConversion local, int count, Action connected, CancellationToken cancellation)
        {
            bool isHost = source != null;
            if (isHost != (client.LocalPlayer.ActorNumber == host) || client.CurrentRoom.MaxPlayers != count)
                throw new InvalidOperationException("커스텀 방의 방장 또는 참가 인원이 일치하지 않습니다.");

            client.EventReceived += OnEvent;
            using var service = new ConnectionServiceScope(client);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeoutMilliseconds);
            try
            {
                connected?.Invoke();
                if (!isHost) Send(Hello, Protocol, host);
                while (true)
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    if (failure != null) throw failure;
                    if (!client.InRoom || client.CurrentRoom.MasterClientId != host)
                        throw new InvalidOperationException("맵 공유 중 방 연결이 끊겼습니다.");

                    if (!isHost && conversion != null) return (conversion, hash);
                    if (isHost)
                    {
                        SendNextChunk();
                        if (client.CurrentRoom.PlayerCount == count && offsets.Count == count - 1 &&
                            offsets.Values.All(offset => offset == source.ByteLength))
                            return (local, source.ContentHash);
                    }

                    await Task.Delay(30, timeout.Token);
                }
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new InvalidOperationException("맵 보유 확인 또는 전송 시간이 초과됐습니다.");
            }
            finally
            {
                client.EventReceived -= OnEvent;
                received = null;
            }
        }

        private void OnEvent(EventData message)
        {
            if (failure != null || message.Code < Hello || message.Code > Chunk ||
                message.Sender == client.LocalPlayer.ActorNumber ||
                client.CurrentRoom?.Players.ContainsKey(message.Sender) != true) return;

            try
            {
                if (source != null) HandleGuest(message);
                else if (message.Sender == host) HandleHost(message);
            }
            catch (Exception error)
            {
                failure = error;
            }
        }

        private void HandleGuest(EventData message)
        {
            int actor = message.Sender;
            if (message.Code == Hello && !offsets.ContainsKey(actor))
            {
                if (message.CustomData as string != Protocol)
                    throw new InvalidOperationException("친구 맵 공유 프로토콜 버전이 다릅니다.");
                offsets.Add(actor, -1);
                Send(Manifest, new object[] { Protocol, MapData.CurrentVersion, source.MapId, source.ContentHash, source.ByteLength }, actor);
                return;
            }

            if ((message.Code == Available || message.Code == Missing) &&
                offsets.TryGetValue(actor, out int offset) && offset == -1)
            {
                if (message.CustomData as string != source.ContentHash)
                    throw new InvalidOperationException("참가자가 다른 맵 해시로 응답했습니다.");
                offsets[actor] = message.Code == Available ? source.ByteLength : 0;
            }
        }

        private void HandleHost(EventData message)
        {
            if (message.Code == Manifest && hash == null)
            {
                if (!(message.CustomData is object[] values) || values.Length != 5 ||
                    values[0] as string != Protocol || values[1] as string != MapData.CurrentVersion ||
                    !(values[2] is string id) || !MapData.TryNormalizeMapId(id, out string normalized) || normalized != id ||
                    !(values[3] is string expectedHash) || expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit) ||
                    !(values[4] is int length) || length <= 0 || length > MapSessionSnapshot.MaximumContentBytes)
                    throw new InvalidOperationException("친구 맵의 버전, ID, 해시 또는 크기가 올바르지 않습니다.");

                mapId = id;
                hash = expectedHash;
                var entry = new SavedMapCatalog(palette, isQuantum: true).FindById(id);
                if (entry?.CanSelect == true &&
                    MapSessionSnapshot.TryCreate(0, entry.Json, MapSessionSnapshot.MaximumContentBytes, palette, out var snapshot, out _) &&
                    snapshot.ContentHash == hash && snapshot.ByteLength == length)
                {
                    conversion = Validate(id, hash, snapshot.Content, palette);
                    Send(Available, hash, host);
                    return;
                }

                received = new byte[length];
                Send(Missing, hash, host);
            }
            else if (message.Code == Chunk)
            {
                if (received == null || !(message.CustomData is object[] values) || values.Length != 2 ||
                    !(values[0] is int offset) || offset != receivedLength ||
                    !(values[1] is byte[] bytes) || bytes.Length != Math.Min(ChunkBytes, received.Length - receivedLength) ||
                    bytes.Length == 0)
                    throw new InvalidOperationException("친구 맵의 청크 순서 또는 길이가 일치하지 않습니다.");

                Buffer.BlockCopy(bytes, 0, received, receivedLength, bytes.Length);
                receivedLength += bytes.Length;
                if (receivedLength == received.Length)
                    conversion = Validate(mapId, hash, received, palette);
            }
        }

        private void SendNextChunk()
        {
            foreach (var pair in offsets.ToArray())
            {
                int offset = pair.Value;
                if (offset < 0 || offset >= source.ByteLength) continue;
                var bytes = new byte[Math.Min(ChunkBytes, source.ByteLength - offset)];
                Buffer.BlockCopy(source.Content, offset, bytes, 0, bytes.Length);
                Send(Chunk, new object[] { offset, bytes }, pair.Key);
                offsets[pair.Key] += bytes.Length;
            }
        }

        private void Send(byte code, object payload, int actor)
        {
            if (!client.OpRaiseEvent(code, payload, new RaiseEventArgs { TargetActors = new[] { actor } }, SendOptions.SendReliable))
                throw new InvalidOperationException("친구 맵 메시지를 보낼 수 없습니다.");
        }
    }
}
