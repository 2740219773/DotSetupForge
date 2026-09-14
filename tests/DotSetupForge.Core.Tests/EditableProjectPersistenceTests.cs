using DotSetupForge.Core.Models;
using DotSetupForge.Core.Json;
using DotSetupForge.UI.Models;

namespace DotSetupForge.Core.Tests;

public sealed class EditableProjectPersistenceTests
{
    [Fact]
    public void FromProject_Should_Restore_Custom_FileRules_For_FileTreeReload()
    {
        var project = new PackageProject
        {
            Files = new DotSetupForge.Core.Models.FileOptions
            {
                Include = ["*.exe", "Data/**"],
                Exclude = ["Data/Config/local.json", "Data/History/20260820-003/R01.zip"],
                SelectedDirectoryPaths = ["Data"],
            },
        };

        var editable = EditableProject.FromProject(project);

        Assert.Equal(project.Files.Include, editable.IncludePatterns);
        Assert.Equal(project.Files.Exclude, editable.ExcludePatterns);
        Assert.Equal(project.Files.SelectedDirectoryPaths, editable.SelectedDirectoryPaths);

        var savedJson = ProjectSerializer.Serialize(editable.ToProject());
        var savedProject = ProjectSerializer.Deserialize(savedJson).Project!;
        var reloaded = EditableProject.FromProject(savedProject).ToProject();
        Assert.Equal(project.Files.Include, reloaded.Files.Include);
        Assert.Equal(project.Files.Exclude, reloaded.Files.Exclude);
        Assert.Equal(project.Files.SelectedDirectoryPaths, reloaded.Files.SelectedDirectoryPaths);
    }
}
