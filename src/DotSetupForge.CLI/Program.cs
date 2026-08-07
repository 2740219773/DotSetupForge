using DotSetupForge.Application.Projects;
using DotSetupForge.Core.Models;

namespace DotSetupForge.CLI;

internal static class Program
{
    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : string.Empty;

        return command switch
        {
            "test" => RunTest(),
            "-h" or "--help" or "" => PrintUsage(),
            _ => PrintUnknown(command),
        };
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
        var schemaOk = loaded.SchemaVersion == PackageProject.CurrentSchemaVersion;
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
              dotpack test       创建 PackageProject 并验证序列化往返

            """);
        return 0;
    }

    private static int PrintUnknown(string command)
    {
        Console.WriteLine($"未知命令: {command}");
        return PrintUsage();
    }
}
