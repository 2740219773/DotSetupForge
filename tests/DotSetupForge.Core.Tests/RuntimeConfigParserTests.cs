using DotSetupForge.Core.Analysis;

namespace DotSetupForge.Core.Tests;

public class RuntimeConfigParserTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private readonly RuntimeConfigParser _parser = new();

    [Fact]
    public void Parse_WindowsDesktop_Should_Return_Two_Frameworks()
    {
        var config = _parser.Parse(File.ReadAllText(Fixture("runtimeconfig-windowsdesktop.json")));

        Assert.Equal("net10.0", config.Tfm);
        Assert.Equal(2, config.Frameworks.Count);
        Assert.Contains(config.Frameworks, f => f.Name == "Microsoft.WindowsDesktop.App" && f.Version == "10.0.0");
        Assert.Contains(config.Frameworks, f => f.Name == "Microsoft.NETCore.App" && f.Version == "10.0.0");
    }

    [Fact]
    public void Parse_Single_Framework_Should_Return_One()
    {
        var config = _parser.Parse(File.ReadAllText(Fixture("runtimeconfig-framework-single.json")));

        Assert.Equal("net10.0", config.Tfm);
        var framework = Assert.Single(config.Frameworks);
        Assert.Equal("Microsoft.WindowsDesktop.App", framework.Name);
    }

    [Fact]
    public void Parse_RollForward_Should_Be_Extracted()
    {
        var config = _parser.Parse(File.ReadAllText(Fixture("runtimeconfig-rollforward.json")));

        Assert.Equal("net8.0", config.Tfm);
        Assert.Equal("LatestMajor", config.RollForward);
        Assert.False(config.ApplyPatches);
    }

    [Fact]
    public void Parse_BrokenJson_Should_Return_Empty()
    {
        var config = _parser.Parse(File.ReadAllText(Fixture("runtimeconfig-broken.json")));

        Assert.Equal(RuntimeConfig.Empty, config);
    }

    [Fact]
    public void ParseFile_Missing_Should_Return_Null()
    {
        var config = _parser.ParseFile(Path.Combine(AppContext.BaseDirectory, "not-exist.runtimeconfig.json"));

        Assert.Null(config);
    }
}
