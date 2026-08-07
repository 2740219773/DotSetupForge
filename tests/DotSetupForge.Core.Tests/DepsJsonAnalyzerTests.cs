using DotSetupForge.Core.Analysis;

namespace DotSetupForge.Core.Tests;

public class DepsJsonAnalyzerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private readonly DepsJsonAnalyzer _analyzer = new();

    [Fact]
    public void Parse_Should_Extract_RuntimeTarget_And_Rids()
    {
        var deps = _analyzer.Parse(File.ReadAllText(Fixture("deps-win-x64.json")));

        Assert.Equal(".NETCoreApp,Version=v10.0", deps.RuntimeTarget);
        Assert.Contains("win-x64", deps.Rids);
        Assert.Contains("win-x86", deps.Rids);
    }

    [Fact]
    public void Parse_Should_Extract_NativeAssets()
    {
        var deps = _analyzer.Parse(File.ReadAllText(Fixture("deps-win-x64.json")));

        Assert.Contains(deps.NativeAssets, a => a.Contains("runtimes/win-x64/native/"));
        Assert.DoesNotContain(deps.NativeAssets, a => a.Contains("lib/"));
    }

    [Fact]
    public void Parse_Should_Extract_ReferencedAssemblies()
    {
        var deps = _analyzer.Parse(File.ReadAllText(Fixture("deps-win-x64.json")));

        Assert.Contains("SampleApp.dll", deps.ReferencedAssemblies);
    }

    [Fact]
    public void Parse_Simple_Should_Have_No_Rids()
    {
        var deps = _analyzer.Parse(File.ReadAllText(Fixture("deps-simple.json")));

        Assert.Empty(deps.Rids);
        Assert.Contains("ConsoleApp.dll", deps.ReferencedAssemblies);
    }

    [Fact]
    public void Parse_Broken_Should_Return_Empty()
    {
        var deps = _analyzer.Parse("{ not json ");

        Assert.Equal(DepsJsonInfo.Empty, deps);
    }
}
