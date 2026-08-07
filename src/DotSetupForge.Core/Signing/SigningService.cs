using System.Diagnostics;

namespace DotSetupForge.Core.Signing;

/// <summary>signtool 定位结果。</summary>
public sealed record SigningLocationResult(bool Found, string? SigntoolPath, string? Error)
{
    public static SigningLocationResult Ok(string path) => new(true, path, null);

    public static SigningLocationResult NotFound(string error) => new(false, null, error);
}

/// <summary>签名结果。</summary>
public sealed record SigningResult(bool Success, int ExitCode, string Output, string Error)
{
    public static SigningResult Ok(int exitCode, string output) => new(true, exitCode, output, string.Empty);

    public static SigningResult Fail(int exitCode, string output, string error) =>
        new(false, exitCode, output, error);
}

/// <summary>
/// 代码签名服务：定位 signtool、签名文件、验证签名。
/// 密码绝不写入 .pack.json —— 由调用方从环境变量 / 凭据管理器 / 构建参数提供。
/// </summary>
public sealed class SigningService
{
    public const string PasswordEnvironmentVariable = "DOTSETFORGE_SIGN_PASSWORD";

    /// <summary>
    /// 定位 signtool.exe。
    /// 优先级：环境变量 SIGTOOL_PATH → Windows SDK Kits（取最高版本）→ PATH。
    /// </summary>
    public SigningLocationResult Locate()
    {
        // 1. 环境变量
        var env = Environment.GetEnvironmentVariable("SIGTOOL_PATH");
        if (!string.IsNullOrEmpty(env))
        {
            var fromEnv = Path.GetFullPath(env);
            if (File.Exists(fromEnv))
            {
                return SigningLocationResult.Ok(fromEnv);
            }
        }

        // 2. Windows Kits 10 bin（按版本号降序，取最高）
        var kitsRoot = @"C:\Program Files (x86)\Windows Kits\10\bin";
        if (Directory.Exists(kitsRoot))
        {
            var sdkVersions = Directory.EnumerateDirectories(kitsRoot)
                .Select(Path.GetFileName)
                .Where(v => v is not null && v.StartsWith("10.", StringComparison.Ordinal))
                .OrderByDescending(v => Version.TryParse(v, out var ver) ? ver : new Version(0, 0));

            foreach (var version in sdkVersions)
            {
                foreach (var arch in new[] { "x64", "x86", "arm64" })
                {
                    var candidate = Path.Combine(kitsRoot, version!, arch, "signtool.exe");
                    if (File.Exists(candidate))
                    {
                        return SigningLocationResult.Ok(candidate);
                    }
                }
            }
        }

        // 3. PATH
        var inPath = FindOnPath("signtool.exe");
        if (inPath is not null)
        {
            return SigningLocationResult.Ok(inPath);
        }

        return SigningLocationResult.NotFound(
            "未找到 signtool.exe。请安装 Windows SDK，或设置环境变量 SIGTOOL_PATH。");
    }

    /// <summary>
    /// 对文件进行 Authenticode 签名。
    /// </summary>
    /// <param name="filePath">待签名文件（Setup.exe）。</param>
    /// <param name="certificatePath">PFX/P12 证书路径。</param>
    /// <param name="password">证书密码（来自环境变量/凭据管理器，不落盘）。</param>
    /// <param name="timestampServer">时间戳服务器；为 null 时不加时间戳。</param>
    public async Task<SigningResult> SignAsync(
        string filePath,
        string certificatePath,
        string password,
        string? timestampServer,
        CancellationToken ct = default)
    {
        var location = Locate();
        if (!location.Found || location.SigntoolPath is null)
        {
            return SigningResult.Fail(-1, string.Empty, location.Error ?? "未找到 signtool.exe");
        }

        var psi = new ProcessStartInfo
        {
            FileName = location.SigntoolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in BuildSignArguments(certificatePath, password, timestampServer, filePath))
        {
            psi.ArgumentList.Add(arg);
        }

        var (exitCode, stdout, stderr) = await RunAsync(psi, ct).ConfigureAwait(false);
        return exitCode == 0
            ? SigningResult.Ok(exitCode, stdout)
            : SigningResult.Fail(exitCode, stdout, Summarize(stderr));
    }

    /// <summary>构建 signtool sign 参数（sign /f <cert> [/p <pwd>] [/t <ts>] /fd SHA256 /q <file>）。</summary>
    internal static IReadOnlyList<string> BuildSignArguments(
        string certificatePath, string password, string? timestampServer, string filePath)
    {
        var args = new List<string> { "sign" };
        args.Add("/f");
        args.Add(certificatePath);
        if (!string.IsNullOrEmpty(password))
        {
            args.Add("/p");
            args.Add(password);
        }

        if (!string.IsNullOrEmpty(timestampServer))
        {
            args.Add("/t");
            args.Add(timestampServer);
        }

        args.Add("/fd");
        args.Add("SHA256");
        args.Add("/q");
        args.Add(filePath);
        return args;
    }

    /// <summary>验证 Authenticode 签名（/pa 使用默认验证策略）。</summary>
    public async Task<SigningResult> VerifyAsync(string filePath, CancellationToken ct = default)
    {
        var location = Locate();
        if (!location.Found || location.SigntoolPath is null)
        {
            return SigningResult.Fail(-1, string.Empty, location.Error ?? "未找到 signtool.exe");
        }

        var psi = new ProcessStartInfo
        {
            FileName = location.SigntoolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("verify");
        psi.ArgumentList.Add("/pa");
        psi.ArgumentList.Add("/v");
        psi.ArgumentList.Add(filePath);

        var (exitCode, stdout, stderr) = await RunAsync(psi, ct).ConfigureAwait(false);
        return exitCode == 0
            ? SigningResult.Ok(exitCode, stdout)
            : SigningResult.Fail(exitCode, stdout, Summarize(stderr));
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        ProcessStartInfo psi,
        CancellationToken ct)
    {
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"无法启动 {psi.FileName}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return (process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
    }

    private static string Summarize(string stderr)
    {
        var lines = stderr
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join("；", lines);
    }

    /// <summary>在 PATH 中查找可执行文件。</summary>
    internal static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            try
            {
                var candidate = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // 无效路径跳过
            }
        }

        return null;
    }
}
