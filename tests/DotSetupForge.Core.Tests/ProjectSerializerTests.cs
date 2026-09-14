using DotSetupForge.Core.Json;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Tests;

public class ProjectSerializerTests
{
    private static PackageProject CreateProject() => new()
    {
        SchemaVersion = PackageProject.CurrentSchemaVersion,
        Product = new ProductInfo
        {
            AppId = Guid.NewGuid(),
            Name = "TestApp",
            Version = "1.0.0",
            Publisher = "Test Publisher",
            MainExecutable = "TestApp.exe",
        },
        Installer = new InstallerOptions
        {
            WizardTheme = InstallerWizardTheme.Zircon,
            SetupIconPath = @"D:\assets\TestApp.ico",
            WizardSmallImagePath = @"D:\assets\TestApp-small.png",
            WizardImagePath = @"D:\assets\TestApp-welcome.png",
        },
    };

    [Fact]
    public void Serialize_Should_Include_SchemaVersion()
    {
        var json = ProjectSerializer.Serialize(CreateProject());

        Assert.Contains("\"schemaVersion\": 1", json);
    }

    [Fact]
    public void Serialize_Should_Include_AppId()
    {
        var project = CreateProject();
        var json = ProjectSerializer.Serialize(project);

        Assert.Contains(project.Product.AppId.ToString(), json);
    }

    [Fact]
    public void RoundTrip_Should_Preserve_AppId()
    {
        var project = CreateProject();
        var json = ProjectSerializer.Serialize(project);

        var result = ProjectSerializer.Deserialize(json);

        Assert.True(result.Success);
        Assert.Equal(project.Product.AppId, result.Project!.Product.AppId);
        Assert.Equal(project.Product.Name, result.Project.Product.Name);
        Assert.Equal(project.Product.Version, result.Project.Product.Version);
        Assert.Equal(project.Installer.SetupIconPath, result.Project.Installer.SetupIconPath);
        Assert.Equal(project.Installer.WizardSmallImagePath, result.Project.Installer.WizardSmallImagePath);
        Assert.Equal(project.Installer.WizardImagePath, result.Project.Installer.WizardImagePath);
        Assert.Equal(InstallerWizardTheme.Zircon, result.Project.Installer.WizardTheme);
    }

    [Fact]
    public void Deserialize_InvalidJson_Should_Return_Error()
    {
        var result = ProjectSerializer.Deserialize("{ not valid json ");

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Deserialize_EmptyJson_Should_Return_Error()
    {
        var result = ProjectSerializer.Deserialize("null");

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.NotEmpty(result.Errors);
    }
}
