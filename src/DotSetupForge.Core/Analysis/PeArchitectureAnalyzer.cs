using System.Reflection.PortableExecutable;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Analysis;

/// <summary>架构置信度。</summary>
public enum ArchitectureConfidence
{
    High,
    Medium,
    Low,
}

/// <summary>架构信息来源。</summary>
public enum ArchitectureSource
{
    PeHeader,
    DepsJson,
    RuntimeConfig,
    User,
    Unknown,
}

/// <summary>架构检测结果：始终带 Confidence 与 Source，便于冲突排查。</summary>
public sealed record ArchitectureResult(
    TargetArchitecture Architecture,
    ArchitectureConfidence Confidence,
    ArchitectureSource Source)
{
    public static readonly ArchitectureResult Unknown =
        new(TargetArchitecture.AnyCpu, ArchitectureConfidence.Low, ArchitectureSource.Unknown);
}

/// <summary>基于 PE 头的 CPU 架构检测。</summary>
public sealed class PeArchitectureAnalyzer
{
    public ArchitectureResult Analyze(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var pe = new PEReader(stream);

            var headers = pe.PEHeaders;
            if (headers is null)
            {
                return ArchitectureResult.Unknown;
            }

            var machine = headers.CoffHeader.Machine;
            var corHeader = headers.CorHeader;
            var ilOnly = corHeader is not null && (corHeader.Flags & CorFlags.ILOnly) != 0;
            var requires32Bit = corHeader is not null && (corHeader.Flags & CorFlags.Requires32Bit) != 0;
            var prefers32Bit = corHeader is not null && (corHeader.Flags & CorFlags.Prefers32Bit) != 0;

            TargetArchitecture architecture = machine switch
            {
                Machine.I386 when ilOnly && !requires32Bit => TargetArchitecture.AnyCpu,
                Machine.I386 => TargetArchitecture.X86,
                Machine.Amd64 => TargetArchitecture.X64,
                Machine.Arm64 => TargetArchitecture.Arm64,
                _ => TargetArchitecture.AnyCpu,
            };

            return new ArchitectureResult(
                architecture,
                ArchitectureConfidence.High,
                ArchitectureSource.PeHeader);
        }
        catch (IOException)
        {
            return ArchitectureResult.Unknown;
        }
        catch (BadImageFormatException)
        {
            return ArchitectureResult.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return ArchitectureResult.Unknown;
        }
    }
}
