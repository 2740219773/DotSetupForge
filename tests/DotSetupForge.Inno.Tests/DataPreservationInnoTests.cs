using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;
using DotSetupForge.Inno;

namespace DotSetupForge.Inno.Tests;

public sealed class DataPreservationInnoTests
{
    [Fact]
    public void Generate_Should_Preserve_Existing_Data_File_On_Upgrade_And_Uninstall()
    {
        var model = new InstallerModel
        {
            Product = new ProductModel(Guid.NewGuid(), "TestApp", "1.0.0", "Test", "TestApp.exe"),
            InstallDirectory = @"{autopf}\Test\TestApp",
            Files =
            [
                new InstallerFile(
                    @"D:\publish\Data\business.db", "Data/business.db", 100,
                    FileCategory.Data, InstallLocation.ApplicationDirectory,
                    "Data", UpgradePolicy.PreserveExisting, UninstallPolicy.NeverUninstall),
            ],
        };

        var script = new InnoScriptGenerator().Generate(model, new InnoScriptOptions("TestApp", @".\dist"));

        Assert.Contains(
            @"Source: ""D:\publish\Data\business.db""; DestDir: ""{app}\Data""; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall",
            script);
    }
}
