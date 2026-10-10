using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public static class MapContentHash
{
    public static string Compute(byte[] content)
    {
        if (content == null) return string.Empty;

        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(content);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash)
            builder.Append(value.ToString("x2"));
        return builder.ToString();
    }
}

public sealed class MapChunkAssembler
{
    private readonly uint generation;
    private readonly string transferId;
    private readonly string mapId;
    private readonly string contentHash;
    private readonly int byteLength;
    private readonly byte[][] chunks;
    private int receivedCount;
    private int receivedBytes;

    public MapChunkAssembler(
        uint generation,
        string transferId,
        string mapId,
        string contentHash,
        int byteLength,
        int chunkCount)
    {
        this.generation = generation;
        this.transferId = transferId;
        this.mapId = mapId;
        this.contentHash = contentHash;
        this.byteLength = byteLength;
        chunks = new byte[chunkCount][];
    }

    public bool Matches(
        uint valueGeneration,
        string valueTransferId,
        string valueMapId,
        string valueHash,
        int chunkCount) =>
        generation == valueGeneration &&
        transferId == valueTransferId &&
        mapId == valueMapId &&
        contentHash == valueHash &&
        chunks.Length == chunkCount;

    public bool TryAdd(int index, byte[] payload, int maximumBytes, out string error)
    {
        error = null;
        if (index < 0 || index >= chunks.Length || payload == null || payload.Length == 0)
        {
            error = "청크 인덱스 또는 payload가 올바르지 않습니다.";
            return false;
        }
        if (chunks[index] != null) return true;
        if (receivedBytes + payload.Length > byteLength ||
            receivedBytes + payload.Length > maximumBytes)
        {
            error = "청크 누적 크기가 manifest 한계를 초과했습니다.";
            return false;
        }

        chunks[index] = (byte[])payload.Clone();
        receivedBytes += payload.Length;
        receivedCount++;
        return true;
    }

    public bool IsComplete => receivedCount == chunks.Length;

    public bool TryAssemble(out byte[] result, out string error)
    {
        result = null;
        if (!IsComplete || receivedBytes != byteLength)
        {
            error = "모든 청크가 도착하지 않았거나 전체 길이가 일치하지 않습니다.";
            return false;
        }

        result = new byte[byteLength];
        int offset = 0;
        foreach (byte[] chunk in chunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        if (MapContentHash.Compute(result) != contentHash)
        {
            result = null;
            error = "재조립된 콘텐츠의 해시가 manifest와 일치하지 않습니다.";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class RoundRobinTransferQueue<T>
{
    private readonly Queue<T> items = new();

    public int Count => items.Count;

    public void Enqueue(T item) => items.Enqueue(item);

    public bool TryDequeue(out T item)
    {
        if (items.Count == 0)
        {
            item = default;
            return false;
        }

        item = items.Dequeue();
        return true;
    }

    public void Clear() => items.Clear();
}

public sealed class SelectionUploadLease
{
    public string TransferId { get; private set; }
    public string MapId { get; private set; }
    public string ContentHash { get; private set; }
    public bool IsActive { get; private set; }
    public double ExpiresAt { get; private set; } = double.PositiveInfinity;

    public void Begin(
        string transferId,
        string mapId,
        string contentHash,
        double expiresAt = double.PositiveInfinity)
    {
        TransferId = transferId;
        MapId = mapId;
        ContentHash = contentHash;
        ExpiresAt = expiresAt;
        IsActive = true;
    }

    public bool HasExpired(double now) => IsActive && now >= ExpiresAt;

    public bool Matches(string transferId, string mapId, string contentHash) =>
        IsActive &&
        TransferId == transferId &&
        MapId == mapId &&
        ContentHash == contentHash;

    public void Invalidate()
    {
        TransferId = null;
        MapId = null;
        ContentHash = null;
        ExpiresAt = double.PositiveInfinity;
        IsActive = false;
    }
}
