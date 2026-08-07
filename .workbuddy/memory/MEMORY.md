# DotSetupForge 项目长期笔记

## 项目概况
- 通用 .NET Windows 应用打包工具（分析→依赖→Inno Setup→Setup.exe），目标项目名 DotSetupForge
- Git 仓库：https://github.com/2740219773/DotSetupForge.git
- 技术栈：.NET 10 / WPF / CommunityToolkit.Mvvm 8.4 / Scriban 7.2.6 / xUnit / Inno Setup 6（外部编译器）
- 分层：UI → Application → Core + Inno；CLI 与 GUI 复用同一 Application 层

## 当前进度（2026-08-07）
- M0 ✅ M1 ✅ M2-a ✅（Runtime 引擎） | M3 GUI 框架+全部页面 ✅（待交互验证） | M2-b / M4 / M5 待做
- 测试基线：72 个通过（改动后需跑 `dotnet test DotSetupForge.sln`）

## 关键设计决策
- Core 模型全部是 init 不可变 record → GUI 用 EditableProject（可写模型）承载编辑，保存时 ToProject() 还原
- InstallerModel 是业务层与 Inno 生成器的唯一隔离边界
- 错误统一 BuildResult + DiagnosticMessage（DP 错误码），不靠异常表达业务错误
- Runtime 部署模式：SmartOffline（默认推荐）/ Online / SelfContained / None

## 踩坑记录（重要）
1. WPF wpftmp 临时项目不继承 ImplicitUsings → UI 项目每个 .cs 文件显式 `using System.IO;`
2. EditableProject 属性名与 Core 枚举重名（SourceType/RuntimeFamily/InstallScope）→ 内部全限定枚举名
3. RelayCommand<T> 的 CommandParameter 需 `{x:Static}` 传枚举，字符串不自动转换
4. 本机未装 Inno Setup 6，真实 build 需用户安装（InnoSetupLocator 支持 INNO_SETUP_HOME 环境变量）
5. RHCVP 真实测试目录：`D:\WorkProjects\2026\QXS26019 抗辐照芯片功能应用\RHCVP\...\bin\Release\net10.0-windows`

## 约定
- 提交信息风格：`feat: M{n}-{字母} 描述（第X阶段）` / `docs:` / `refactor:`
- 提交按开发计划阶段推进，每阶段配套单元测试
- 开发计划与设计文档根目录中文版 + docs/ 英文名副本，改动需同步
