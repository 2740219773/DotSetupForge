using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Tests;

public class PeArchitectureTests
{
    private readonly PeArchitectureAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_ManagedAssembly_Should_Return_AnyCpu_With_High_Confidence()
    {
        // 本测试程序集为 AnyCPU 托管程序集
        var file = typeof(PeArchitectureTests).Assembly.Location;

        var result = _analyzer.Analyze(file);

        Assert.Equal(TargetArchitecture.AnyCpu, result.Architecture);
        Assert.Equal(ArchitectureConfidence.High, result.Confidence);
        Assert.Equal(ArchitectureSource.PeHeader, result.Source);
    }

    [Fact]
    public void Analyze_DotNetHost_Dll_Should_Return_Native_Architecture()
    {
        // System.Private.CoreLib.dll 为原生架构（ReadyToRun），非 AnyCPU
        var file = typeof(object).Assembly.Location;

        var result = _analyzer.Analyze(file);

        Assert.Equal(ArchitectureConfidence.High, result.Confidence);
        Assert.Equal(ArchitectureSource.PeHeader, result.Source);
        Assert.NotEqual(TargetArchitecture.AnyCpu, result.Architecture);
    }

    [Fact]
    public void Analyze_MissingFile_Should_Return_Unknown()
    {
        var result = _analyzer.Analyze(Path.Combine(AppContext.BaseDirectory, "not-exist.dll"));

        Assert.Equal(ArchitectureConfidence.Low, result.Confidence);
        Assert.Equal(ArchitectureSource.Unknown, result.Source);
    }
}
