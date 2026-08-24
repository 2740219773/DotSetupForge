一、总体开发目标

项目暂定：

**DotNetPackager**

V1.0 最终目标是：

> 用户选择一个 .NET Windows 应用的发布目录或 `.csproj`，工具自动识别程序类型、.NET 版本、运行时、CPU 架构和主要文件，自动配置 .NET Runtime 部署策略，并生成可直接交付客户的单文件 Windows 安装程序。

V1.0 最终需要形成三种使用方式：

```text
GUI
DotNetPackager.exe

CLI
dotpack.exe

项目配置
RHCVP.pack.json
```

最终完整流程：

```text
选择应用
   ↓
自动分析
   ↓
生成推荐配置
   ↓
调整文件和安装选项
   ↓
处理 Runtime
   ↓
生成 Inno Setup
   ↓
编译
   ↓
签名（可选）
   ↓
验证
   ↓
XXX_Setup_1.0.0.exe
```

------

# 二、版本路线

建议不要直接叫 V1.0，而是按下面推进：

```text
0.1  核心分析原型
0.2  安装包生成原型
0.3  Runtime 智能部署
0.4  GUI MVP
0.5  升级、卸载、文件策略
0.6  项目化配置
0.7  CLI 和自动化
0.8  签名、完整验证
0.9  Release Candidate
1.0  正式版本
```

整个项目建议分成 9 个开发阶段。

如果一个人使用 AI 辅助开发，比较合理的时间是：

**6～10 周。**

如果先只做到 RHCVP 能用的 MVP：

**2～4 周即可形成可运行版本。**

------

# 三、第一阶段：项目骨架和核心模型

目标：

> 先建立一个不会随着功能增加而失控的软件架构。

这一阶段暂时不做复杂 GUI。

预计：

**2～3 天**

建立解决方案：

```text
DotNetPackager.sln

src/
├─ DotNetPackager.Core
├─ DotNetPackager.Application
├─ DotNetPackager.Inno
├─ DotNetPackager.CLI
└─ DotNetPackager.UI

tests/
├─ DotNetPackager.Core.Tests
├─ DotNetPackager.Inno.Tests
└─ DotNetPackager.IntegrationTests
```

第一批核心数据模型：

```text
ApplicationInfo
RuntimeInfo
ArchitectureInfo
FrameworkInfo

PackageProject
PackageSource
PackageProduct

InstallerOptions
RuntimeOptions
FileOptions
SigningOptions

PrerequisiteInfo
BuildContext
BuildResult
DiagnosticMessage
```

其中最重要的模型是：

```text
PackageProject
```

它代表一个完整的打包项目。

例如：

```text
PackageProject
│
├─ Product
│  ├─ Name
│  ├─ Version
│  ├─ Publisher
│  ├─ AppId
│  └─ MainExecutable
│
├─ Source
│
├─ Runtime
│
├─ Files
│
├─ Prerequisites
│
├─ Installer
│
├─ Signing
│
└─ Output
```

这一阶段同时定义统一结果类型，例如：

```text
BuildResult

Success
Warnings[]
Errors[]
Artifacts[]
Duration
```

以后不要靠抛异常表达所有业务错误。

### 第一阶段验收标准

能够写一个简单 Console：

```text
dotpack test
```

创建：

```text
PackageProject
```

序列化成为：

```text
test.pack.json
```

然后重新读取。

要求：

- 配置保存正常
- 配置恢复正常
- SchemaVersion 存在
- AppId 能持久保存
- 单元测试通过

------

# 四、第二阶段：ApplicationAnalyzer

这是第一个真正的核心模块。

预计：

**4～6 天**

目标：

输入：

```text
D:\...\RHCVP\bin\Release\net10.0-windows
```

自动输出：

```text
主程序：RHCVP.exe
程序类型：WPF
TFM：net10.0
Framework：Microsoft.WindowsDesktop.App
Runtime：.NET Desktop Runtime
架构：x64
部署模式：Framework-dependent
版本：xxx
```

## 2.1 目录扫描

实现：

```text
DirectoryScanner
```

扫描：

```text
*.exe
*.dll
*.runtimeconfig.json
*.deps.json
*.config
*.json
runtimes/
```

同时记录：

```text
文件名
相对路径
大小
扩展名
SHA256
```

------

## 2.2 主程序识别

实现：

```text
ExecutableResolver
```

算法优先级：

第一优先：

```text
AAA.exe
AAA.dll
AAA.runtimeconfig.json
AAA.deps.json
```

四者同名。

第二优先：

查找：

```text
*.runtimeconfig.json
```

然后寻找同名 EXE。

第三优先：

PE + Managed Assembly 分析。

如果还是有多个候选：

```text
需要用户选择
```

绝对不要自动选错。

------

# 五、runtimeconfig 解析器

实现：

```text
RuntimeConfigParser
```

需要识别：

```text
runtimeOptions.tfm

runtimeOptions.framework

runtimeOptions.frameworks

rollForward

applyPatches
```

输出：

```text
TargetFramework
FrameworkName
FrameworkVersion
RollForwardPolicy
```

例如：

```text
net10.0-windows
Microsoft.WindowsDesktop.App
10.0.0
```

进一步映射：

```text
Microsoft.WindowsDesktop.App
→ .NET Desktop Runtime
```

------

# 六、deps.json 分析

实现：

```text
DepsJsonAnalyzer
```

第一版不用做得特别深入。

至少提取：

```text
runtimeTarget
targets
libraries
runtime
native
```

主要用来判断：

```text
RID
native dependencies
runtime dependencies
```

例如：

```text
win-x64
```

------

# 七、PE 和 CPU 架构检测

实现：

```text
PeArchitectureAnalyzer
```

使用：

```text
System.Reflection.PortableExecutable
```

识别：

```text
x86
x64
arm64
AnyCPU
```

结果一定带：

```text
Confidence
```

例如：

```text
Architecture = x64
Confidence = High
Source = PEHeader
```

这样以后发生冲突时容易排查。

------

# 八、程序集信息分析

读取：

```text
AssemblyName
AssemblyVersion
FileVersion
ProductVersion
Company
Description
```

用于自动填：

```text
ProductName
Version
Publisher
```

如果读取不到，再允许用户填写。

------

# 九、Framework-dependent / Self-contained 判断

需要识别：

```text
Framework-dependent
Self-contained
Single-file
```

最终形成：

```text
ApplicationAnalysisResult
```

### 第二阶段验收标准

输入 RHCVP 目录：

```text
dotpack analyze D:\xxx\RHCVP
```

Console 至少输出：

```text
Application
RHCVP

Executable
RHCVP.exe

Framework
Microsoft.WindowsDesktop.App 10.x

Runtime
.NET Desktop Runtime

Architecture
x64

Deployment
Framework-dependent

Files
xx
```

并生成：

```text
analysis.json
```

这一步通过之后才能继续。

------

# 十、第三阶段：RuntimeResolver

预计：

**4～6 天**

这是整个项目第二个核心模块。

目标：

根据：

```text
Microsoft.WindowsDesktop.App
10.0
x64
```

得到：

```text
.NET 10 Desktop Runtime x64
```

以及对应安装包。

------

# 十一、Runtime 统一模型

定义：

```text
RuntimeRequirement

Family
Version
Architecture
MinimumVersion
RollForward
```

例如：

```text
Family:
WindowsDesktop

Version:
10.0

Architecture:
x64
```

Runtime 类型：

```text
DotNetRuntime
DesktopRuntime
AspNetCoreRuntime
```

------

# 十二、RuntimeCatalog

实现：

```text
IRuntimeCatalog
```

第一版：

```text
MicrosoftRuntimeCatalog
```

职责：

```text
根据版本
↓
找到对应 Runtime
↓
得到下载地址
↓
得到文件名
↓
得到校验信息
```

同时要预留：

```text
OfflineCatalog
```

以后公司内网可以使用自己的镜像源。

------

# 十三、下载管理器

实现：

```text
RuntimeDownloadManager
```

需要支持：

```text
断点逻辑可以后做
进度
取消
超时
失败重试
Hash 校验
缓存
```

缓存：

```text
%LOCALAPPDATA%
\DotNetPackager
\Cache
\Runtime
```

例如：

```text
dotnet
└─ windowsdesktop
   └─ 10.0
      └─ x64
         └─ windowsdesktop-runtime-10.xx-win-x64.exe
```

旁边保存：

```text
metadata.json
```

包含：

```text
URL
SHA256
DownloadedAt
Version
Architecture
```

------

# 十四、Runtime 缓存管理

CLI：

```text
dotpack runtime list
```

显示：

```text
.NET Desktop 8 x64
.NET Desktop 10 x64
```

后期支持：

```text
dotpack runtime clean
```

GUI 以后做缓存管理页面。

### 第三阶段验收

RHCVP：

```text
ApplicationAnalyzer
        ↓
RuntimeResolver
        ↓
.NET Desktop Runtime 10 x64
        ↓
Cache
```

第一次下载。

第二次构建：

```text
直接命中缓存
```

不重新下载。

------

# 十五、第四阶段：文件规则和安装模型

预计：

**3～5 天**

目标：

> 不直接把源目录整个扔进 Inno Setup。

先转换成统一：

```text
InstallerModel
```

------

# 十六、FileRuleEngine

实现：

```text
FileRuleEngine
```

文件分类：

```text
Application
Library
Configuration
Data
Log
Debug
Runtime
Native
Unknown
```

默认规则：

包含：

```text
*.exe
*.dll
*.json
*.config
runtimes/**
```

默认排除：

```text
*.pdb
Logs/**
*.log
```

但不能硬编码。

统一：

```text
FileRule
```

例如：

```text
Pattern:
*.pdb

Action:
Exclude
```

------

# 十七、安装位置规则

支持：

```text
ApplicationDirectory
ProgramData
LocalAppData
AppData
Custom
```

例如：

```text
RHCVP.exe
→ {app}

Data
→ {app}\Data

Logs
→ 不安装 / ProgramData
```

这一块后面很重要。

------

# 十八、文件升级策略

定义：

```text
OverwriteAlways

PreserveExisting

InstallIfMissing

DeleteOnUpgrade

NeverUninstall
```

例如：

```text
appsettings.json

PreserveExisting = true
```

以后升级 RHCVP 不会把客户参数覆盖。

------

# 十九、InstallerModel

最终安装器生成模块只认：

```text
InstallerModel
```

不再直接接触：

```text
ApplicationAnalyzer
```

结构：

```text
InstallerModel

Product
InstallScope
InstallDirectory
MainExecutable

Files[]
Directories[]

Shortcuts[]

Prerequisites[]

RegistryEntries[]

Upgrade

Uninstall

Signing
```

这是非常关键的一层隔离。

------

# 二十、第五阶段：Inno Setup 引擎

预计：

**5～7 天**

这一阶段开始真正生成：

```text
Setup.exe
```

------

# 二十一、Inno Setup 集成

首先实现：

```text
InnoSetupLocator
```

寻找：

```text
ISCC.exe
```

推荐两种策略：

开发环境：

```text
用户安装 Inno Setup
```

正式 DotNetPackager：

```text
工具目录携带 Inno 编译器
```

但正式发布之前要再次核对 Inno Setup 的再发布许可。

------

# 二十二、模板生成器

实现：

```text
InnoScriptGenerator
```

使用模板，不拼字符串。

模板：

```text
Templates/Inno/

Setup.sbn
Files.sbn
Icons.sbn
Run.sbn
Registry.sbn
Code.sbn
```

编译前：

```text
InstallerModel
      ↓
Scriban
      ↓
installer.iss
```

------

# 二十三、第一版 Setup

至少支持：

```text
AppName
AppVersion
Publisher
AppId
DefaultDir
PrivilegesRequired

Files

Icons

Run

Uninstall
```

输出：

```text
RHCVP_Setup_1.0.0.exe
```

------

# 二十四、ISCC 调用

实现：

```text
InnoCompiler
```

捕获：

```text
stdout
stderr
exit code
```

解析为：

```text
Build Log
```

不要只判断：

```text
ExitCode != 0
```

还要把编译器错误直接显示出来。

### 第五阶段的重要里程碑

这时候先完全不管 Runtime。

要求做到：

```text
RHCVP目录
↓
分析
↓
InstallerModel
↓
installer.iss
↓
RHCVP_Setup.exe
```

并成功：

```text
安装
运行
卸载
```

这就是：

**Milestone M1**

------

# 二十五、第六阶段：.NET Runtime 安装引导

预计：

**4～7 天**

这是实现最初需求的关键阶段。

最终：

```text
RHCVP_Setup.exe
```

包含：

```text
RHCVP
+
.NET Desktop Runtime
```

------

# 二十六、Runtime 检测脚本

编写 Inno Pascal Script：

```text
DotNetRuntimeDetection.iss
```

输入：

```text
Framework
Major
Minor
Architecture
```

例如：

```text
Microsoft.WindowsDesktop.App
10
0
x64
```

检查目标机。

建议检测顺序：

```text
注册表定位 dotnet
      ↓
检查 Shared Framework Directory
      ↓
解析 Runtime Version
      ↓
兼容性判断
```

如果检测失败，再考虑备用：

```text
dotnet --list-runtimes
```

但不能作为唯一检测机制。

------

# 二十七、PrerequisiteInstaller

安装流程：

```text
NeedRuntime?
    ↓
yes
    ↓
ExtractTemporaryFile
    ↓
Runtime.exe
/install /quiet /norestart
```

处理退出码：

```text
0
成功

3010
成功，需要重启

其他
失败
```

------

# 二十八、安装顺序

正式流程：

```text
InitializeSetup

↓

Windows 检查

↓

Architecture 检查

↓

Prerequisites

↓

.NET Runtime

↓

Main Application

↓

Shortcut

↓

完成
```

如果 Runtime 安装失败：

```text
禁止继续安装应用
```

并给出明确错误。

------

# 二十九、重启处理

一定提前设计。

例如：

```text
.NET 返回 3010
```

不能简单认为失败。

应该：

```text
Runtime 安装成功
RebootRequired = true
```

然后：

```text
继续安装应用
```

最终提示：

```text
安装成功，建议重新启动计算机。
```

------

# 三十、Runtime 解压清理

Runtime installer 不应该长期存在：

```text
Program Files\RHCVP
```

而是：

```text
Setup 内嵌
↓
临时目录
↓
安装
↓
清理
```

------

# 三十一、第六阶段验收

准备两台或两套虚拟机。

机器 A：

```text
没有 .NET 10 Desktop Runtime
```

安装：

```text
RHCVP_Setup.exe
```

预期：

```text
自动安装 .NET
自动安装 RHCVP
RHCVP 正常运行
```

机器 B：

```text
已有满足版本
```

预期：

```text
Runtime 安装步骤自动跳过
```

机器 C：

```text
只有 .NET 9
```

预期：

```text
安装 .NET 10
```

机器 D：

```text
.NET 10 已存在
```

预期：

```text
不重复安装
```

这个里程碑完成，就是：

**MVP Core 完成。**

------

# 三十二、第七阶段：WPF GUI

预计：

**5～8 天**

这时候再开始 UI。

因为核心链路已经验证。

------

# 三十三、GUI 第一版页面

只做：

```text
1 首页
2 应用
3 文件
4 运行环境
5 安装设置
6 构建
```

暂时不做：

```text
插件市场
自动升级
复杂主题
账号
云服务
```

------

# 三十四、首页

```text
最近项目

RHCVP
XXX
XXX

[新建项目]

从项目创建
从程序目录创建
```

------

# 三十五、应用页面

显示：

```text
源目录

主程序

产品名称

版本

发布者
```

下方：

```text
自动检测结果

WPF

.NET 10

Desktop Runtime

x64

Framework-dependent
```

------

# 三十六、文件页面

采用树：

```text
☑ RHCVP.exe
☑ RHCVP.dll
☑ Data
☑ runtimes
☑ appsettings.json
☐ Logs
☐ RHCVP.pdb
```

右侧属性：

```text
安装位置

升级策略

卸载策略
```

------

# 三十七、运行环境

显示：

```text
.NET Desktop Runtime

10.x

x64
```

部署模式：

```text
● 智能离线

○ 在线

○ Self-contained

○ 不处理
```

V0.4 时只真正启用：

```text
智能离线
不处理
```

其他可以先标：

```text
即将支持
```

------

# 三十八、安装设置

```text
安装目录

安装范围

桌面快捷方式

开始菜单

安装完成启动
```

高级：

```text
升级
用户数据
管理员权限
```

------

# 三十九、构建页面

这部分要重点做好。

不要只做 ProgressBar。

显示完整 Pipeline：

```text
✓ 项目验证

✓ 应用分析

✓ 文件收集

✓ Runtime 准备

✓ 生成安装脚本

→ 编译安装包

○ 数字签名

○ 验证
```

下方：

```text
实时日志
```

构建完成：

```text
RHCVP_Setup_1.0.0.exe

103.5 MB

[打开目录]
```

------

# 四十、第八阶段：项目配置系统

预计：

**3～4 天**

前面虽然已经有模型，这阶段把项目化彻底完成。

正式使用：

```text
*.pack.json
```

支持：

```text
新建
打开
保存
另存为
最近项目
```

增加：

```text
schemaVersion
```

例如：

```json
{
  "schemaVersion": 1
}
```

以后 V2 配置改变可以迁移。

------

# 四十一、配置迁移

一开始就加：

```text
IProjectMigration
```

以后：

```text
Schema 1
↓
Schema 2
```

自动升级。

不要等以后格式变了再补。

------

# 四十二、第九阶段：升级与卸载

预计：

**4～6 天**

需要实际测试：

```text
RHCVP 1.0
↓
RHCVP 1.1
```

------

# 四十三、覆盖升级

必须保持：

```text
AppId
```

不变。

测试：

```text
1.0 安装
↓
配置 appsettings.json
↓
安装 1.1
↓
确认用户配置和 Data/** 业务数据保留
↓
程序文件更新
```

------

# 四十四、运行程序检测

升级时如果：

```text
RHCVP.exe
```

正在运行。

安装器需要：

```text
检测进程
↓
提示关闭
↓
自动关闭/用户关闭
↓
升级
```

不能直接文件复制失败。

------

# 四十五、卸载规则

区分：

```text
程序文件
用户配置
数据
日志
```

推荐默认：

```text
程序文件 → 删除

快捷方式 → 删除

配置 → 保留

数据 → 保留

日志 → 可删除
```

------

# 四十六、第十阶段：前置依赖插件体系

这是 V1.0 之前非常值得完成的一项。

预计：

**4～6 天**

建立：

```text
IPrerequisiteProvider
```

第一批内置：

```text
DotNetRuntimeProvider

DotNetDesktopRuntimeProvider

CustomExeProvider

CustomMsiProvider
```

以后扩展：

```text
WebView2
VC++
NI-VISA
数据库
驱动
```

而核心安装 Pipeline 不需要改。

------

# 四十七、自定义 EXE

用户选择：

```text
driver.exe
```

配置：

```text
名称

版本

检测规则

静默安装参数

返回码
```

例如：

```text
/install /quiet /norestart
```

这对工业上位机非常实用。

------

# 四十八、自定义 MSI

使用：

```text
msiexec
```

例如：

```text
/i driver.msi /qn /norestart
```

支持：

```text
ProductCode
```

检测是否已经安装。

------

# 四十九、第十一阶段：CLI

预计：

**3～5 天**

正式实现：

```text
dotpack
```

第一版命令：

```text
dotpack analyze

dotpack init

dotpack restore

dotpack build

dotpack clean
```

例如：

```text
dotpack analyze ./publish
```

------

# 五十、CI 模式

必须支持：

```text
dotpack build RHCVP.pack.json --non-interactive
```

特点：

```text
不弹窗
不要求用户确认
错误返回非零 ExitCode
```

未来可以直接用：

```text
GitHub Actions
GitLab
Jenkins
Azure DevOps
```

------

# 五十一、第十二阶段：代码签名

预计：

**2～4 天**

实现：

```text
SigningService
```

支持：

```text
PFX

Windows Certificate Store
```

调用：

```text
signtool
```

签名对象：

```text
程序 EXE
DLL（可选）
Setup.exe
```

建议默认：

```text
只签 Setup.exe
```

以后高级模式再批量签。

------

# 五十二、密码安全

`.pack.json` 绝不能保存：

```text
PFX 密码
```

明文。

应支持：

```text
环境变量

Windows Credential Manager

构建时输入
```

后期 CI：

```text
环境变量
```

------

# 五十三、第十三阶段：完整测试体系

这部分不能省。

至少建立：

```text
Unit Tests

Integration Tests

Installation Tests
```

------

# 五十四、核心单元测试

重点：

```text
RuntimeConfigParserTests

DepsJsonParserTests

PeArchitectureTests

VersionResolverTests

FileRuleTests

ProjectSerializationTests

InstallerModelTests
```

------

# 五十五、建立测试样本库

在：

```text
tests/fixtures
```

准备：

```text
Net8Console

Net8Wpf

Net10Wpf

Net10WinForms

Net10X64

Net10X86

FrameworkDependent

SelfContained

MultiExecutable
```

不要所有测试都依赖 RHCVP。

RHCVP 用于集成测试。

------

# 五十六、安装验证矩阵

至少：

| 环境                     | 预期         |
| ------------------------ | ------------ |
| Win10 x64 + 无 Runtime   | 安装 Runtime |
| Win10 x64 + 已有 Runtime | 跳过         |
| Win11 x64 + 无 Runtime   | 安装         |
| Win11 x64 + Runtime 正确 | 跳过         |
| 只有 .NET 8              | 安装 .NET 10 |
| 已安装 RHCVP 旧版本      | 升级         |
| RHCVP 正在运行           | 提示关闭     |
| 普通用户                 | 正确 UAC     |
| 离线环境                 | 完整安装     |
| 安装后卸载               | 卸载正常     |

最好建立：

```text
Windows Sandbox
```

或者 Hyper-V 快照环境。

每次测试恢复干净系统。

------

# 五十七、错误测试

人为制造：

```text
Runtime 文件损坏

磁盘不足

安装目录无权限

Runtime 安装失败

Inno Setup 编译失败

app.ico 不存在

主 EXE 不存在

runtimeconfig 损坏
```

工具都应该产生明确错误。

例如：

```text
DP2002

.NET Runtime 文件校验失败
```

而不是：

```text
Object reference...
```

------

# 五十八、第十四阶段：发布和自身打包

DotNetPackager 自己：

```text
Self-contained
win-x64
```

最好：

```text
single-file
```

最终：

```text
DotNetPackager.exe
```

自身不要求目标电脑安装 .NET。

然后：

```text
DotNetPackager
```

也使用自己生成：

```text
DotNetPackager_Setup.exe
```

这相当于：

> 用自己的打包器打包自己。

这是非常好的成熟度测试。

------

# 五十九、完整开发时间表

如果一个人 + AI 辅助开发，我建议按照这个节奏。

| 周期        | 开发内容                        | 里程碑             |
| ----------- | ------------------------------- | ------------------ |
| 第 1 周     | 架构、模型、ApplicationAnalyzer | 能分析 RHCVP       |
| 第 2 周     | RuntimeResolver、缓存、FileRule | 得到完整安装模型   |
| 第 3 周     | Inno Setup、Setup 生成          | M1：RHCVP 能安装   |
| 第 4 周     | .NET 检测、Runtime 离线部署     | M2：真正 MVP       |
| 第 5 周     | WPF GUI、项目配置               | M3：工具可正常使用 |
| 第 6 周     | 升级、卸载、用户数据策略        | Beta               |
| 第 7 周     | Prerequisite、CLI               | 功能基本完整       |
| 第 8 周     | 签名、测试、错误体系            | RC                 |
| 第 9～10 周 | Bug 修复、真实项目验证          | V1.0               |

如果开发速度比较快，可以压缩到：

**6～8 周。**

------

# 六十、建议拆成 6 个里程碑

比按照“完成百分比”更容易管理。

### M0：核心分析器

完成：

```text
RHCVP
↓
ApplicationAnalysisResult
```

验收：

能够正确识别：

```text
RHCVP.exe
.NET 10
Desktop Runtime
x64
```

------

### M1：基础安装包

完成：

```text
RHCVP
↓
Setup.exe
```

可以：

```text
安装
运行
卸载
```

暂不自动部署 .NET。

------

### M2：智能 Runtime

完成：

```text
Runtime 检测

离线 Runtime

自动安装

已有环境自动跳过
```

这就是：

> 第一版真正可使用的 MVP。

------

### M3：GUI

普通用户无需：

```text
命令行
编辑 JSON
编辑 ISS
```

即可完成打包。

------

### M4：工程化

完成：

```text
.pack.json

CLI

升级

自定义 prerequisite

日志

错误码
```

------

### M5：V1.0

完成：

```text
签名

完整测试

正式文档

自身打包

真实项目验证
```

------

# 六十一、任务优先级

建议严格划分。

P0：

```text
ApplicationAnalyzer

runtimeconfig

架构检测

RuntimeResolver

InstallerModel

Inno Setup

.NET Runtime 检测和安装
```

没有这些，产品没有意义。

P1：

```text
GUI

FileRule

配置保存

升级

卸载

日志
```

P2：

```text
CLI

自定义依赖

签名

缓存管理
```

P3：

```text
在线安装

Self-contained 自动 publish

自动更新

插件市场

多语言

主题
```

尤其是：

**自动更新先不要做。**

------

# 六十二、第一阶段实际开发 Issue

如果准备现在就进 GitHub 开始开发，我建议直接建立下面这些 Issue。

```text
#001 创建 DotNetPackager Solution

#002 定义 PackageProject

#003 实现 Pack Project JSON

#004 DirectoryScanner

#005 ExecutableResolver

#006 RuntimeConfigParser

#007 DepsJsonAnalyzer

#008 PeArchitectureAnalyzer

#009 AssemblyMetadataAnalyzer

#010 ApplicationAnalyzer 聚合

#011 为 RHCVP 建立测试样本

#012 RuntimeRequirement

#013 MicrosoftRuntimeCatalog

#014 RuntimeCache

#015 RuntimeDownloadManager

#016 FileRuleEngine

#017 InstallerModel

#018 Inno Template Engine

#019 InnoScriptGenerator

#020 ISCC Compiler

#021 基础 RHCVP Setup

#022 DotNet Runtime Detection Script

#023 DotNet Runtime Installer

#024 Runtime Exit Code Handler

#025 RHCVP 离线安装测试
```

做到 `#025`：

**核心 MVP 就已经出来了。**

然后第二批：

```text
#026 WPF Shell

#027 New Project Wizard

#028 Application Page

#029 Files Page

#030 Runtime Page

#031 Installer Page

#032 Build Page

#033 Build Progress

#034 Build Log

#035 Recent Projects

#036 Upgrade Support

#037 Process Detection

#038 Configuration Preservation

#039 Uninstall Data Policy
```

第三批：

```text
#040 Prerequisite API

#041 Custom EXE Provider

#042 Custom MSI Provider

#043 CLI

#044 Signing

#045 Installation Test Matrix

#046 Error Codes

#047 User Documentation

#048 DotNetPackager Self Packaging

#049 RC Testing

#050 V1.0 Release
```

这样整个 V1.0 大概就是 **50 个比较清晰的 Issue**。

------

# 六十三、建议的第一个开发 Sprint

现在最应该做的不是 WPF 界面，而是一个：

```text
DotNetPackager.CLI
```

第一版甚至只需要：

```text
dotpack analyze
```

例如：

```text
dotpack analyze "D:\RHCVP\bin\Release\net10.0-windows"
```

输出：

```text
DotNetPackager

Analyzing...

Application
  Name: RHCVP
  Executable: RHCVP.exe
  Version: 1.0.0

.NET
  TFM: net10.0-windows
  Framework: Microsoft.WindowsDesktop.App
  Version: 10.0.x
  Required Runtime: .NET Desktop Runtime

Platform
  Architecture: x64

Deployment
  Framework-dependent

Files
  Included: 23
  Excluded: 2

Analysis successful.
```

如果这一步都分析不稳定，那么后面的 GUI 和 Inno Setup 都是在建立在不稳定基础上。

------

# 六十四、下一步实际开发顺序

如果从今天正式开始做，我建议严格按下面顺序：

```text
第 1 步

创建 GitHub 仓库和 Solution
```

↓

```text
第 2 步

完成核心数据模型
```

↓

```text
第 3 步

用 RHCVP 做 ApplicationAnalyzer
```

↓

```text
第 4 步

完成 dotpack analyze
```

↓

```text
第 5 步

RuntimeResolver
```

↓

```text
第 6 步

InstallerModel
```

↓

```text
第 7 步

生成第一个 installer.iss
```

↓

```text
第 8 步

生成第一个 RHCVP_Setup.exe
```

↓

```text
第 9 步

加入 .NET 10 Runtime
```

↓

```text
第 10 步

用干净虚拟机安装验证
```

