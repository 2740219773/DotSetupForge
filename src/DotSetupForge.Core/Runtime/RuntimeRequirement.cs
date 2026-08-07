using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>运行时需求（由 ApplicationAnalyzer 结果生成）。</summary>
public sealed record RuntimeRequirement(
    RuntimeFamily Family,
    string Version,
    TargetArchitecture Architecture,
    string? MinimumVersion = null,
    string? RollForward = null);
