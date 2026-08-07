namespace DotSetupForge.Core.Runtime;

/// <summary>
/// 本地文件元数据提供者（离线/内网镜像）：
/// 基目录下按 {major.minor}/releases.json 组织，与官方 release-metadata 结构一致。
/// </summary>
public sealed class FileReleaseMetadataProvider : IReleaseMetadataProvider
{
    private readonly string _baseDirectory;

    public FileReleaseMetadataProvider(string baseDirectory) => _baseDirectory = baseDirectory;

    public Task<string> GetReleasesJsonAsync(string majorMinor, CancellationToken ct = default)
    {
        var path = Path.Combine(_baseDirectory, majorMinor, "releases.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"离线元数据不存在：{path}");
        }
        return File.ReadAllTextAsync(path, ct);
    }
}
