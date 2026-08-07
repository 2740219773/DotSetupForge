using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;
using DotSetupForge.Core.Runtime;

namespace DotSetupForge.Core.Tests;

public class RuntimeRequirementFactoryTests
{
    [Fact]
    public void FromAnalysis_WindowsDesktop_Should_Map_Family_Version_Architecture()
    {
        var analysis = new ApplicationAnalysisResult
        {
            FrameworkName = "Microsoft.WindowsDesktop.App",
            FrameworkVersion = "10.0.5",
            Architecture = TargetArchitecture.X64,
        };

        var requirement = RuntimeRequirementFactory.FromAnalysis(analysis);

        Assert.Equal(RuntimeFamily.WindowsDesktop, requirement.Family);
        Assert.Equal("10.0", requirement.Version);
        Assert.Equal(TargetArchitecture.X64, requirement.Architecture);
    }

    [Fact]
    public void FromAnalysis_CoreOnly_Should_Map_DotNet()
    {
        var analysis = new ApplicationAnalysisResult
        {
            FrameworkName = "Microsoft.NETCore.App",
            FrameworkVersion = "8.0.0",
            Architecture = TargetArchitecture.X86,
        };

        var requirement = RuntimeRequirementFactory.FromAnalysis(analysis);

        Assert.Equal(RuntimeFamily.DotNet, requirement.Family);
        Assert.Equal("8.0", requirement.Version);
        Assert.Equal(TargetArchitecture.X86, requirement.Architecture);
    }

    [Fact]
    public void MajorMinor_Should_Trim_Patch()
    {
        Assert.Equal("10.0", RuntimeRequirementFactory.MajorMinor("10.0.7"));
        Assert.Equal("9.0", RuntimeRequirementFactory.MajorMinor("9.0"));
    }
}

public class RuntimePrerequisiteBuilderTests
{
    [Fact]
    public void Build_Should_Produce_FrameworkDirectory_Prerequisite()
    {
        var requirement = new RuntimeRequirement(
            RuntimeFamily.WindowsDesktop, "10.0", TargetArchitecture.X64);
        var definition = new RuntimeDefinition(
            RuntimeFamily.WindowsDesktop,
            "10.0.1",
            TargetArchitecture.X64,
            ".NET Desktop Runtime 10.0.1 X64",
            "https://example/windowsdesktop-runtime-10.0.1-win-x64.exe",
            "windowsdesktop-runtime-10.0.1-win-x64.exe",
            "HASH");

        var prerequisite = new RuntimePrerequisiteBuilder().Build(requirement, definition);

        Assert.Equal("dotnet-windowsdesktop-10.0.1-x64", prerequisite.Id);
        Assert.Equal(".NET Desktop Runtime 10.0.1 X64", prerequisite.Name);
        Assert.Equal("windowsdesktop-runtime-10.0.1-win-x64.exe", prerequisite.InstallerFileName);
        Assert.Equal("/install /quiet /norestart", prerequisite.InstallArguments);
        Assert.Equal([0], prerequisite.SuccessExitCodes);
        Assert.Equal([3010], prerequisite.RebootExitCodes);
        Assert.Equal(PrerequisiteDetection.FrameworkDirectory, prerequisite.Detection);
        Assert.Equal("Microsoft.WindowsDesktop.App", prerequisite.DetectionPath);
    }

    [Fact]
    public void Build_CoreOnly_Should_Use_NetCore_FrameworkDir()
    {
        var requirement = new RuntimeRequirement(
            RuntimeFamily.DotNet, "8.0", TargetArchitecture.X64);
        var definition = new RuntimeDefinition(
            RuntimeFamily.DotNet, "8.0.15", TargetArchitecture.X64,
            ".NET Runtime 8.0.15 X64", "https://example/x.exe",
            "dotnet-runtime-8.0.15-win-x64.exe", "HASH");

        var prerequisite = new RuntimePrerequisiteBuilder().Build(requirement, definition);

        Assert.Equal("Microsoft.NETCore.App", prerequisite.DetectionPath);
    }
}
