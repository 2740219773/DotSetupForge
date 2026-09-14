using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>从应用分析结果生成运行时需求。</summary>
public static class RuntimeRequirementFactory
{
    public static RuntimeRequirement FromAnalysis(ApplicationAnalysisResult analysis)
    {
        var family = analysis.FrameworkName switch
        {
            "Microsoft.NETFramework" => RuntimeFamily.NetFramework,
            "Microsoft.WindowsDesktop.App" => RuntimeFamily.WindowsDesktop,
            "Microsoft.AspNetCore.App" => RuntimeFamily.AspNetCore,
            _ => RuntimeFamily.DotNet,
        };

        return new RuntimeRequirement(
            family,
            MajorMinor(analysis.FrameworkVersion),
            analysis.Architecture);
    }

    /// <summary>"10.0.0" → "10.0"；解析失败原样返回。</summary>
    public static string MajorMinor(string version)
    {
        if (Version.TryParse(version, out var v))
        {
            return $"{v.Major}.{v.Minor}";
        }

        return version;
    }
}
