using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Runtime;

/// <summary>ClickOnce Bootstrapper 中可用于离线部署的 .NET Framework 4.x 包定义。</summary>
public sealed record LegacyFrameworkPackage(
    string Version,
    int ReleaseValue,
    string DisplayName,
    string InstallerFileName,
    string InstallArguments,
    IReadOnlyList<int> SuccessExitCodes,
    IReadOnlyList<int> RebootExitCodes)
{
    /// <summary>定义文件所在 Bootstrapper 包目录。</summary>
    public string PackageDirectory { get; init; } = string.Empty;

    /// <summary>若 SDK 已附带离线安装器，则可直接导入缓存。</summary>
    public string LocalInstallerPath => Path.Combine(PackageDirectory, InstallerFileName);
}

/// <summary>已导入的 .NET Framework 离线安装器。</summary>
public sealed record CachedLegacyFramework(
    string Version,
    int ReleaseValue,
    string FileName,
    string Directory)
{
    public string InstallerPath => Path.Combine(Directory, FileName);
}

/// <summary>
/// 从 Visual Studio ClickOnce Bootstrapper 的 product.xml 读取 .NET Framework 4.x 包定义。
/// 只接受含完整离线 EXE 与 Release 注册表阈值的包；3.5 SP1 多文件包被明确排除。
/// </summary>
public sealed class LegacyFrameworkBootstrapperCatalog
{
    public const string DefaultPackagesDirectory = @"C:\Program Files (x86)\Microsoft SDKs\ClickOnce Bootstrapper\Packages";
    private static readonly XNamespace BootstrapperNamespace = "http://schemas.microsoft.com/developer/2004/01/bootstrapper";
    private static readonly Regex ProductVersionPattern = new(@"^\.NETFramework,Version=v(?<version>4(?:\.\d+){0,2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InstallerLanguagePattern = new(@"-(?<language>ENU|CHS)(?=\.exe$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public LegacyFrameworkBootstrapperCatalog(string? packagesDirectory = null) =>
        PackagesDirectory = packagesDirectory ?? DefaultPackagesDirectory;

    public string PackagesDirectory { get; }

    public IReadOnlyList<LegacyFrameworkPackage> List()
    {
        if (!Directory.Exists(PackagesDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(PackagesDirectory)
            .Select(directory => Path.Combine(directory, "product.xml"))
            .Where(File.Exists)
            .Select(Parse)
            .Where(p => p is not null)
            .Cast<LegacyFrameworkPackage>()
            .GroupBy(p => p.Version, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => ParseVersion(p.Version))
            .ToList();
    }

    /// <summary>选择满足最低版本的最小 4.x 离线包；4.5+ 为就地升级，较新版本可满足旧应用。</summary>
    public LegacyFrameworkPackage? Resolve(string requiredVersion) =>
        !Version.TryParse(requiredVersion, out var required)
            ? null
            : List().Where(p => ParseVersion(p.Version) >= required).OrderBy(p => ParseVersion(p.Version)).FirstOrDefault();

    /// <summary>
    /// 根据用户选中的 EXE 反查 Bootstrapper 定义。4.x 是就地升级，允许选择不低于应用最低要求的包。
    /// </summary>
    public LegacyFrameworkPackage? ResolveForInstaller(string requiredVersion, string installerFile)
    {
        if (!Version.TryParse(requiredVersion, out var required) || string.IsNullOrWhiteSpace(installerFile))
        {
            return null;
        }

        var fileName = Path.GetFileName(installerFile);
        return List()
            .Where(p => ParseVersion(p.Version) >= required && IsSupportedLanguageVariant(p.InstallerFileName, fileName))
            .OrderBy(p => ParseVersion(p.Version))
            .Select(p => p with { InstallerFileName = fileName })
            .FirstOrDefault();
    }

    /// <summary>在包目录中查找 ENU 或 CHS 语言变体；优先 product.xml 中声明的原始文件名。</summary>
    public LegacyFrameworkPackage? FindLocalInstallerPackage(LegacyFrameworkPackage package)
    {
        if (!Directory.Exists(package.PackageDirectory))
        {
            return null;
        }

        var installer = Directory.EnumerateFiles(package.PackageDirectory, "*.exe", SearchOption.TopDirectoryOnly)
            .OrderBy(path => string.Equals(Path.GetFileName(path), package.InstallerFileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => IsSupportedLanguageVariant(package.InstallerFileName, Path.GetFileName(path)));

        return installer is null ? null : package with { InstallerFileName = Path.GetFileName(installer) };
    }

    /// <summary>返回可手工选择的安装器文件名；仅接受 ENU/CHS 两种离线安装器语言变体。</summary>
    public static IReadOnlyList<string> GetSupportedInstallerFileNames(LegacyFrameworkPackage package)
    {
        var match = InstallerLanguagePattern.Match(package.InstallerFileName);
        if (!match.Success)
        {
            return [package.InstallerFileName];
        }

        return [
            InstallerLanguagePattern.Replace(package.InstallerFileName, "-ENU", 1),
            InstallerLanguagePattern.Replace(package.InstallerFileName, "-CHS", 1),
        ];
    }

    private static LegacyFrameworkPackage? Parse(string productXml)
    {
        try
        {
            var document = XDocument.Load(productXml);
            var productCode = document.Root?.Attribute("ProductCode")?.Value ?? string.Empty;
            var match = ProductVersionPattern.Match(productCode);
            if (!match.Success || !Version.TryParse(match.Groups["version"].Value, out var version))
            {
                return null;
            }

            var command = document.Descendants(BootstrapperNamespace + "Command")
                .FirstOrDefault(c => (string?)c.Attribute("PackageFile") is { Length: > 0 } name &&
                                     name.Contains("AllOS", StringComparison.OrdinalIgnoreCase));
            if (command is null)
            {
                return null;
            }

            var releaseBypass = command.Descendants(BootstrapperNamespace + "BypassIf")
                .FirstOrDefault(e => ((string?)e.Attribute("Property"))?.Contains("Release", StringComparison.OrdinalIgnoreCase) == true &&
                                     int.TryParse((string?)e.Attribute("Value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
            if (releaseBypass is null || !int.TryParse((string?)releaseBypass.Attribute("Value"), out var release))
            {
                return null;
            }

            if (string.Equals((string?)releaseBypass.Attribute("Compare"), "ValueGreaterThan", StringComparison.OrdinalIgnoreCase))
            {
                release++;
            }

            var exitCodes = command.Descendants(BootstrapperNamespace + "ExitCode").ToList();
            var success = exitCodes.Where(e => string.Equals((string?)e.Attribute("Result"), "Success", StringComparison.OrdinalIgnoreCase))
                .Select(e => int.TryParse((string?)e.Attribute("Value"), out var value) ? value : -1).Where(v => v >= 0).ToList();
            var reboot = exitCodes.Where(e => string.Equals((string?)e.Attribute("Result"), "SuccessReboot", StringComparison.OrdinalIgnoreCase))
                .Select(e => int.TryParse((string?)e.Attribute("Value"), out var value) ? value : -1).Where(v => v >= 0).ToList();

            return new LegacyFrameworkPackage(
                version.ToString(), release, $".NET Framework {version}",
                (string)command.Attribute("PackageFile")!,
                (string?)command.Attribute("Arguments") ?? " /q /norestart",
                success.Count == 0 ? [0] : success,
                reboot.Count == 0 ? [3010] : reboot)
            {
                PackageDirectory = Path.GetDirectoryName(productXml) ?? string.Empty,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static Version ParseVersion(string value) => Version.TryParse(value, out var version) ? version : new Version(0, 0);

    private static bool IsSupportedLanguageVariant(string expectedFileName, string selectedFileName)
    {
        if (string.Equals(expectedFileName, selectedFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expected = InstallerLanguagePattern.Match(expectedFileName);
        var selected = InstallerLanguagePattern.Match(selectedFileName);
        return expected.Success && selected.Success &&
               string.Equals(
                   InstallerLanguagePattern.Replace(expectedFileName, "-{language}"),
                   InstallerLanguagePattern.Replace(selectedFileName, "-{language}"),
                   StringComparison.OrdinalIgnoreCase);
    }
}
