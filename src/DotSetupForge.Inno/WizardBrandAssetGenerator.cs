using DotSetupForge.Core.Packaging;

namespace DotSetupForge.Inno;

/// <summary>生成仅用于本次构建的安装向导品牌资源。</summary>
public sealed class WizardBrandAssetGenerator
{
    public WizardBrandAssets Resolve(InstallerModel model, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var warnings = new List<string>();
        var small = ResolveCustomImage(model.WizardSmallImagePath, "右上角品牌图", warnings);
        var welcome = ResolveCustomImage(model.WizardImagePath, "欢迎页品牌图", warnings);

        try
        {
            if (small is null)
            {
                small = CopyDefaultAsset(outputDirectory, "quark-small.bmp", "wizard-small.bmp");
            }

            if (welcome is null)
            {
                welcome = CopyDefaultAsset(outputDirectory, "quark-welcome.bmp", "wizard-welcome.bmp");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            warnings.Add($"无法生成安装向导默认品牌图，将使用无图片主题：{ex.Message}");
            small = null;
            welcome = null;
        }

        return new WizardBrandAssets(small, welcome, warnings);
    }

    private static string? ResolveCustomImage(string path, string label, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (File.Exists(path)) return path;
        warnings.Add($"{label}不存在，已改用自动品牌图：{path}");
        return null;
    }

    private static string CopyDefaultAsset(string outputDirectory, string assetName, string outputName)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", assetName);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"内置 QUARK 品牌图不存在：{source}", source);
        }

        var destination = Path.Combine(outputDirectory, outputName);
        File.Copy(source, destination, overwrite: true);
        return destination;
    }

}

public sealed record WizardBrandAssets(string? SmallImagePath, string? WelcomeImagePath, IReadOnlyList<string> Warnings);
