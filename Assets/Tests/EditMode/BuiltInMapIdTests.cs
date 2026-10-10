using NUnit.Framework;

public sealed class BuiltInMapIdTests
{
    [TestCase("stage-01")]
    [TestCase("stage12")]
    [TestCase("tutorial-jump-2")]
    public void 소문자kebab형식을허용한다(string mapId)
    {
        Assert.That(BuiltInMapId.IsValid(mapId), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Stage-01")]
    [TestCase("stage_01")]
    [TestCase("stage 01")]
    [TestCase("-stage")]
    [TestCase("stage-")]
    [TestCase("stage--01")]
    [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    public void 형식밖의ID를거부한다(string mapId)
    {
        Assert.That(BuiltInMapId.IsValid(mapId), Is.False);
    }

    [Test]
    public void 올바른목록은문제가없다()
    {
        Assert.That(
            BuiltInMapId.TryFindProblem(new[] { "stage-01", "stage-02" }, out string problem),
            Is.False);
        Assert.That(problem, Is.Null);
    }

    [Test]
    public void 중복ID를찾는다()
    {
        Assert.That(
            BuiltInMapId.TryFindProblem(
                new[] { "stage-01", "stage-02", "stage-01" }, out string problem),
            Is.True);
        Assert.That(problem, Does.Contain("중복").And.Contain("stage-01"));
    }

    [Test]
    public void 형식이틀린항목의위치를알려준다()
    {
        Assert.That(
            BuiltInMapId.TryFindProblem(new[] { "stage-01", "" }, out string problem),
            Is.True);
        Assert.That(problem, Does.Contain("1번"));
    }
}
