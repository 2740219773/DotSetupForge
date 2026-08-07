using System.Diagnostics;

namespace DotSetupForge.Inno;

/// <summary>日志级别。</summary>
public enum InnoLogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>编译日志条目。</summary>
public sealed record InnoBuildLogEntry(InnoLogLevel Level, string Message);

/// <summary>编译结果。</summary>
public sealed record InnoBuildResult(
    bool Success,
    int ExitCode,
    IReadOnlyList<InnoBuildLogEntry> Log,
    string? OutputFile);

/// <summary>调用 ISCC.exe 编译 installer.iss，捕获 stdout/stderr 并解析为构建日志。</summary>
public sealed class InnoCompiler
{
    public async Task<InnoBuildResult> CompileAsync(
        string isccPath,
        string issPath,
        string? outputFileName = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = isccPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("/O+");
        if (!string.IsNullOrEmpty(outputFileName))
        {
            psi.ArgumentList.Add($"/F{outputFileName}");
        }
        psi.ArgumentList.Add(issPath);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"无法启动 {isccPath}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        var log = ParseLog(stdout, stderr);

        return new InnoBuildResult(
            process.ExitCode == 0,
            process.ExitCode,
            log,
            FindOutputFile(log, outputFileName));
    }

    private static List<InnoBuildLogEntry> ParseLog(string stdout, string stderr)
    {
        var entries = new List<InnoBuildLogEntry>();

        foreach (var raw in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            entries.Add(Classify(raw));
        }

        foreach (var raw in stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }
            entries.Add(Classify(raw));
        }

        return entries;
    }

    private static InnoBuildLogEntry Classify(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
        {
            return new InnoBuildLogEntry(InnoLogLevel.Error, trimmed);
        }

        if (trimmed.StartsWith("Warning", StringComparison.OrdinalIgnoreCase))
        {
            return new InnoBuildLogEntry(InnoLogLevel.Warning, trimmed);
        }

        return new InnoBuildLogEntry(InnoLogLevel.Info, trimmed);
    }

    private static string? FindOutputFile(IReadOnlyList<InnoBuildLogEntry> log, string? outputFileName)
    {
        // ISCC 成功日志含 "Output file is <path>"（可能本地化），取之；取不到由调用方按 OutputDir 计算
        var marker = "Output file is";
        var hit = log.FirstOrDefault(e =>
            e.Message.Contains(marker, StringComparison.OrdinalIgnoreCase));
        if (hit is null)
        {
            return null;
        }

        var index = hit.Message.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length;
        var path = hit.Message[index..].Trim();
        return path.Length > 0 ? path : null;
    }
}
