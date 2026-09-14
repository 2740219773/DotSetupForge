using DotSetupForge.UI.Models;
using DotSetupForge.UI.ViewModels;

namespace DotSetupForge.Core.Tests;

public sealed class FileNodeTests
{
    [Fact]
    public void ApplyUserState_OnDirectory_UpdatesEveryDescendant_WithoutChangingParent()
    {
        var (root, directory, first, second) = CreateTree();

        directory.ApplyUserState(false);

        Assert.False(directory.CheckedState);
        Assert.False(first.CheckedState);
        Assert.False(second.CheckedState);
        Assert.True(root.CheckedState);

        directory.ApplyUserState(true);

        Assert.True(directory.CheckedState);
        Assert.True(first.CheckedState);
        Assert.True(second.CheckedState);
        Assert.True(root.CheckedState);
    }

    [Fact]
    public void CheckedState_OnLeaf_DoesNotChangeAnyAncestor()
    {
        var (root, directory, first, second) = CreateTree();

        first.CheckedState = false;

        Assert.True(directory.CheckedState);
        Assert.True(root.CheckedState);
        Assert.False(first.CheckedState);
        Assert.True(second.CheckedState);
    }

    [Fact]
    public void CheckedState_OnLeaf_CanRestoreItsOwnStateWithoutChangingAncestors()
    {
        var (root, directory, first, second) = CreateTree();
        first.CheckedState = false;

        first.CheckedState = true;

        Assert.True(directory.CheckedState);
        Assert.True(root.CheckedState);
        Assert.True(second.CheckedState);
    }

    [Fact]
    public void CheckedState_OnNestedLeaf_DoesNotChangeAncestorStates()
    {
        var root = new FileNode("root", "root", true, null);
        var directory = new FileNode("child", "root/child", true, root);
        var first = new FileNode("one.bin", "root/child/one.bin", false, directory);
        var second = new FileNode("two.bin", "root/child/two.bin", false, directory);
        var directFile = new FileNode("three.bin", "root/three.bin", false, root);
        root.Children.Add(directory);
        root.Children.Add(directFile);
        directory.Children.Add(first);
        directory.Children.Add(second);
        root.ApplyUserState(true);

        first.CheckedState = false;

        Assert.True(directory.CheckedState);
        Assert.True(root.CheckedState);
        Assert.True(directFile.CheckedState);
    }

    [Fact]
    public void SynchronizeExcludePatterns_ReplacesDirectoryRuleWithFinalLeafStates()
    {
        var (root, _, first, second) = CreateTree();
        first.CheckedState = false;
        var project = new EditableProject();
        project.ExcludePatterns.Clear();
        project.ExcludePatterns.Add("root/child/**");
        project.ExcludePatterns.Add("unrelated/**");

        FilesPageViewModel.SynchronizeExcludePatterns(project, [root]);

        Assert.Contains("root/child/one.bin", project.ExcludePatterns);
        Assert.DoesNotContain("root/child/two.bin", project.ExcludePatterns);
        Assert.DoesNotContain("root/child/**", project.ExcludePatterns);
        Assert.Contains("unrelated/**", project.ExcludePatterns);
        Assert.Contains("root", project.SelectedDirectoryPaths);
        Assert.Contains("root/child", project.SelectedDirectoryPaths);
        Assert.True(second.CheckedState);
    }

    [Fact]
    public void ApplyUserState_WithIndeterminateCheckboxValue_ClearsDirectoryAndDescendants()
    {
        var (root, directory, first, second) = CreateTree();

        directory.ApplyUserState(null);

        Assert.False(directory.CheckedState);
        Assert.False(first.CheckedState);
        Assert.False(second.CheckedState);
        Assert.True(root.CheckedState);
    }

    [Fact]
    public void ApplyUserState_NotifiesEveryDescendantCheckedStateBinding()
    {
        var (_, directory, first, second) = CreateTree();
        var notified = new List<string?>();
        first.PropertyChanged += (_, args) => notified.Add(args.PropertyName);
        second.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        directory.ApplyUserState(false);

        Assert.All(notified, name => Assert.NotEqual("SetState", name));
        Assert.Contains(nameof(FileNode.CheckedState), notified);
    }

    [Fact]
    public void ApplyNodeOnlyState_OnDirectory_DoesNotChangeParentOrDescendants()
    {
        var (root, directory, first, second) = CreateTree();

        directory.ApplyNodeOnlyState(false);

        Assert.True(root.CheckedState);
        Assert.False(directory.CheckedState);
        Assert.True(first.CheckedState);
        Assert.True(second.CheckedState);
    }

    [Fact]
    public void RestoreDirectorySelections_Should_Restore_Checked_Directory_Without_Changing_Excluded_Children()
    {
        var (root, directory, first, second) = CreateTree();
        directory.ApplyUserState(false);

        FilesPageViewModel.RestoreDirectorySelections([root], ["root/child"]);

        Assert.True(directory.CheckedState);
        Assert.False(first.CheckedState);
        Assert.False(second.CheckedState);
        Assert.True(root.CheckedState);
    }

    private static (FileNode Root, FileNode Directory, FileNode First, FileNode Second) CreateTree()
    {
        var root = new FileNode("root", "root", true, null);
        var directory = new FileNode("child", "root/child", true, root);
        var first = new FileNode("one.bin", "root/child/one.bin", false, directory);
        var second = new FileNode("two.bin", "root/child/two.bin", false, directory);
        root.Children.Add(directory);
        directory.Children.Add(first);
        directory.Children.Add(second);
        root.ApplyUserState(true);
        return (root, directory, first, second);
    }
}
