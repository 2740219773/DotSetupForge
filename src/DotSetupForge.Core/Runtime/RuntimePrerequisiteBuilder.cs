using DotSetupForge.Core.Models;
using DotSetupForge.Core.Packaging;

namespace DotSetupForge.Core.Runtime;

/// <summary>把 Runtime 需求 + 安装包定义转换为安装器前置依赖（PrerequisiteModel）。</summary>
public sealed class RuntimePrerequisiteBuilder
{
    /// <summary>默认静默安装参数（.NET 官方运行时安装器）。</summary>
    public const string DefaultInstallArguments = "/install /quiet /norestart";

    /// <summary>成功退出码。</summary>
    public static readonly int[] SuccessExitCodes = [0];

    /// <summary>成功但需要重启的退出码。</summary>
    public static readonly int[] RebootExitCodes = [3010];

    public PrerequisiteModel Build(RuntimeRequirement requirement, RuntimeDefinition definition)
    {
        var frameworkDir = requirement.Family switch
        {
            RuntimeFamily.WindowsDesktop => "Microsoft.WindowsDesktop.App",
            RuntimeFamily.AspNetCore => "Microsoft.AspNetCore.App",
            _ => "Microsoft.NETCore.App",
        };

        var id = $"dotnet-{requirement.Family.ToString().ToLowerInvariant()}-" +
                 $"{definition.Version}-{requirement.Architecture.ToString().ToLowerInvariant()}";

        return new PrerequisiteModel(
            Id: id,
            Name: definition.DisplayName,
            Version: definition.Version,
            Architecture: requirement.Architecture,
            InstallerFileName: definition.FileName,
            InstallArguments: DefaultInstallArguments,
            SuccessExitCodes: SuccessExitCodes,
            RebootExitCodes: RebootExitCodes,
            Detection: PrerequisiteDetection.FrameworkDirectory,
            DetectionPath: frameworkDir);
    }
}
