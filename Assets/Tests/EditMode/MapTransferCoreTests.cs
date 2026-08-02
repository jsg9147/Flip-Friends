using System.Linq;
using System.Text;
using NUnit.Framework;

public sealed class MapTransferCoreTests
{
    private const int MaximumBytes = 512 * 1024;
    private const string TransferId = "transfer-a";
    private const string MapId = "0123456789abcdef0123456789abcdef";

    [Test]
    public void ChunkAssembler_역순청크를원본순서로조립한다()
    {
        byte[] content = Encoding.UTF8.GetBytes("첫째-둘째-셋째");
        byte[][] chunks = Split(content, 7);
        var assembler = CreateAssembler(content, chunks.Length);

        for (int index = chunks.Length - 1; index >= 0; index--)
            Assert.That(assembler.TryAdd(index, chunks[index], MaximumBytes, out _), Is.True);

        Assert.That(assembler.TryAssemble(out byte[] result, out _), Is.True);
        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void ChunkAssembler_중복청크를한번만집계한다()
    {
        byte[] content = Encoding.UTF8.GetBytes("duplicate-safe");
        byte[][] chunks = Split(content, 5);
        var assembler = CreateAssembler(content, chunks.Length);

        Assert.That(assembler.TryAdd(0, chunks[0], MaximumBytes, out _), Is.True);
        Assert.That(assembler.TryAdd(0, chunks[0], MaximumBytes, out _), Is.True);
        Assert.That(assembler.IsComplete, Is.False);

        for (int index = 1; index < chunks.Length; index++)
            Assert.That(assembler.TryAdd(index, chunks[index], MaximumBytes, out _), Is.True);

        Assert.That(assembler.TryAssemble(out byte[] result, out _), Is.True);
        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void ChunkAssembler_누락된청크는완료되지않는다()
    {
        byte[] content = Encoding.UTF8.GetBytes("missing-chunk");
        byte[][] chunks = Split(content, 4);
        var assembler = CreateAssembler(content, chunks.Length);

        Assert.That(assembler.TryAdd(0, chunks[0], MaximumBytes, out _), Is.True);

        Assert.That(assembler.IsComplete, Is.False);
        Assert.That(assembler.TryAssemble(out _, out string error), Is.False);
        Assert.That(error, Does.Contain("모든 청크"));
    }

    [Test]
    public void ChunkAssembler_다른전송식별자를거부한다()
    {
        byte[] content = Encoding.UTF8.GetBytes("identity");
        var assembler = CreateAssembler(content, 1);

        Assert.That(
            assembler.Matches(
                1, "old-transfer", MapId, MapContentHash.Compute(content), 1),
            Is.False);
        Assert.That(
            assembler.Matches(
                2, TransferId, MapId, MapContentHash.Compute(content), 1),
            Is.False);
    }

    [Test]
    public void ChunkAssembler_누적크기초과를거부한다()
    {
        byte[] content = Encoding.UTF8.GetBytes("1234");
        var assembler = CreateAssembler(content, 2);

        Assert.That(assembler.TryAdd(0, new byte[] { 1, 2, 3 }, MaximumBytes, out _), Is.True);
        Assert.That(
            assembler.TryAdd(1, new byte[] { 4, 5 }, MaximumBytes, out string error),
            Is.False);
        Assert.That(error, Does.Contain("누적 크기"));
    }

    [Test]
    public void RoundRobinQueue_재등록된항목을다른대상뒤로보낸다()
    {
        var queue = new RoundRobinTransferQueue<string>();
        queue.Enqueue("player-a");
        queue.Enqueue("player-b");

        Assert.That(queue.TryDequeue(out string first), Is.True);
        queue.Enqueue(first);
        Assert.That(queue.TryDequeue(out string second), Is.True);
        Assert.That(queue.TryDequeue(out string third), Is.True);

        Assert.That(new[] { first, second, third },
            Is.EqualTo(new[] { "player-a", "player-b", "player-a" }));
    }

    [Test]
    public void RoundRobinQueue_취소하면대기항목을모두제거한다()
    {
        var queue = new RoundRobinTransferQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        queue.Clear();

        Assert.That(queue.Count, Is.Zero);
        Assert.That(queue.TryDequeue(out _), Is.False);
    }

    [Test]
    public void SelectionUploadLease_무효화후늦은청크식별자를거부한다()
    {
        var lease = new SelectionUploadLease();
        string hash = MapContentHash.Compute(Encoding.UTF8.GetBytes("map"));
        lease.Begin(TransferId, MapId, hash);

        Assert.That(lease.Matches(TransferId, MapId, hash), Is.True);
        lease.Invalidate();

        Assert.That(lease.IsActive, Is.False);
        Assert.That(lease.Matches(TransferId, MapId, hash), Is.False);
    }

    [Test]
    public void SelectionUploadLease_새업로드는이전식별자를거부한다()
    {
        var lease = new SelectionUploadLease();
        lease.Begin("old", MapId, "old-hash");
        lease.Begin("new", MapId, "new-hash");

        Assert.That(lease.Matches("old", MapId, "old-hash"), Is.False);
        Assert.That(lease.Matches("new", MapId, "new-hash"), Is.True);
    }

    private static MapChunkAssembler CreateAssembler(byte[] content, int chunkCount)
    {
        return new MapChunkAssembler(
            1,
            TransferId,
            MapId,
            MapContentHash.Compute(content),
            content.Length,
            chunkCount);
    }

    private static byte[][] Split(byte[] content, int chunkBytes)
    {
        return content
            .Select((value, index) => new { value, index })
            .GroupBy(item => item.index / chunkBytes)
            .Select(group => group.Select(item => item.value).ToArray())
            .ToArray();
    }
}
