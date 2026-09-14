using System.IO;
using DotSetupForge.Core.Packaging;
using DotSetupForge.Core.Models;
using Scriban;

namespace DotSetupForge.Inno;

/// <summary>脚本生成选项。</summary>
public sealed record InnoScriptOptions(
    string OutputBaseFilename,
    string OutputDirectory,
    string PrivilegesRequired = "admin");

/// <summary>生成结果。</summary>
public sealed record InnoScriptResult(string Script, string IssPath);

/// <summary>Inno Setup 脚本生成器：InstallerModel → Scriban 模板 → installer.iss。</summary>
public sealed class InnoScriptGenerator
{
    private const string ShortcutIconFileName = "DotSetupForge.Shortcut.ico";

    /// <summary>渲染完整 installer.iss 文本。</summary>
    public string Generate(InstallerModel model, InnoScriptOptions options)
    {
        var prerequisites = model.Prerequisites
            .Where(p => p.Detection is PrerequisiteDetection.FrameworkDirectory or PrerequisiteDetection.NetFrameworkRelease)
            .Select(ToPrerequisiteViewModel)
            .ToList();
        var usesPreferredDataDrive = UsesPreferredDataDriveDefault(model);

        var data = new Dictionary<string, object?>
        {
            ["appId"] = $"{{{{{model.Product.AppId}}}}}", // Inno 要求 AppId={{GUID}} 双花括号
            ["appName"] = model.Product.Name,
            ["appVersion"] = model.Product.Version,
            ["appPublisher"] = model.Product.Publisher,
            ["installDirectory"] = usesPreferredDataDrive
                ? $"{{code:GetPreferredDataDriveInstallDir|{BuildInstallSubdirectory(model.Product)}}}"
                : model.InstallDirectory,
            ["hasSetupIcon"] = !string.IsNullOrWhiteSpace(model.SetupIconPath),
            ["setupIconFile"] = model.SetupIconPath,
            ["hasWizardSmallImage"] = !string.IsNullOrWhiteSpace(model.WizardSmallImagePath),
            ["wizardSmallImageFile"] = model.WizardSmallImagePath,
            ["hasWizardImage"] = !string.IsNullOrWhiteSpace(model.WizardImagePath),
            ["wizardImageFile"] = model.WizardImagePath,
            ["wizardStyle"] = GetWizardStyle(model.WizardTheme),
            ["uninstallDisplayIcon"] = model.SetupIconPath,
            ["privilegesRequired"] = options.PrivilegesRequired,
            ["outputDirectory"] = options.OutputDirectory,
            ["outputBaseFilename"] = options.OutputBaseFilename,
            ["files"] = model.Files.Select(ToFileViewModel).ToList(),
            ["shortcuts"] = model.Shortcuts.Select(s => ToShortcutViewModel(s, model)).ToList(),
            ["mainExecutable"] = model.MainExecutable,
            ["productName"] = model.Product.Name,
            ["launchAfterInstall"] = model.LaunchAfterInstall,
            ["hasPrerequisites"] = prerequisites.Count > 0,
            ["hasPreferredDataDrive"] = usesPreferredDataDrive,
            ["hasUpgradeHandling"] = model.Upgrade.Enabled,
            ["uninstallKey"] = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{{{model.Product.AppId}}}_is1",
            ["productVersion"] = model.Product.Version,
            ["prerequisites"] = prerequisites,
            ["successCode"] = model.Prerequisites.FirstOrDefault()?.SuccessExitCodes.FirstOrDefault() ?? 0,
            ["rebootCode"] = model.Prerequisites.FirstOrDefault()?.RebootExitCodes.FirstOrDefault() ?? 3010,
        };

        var parts = new List<string>
        {
            Render("Setup.sbn", data),
        };

        if (model.Files.Count > 0 || prerequisites.Count > 0 || !string.IsNullOrWhiteSpace(model.SetupIconPath))
        {
            parts.Add(RenderFiles(model, prerequisites, data));
        }

        if (model.Shortcuts.Count > 0)
        {
            parts.Add(Render("Icons.sbn", data));
        }

        if (model.MainExecutable.Length > 0)
        {
            parts.Add(Render("Run.sbn", data));
        }

        if (prerequisites.Count > 0 || usesPreferredDataDrive || model.Upgrade.Enabled)
        {
            parts.Add(Render("Code.sbn", data));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string GetWizardStyle(InstallerWizardTheme theme) => theme switch
    {
        InstallerWizardTheme.Slate => "modern dynamic slate includetitlebar",
        InstallerWizardTheme.Zircon => "modern zircon includetitlebar",
        InstallerWizardTheme.ModernLight => "modern dynamic windows11 includetitlebar",
        // Inno Setup 6.7 ships Polar as the dark blue built-in style; keep the product name Stellar.
        _ => "modern dynamic polar includetitlebar",
    };

    /// <summary>渲染 [Files] 段：应用文件 + 前置依赖安装包（dontcopy，仅提取到 {tmp}）。</summary>
    private static string RenderFiles(
        InstallerModel model,
        IReadOnlyList<object> prerequisites,
        Dictionary<string, object?> data)
    {
        var rendered = Render("Files.sbn", data).TrimEnd();

        var prerequisiteLines = new List<string>();
        foreach (var p in prerequisites.OfType<Dictionary<string, object?>>())
        {
            var sourcePath = (string?)p["sourcePath"];
            var fileName = (string?)p["installerFileName"];
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            prerequisiteLines.Add(
                $"Source: \"{sourcePath}\"; DestDir: \"{{tmp}}\"; Flags: dontcopy");
        }

        if (prerequisiteLines.Count == 0)
        {
            return AppendShortcutIconFile(rendered, model.SetupIconPath);
        }

        return AppendShortcutIconFile(
            rendered + Environment.NewLine + string.Join(Environment.NewLine, prerequisiteLines),
            model.SetupIconPath);
    }

    private static string AppendShortcutIconFile(string rendered, string setupIconPath) =>
        string.IsNullOrWhiteSpace(setupIconPath)
            ? rendered
            : rendered + Environment.NewLine +
              $"Source: \"{setupIconPath}\"; DestDir: \"{{app}}\"; DestName: \"{ShortcutIconFileName}\"; Flags: ignoreversion";

    /// <summary>生成并写入 installer.iss，返回脚本文本与文件路径。</summary>
    public InnoScriptResult GenerateToFile(
        InstallerModel model,
        InnoScriptOptions options,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var issPath = Path.Combine(outputDirectory, "installer.iss");
        var script = Generate(model, options);
        File.WriteAllText(issPath, script);
        return new InnoScriptResult(script, issPath);
    }

    private static string Render(string templateName, object data)
    {
        var templateText = LoadTemplate(templateName);
        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"模板 {templateName} 解析失败：{string.Join("; ", template.Messages.Select(m => m.ToString()))}");
        }
        return template.Render(data);
    }

    private static string LoadTemplate(string name)
    {
        var resourceName = $"DotSetupForge.Inno.Templates.{name}";
        var assembly = typeof(InnoScriptGenerator).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"嵌入模板不存在：{resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static object ToFileViewModel(InstallerFile file)
    {
        var flags = "ignoreversion";

        if (file.UpgradePolicy is UpgradePolicy.PreserveExisting or UpgradePolicy.InstallIfMissing)
        {
            flags += " onlyifdoesntexist";
        }

        if (file.UninstallPolicy == UninstallPolicy.NeverUninstall)
        {
            flags += " uninsneveruninstall";
        }

        return new Dictionary<string, object?>
        {
            ["sourcePath"] = file.SourcePath,
            ["destDir"] = BuildDestDir(file),
            ["flags"] = flags,
        };
    }

    private static bool UsesPreferredDataDriveDefault(InstallerModel model) =>
        string.Equals(
            NormalizeDirectory(model.InstallDirectory),
            NormalizeDirectory($@"D:\Apps\{BuildInstallSubdirectory(model.Product)}"),
            StringComparison.OrdinalIgnoreCase);

    private static string BuildInstallSubdirectory(ProductModel product) =>
        string.IsNullOrWhiteSpace(product.Publisher)
            ? product.Name
            : $"{product.Publisher}\\{product.Name}";

    private static string NormalizeDirectory(string path) => path.TrimEnd('\\', '/');

    private static object ToShortcutViewModel(ShortcutModel shortcut, InstallerModel model)
    {
        var location = shortcut.Desktop ? "{autodesktop}" : "{autoprograms}";
        var target = shortcut.TargetRelativePath.Length > 0
            ? shortcut.TargetRelativePath
            : model.MainExecutable;

        return new Dictionary<string, object?>
        {
            ["fullName"] = $"{location}\\{shortcut.Name}",
            ["target"] = target,
            ["hasIcon"] = !string.IsNullOrWhiteSpace(shortcut.IconRelativePath) || !string.IsNullOrWhiteSpace(model.SetupIconPath),
            ["iconPath"] = shortcut.IconRelativePath ?? ShortcutIconFileName,
        };
    }

    private static object ToPrerequisiteViewModel(PrerequisiteModel p)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = SanitizeIdentifier(p.Id),
            ["name"] = p.Name,
            ["version"] = p.Version,
            ["installerFileName"] = p.InstallerFileName,
            ["installArguments"] = p.InstallArguments,
            ["detection"] = p.Detection.ToString(),
            ["releaseValue"] = p.DetectionPath ?? "0",
            ["registryArchitecture"] = p.Architecture switch
            {
                DotSetupForge.Core.Models.TargetArchitecture.X86 => "x86",
                DotSetupForge.Core.Models.TargetArchitecture.Arm64 => "arm64",
                _ => "x64",
            },
            ["frameworkDir"] = p.DetectionPath ?? "Microsoft.NETCore.App",
            ["sourcePath"] = p.SourcePath,
        };
    }

    /// <summary>Pascal 标识符：仅保留字母数字。</summary>
    internal static string SanitizeIdentifier(string id)
    {
        var chars = id.Where(char.IsLetterOrDigit).ToArray();
        var result = new string(chars);
        return result.Length == 0 || !char.IsLetter(result[0])
            ? "Prereq" + result
            : result;
    }

    internal static string BuildDestDir(InstallerFile file)
    {
        var baseDir = file.InstallLocation switch
        {
            InstallLocation.ProgramData => "{commonappdata}",
            InstallLocation.LocalAppData => "{localappdata}\\Programs",
            InstallLocation.AppData => "{userappdata}",
            _ => "{app}",
        };

        if (string.IsNullOrEmpty(file.TargetSubDirectory))
        {
            return baseDir;
        }

        var sub = file.TargetSubDirectory.Replace('/', '\\');
        return $"{baseDir}\\{sub}";
    }
}
