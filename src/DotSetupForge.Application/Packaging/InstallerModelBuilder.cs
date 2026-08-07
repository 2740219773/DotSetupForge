using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;

namespace DotSetupForge.Application.Packaging;

/// <summary>把分析结果 + 项目配置转换为统一的 InstallerModel（安装器生成模块的唯一输入）。</summary>
public sealed class InstallerModelBuilder
{
    public InstallerModel Build(ApplicationAnalysisResult analysis, PackageProject? project = null)
    {
        var product = BuildProduct(analysis, project);
        var files = BuildFiles(analysis, project);

        var mainExeName = Path.GetFileName(analysis.MainExecutable ?? string.Empty);

        var shortcuts = new List<ShortcutModel>();
        if (project?.Installer.CreateDesktopShortcut ?? true)
        {
            shortcuts.Add(new ShortcutModel(
                product.Name, mainExeName, null, Desktop: true, StartMenu: false));
        }

        if (project?.Installer.CreateStartMenuShortcut ?? true)
        {
            shortcuts.Add(new ShortcutModel(
                product.Name, mainExeName, null, Desktop: false, StartMenu: true));
        }

        var installDirectory = project?.Installer.InstallDirectory;
        if (string.IsNullOrEmpty(installDirectory))
        {
            installDirectory = string.IsNullOrEmpty(product.Publisher)
                ? $"{{autopf}}\\{product.Name}"
                : $"{{autopf}}\\{product.Publisher}\\{product.Name}";
        }

        return new InstallerModel
        {
            Product = product,
            Scope = project?.Installer.Scope ?? InstallScope.Machine,
            InstallDirectory = installDirectory,
            MainExecutable = mainExeName,
            Files = files,
            Shortcuts = shortcuts,
            Upgrade = new UpgradeModel(
                project?.Installer.AllowUpgrade ?? true,
                string.IsNullOrEmpty(mainExeName) ? null : Path.GetFileNameWithoutExtension(mainExeName)),
            Uninstall = new UninstallModel(KeepUserData: true, KeepLogs: false),
            Signing = project?.Signing ?? new SigningOptions(),
        };
    }

    private static ProductModel BuildProduct(ApplicationAnalysisResult analysis, PackageProject? project)
    {
        var mainExeName = Path.GetFileName(analysis.MainExecutable ?? string.Empty);

        if (project is null)
        {
            // 无项目配置：直接采用分析结果
            return new ProductModel(
                Guid.NewGuid(),
                analysis.ApplicationName,
                analysis.Version,
                string.Empty,
                mainExeName);
        }

        var product = project.Product;
        return new ProductModel(
            product.AppId,
            product.Name,
            product.Version,
            product.Publisher,
            mainExeName);
    }

    private static IReadOnlyList<InstallerFile> BuildFiles(
        ApplicationAnalysisResult analysis,
        PackageProject? project)
    {
        // 用户规则（追加到默认规则之后，可覆盖默认）
        var userRules = new List<FileRule>();
        if (project?.Files is not null)
        {
            userRules.AddRange(project.Files.Include.Select(p => new FileRule(p, FileRuleAction.Include)));
            userRules.AddRange(project.Files.Exclude.Select(p => new FileRule(p, FileRuleAction.Exclude)));
        }

        var engine = new FileRuleEngine();
        var result = engine.Apply(analysis.Files, userRules);

        var sourceRoot = Path.GetDirectoryName(analysis.MainExecutable)
            ?? Directory.GetCurrentDirectory();

        return result.Included
            .Select(f => ToInstallerFile(sourceRoot, f))
            .OrderBy(f => f.SourceRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static InstallerFile ToInstallerFile(string sourceRoot, ScannedFile file)
    {
        var relative = file.RelativePath;
        var subDirectory = Path.GetDirectoryName(relative)?.Replace('\\', '/');

        // 根目录文件不设子目录（安装到 {app} 根）
        if (string.IsNullOrEmpty(subDirectory) || subDirectory == ".")
        {
            subDirectory = null;
        }

        // 位置与策略按类别推荐
        var (location, upgrade, uninstall) = file.Category switch
        {
            FileCategory.Configuration => (InstallLocation.ApplicationDirectory, UpgradePolicy.PreserveExisting, UninstallPolicy.NeverUninstall),
            FileCategory.Data => (InstallLocation.ApplicationDirectory, UpgradePolicy.OverwriteAlways, UninstallPolicy.NeverUninstall),
            FileCategory.Log or FileCategory.Debug => (InstallLocation.ApplicationDirectory, UpgradePolicy.DeleteOnUpgrade, UninstallPolicy.Delete),
            _ => (InstallLocation.ApplicationDirectory, UpgradePolicy.OverwriteAlways, UninstallPolicy.Delete),
        };

        return new InstallerFile(
            Path.Combine(sourceRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)),
            file.RelativePath,
            file.Size,
            file.Category,
            location,
            subDirectory,
            upgrade,
            uninstall);
    }
}
