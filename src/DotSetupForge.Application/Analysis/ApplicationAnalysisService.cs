using DotSetupForge.Core.Analysis;

namespace DotSetupForge.Application.Analysis;

/// <summary>应用分析的应用层门面。</summary>
public sealed class ApplicationAnalysisService
{
    private readonly ApplicationAnalyzer _analyzer = new();

    public ApplicationAnalysisResult Analyze(string directory, string? mainExecutable = null) =>
        _analyzer.Analyze(directory, mainExecutable);
}
