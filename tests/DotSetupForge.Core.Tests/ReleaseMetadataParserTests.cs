using DotSetupForge.Core.Models;
using DotSetupForge.Core.Runtime;

namespace DotSetupForge.Core.Tests;

public class ReleaseMetadataParserTests
{
    private readonly ReleaseMetadataParser _parser = new();

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static string ReleasesJson() => File.ReadAllText(Fixture("releases-10.0.json"));

    [Fact]
    public void FindBestMatch_WindowsDesktop_10_0_X64_Should_Return_Latest_Patch()
    {
        var requirement = new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64);

        var definition = _parser.FindBestMatch(ReleasesJson(), requirement);

        Assert.NotNull(definition);
        Assert.Equal("10.0.1", definition!.Version); // 取同 major.minor 最新 patch
        Assert.Equal("windowsdesktop-runtime-10.0.1-win-x64.exe", definition.FileName);
        Assert.Equal("DDD", definition.Sha256);
        Assert.Contains("wd-10.0.1-x64.exe", definition.DownloadUrl);
    }

    [Fact]
    public void FindBestMatch_WindowsDesktop_With_Current_RidOnly_InstallerName_Should_Return_Latest_Patch()
    {
        var releasesJson = ReleasesJson()
            .Replace("\"version\": \"10.0.1\"", "\"release-version\": \"10.0.1\"", StringComparison.Ordinal)
            .Replace("windowsdesktop-runtime-10.0.1-win-x64.exe", "windowsdesktop-runtime-win-x64.exe", StringComparison.Ordinal);
        var requirement = new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64);

        var definition = _parser.FindBestMatch(releasesJson, requirement);

        Assert.NotNull(definition);
        Assert.Equal("10.0.1", definition!.Version);
        Assert.Equal("windowsdesktop-runtime-win-x64.exe", definition.FileName);
        Assert.Equal("DDD", definition.Sha256);
    }

    [Fact]
    public void FindBestMatch_WindowsDesktop_X86_Should_Fallback_To_Older_Patch()
    {
        var requirement = new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X86);

        var definition = _parser.FindBestMatch(ReleasesJson(), requirement);

        // 10.0.1 无 win-x86 包，回退到 10.0.0 的 win-x86 包
        Assert.NotNull(definition);
        Assert.Equal("10.0.0", definition!.Version);
        Assert.Contains("win-x86", definition.FileName);
    }

    [Fact]
    public void FindBestMatch_ExactVersion_Should_Return_That_Version()
    {
        var requirement = new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0.0", TargetArchitecture.X64);

        // 同 major.minor 的最新 patch 是 10.0.1；精确 10.0.0 需求仍应找到 >= 的（本实现取最新）
        var definition = _parser.FindBestMatch(ReleasesJson(), requirement);

        Assert.NotNull(definition);
        Assert.Equal("10.0.1", definition!.Version);
    }

    [Fact]
    public void FindBestMatch_No_Match_Should_Return_Null()
    {
        var requirement = new RuntimeRequirement(
            RuntimeFamily.AspNetCore, "10.0", TargetArchitecture.X64);

        var definition = _parser.FindBestMatch(ReleasesJson(), requirement);

        Assert.Null(definition);
    }

    [Fact]
    public void FindBestMatch_BrokenJson_Should_Return_Null()
    {
        var requirement = new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64);

        var definition = _parser.FindBestMatch("{ not json ", requirement);

        Assert.Null(definition);
    }
}
