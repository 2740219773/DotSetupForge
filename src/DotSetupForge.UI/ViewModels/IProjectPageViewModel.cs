using DotSetupForge.UI.Models;

namespace DotSetupForge.UI.ViewModels;

/// <summary>需要绑定具体项目后才能工作的页面。</summary>
public interface IProjectPageViewModel
{
    /// <summary>绑定当前编辑中的项目。</summary>
    void Bind(EditableProject project);
}
