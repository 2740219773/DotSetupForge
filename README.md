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
- [ ] M1: 基础安装包（ApplicationAnalyzer → InstallerModel → Setup.exe）
- [ ] M2: 智能 Runtime（检测、离线部署、自动跳过）
- [ ] M3: GUI
- [ ] M4: 工程化（.pack.json、CLI、升级、自定义 prerequisite）
- [ ] M5: V1.0（签名、测试、文档、自身打包）

## 快速开始

```bash
# 运行 CLI test 命令（创建/序列化/反序列化 PackageProject 验收）
dotnet run --project src/DotSetupForge.CLI -- test
```

## 文档

- [软件设计方案](docs/software-design.md)
- [开发计划](docs/development-plan.md)
- [架构说明](docs/architecture.md)
- [路线图](docs/roadmap.md)
