using Microsoft.Win32;

namespace DotSetupForge.Inno;

/// <summary>ISCC.exe 定位结果。</summary>
public sealed record InnoSetupLocationResult(
    bool Found,
    string? IsccPath,
    string? Error)
{
    public static InnoSetupLocationResult Ok(string isccPath) => new(true, isccPath, null);

    public static InnoSetupLocationResult NotFound(string error) => new(false, null, error);
}

/// <summary>
/// 定位 Inno Setup 编译器 ISCC.exe。
/// 优先级：环境变量 INNO_SETUP_HOME → 注册表（Inno Setup 6）→ 常见安装路径。
/// </summary>
public sealed class InnoSetupLocator
{
    public InnoSetupLocationResult Locate()
    {
        // 1. 环境变量
        var env = Environment.GetEnvironmentVariable("INNO_SETUP_HOME");
        if (!string.IsNullOrEmpty(env))
        {
            var fromEnv = Path.Combine(env, "ISCC.exe");
            if (File.Exists(fromEnv))
            {
                return InnoSetupLocationResult.Ok(fromEnv);
            }
        }

        // 2. 注册表（64/32 位视图）
        foreach (var hive in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, hive)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup_is1");
                var path = key?.GetValue("Inno Setup: App Path") as string;
                if (!string.IsNullOrEmpty(path))
                {
                    var candidate = Path.Combine(path, "ISCC.exe");
                    if (File.Exists(candidate))
                    {
                        return InnoSetupLocationResult.Ok(candidate);
                    }
                }
            }
            catch (PlatformNotSupportedException)
            {
                // 非 Windows
            }
        }

        // 3. 常见安装路径
        var candidates = new[]
        {
            @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            @"C:\Program Files\Inno Setup 6\ISCC.exe",
            @"C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return InnoSetupLocationResult.Ok(candidate);
            }
        }

        return InnoSetupLocationResult.NotFound(
            "未找到 ISCC.exe。请安装 Inno Setup 6，或设置环境变量 INNO_SETUP_HOME。");
    }
}
