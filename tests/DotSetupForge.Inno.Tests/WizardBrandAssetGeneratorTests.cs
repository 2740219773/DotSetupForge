using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;
using DotSetupForge.Inno;

namespace DotSetupForge.Inno.Tests;

public sealed class WizardBrandAssetGeneratorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"dsf-brand-{Guid.NewGuid():N}");

    public WizardBrandAssetGeneratorTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Resolve_Without_CustomAssets_Generates_Temporary_BmpAssets()
    {
        var assets = new WizardBrandAssetGenerator().Resolve(CreateModel(), _directory);

        Assert.Empty(assets.Warnings);
        Assert.NotNull(assets.SmallImagePath);
        Assert.NotNull(assets.WelcomeImagePath);
        Assert.True(File.Exists(assets.SmallImagePath));
        Assert.True(File.Exists(assets.WelcomeImagePath));
        Assert.Equal("wizard-small.bmp", Path.GetFileName(assets.SmallImagePath));
        Assert.Equal("wizard-welcome.bmp", Path.GetFileName(assets.WelcomeImagePath));
        Assert.True(new FileInfo(assets.SmallImagePath).Length > 512);
        Assert.True(new FileInfo(assets.WelcomeImagePath).Length > 1024);
        Assert.Equal((byte)'B', File.ReadAllBytes(assets.SmallImagePath)[0]);
        Assert.Equal((byte)'M', File.ReadAllBytes(assets.WelcomeImagePath)[1]);
    }

    [Fact]
    public async Task Generated_StellarScript_Should_Compile_With_LocalInnoSetup()
    {
        var locator = new InnoSetupLocator().Locate();
        if (!locator.Found)
        {
            return;
        }

        var assets = new WizardBrandAssetGenerator().Resolve(CreateModel(), _directory);
        var model = CreateModel() with
        {
            WizardSmallImagePath = assets.SmallImagePath!,
            WizardImagePath = assets.WelcomeImagePath!,
        };
        var script = new InnoScriptGenerator().GenerateToFile(
            model,
            new InnoScriptOptions("BrandingValidation", _directory),
            _directory);

        var result = await new InnoCompiler().CompileAsync(locator.IsccPath!, script.IssPath, "BrandingValidation");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Log.Select(entry => entry.Message)));
        Assert.True(File.Exists(Path.Combine(_directory, "BrandingValidation.exe")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }

    private static InstallerModel CreateModel() => new()
    {
        Product = new ProductModel(Guid.NewGuid(), "品牌验证", "1.0.0", "DotSetupForge", string.Empty),
        InstallDirectory = @"D:\Apps\DotSetupForge\品牌验证",
        WizardTheme = InstallerWizardTheme.Stellar,
        Upgrade = new UpgradeModel(true, null),
    };
}
