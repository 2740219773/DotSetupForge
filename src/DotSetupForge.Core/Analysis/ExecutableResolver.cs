namespace DotSetupForge.Core.Analysis;

/// <summary>主程序识别：四件套同名优先 → runtimeconfig 反查 → 全部候选。多候选绝不自动选错。</summary>
public sealed class ExecutableResolver
{
    /// <param name="files">扫描结果（相对路径）。</param>
    public ExecutableResolutionResult Resolve(IReadOnlyList<ScannedFile> files)
    {
        var exes = files
            .Where(f => f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.RelativePath)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (exes.Count == 0)
        {
            return new ExecutableResolutionResult(null, []);
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            names.Add(Path.GetFileName(f.RelativePath));
        }

        // 第一优先：四件套同名
        var fourPiece = exes
            .Where(exe =>
            {
                var stem = Path.GetFileNameWithoutExtension(exe);
                return names.Contains($"{stem}.dll")
                    && names.Contains($"{stem}.runtimeconfig.json")
                    && names.Contains($"{stem}.deps.json");
            })
            .ToList();

        if (fourPiece.Count == 1)
        {
            return new ExecutableResolutionResult(fourPiece[0], []);
        }

        if (fourPiece.Count > 1)
        {
            return new ExecutableResolutionResult(null, fourPiece);
        }

        // 第二优先：runtimeconfig 反查同名 exe
        var runtimeConfigStems = files
            .Where(f => f.FileName.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.FileName[..^".runtimeconfig.json".Length])
            .ToList();

        var byConfig = exes
            .Where(exe => runtimeConfigStems.Contains(Path.GetFileNameWithoutExtension(exe), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (byConfig.Count == 1)
        {
            return new ExecutableResolutionResult(byConfig[0], []);
        }

        if (byConfig.Count > 1)
        {
            return new ExecutableResolutionResult(null, byConfig);
        }

        // 第三优先：managed exe（有对应 dll 的）
        var managed = exes
            .Where(exe =>
            {
                var stem = Path.GetFileNameWithoutExtension(exe);
                return names.Contains($"{stem}.dll");
            })
            .ToList();

        if (managed.Count == 1)
        {
            return new ExecutableResolutionResult(managed[0], []);
        }

        if (managed.Count > 1)
        {
            return new ExecutableResolutionResult(null, managed);
        }

        // 多候选：交用户选择
        return new ExecutableResolutionResult(null, exes);
    }
}
