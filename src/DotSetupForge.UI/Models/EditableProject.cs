using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DotSetupForge.Core.Models;

namespace DotSetupForge.UI.Models;

/// <summary>
/// GUI 可编辑项目模型。
/// Core 的 PackageProject 是 init 不可变 record，不适合直接绑定；
/// 本类型提供全部可写属性，保存时通过 <see cref="ToProject"/> 还原为 PackageProject。
/// </summary>
public partial class EditableProject : ObservableObject
{
    /// <summary>AppId 只在新建时生成，加载后禁止修改（升级保持一致的依据）。</summary>
    public Guid AppId { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _version = "1.0.0";

    [ObservableProperty]
    private string _publisher = string.Empty;

    [ObservableProperty]
    private string _mainExecutable = string.Empty;

    // ---- 源 ----

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _sourceType = "目录";

    [ObservableProperty]
    private string _configuration = "Release";

    // ---- 运行环境 ----

    [ObservableProperty]
    private string _runtimeFamily = "WindowsDesktop";

    [ObservableProperty]
    private string _runtimeVersion = string.Empty;

    [ObservableProperty]
    private string _runtimeArchitecture = "X64";

    [ObservableProperty]
    private string _runtimeMode = "SmartOffline";

    [ObservableProperty]
    private bool _autoDetect = true;

    // ---- 安装器 ----

    [ObservableProperty]
    private string _installScope = "Machine";

    [ObservableProperty]
    private string _installDirectory = string.Empty;

    [ObservableProperty]
    private bool _createDesktopShortcut = true;

    [ObservableProperty]
    private bool _createStartMenuShortcut = true;

    [ObservableProperty]
    private bool _launchAfterInstall;

    [ObservableProperty]
    private bool _allowUpgrade = true;

    // ---- 文件规则 ----

    public ObservableCollection<string> IncludePatterns { get; } =
    [
        "*.exe",
        "*.dll",
        "*.json",
        "*.config",
        "runtimes/**",
    ];

    public ObservableCollection<string> ExcludePatterns { get; } =
    [
        "*.pdb",
        "*.log",
        "Logs/**",
        "obj/**",
    ];

    // ---- 签名 ----

    [ObservableProperty]
    private bool _signingEnabled;

    [ObservableProperty]
    private string _certificatePath = string.Empty;

    [ObservableProperty]
    private string _timestampServer = "http://timestamp.digicert.com";

    // ---- 输出 ----

    [ObservableProperty]
    private string _outputDirectory = "./dist";

    [ObservableProperty]
    private string _outputFileName = "{ProductName}_Setup_{Version}.exe";

    // ---- 分析结果（只读展示） ----

    [ObservableProperty]
    private string _analysisSummary = string.Empty;

    /// <summary>从 Core 项目加载（拷贝而非引用，避免改坏原模型）。</summary>
    public static EditableProject FromProject(PackageProject project) => new()
    {
        AppId = project.Product.AppId,
        ProductName = project.Product.Name,
        Version = project.Product.Version,
        Publisher = project.Product.Publisher,
        MainExecutable = project.Product.MainExecutable,
        SourcePath = project.Source.Path,
        SourceType = project.Source.Type == DotSetupForge.Core.Models.SourceType.Project ? "项目" : "目录",
        Configuration = project.Source.Configuration,
        RuntimeFamily = project.Runtime.Family.ToString(),
        RuntimeVersion = project.Runtime.Version,
        RuntimeArchitecture = project.Runtime.Architecture.ToString(),
        RuntimeMode = project.Runtime.Mode.ToString(),
        AutoDetect = project.Runtime.AutoDetect,
        InstallScope = project.Installer.Scope.ToString(),
        InstallDirectory = project.Installer.InstallDirectory,
        CreateDesktopShortcut = project.Installer.CreateDesktopShortcut,
        CreateStartMenuShortcut = project.Installer.CreateStartMenuShortcut,
        LaunchAfterInstall = project.Installer.LaunchAfterInstall,
        AllowUpgrade = project.Installer.AllowUpgrade,
        SigningEnabled = project.Signing.Enabled,
        CertificatePath = project.Signing.CertificatePath,
        TimestampServer = project.Signing.TimestampServer,
        OutputDirectory = project.Output.Directory,
        OutputFileName = project.Output.FileName,
    };

    private static void SyncList(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    public void SyncFrom(EditableProject other)
    {
        AppId = other.AppId;
        ProductName = other.ProductName;
        Version = other.Version;
        Publisher = other.Publisher;
        MainExecutable = other.MainExecutable;
        SourcePath = other.SourcePath;
        SourceType = other.SourceType;
        Configuration = other.Configuration;
        RuntimeFamily = other.RuntimeFamily;
        RuntimeVersion = other.RuntimeVersion;
        RuntimeArchitecture = other.RuntimeArchitecture;
        RuntimeMode = other.RuntimeMode;
        AutoDetect = other.AutoDetect;
        InstallScope = other.InstallScope;
        InstallDirectory = other.InstallDirectory;
        CreateDesktopShortcut = other.CreateDesktopShortcut;
        CreateStartMenuShortcut = other.CreateStartMenuShortcut;
        LaunchAfterInstall = other.LaunchAfterInstall;
        AllowUpgrade = other.AllowUpgrade;
        SigningEnabled = other.SigningEnabled;
        CertificatePath = other.CertificatePath;
        TimestampServer = other.TimestampServer;
        OutputDirectory = other.OutputDirectory;
        OutputFileName = other.OutputFileName;
        SyncList(IncludePatterns, other.IncludePatterns);
        SyncList(ExcludePatterns, other.ExcludePatterns);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>转换为 Core PackageProject（保存 / 构建时调用）。</summary>
    public PackageProject ToProject() => new()
    {
        SchemaVersion = PackageProject.CurrentSchemaVersion,
        Product = new ProductInfo
        {
            AppId = AppId,
            Name = ProductName,
            Version = Version,
            Publisher = Publisher,
            MainExecutable = MainExecutable,
        },
        Source = new SourceInfo
        {
            Type = SourceType == "项目" ? DotSetupForge.Core.Models.SourceType.Project : DotSetupForge.Core.Models.SourceType.Directory,
            Path = SourcePath,
            Configuration = Configuration,
        },
        Runtime = new RuntimeInfo
        {
            Family = Enum.TryParse<DotSetupForge.Core.Models.RuntimeFamily>(RuntimeFamily, out var family) ? family : DotSetupForge.Core.Models.RuntimeFamily.WindowsDesktop,
            Version = RuntimeVersion,
            Architecture = Enum.TryParse<TargetArchitecture>(RuntimeArchitecture, out var arch) ? arch : TargetArchitecture.X64,
            Mode = Enum.TryParse<RuntimeDeploymentMode>(RuntimeMode, out var mode) ? mode : RuntimeDeploymentMode.SmartOffline,
            AutoDetect = AutoDetect,
        },
        Installer = new InstallerOptions
        {
            Scope = Enum.TryParse<DotSetupForge.Core.Models.InstallScope>(InstallScope, out var scope) ? scope : DotSetupForge.Core.Models.InstallScope.Machine,
            InstallDirectory = InstallDirectory,
            CreateDesktopShortcut = CreateDesktopShortcut,
            CreateStartMenuShortcut = CreateStartMenuShortcut,
            LaunchAfterInstall = LaunchAfterInstall,
            AllowUpgrade = AllowUpgrade,
        },
        Files = new FileOptions
        {
            Include = IncludePatterns.ToList(),
            Exclude = ExcludePatterns.ToList(),
        },
        Signing = new SigningOptions
        {
            Enabled = SigningEnabled,
            CertificatePath = CertificatePath,
            TimestampServer = TimestampServer,
        },
        Output = new OutputOptions
        {
            Directory = OutputDirectory,
            FileName = OutputFileName,
        },
    };
}
