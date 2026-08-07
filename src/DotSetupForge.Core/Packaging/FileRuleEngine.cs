using System.IO.Enumeration;
using DotSetupForge.Core.Analysis;

namespace DotSetupForge.Core.Packaging;

/// <summary>文件规则应用结果。</summary>
public sealed record FileRuleResult(
    IReadOnlyList<ScannedFile> Included,
    IReadOnlyList<ScannedFile> Excluded);

/// <summary>
/// 文件规则引擎：把扫描文件按规则过滤为包含/排除。
/// 规则按顺序应用，后命中的规则覆盖先命中的；默认规则后置 Exclude，保证 Logs/pdb 等安全排除。
/// </summary>
public sealed class FileRuleEngine
{
    /// <summary>内置默认规则（不硬编码进引擎逻辑，全部以 FileRule 表达、可被用户规则覆盖）。</summary>
    public static IReadOnlyList<FileRule> DefaultRules { get; } =
    [
        new("*.exe", FileRuleAction.Include),
        new("*.dll", FileRuleAction.Include),
        new("*.json", FileRuleAction.Include),
        new("*.config", FileRuleAction.Include),
        new("runtimes/**", FileRuleAction.Include),
        new("Data/**", FileRuleAction.Include),
        new("*.pdb", FileRuleAction.Exclude),
        new("*.log", FileRuleAction.Exclude),
        new("Logs/**", FileRuleAction.Exclude),
        new("obj/**", FileRuleAction.Exclude),
    ];

    /// <summary>应用规则集合（默认规则 + 用户规则，用户规则在后可覆盖默认）。</summary>
    public FileRuleResult Apply(
        IReadOnlyList<ScannedFile> files,
        IReadOnlyList<FileRule>? userRules = null)
    {
        var rules = DefaultRules.Concat(userRules ?? []).ToList();

        var included = new List<ScannedFile>();
        var excluded = new List<ScannedFile>();

        foreach (var file in files)
        {
            var action = Decide(file.RelativePath, rules);
            if (action == FileRuleAction.Include)
            {
                included.Add(file);
            }
            else
            {
                excluded.Add(file);
            }
        }

        return new FileRuleResult(included, excluded);
    }

    private static FileRuleAction Decide(string relativePath, IReadOnlyList<FileRule> rules)
    {
        var action = FileRuleAction.Include; // 默认包含（无规则命中时）
        foreach (var rule in rules)
        {
            if (Matches(rule.Pattern, relativePath))
            {
                action = rule.Action;
            }
        }
        return action;
    }

    internal static bool Matches(string pattern, string relativePath)
    {
        // ** 在 MatchesSimpleExpression 中与 * 等价（可跨目录匹配），满足 runtimes/**、Logs/** 语义
        return FileSystemName.MatchesSimpleExpression(pattern, relativePath, ignoreCase: true);
    }
}
