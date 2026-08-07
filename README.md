# DotSetupForge

面向 C#/.NET Windows 桌面程序的智能发布、依赖分析、环境部署和安装包生成工具。

## 简介

选择 .NET Windows 应用的发布目录或 `.csproj`，自动识别程序类型、.NET 版本、运行时、CPU 架构和主要文件，配置 .NET Runtime 部署策略，生成可直接交付客户的单文件 Windows 安装程序（基于 Inno Setup）。

## 使用方式

```text
GUI    DotSetupForge.exe
CLI    dotpack.exe
配置   RHCVP.pack.json
```

## 项目结构

```text
src/
├─ DotSetupForge.Core         核心模型与分析逻辑
├─ DotSetupForge.Application  项目管理 / 工作流 / Build Job
├─ DotSetupForge.Inno         Inno Setup 集成（脚本生成、ISCC 编译）
├─ DotSetupForge.CLI          dotpack 命令行
└─ DotSetupForge.UI           WPF 界面

tests/
├─ DotSetupForge.Core.Tests
├─ DotSetupForge.Inno.Tests
└─ DotSetupForge.IntegrationTests
```

## 开发状态

- [x] M0: 项目骨架初始化（解决方案、项目结构、核心数据模型、`dotpack test` 序列化验收）
- [x] M1: 基础安装包（ApplicationAnalyzer → InstallerModel → Setup.exe）
- [x] M2-a: 智能 Runtime 引擎（检测、下载、SHA256 校验、缓存、前置依赖生成）
- [ ] M2-b: 智能 Runtime 真实安装验证（虚拟机 A/B/C/D 验收）
- [x] M3: WPF GUI（首页 / 应用 / 文件 / 运行环境 / 安装设置 / 版本与签名 / 构建）— 框架与全部页面完成
- [ ] M4: 工程化（CLI 完整子命令、升级卸载、自定义 prerequisite、签名）
- [ ] M5: V1.0（完整测试、正式文档、自身打包、真实项目验证）

## 快速开始

```bash
# 运行 GUI（需先安装 Inno Setup 6 才能完整构建安装包）
dotnet run --project src/DotSetupForge.UI

# 创建项目配置并自动分析发布目录
dotnet run --project src/DotSetupForge.CLI -- init --name MyApp --directory ./publish

# 分析应用（生成 analysis.json）
dotnet run --project src/DotSetupForge.CLI -- analyze ./publish

# 解析项目 Runtime 需求并下载到本地缓存
dotnet run --project src/DotSetupForge.CLI -- restore MyApp.pack.json

# 完整构建安装包
dotnet run --project src/DotSetupForge.CLI -- build MyApp.pack.json

# 运行时缓存管理
dotnet run --project src/DotSetupForge.CLI -- runtime list
dotnet run --project src/DotSetupForge.CLI -- clean
```

## 文档

- [软件设计方案](docs/software-design.md)
- [开发计划](docs/development-plan.md)
- [架构说明](docs/architecture.md)
- [路线图](docs/roadmap.md)
