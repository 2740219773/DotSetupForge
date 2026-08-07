using DotSetupForge.Core.Packaging;
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
    /// <summary>渲染完整 installer.iss 文本。</summary>
    public string Generate(InstallerModel model, InnoScriptOptions options)
    {
        // 注：不使用嵌套对象（Scriban 对 record 直接 string 属性访问异常），全部扁平化为字典
        var data = new Dictionary<string, object?>
        {
            ["appId"] = $"{{{{{model.Product.AppId}}}}}", // Inno 要求 AppId={{GUID}} 双花括号
            ["appName"] = model.Product.Name,
            ["appVersion"] = model.Product.Version,
            ["appPublisher"] = model.Product.Publisher,
            ["installDirectory"] = model.InstallDirectory,
            ["privilegesRequired"] = options.PrivilegesRequired,
            ["outputDirectory"] = options.OutputDirectory,
            ["outputBaseFilename"] = options.OutputBaseFilename,
            ["files"] = model.Files.Select(ToFileViewModel).ToList(),
            ["shortcuts"] = model.Shortcuts.Select(s => ToShortcutViewModel(s, model)).ToList(),
            ["mainExecutable"] = model.MainExecutable,
            ["productName"] = model.Product.Name,
            ["launchAfterInstall"] = model.LaunchAfterInstall,
        };

        var parts = new List<string>
        {
            Render("Setup.sbn", data),
        };

        if (model.Files.Count > 0)
        {
            parts.Add(Render("Files.sbn", data));
        }

        if (model.Shortcuts.Count > 0)
        {
            parts.Add(Render("Icons.sbn", data));
        }

        if (model.MainExecutable.Length > 0)
        {
            parts.Add(Render("Run.sbn", data));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

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
        };
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
