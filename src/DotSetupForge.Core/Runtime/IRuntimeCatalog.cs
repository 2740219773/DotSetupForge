namespace DotSetupForge.Core.Runtime;

/// <summary>运行时目录：根据需求解析出安装包定义。</summary>
public interface IRuntimeCatalog
{
    Task<RuntimeDefinition?> ResolveAsync(RuntimeRequirement requirement, CancellationToken ct = default);
}

/// <summary>releases.json 元数据提供者（抽象网络，便于测试与离线镜像）。</summary>
public interface IReleaseMetadataProvider
{
    /// <summary>获取 release-metadata/{major.minor}/releases.json 内容。</summary>
    Task<string> GetReleasesJsonAsync(string majorMinor, CancellationToken ct = default);
}
