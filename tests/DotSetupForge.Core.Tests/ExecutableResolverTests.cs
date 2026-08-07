using DotSetupForge.Core.Analysis;

namespace DotSetupForge.Core.Tests;

public class ExecutableResolverTests
{
    private readonly ExecutableResolver _resolver = new();

    private static ScannedFile File(string name) => new(
        name, name, 0, Path.GetExtension(name).ToLowerInvariant(), string.Empty, FileCategory.Unknown);

    [Fact]
    public void Resolve_FourPieceMatch_Should_Pick_It()
    {
        var files = new List<ScannedFile>
        {
            File("RHCVP.exe"),
            File("RHCVP.dll"),
            File("RHCVP.runtimeconfig.json"),
            File("RHCVP.deps.json"),
            File("Helper.exe"),
        };

        var result = _resolver.Resolve(files);

        Assert.NotNull(result.Resolved);
        Assert.Equal("RHCVP.exe", result.Resolved);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Resolve_RuntimeConfig_Reverse_Lookup_Should_Pick()
    {
        var files = new List<ScannedFile>
        {
            File("App.exe"),
            File("App.runtimeconfig.json"),
            File("OtherTool.exe"),
        };

        var result = _resolver.Resolve(files);

        Assert.Equal("App.exe", result.Resolved);
    }

    [Fact]
    public void Resolve_Multiple_Candidates_Should_Not_Pick()
    {
        var files = new List<ScannedFile>
        {
            File("Aaa.exe"),
            File("Bbb.exe"),
            File("Ccc.exe"),
        };

        var result = _resolver.Resolve(files);

        Assert.Null(result.Resolved);
        Assert.Equal(3, result.Candidates.Count);
    }

    [Fact]
    public void Resolve_No_Exe_Should_Return_Empty()
    {
        var files = new List<ScannedFile>
        {
            File("data.json"),
            File("lib.dll"),
        };

        var result = _resolver.Resolve(files);

        Assert.Null(result.Resolved);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Resolve_Managed_Exe_With_Dll_Should_Pick()
    {
        var files = new List<ScannedFile>
        {
            File("App.exe"),
            File("App.dll"),
            File("Tool.exe"),
        };

        var result = _resolver.Resolve(files);

        Assert.Equal("App.exe", result.Resolved);
    }
}
