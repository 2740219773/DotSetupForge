using System.Text.Json;
using System.Text.Json.Serialization;
using DotSetupForge.Application.Analysis;
using DotSetupForge.Application.Projects;
using DotSetupForge.Core.Analysis;

namespace DotSetupForge.CLI;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : string.Empty;

        return command switch
        {
            "test" => RunTest(),
            "analyze" => RunAnalyze(args[1..]),
            "-h" or "--help" or "" => PrintUsage(),
            _ => PrintUnknown(command),
        };
    }

    private static int RunAnalyze(string[] args)
    {
        var directory = string.Empty;
        string? mainExe = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--main-exe" when i + 1 < args.Length:
                    mainExe = args[++i];
                    break;
                case "--help" or "-h":
                    Console.WriteLine("用法: dotpack analyze <目录> [--main-exe <exe>]");
                    return 0;
                default:
                    if (string.IsNullOrEmpty(directory))
                    {
                        directory = args[i];
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(directory))
        {
            Console.WriteLine("用法: dotpack analyze <目录> [--main-exe <exe>]");
            return 1;
        }

        Console.WriteLine("DotSetupForge");
        Console.WriteLine("Analyzing...");
        Console.WriteLine();

        var service = new ApplicationAnalysisService();
        var result = service.Analyze(directory, mainExe);

        if (!result.Success)
        {
            foreach (var d in result.Diagnostics)
            {
                Console.WriteLine($"{d.Severity} {d.Code}: {d.Message}");
            }
            return result.Diagnostics.Any(d => d.Severity == DotSetupForge.Core.Models.DiagnosticSeverity.Warning) ? 2 : 1;
        }

        PrintAnalysis(result);

        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText("analysis.json", json);
        Console.WriteLine();
        Console.WriteLine("Analysis successful. analysis.json generated.");

        return 0;
    }

    private static void PrintAnalysis(ApplicationAnalysisResult result)
    {
        Console.WriteLine("Application");
        Console.WriteLine($"  Name: {result.ApplicationName}");
        Console.WriteLine($"  Executable: {Path.GetFileName(result.MainExecutable)}");
        Console.WriteLine($"  Version: {result.Version}");

        Console.WriteLine();
        Console.WriteLine(".NET");
        Console.WriteLine($"  TFM: {result.TargetFramework}");
        Console.WriteLine($"  Framework: {result.FrameworkName}");
        Console.WriteLine($"  Version: {result.FrameworkVersion}");
        Console.WriteLine($"  Required Runtime: {result.RuntimeName}");

        Console.WriteLine();
        Console.WriteLine("Platform");
        Console.WriteLine($"  Architecture: {result.Architecture}");

        Console.WriteLine();
        Console.WriteLine("Deployment");
        Console.WriteLine($"  {result.DeploymentMode}");

        Console.WriteLine();
        Console.WriteLine("Files");
        Console.WriteLine($"  Total: {result.Files.Count}");

        foreach (var d in result.Diagnostics)
        {
            Console.WriteLine();
            Console.WriteLine($"{d.Severity} {d.Code}: {d.Message}");
        }
    }

    private static int RunTest()
    {
        Console.WriteLine("DotSetupForge");
        Console.WriteLine("Running 'test'...");
        Console.WriteLine();

        var service = new PackageProjectService();

        // 1. 创建 PackageProject
        var project = service.CreateTestProject();

        // 2. 序列化为 test.pack.json
        var json = service.Serialize(project);
        File.WriteAllText("test.pack.json", json);
        Console.WriteLine("Written: test.pack.json");

        // 3. 重新读取并验证
        var result = service.Deserialize(json);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"FAIL: {error.Code} {error.Message}");
            }
            return 1;
        }

        var loaded = result.Project!;
        var schemaOk = loaded.SchemaVersion == DotSetupForge.Core.Models.PackageProject.CurrentSchemaVersion;
        var appIdOk = loaded.Product.AppId == project.Product.AppId;

        Console.WriteLine();
        Console.WriteLine($"  SchemaVersion : {loaded.SchemaVersion}");
        Console.WriteLine($"  AppId         : {loaded.Product.AppId}");
        Console.WriteLine($"  Name          : {loaded.Product.Name}");
        Console.WriteLine($"  Version       : {loaded.Product.Version}");

        if (schemaOk && appIdOk)
        {
            Console.WriteLine();
            Console.WriteLine("PASS: 序列化/反序列化验证通过");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine($"FAIL: schemaOk={schemaOk}, appIdOk={appIdOk}");
        return 1;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            DotSetupForge - .NET Windows 应用打包工具

            用法:
              dotpack test               创建 PackageProject 并验证序列化往返
              dotpack analyze <目录>      分析应用并生成 analysis.json
                          [--main-exe <exe>]  指定主程序（多候选时必填）

            """);
        return 0;
    }

    private static int PrintUnknown(string command)
    {
        Console.WriteLine($"未知命令: {command}");
        return PrintUsage();
    }
}
