using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Packaging;

namespace DotSetupForge.Core.Tests;

public class FileRuleEngineTests
{
    private readonly FileRuleEngine _engine = new();

    private static ScannedFile File(string relativePath) => new(
        Path.GetFileName(relativePath),
        relativePath,
        0,
        Path.GetExtension(relativePath).ToLowerInvariant(),
        string.Empty,
        FileCategory.Unknown);

    [Fact]
    public void DefaultRules_Should_Include_App_Files()
    {
        var files = new List<ScannedFile>
        {
            File("FakeApp.exe"),
            File("FakeApp.dll"),
            File("appsettings.json"),
            File("config.xml"),
        };

        var result = _engine.Apply(files);

        Assert.Equal(4, result.Included.Count);
        Assert.Empty(result.Excluded);
    }

    [Fact]
    public void DefaultRules_Should_Exclude_Pdb_And_Logs()
    {
        var files = new List<ScannedFile>
        {
            File("FakeApp.pdb"),
            File("Logs/app.log"),
            File("Logs/2026-08/trace.json"), // Logs 里即使 json 也排除
            File("FakeApp.exe"),
        };

        var result = _engine.Apply(files);

        Assert.Single(result.Included);
        Assert.Equal("FakeApp.exe", result.Included[0].RelativePath);
        Assert.Equal(3, result.Excluded.Count);
    }

    [Fact]
    public void DefaultRules_Should_Include_Runtimes_And_Data()
    {
        var files = new List<ScannedFile>
        {
            File("runtimes/win-x64/native/libHarfBuzzSharp.dll"),
            File("Data/config.dat"),
            File("Data/static/image.png"),
        };

        var result = _engine.Apply(files);

        Assert.Equal(3, result.Included.Count);
        Assert.Empty(result.Excluded);
    }

    [Fact]
    public void UserRule_Should_Override_Default()
    {
        var files = new List<ScannedFile>
        {
            File("FakeApp.pdb"),
            File("notes.txt"),
        };

        // 用户规则在后：显式包含 pdb 和 txt → 覆盖默认排除
        var result = _engine.Apply(files,
        [
            new FileRule("*.pdb", FileRuleAction.Include),
            new FileRule("*.txt", FileRuleAction.Include),
        ]);

        Assert.Equal(2, result.Included.Count);
        Assert.Empty(result.Excluded);
    }

    [Fact]
    public void UserRule_Should_Exclude_Json()
    {
        var files = new List<ScannedFile>
        {
            File("appsettings.json"),
            File("FakeApp.exe"),
        };

        var result = _engine.Apply(files, [new FileRule("*.json", FileRuleAction.Exclude)]);

        Assert.Single(result.Included);
        Assert.Equal("FakeApp.exe", result.Included[0].RelativePath);
        Assert.Single(result.Excluded);
    }

    [Fact]
    public void DefaultRules_Should_Exclude_Obj()
    {
        var files = new List<ScannedFile>
        {
            File("obj/Debug/FakeApp.cs"),
        };

        var result = _engine.Apply(files);

        Assert.Empty(result.Included);
        Assert.Single(result.Excluded);
    }
}
