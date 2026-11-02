# ContextMenuX：产品规划、架构设计与 .NET 10 迁移实施方案

> **文档性质：** 可直接交给编程 Agent 执行的项目总方案与任务说明（**不是** `AGENTS.md`）  
> **文档版本：** 1.0 / 2026-11-02  
> **目标产品：** ContextMenuX 4.0  
> **上游项目：** [BluePointLilac/ContextMenuManager](https://github.com/BluePointLilac/ContextMenuManager)  
> **技术路线：** C# + .NET 10 LTS + Windows Forms  
> **主要目标平台：** Windows 11 x64；Windows 10 按官方支持矩阵分级处理  
> **发布目标：** 不要求终端用户预先安装 .NET Runtime 的便携版  
> **优先次序：** 系统安全与行为正确性 ＞ 兼容性 ＞ 功能完整性 ＞ 性能 ＞ 外观改造

---

## 使用说明：给人和 Agent 的同一份任务书

这份文件是**项目规格、技术决策、迁移计划、验收清单和 Agent 任务拆解的合集**，不是要求 Agent 无条件修改全部代码的超级提示词。建议将文件放到仓库根目录的 `docs/ContextMenuX_开发与迁移实施方案.md`，或者直接放在根目录。

**第一次交给 Agent 时只执行“阶段 0：只读审计”，不要立即迁移整个工程。** 后续每完成一个阶段，确认编译、测试、回归和 Git diff，再开始下一个阶段。

本方案同时区分三类信息：

- **[已核实]**：已从上游公开源码、仓库说明或官方文档中直接核对的事实；后续必须在本地检出的具体 commit 上再次确认。
- **[待审计]**：有风险线索、合理推测或既有讨论，但尚未完成全仓扫描和运行验证；禁止当作已证明的缺陷。
- **[规划]**：ContextMenuX 希望实现的能力，不代表原项目现在已有或技术上一定可实现。

**重要限制：** 本文件不是对完整仓库逐行审计的结果。此前针对若干关键源码文件进行了公开源代码抽查；真实开发必须以本地 checkout 的源码、编译器输出和隔离 Windows 环境中的测试为准。文件行号和分支可能变化。

---

# 第一篇：产品与工程决策

## 1. 项目定位

### 1.1 产品名称与版本

- 对外产品名：**ContextMenuX**。
- 初始重大版本：**4.0**。
- 原项目名：**ContextMenuManager**。
- “X”是品牌识别元素，不是要求产品版本始终等于 .NET 的主版本；未来 .NET 升级与产品版本独立演进。
- 原项目属于上游。若以复制、修改原仓库的方式开发，必须清楚标注“基于/衍生自 ContextMenuManager”，不得虚构独立原创历史。

### 1.2 一句话定义

ContextMenuX 是专门面向 Windows 文件资源管理器和系统右键菜单的**安全、可解释、可恢复**的菜单管理工具。

### 1.3 目标用户

1. 想隐藏、多选管理、修改右键菜单的普通 Windows 用户。
2. 需要追踪 Shell 扩展所属软件、CLSID、DLL 的高级用户。
3. 需要通过自定义命令扩展文件/文件夹菜单的开发者和系统管理员。

### 1.4 产品底线

- 不能为了“展示所有菜单”而修改系统注册表。
- 不能为了实现禁用功能而假定任意 Shell 扩展都遵循同一条禁用规则。
- 不能让普通的扫描、刷新、搜索、查看详情具有写入副作用。
- 删除操作必须清楚区别于隐藏/禁用，并提供可审计、尽可能可恢复的路径。
- 不把“Windows 11 新右键菜单能扫描”宣传成“全部现代菜单都能安全禁用”。
- 不演变为不加判断的“Windows 优化大师”或通用注册表清理器。

## 2. 已确定的技术决策（ADR）

| 决策 | 选择 | 原因 | 复审触发条件 |
|---|---|---|---|
| 编程语言 | C# | 复用既有代码、WinForms 和 Windows Interop | 发现无法满足核心 Windows 接口需求且替代路径经过验证 |
| 运行时 | .NET 10 LTS | 现代 API、长期支持、自包含发布 | .NET 10 不可解决的特定兼容问题 |
| UI | WinForms | 保留原界面，避免 Qt/C++ 重写成本 | 出现实质性 UI 能力阻塞且获得项目负责人批准 |
| 工程系统 | SDK-style MSBuild | 简化引用、构建与 CI | 无 |
| IDE | Rider 或 Visual Studio | 本地调试 + Agent 协作 | 无 |
| 架构 | 渐进式分层 | 保留现有功能，测试容易，降低破坏面 | 实际代码依赖不允许安全拆分 |
| 默认架构 | win-x64 | 降低首发兼容成本 | 明确需要 x86/Arm64 |
| 部署 | 自包含目录版优先，单文件版其次 | 两者都不需要用户安装运行时，目录版更易定位问题 | 单文件兼容性测试全面通过 |
| 安全策略 | 默认只读、按操作提权 | 避免改变 ACL/破坏系统 | 无 |
| 发布许可 | 继续遵循原仓库 GPL-3.0 | 源代码衍生作品的合规要求 | 经法律/版权审查后明确变化 |

### 2.1 为什么不换 C++/Qt/WPF

本次项目主要瓶颈不是计算性能，而是旧代码兼容、复杂 Windows Shell 行为、注册表安全性和维护成本。语言重写无法自动修复这些问题，并会引入 UI 复刻和功能回归风险。本项目**保留 C# + WinForms**，不允许 Agent 自行改变此路线。

### 2.2 “不让用户装 .NET”的具体定义

这里要求的是：**终端用户无需安装任何额外 .NET Runtime 即可启动**，而不是最终 EXE 不含 .NET，也不是机器上不存在系统自带 .NET Framework。

正确的发布模型：

- **Self-contained**：应用随发布产物携带指定 .NET 运行时。
- **Single-file**：可选择把大量托管程序集与运行时打包进单个启动文件；部分原生组件仍可能需要解压、留下伴随文件或需要特殊处理。
- 不能仅设置 `PublishSingleFile=true` 就声称自包含；应明确 `--self-contained true`。
- 自包含发布附带的运行时必须随每次新补丁**重新构建和发布**，不能依赖终端机系统全局运行时更新来修补应用中内置的运行时。
- WinForms/COM/反射相关代码迁移期禁止默认开启 `PublishTrimmed`，也不把 Native AOT 作为 4.0 目标。

### 2.3 Windows 10 / 11 支持声明（重要）

用户希望支持 Windows 10/11，但 **.NET 10 官方 Windows 10 支持范围仅限相应 Enterprise/LTSC 等官方列表中的版本**，不是所有家庭版/专业版。Windows 10 普通消费者版本的生命周期也须与微软官方政策区分讨论。

因此制定三级兼容原则：

1. **官方支持且必须验证**：微软 .NET 10 支持矩阵中的 Windows 11 x64 版本。
2. **列入验证计划**：微软官方矩阵中支持的 Windows 10 Enterprise/LTSC 版本。
3. **可尝试运行，但不承诺官方支持**：Windows 10 Home/Pro 等未列入当前 .NET 10 官方支持矩阵的版本。若实际测试正常，可记录“实测兼容”，但不能宣传为“微软官方支持”。

开发者必须在 README 和 Release Notes 明确区分“微软官方运行时支持”和“本应用实机测试通过”。

参考：[微软 .NET Windows 支持矩阵](https://learn.microsoft.com/en-us/dotnet/core/install/windows)。

## 3. 4.0 的范围边界

### 3.1 必须保留

上游 README 列出的主要能力（具体行为以审计建立的功能表为准）：

- 文件、文件夹、目录背景、桌面、磁盘分区等上下文菜单管理。
- 新建菜单、发送到、打开方式、自定义扩展名、WinX 等分类。
- 已支持的菜单项启用、禁用、重命名、图标修改、定位注册表/文件、删除。
- 自定义命令和自定义菜单项。
- 既有 GUID/CLSID 字典、语言包、图像资源、工具栏与页面导航。
- 既有操作习惯和主要 UI 结构。

### 3.2 4.0 必须交付

- [ ] 以 `.NET 10 + WinForms` 成功编译与启动。
- [ ] 原有核心功能完成回归验证。
- [ ] 读取/扫描路径不修改注册表所有者、DACL 或值。
- [ ] 权限相关敏感操作具有明确授权和失败处理。
- [ ] 修改前最小范围备份，至少能恢复软件主动修改过的关键菜单数据。
- [ ] 实现结构化操作结果与明确错误提示。
- [ ] 可生成不依赖终端用户安装 .NET 的 `win-x64` 发布包。
- [ ] 提供审计文档、兼容性说明、基本自动化测试、发布校验。

### 3.3 允许安排到 4.1/后续

- Windows 11 **全部**现代菜单机制的可修改支持。
- 完整全局搜索与复杂过滤器。
- 菜单来源及数字签名完整分析。
- 菜单自动重新出现监控。
- 更高级的备份对比、跨机器配置导入导出。
- 深色主题、现代 UI 大幅改版。

**原则：宁可 4.0 只支持“可靠识别但只读”的新式菜单，也不要发布会损坏系统的修改按钮。**

---

# 第二篇：上游现状与审计范围

## 4. 当前已核实的上游线索

以下内容来自 2026-11-02 的公开源码抽查；Agent 必须以本地代码验证。

| 项目 | 现有迹象 | 分类 | 动作 |
|---|---|---|---|
| `ContextMenuManager/ContextMenuManager.csproj` | `ToolsVersion="15.0"`、`TargetFrameworkVersion` 为 `v4.0`，旧式工程结构 | [已核实] | 评估转换 SDK-style 和目标框架迁移 |
| 同上 | 含固定本机 `PublishUrl` 以及传统 ClickOnce/Bootstrapper 配置 | [已核实] | 分辨仍使用的配置，删除无效历史设置 |
| `Controls/ShellList.cs` | `LoadItems` 在扫描时调用 `TakeRegKeyOwnerShip`；菜单列表枚举调用 `TakeRegTreeOwnerShip` | [已核实] | 极高优先级隔离读写副作用 |
| `BluePointLilac.Methods/RegTrustedInstaller.cs` | 可设置当前用户为所有者、加入 `FullControl`；恢复相关代码被注释，某处存在 `catch { }` | [已核实] | 修复/移除扫描中的权限修改；调查所有调用点 |
| `BluePointLilac.Methods/RegistryEx.cs` | `MoveTo` 为先 `CopyTo` 后 `DeleteKeyTree`；复制/删除缺少可靠恢复闭环 | [已核实] | 加入操作前快照、校验和补偿式回滚 |
| 项目 manifest | 既有对提升权限的依赖 | [待审计] | 评估普通权限启动和按需提权 |
| 更新器 / 旧异步模式 | 过去讨论发现 `WebClient`、委托 `BeginInvoke` 等线索 | [待审计] | 以真实搜索结果为准，逐项替换 |
| `HighDpi.cs` | 过去讨论发现主显示器/手动缩放方式 | [待审计] | 检查在多 DPI 环境中的正确性 |
| 用户反馈 Issue #310 | 有用户报告 Windows Shell 注册表 ACL 异常关联更新失败 | [已核实：存在用户报告；**未独立验证全部因果**] | 建立重现用例，进行隔离调查 |
| 仓库说明 | 原始 README 提供多语言资源、旧 Windows 兼容信息及多个菜单场景 | [已核实] | 制作完整功能清单及迁移兼容表 |

**关键来源：**

- [原始 csproj](https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/ContextMenuManager.csproj)
- [ShellList.cs](https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/Controls/ShellList.cs)
- [RegTrustedInstaller.cs](https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/BluePointLilac.Methods/RegTrustedInstaller.cs)
- [RegistryEx.cs](https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/BluePointLilac.Methods/RegistryEx.cs)
- [相关报告 #310](https://github.com/BluePointLilac/ContextMenuManager/issues/310)

### 4.1 不可以写进报告的伪结论

- “仓库全部安全问题都已定位。”（没有完整审计和全调用链证据）
- “删除接管权限代码后其他功能一定不会损坏。”（可能有操作路径依赖该行为）
- “Win11 所有新菜单都可以用修改注册表方式禁用。”
- “构建成功说明功能完整。”
- “Windows 10 Home 已被 .NET 10 官方支持。”
- “单文件发布保证只生成一个文件、任何插件都无需处理。”
- “回滚机制可以百分之百恢复任何外部变化。”

## 5. Phase 0 必须输出的审计成果

Agent 从项目根目录开始，只读查阅，不修改任何代码或操作系统设置。生成报告（报告文件的写入不属于程序源码修改，但第一次可先只在回复中输出，待用户同意后落盘）：

```text
docs/audit/
  00_REPOSITORY_MAP.md           # 工程树、项目依赖关系、入口与资源
  01_FEATURE_INVENTORY.md        # 全量现有功能/页面/用户动作
  02_MIGRATION_BLOCKERS.md       # Framework → .NET 10 编译/API 阻塞
  03_REGISTRY_SECURITY.md        # ACL/所有权/破坏性操作调用图
  04_SHELL_COMPATIBILITY.md      # Shell、COM、x64、Windows 11 兼容性
  05_UI_AND_RESOURCES.md         # WinForms 控件、资源与 DPI 风险
  06_UPDATE_AND_DEPLOY.md        # 更新器、运行时、单文件发布阻塞
  07_RISK_REGISTER.md            # 风险台账、严重度、证据、处理建议
  08_EXECUTION_BACKLOG.md        # 可实施的任务列表和依赖关系
```

### 5.1 审计步骤

1. 固定上游 commit hash 和分支；记录仓库状态及当前未提交修改。
2. 列出 `.sln`、`.csproj`、`.resx`、`.config`、`.manifest`、`.settings`、语言资源和所有编译输出依赖。
3. 确认应用启动路径、构建目标、权限声明、文件布局、进程启动方式。
4. 构建 UI 导航 → 菜单扫描 → 菜单项渲染 → 状态变更 → 注册表修改 → 资源/更新器的调用链。
5. 枚举所有写操作和特权操作调用点；分类为启动、扫描、点击、后台任务、更新等触发场景。
6. 检查空 `catch`、反射、旧式网络、委托异步、同步阻塞 UI、不可移植资源路径。
7. 识别原有所有功能和菜单分类，不以 README 作为唯一依据。
8. 形成“原始行为/预期行为/迁移行为/测试方法”的对照表。
9. 标注无法在只读静态审计中证实的事项，计划隔离测试。

### 5.2 建议使用的只读搜索命令

```powershell
# 在仓库根目录执行；这些搜索不会修改系统注册表。
rg -n 'TargetFrameworkVersion|ToolsVersion|Reference Include|HintPath|PackageReference' -g '*.csproj'
rg -n 'RegistryEx|RegTrustedInstaller|TakeRegKeyOwnerShip|TakeRegTreeOwnerShip|SetAccessControl' .
rg -n 'DeleteSubKeyTree|DeleteValue|SetValue|CreateSubKey|RegistryKey' -g '*.cs' .
rg -n 'BeginInvoke|EndInvoke|WebClient|ServicePointManager|SecurityProtocol' -g '*.cs' .
rg -n 'DllImport|MarshalAs|CoCreate|COMException|Activator.CreateInstance|Guid' -g '*.cs' .
rg -n 'requireAdministrator|requestedExecutionLevel|dpiAware|dpiAwareness' .
rg -n 'ApplicationSettingsBase|App.config|ConfigurationManager|Settings.Default' .
```

说明：`BeginInvoke` 匹配到的 **Delegate.BeginInvoke** 与 WinForms 控件的 **Control.BeginInvoke** 不是同一个迁移问题；必须分辨类型，不能批量替换。

### 5.3 审计每一项发现的格式

```markdown
#### [P0] SEC-001：菜单枚举路径存在权限修改副作用
- 状态：[已核实/待复现/已修复/已验证]
- 证据：具体文件 + 方法 + 本地行号 + 调用链
- 原始行为：...
- 用户影响：...
- 发生条件：...
- 根因：...
- 建议修复：...
- 不应采用的修复：...
- 测试计划：...
- 回滚计划：...
- 负责人：Agent/开发者
- 验收结果：未完成/已通过
```

---

# 第三篇：架构与数据模型

## 6. 目标架构：渐进式分层，而非过度重构

建议的最终目录（**不是第一天必须完成的目录**）：

```text
ContextMenuX/
├─ ContextMenuX.sln
├─ README.md
├─ LICENSE
├─ CHANGELOG.md
├─ docs/
│  ├─ ContextMenuX_开发与迁移实施方案.md
│  ├─ audit/
│  ├─ architecture/
│  ├─ migration/
│  ├─ security/
│  └─ releases/
├─ src/
│  ├─ ContextMenuX.App/               # WinForms，现有自定义控件、资源、页面
│  ├─ ContextMenuX.Core/              # 菜单模型、用例、策略、操作计划
│  └─ ContextMenuX.Windows/           # Registry/COM/Shell/Privilege/Explorer
├─ tests/
│  ├─ ContextMenuX.Core.Tests/
│  └─ ContextMenuX.Windows.Tests/
├─ tools/                              # 可选：只读诊断/资源迁移工具
└─ .github/workflows/                  # CI、静态检查、发布
```

### 6.1 分层原则

- **App/UI**：处理 WinForms 控件、事件、状态提示、搜索、操作确认；不自行处理注册表所有权。
- **Core**：菜单数据、用户操作语义、权限需求评估、变化计划和操作历史；不依赖 WinForms 或直接 P/Invoke。
- **Windows**：注册表读取/写入、Shell 扩展元信息、UAC、Windows API；可以依赖 Windows，但应暴露最小接口。
- **Tests**：单元测试使用内存假实现或隔离数据；真实 Windows 集成测试单独运行。

### 6.2 依赖方向

```text
ContextMenuX.App  ─────>  ContextMenuX.Core
       │                        ▲
       └──────> ContextMenuX.Windows
                                    │
                                    └─ 实现 Core 中定义的业务接口
```

实际工程可先维持原单项目，在一个阶段内通过命名空间和类划分逐步解耦，确保每一步可构建。

### 6.3 菜单模型（示例，非最终协议）

```csharp
public enum MenuMechanism
{
    ClassicVerb,
    ShellEx,
    ShellNew,
    SendTo,
    WinX,
    ModernExplorerCommand,
    Unknown
}

public enum ModificationCapability
{
    ReadOnly,
    CanDisable,
    CanEnable,
    CanEdit,
    CanRemove
}

public sealed record ContextMenuEntry(
    string Id,
    string DisplayName,
    MenuMechanism Mechanism,
    string? SourceRegistryPath,
    RegistryView? RegistryView,
    string? Clsid,
    string? ExecutablePath,
    string? PackageFamilyName,
    IReadOnlySet<ModificationCapability> Capabilities);
```

必须避免：

- `IsEnabled` 只有 `true/false`，却无法表达“系统菜单有条件展示”“被别的软件禁用”“无法检测状态”。
- `RegistryPath` 只是一串字符串，没有 Hive、32/64 位视图、来源位置和解析失败状态。
- 使用字符串 GUID 但从未验证格式。
- UI 直接调用 `DeleteKeyTree` 实现“禁用”。

### 6.4 模型建议的关键字段

| 字段 | 用途 |
|---|---|
| `StableId` | 从注册机制、来源、场景生成的稳定标识，避免名称变更导致匹配错误 |
| `DisplayName` | 友好名称，支持本地化 |
| `MenuContext` | File / Folder / DirectoryBackground / Drive 等场景 |
| `Mechanism` | ClassicVerb / ShellEx / ModernExplorerCommand 等 |
| `Source` | 注册表位置、物理位置、包身份、文件等来源信息 |
| `RegistryView` | 32 位/64 位视图 |
| `VisibilityState` | Visible / Disabled / Conditional / Unknown / Unsupported |
| `Capabilities` | 具体支持查看、禁用、重命名、删除等动作 |
| `PublisherInfo` | 发布者、文件版本和签名状态（有依据才显示） |
| `Diagnostics` | 缺失文件、权限拒绝、无法解析等诊断信息 |

### 6.5 接口设计原则

建议将**查询**和**命令**区分。只读路径不依赖修改服务，便于静态检查和测试。

```csharp
public interface IMenuDiscoveryService
{
    Task<IReadOnlyList<ContextMenuEntry>> ScanAsync(
        MenuScanRequest request,
        CancellationToken cancellationToken);
}

public interface IMenuChangePlanner
{
    Task<OperationPlan> PreviewAsync(
        MenuChangeRequest request,
        CancellationToken cancellationToken);
}

public interface IMenuChangeExecutor
{
    Task<OperationResult> ExecuteAsync(
        OperationPlan plan,
        CancellationToken cancellationToken);
}
```

以上代码属于结构示例。实现 `ExecuteAsync` 时不允许直接把任意 RegistryPath 当作无需校验的写入目标；必须有白名单/规则约束、明确操作能力和权限检查。

---
# 第四篇：最高优先级——注册表安全与权限整改

## 7. 安全基线：读取不能写入

### 7.1 已核实的调用问题

上游 `ShellList.LoadItems(...)` 在扫描菜单时调用 `RegTrustedInstaller.TakeRegKeyOwnerShip(...)`；`LoadShellItems(...)` 和 `LoadShellExItems(...)` 会调用 `TakeRegTreeOwnerShip(...)`。该权限模块具有设置所有者和增加 FullControl ACE 的实现，相关恢复代码处于注释状态。这是必须优先处理的实际代码风险，不是单纯的“代码风格过时”。

尤其应注意：

- **查看菜单、刷新菜单、进入分类页面、搜索**均不得修改 ACL/Owner。
- 读取受限项失败时，应记录“访问拒绝/部分不可见”，而不是先获取所有权再读取。
- 对无法访问的系统项，允许界面显示“权限不足、未扫描完整”，不可假装无菜单项。
- 在重构前，先列出所有调用 `TakeRegKeyOwnerShip` / `TakeRegTreeOwnerShip` 的位置及其业务目的。
- 不得只删除被调用函数、完全不管真实写入功能是否因此失败；应分离 **只读扫描** 和 **明确授权的修改** 两条路径。

### 7.2 扫描的安全验收

在 Windows 测试虚拟机中：

1. 为可测试的自有 HKCU 注册表键记录内容摘要、所有者、DACL、安全描述符。
2. 启动程序，进入所有分类，刷新、搜索、查看详情。
3. 比较操作前后快照；Owner/DACL/注册表值应无变化。
4. 对权限拒绝场景验证：程序提示部分失败，但不会主动接管权限。
5. 必要时使用 Process Monitor 辅助观察写入请求，但最终判定应依据快照和操作记录。
6. 不用生产设备上的受保护系统注册表项做 ACL 破坏性测试。

> 注意：仅靠注册表内容摘要不能证明 ACL 未变化，必须单独比较 Owner/DACL/继承配置。真实安全描述符处理还涉及权限、继承与审计部分，不能随便承诺“完全恢复所有 SACL”。

## 8. 操作模型：预览 → 备份 → 校验 → 执行 → 验证 → 记录

任何有状态更改都必须遵循以下流程：

```text
UI 明确发起操作
  ↓
识别菜单机制与支持的操作类型
  ↓
生成只读 OperationPlan（展示影响范围）
  ↓
检查目标、权限、注册表视图、并发变化
  ↓
保存前置快照（已验证可读取）
  ↓
必要时取得明确授权 / UAC
  ↓
执行最小修改
  ↓
读取并验证新状态
  ├─ 成功：写入操作日志 / 通知 UI
  └─ 失败：尝试补偿式恢复 / 告知残留影响
```

### 8.1 为什么不能笼统称为“事务”

上游 `RegistryEx.MoveTo` 把复制和删除串联，没有跨进程、跨应用或可靠原子提交保证。普通备份加恢复也无法应对所有竞态和外部程序同时修改情形。

因此建议使用名词：**带日志的补偿式操作（journaled operation with compensating rollback）**。除非经过严格证明，不宣称具备数据库式 ACID 原子性。

### 8.2 注册表移动操作验收

要涵盖：

- 复制失败：源项不得删除。
- 目标已存在：不能无提示覆盖；必须支持冲突策略。
- 复制成功、验证失败：源项保持不变，并处理不完整目标。
- 复制成功、源删除失败：报告实际出现的双份数据，不能伪报成功。
- 同时有外部进程修改源项：使用变更标识/前后快照检测并发冲突，拒绝危险覆盖。
- 源或目标为注册表根/高危父路径：强制拒绝。
- 类型是 REG_EXPAND_SZ / REG_MULTI_SZ / REG_BINARY：复制/恢复不丢失原始类型。
- 32/64 位视图不同：明确展示实际目标视图。
- ACL 复制和恢复仅在明确需求且具有相应权限时执行；处理失败后不能伪报恢复成功。

### 8.3 禁用、隐藏、删除必须区分

- **禁用**：按该扩展机制适用的、已验证的最小变更实现；保留恢复信息。
- **隐藏**：部分注册机制只支持特定情境下的隐藏，需要准确描述语义。
- **删除**：删除注册信息或文件前必须备份与明确确认；有些文件由其他软件安装包维护，应提示可能被恢复。
- **不支持**：未知或包注册机制无法可靠操作时，显示只读或禁用按钮，并说明原因。

每个机制需一张操作能力表，不能通过 `if (RegistryPath != null) CanDelete=true` 推导。

## 9. 备份与历史记录设计

### 9.1 备份结构建议

```text
%LOCALAPPDATA%/ContextMenuX/
  settings/
  logs/
  history/
  backups/
    <operation-id>/
      metadata.json
      registry-snapshot.bin   # 无损的类型化记录 / 安全描述符材料
      checksum.sha256
```

这是建议的本地用户目录，而不是已经确定的原版设置位置。迁移前应确认原版的设置路径并兼容读取。对带管理员权限的操作，要确保提升的进程不会把备份错误地写入另一个账户的用户目录。

`metadata.json` 示例：

```json
{
  "schemaVersion": 1,
  "operationId": "example-uuid",
  "operationType": "DisableEntry",
  "createdAtUtc": "2026-11-02T00:00:00Z",
  "applicationVersion": "4.0.0",
  "menuStableId": "example-id",
  "registryHive": "HKCU",
  "registryView": "Registry64",
  "registryPath": "Software\\Classes\\*\\shell\\Example",
  "restoreSupported": true,
  "snapshotFormat": "ContextMenuXRegistrySnapshotV1",
  "result": "Prepared"
}
```

上例只演示数据形状，绝不能作为真实运行时生成的结果。实际备份还需无损保留值数据、层级和必要的权限信息。

### 9.2 备份生命周期

- 初次写操作必须先有**落盘成功且可验证**的备份。
- 备份必须包括版本号、时间、作用域、原始数据、具体写操作计划和完整性校验。
- 写入过程中先写临时文件、完成校验后再原子替换/移动（按文件系统能力实现）。
- 删除旧备份要经过保留策略，不允许 Agent 擅自清空历史目录。
- 用户可以导出备份，但不能简单把“备份导出”当成恢复成功的证据。
- 默认不记录任何注册表之外无关的用户敏感信息。

### 9.3 历史记录状态机

```text
Planned → BackedUp → Applying → Applied → Verified
                        └── Failed → RollingBack → RolledBack / NeedsManualRecovery
```

- 启动时检测上次未完成操作，提示诊断，不在用户不知情时自动进行危险“清理”。
- 若其他软件改变了同一个注册表项，恢复前需提示“当前值与历史状态不一致”。
- 不承诺撤销其他程序的更改。
- “一键恢复”仅恢复 ContextMenuX 已记录且可验证的操作，**不等于** Windows 系统还原。

## 10. 最小权限与 UAC

### 10.1 长期目标

- 普通用户启动主界面。
- 无需提升即可查看 HKCU/可读的 HKLM 数据。
- 只有明确要修改需要管理员访问的对象时才弹 UAC。
- 复杂特权操作尽量隔离到狭窄的辅助进程，不让 UI 始终高权限运行。

### 10.2 实施约束

- **第一阶段不要单纯删除 manifest 的 `requireAdministrator`**，因为原有操作逻辑可能依赖提升权限；先审计功能依赖和拖放行为，再分阶段切换。
- 提权辅助进程不应接受任意可执行文件路径或无限制注册表删除请求。
- IPC 要验证参数、调用来源与操作允许列表；按需短生命周期启动。
- 用户取消 UAC 是正常业务结果，不能直接转为“未知异常”。
- 操作成功后优先恢复原始权限；如果无法保证，应重新设计为不触碰权限或显式报告风险并取消该动作。

## 11. 32 位/64 位注册表、HKCR 和 UAC 陷阱

- `HKEY_CLASSES_ROOT` 是 HKCU / HKLM 类注册信息的合并视图，不是独立、可直观归属的唯一物理路径。
- 查找结果应尽可能展示真实来源（用户级、机器级、合并视图），避免用户误以为只需编辑一个 HKCR 路径。
- x64 进程默认注册表视图与 x86 不同；使用 `RegistryKey.OpenBaseKey(hive, RegistryView.Registry64/Registry32)` 等明确策略。
- 对 COM 注册信息，特别要考虑扩展模块位数、注册方式和 Explorer 实际可加载性。
- 不默认提升权限写入 `HKLM`，更不可以在扫描阶段改写 `HKLM` 安全描述符。
- 对包化/现代 Shell 扩展，不应用传统 `HKCR\...\shell` 思维强行套用。

---

# 第五篇：.NET 10 迁移实施细则

## 12. 项目迁移顺序

**迁移工程形式与迁移业务逻辑必须分开。** 最好在可回滚的独立分支进行。

### 12.1 迁移前建立基线

1. `git status --short` 确认工作区状态，记录 SHA。
2. 以原工程可使用的开发工具尝试构建。旧框架的 targeting pack 不一定包含在新 SDK 内，不应简单判断为“源码坏了”。
3. 截图记录 UI、页面顺序、主要操作、已有功能列表。
4. 固定已有资源、语言文件、配置存储、程序图标和程序集标识。
5. 标注哪些功能需要真实机器/虚拟机才能验证，禁止在开发者生产机上强行测试。

### 12.2 迁移计划

可选采用 **旧工程 → SDK-style（暂时仍面向 Framework）→ .NET 10** 的过渡路径；如果发现这种中间形态无法可靠构建，则按照实际兼容问题采用最小改动直接迁移。不要把“先升到 Framework 4.8”写成不可变要求。

建议按以下次序处理：

- 旧式 `.csproj` 的显式源文件项、资源项和引用；避免 SDK 默认自动包含与旧显式 `Compile Include` 重复。
- `App.config`、binding redirects、`Settings`、应用程序 manifest 的差异。
- `.resx`、卫星程序集、多语言资源、Designer 生成代码以及图像资源。
- COM Interop、P/Invoke 调用、结构布局、调用约定、x64 架构和异常处理。
- .NET Framework 独有程序集和 API 的可用性。
- 单文件/自包含对插件、资源加载、`Assembly.Location`、工作目录相关逻辑的影响。
- 旧的自动更新机制和多版本运行时判断。

### 12.3 初始工程文件示例（迁移成功后的目标）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Platforms>x64</Platforms>
    <ApplicationIcon>Properties\AppIcon.ico</ApplicationIcon>
  </PropertyGroup>
</Project>
```

这只是简化示意。**禁止直接覆盖旧项目文件**：先移植资源、旧引用、嵌入字典、manifest、特殊构建设置和代码生成依赖。

- 对整个旧项目一次性打开 `Nullable=enable` 可能产生大量警告；允许先在新模块启用，再逐步推广，避免与迁移阻塞混在同一提交。
- 发布相关属性可以放在 `PublishProfile` 或 CI 的 CLI 参数中，不必硬塞到所有 Debug 构建配置里。
- 原来 `AnyCPU` 的行为应结合 x86/x64 P/Invoke 及原生 DLL 审计；4.0 的默认发布目标先限定 x64。
- 第一次构建应追求“不改 UI 行为”，不要同时替换全部自定义控件。

## 13. 重点旧 API 清单

### 13.1 委托 BeginInvoke / EndInvoke

.NET Framework 时代对 `Action` / 自定义 delegate 的异步调用方式在现代 .NET 不再受支持，应改为 Task、async/await 或明确的线程调度。**但 `Control.BeginInvoke` 是 WinForms 把操作排队到 UI 线程的 API，不等同于过时的委托 BeginInvoke，不能用正则全量替换。**

### 13.2 WebClient 与 HTTP

- 避免在新实现继续使用 `WebClient`。
- 使用共享或通过 `IHttpClientFactory` 管理的 `HttpClient`，视工程规模选择。
- 超时、取消、HTTPS、响应状态码与下载大小上限均须处理。
- 不强制打开 SSL 3.0、TLS 1.0、TLS 1.1 等已不安全的旧协议。
- 更新下载使用流式写入和完整性校验。

### 13.3 旧 .NET Framework 特有依赖

按引用审计：`System.Web`、`System.ServiceModel.Web`、`System.Deployment`、`System.Management`、`PresentationFramework` 等现有或可能存在的旧依赖。不要照搬或随意删除：

- 若仅用某程序集的一两个 API，应考虑替换为 .NET/Win32 原生能力。
- 若有现代兼容 NuGet 包，审查其支持版本、维护状态与许可。
- 对无法直接迁移的行为，保留清晰的兼容适配层及测试。

### 13.4 WinForms 生命周期

- 线程切换必须回到 UI 线程更新控件。
- 访问已释放窗体/控件须正确处理取消和退出状态。
- `Task.Run` 可用于纯托管 CPU 工作和适合后台执行的扫描逻辑；**不要把必须在 STA 上运行的 COM/Shell 调用盲目扔进线程池**。
- 需要 STA 的调用，应明确专用 STA 线程/调度器与对象生命周期。
- 所有句柄、HICON、注册表键、流、COM 对象应有清晰释放责任。

## 14. 原有 UI 的保留与 DPI 改进

### 14.1 第一目标：不变

- 主窗体尺寸、导航位置、功能入口、菜单行高、资源图标和交互方式尽量保留。
- 首次迁移不要同时引入完全不同的侧边栏/Fluent 设计。
- 原 WinForms Designer 生成的局部代码尽量通过可视化设计器或原有惯用方式维护。

### 14.2 第二目标：可用性

- 支持不同缩放比例：100%、125%、150%、200%。
- 支持多显示器之间移动，解决字体重叠、行高固定、图标模糊问题。
- 评估 `PerMonitorV2`，先消除旧手动缩放与新自动缩放的冲突。
- 确保中文、英文、长菜单名称、多语言资源不会被截断。
- 提供键盘导航、焦点可见性、合理的 `AccessibleName`、屏幕阅读器基本信息。

### 14.3 不改 UI 但改数据访问

建议在 UI 层引入轻量 ViewModel/Presenter 或 `BindingSource` 等适合 WinForms 的模式，至少保证页面控件不直接操作注册表 ACL。不要为了追求设计模式而把简单窗口拆成几十个抽象类。

## 15. 异步扫描和性能

### 15.1 性能问题来源

- 递归遍历注册表、COM 元信息解析、文件图标提取、资源查找、网络更新检查、UI 逐项创建控件。
- Windows Shell 机制本身存在耗时和第三方组件风险；并非简单切换语言就能显著提速。

### 15.2 性能设计

- 先为扫描过程打点记录阶段耗时、加载菜单条数、失败条数。
- 后台扫描应取消友好、可限制并发，并在 UI 线程分批展示结果。
- 取消请求后不再把过期扫描结果覆盖新页面。
- 优先使用 metadata/注册表读取识别 COM 扩展；扫描阶段不实例化未知第三方 COM 对象。
- 提取图标设置缓存，缓存键包含文件路径 + 更新时间/版本等能有效失效的数据。
- 不把所有菜单项无上限地堆成数千个独立 QWidget/Control；WinForms 长列表需要虚拟化或渐进填充策略。

### 15.3 验收指标（初始目标，需基线化）

| 指标 | 初始建议 | 注意 |
|---|---|---|
| 主窗口可交互 | 尽快出现 UI，扫描不长时间锁住主线程 | 不强行指定脱离硬件条件的毫秒数 |
| 页面切换 | 先显示界面/占位信息，再异步刷新 | 仍可允许小型缓存结果瞬时呈现 |
| 取消扫描 | 能在合理时间停止未开始的工作 | 已开始的不可取消 Win32 API 不能假装可取消 |
| 错误显示 | 权限不足、损坏注册项、未知来源分别提示 | 不能统一吞异常 |
| COM 风险 | 不执行第三方未知扩展代码进行“识别” | 以元信息静态扫描为首选 |

---

# 第六篇：Windows 11 与菜单发现引擎

## 16. 菜单类型与注册机制

建议逐个实现 `Provider`，每个 Provider 必须声明：“扫描方法”“状态判定”“可执行操作”“操作风险”“测试样例”。

| 类型 | 示例 | 首发能力 | 允许的写操作前提 |
|---|---|---|---|
| 静态 Shell Verb | `...\shell\<verb>` | 扫描、展示、审计 | 确认对应菜单上下文和支持的禁用规则 |
| 传统 ShellEx | `ContextMenuHandlers`、CLSID | 扫描、查看 GUID/DLL | 确认注册链与禁用实现，不触碰无关 CLSID |
| 文件关联 | ProgID / SystemFileAssociations | 扫描、来源识别 | 正确处理 HKCR 合并与扩展名关联 |
| ShellNew | “新建”菜单 | 保留原功能 | 先确认原版兼容规则 |
| SendTo | “发送到” | 保留原功能 | 文件/快捷方式操作需备份 |
| WinX | Win+X 菜单 | 保留原功能 | 保持哈希、快捷方式和版本兼容 |
| Windows 11 现代菜单 | `IExplorerCommand` 等 | **只读识别优先** | 在特定机制验证后分批支持安全修改 |
| 未知第三方扩展 | 未知/不完整注册 | 诊断、只读展示 | 不进行猜测性删除 |

## 17. Windows 11 新菜单的具体策略

Windows 11 新版资源管理器的一级菜单和传统“显示更多选项”并不完全相同。现代应用可通过特定 Windows App SDK/应用身份与包声明机制扩展 Explorer 菜单，部分使用 `IExplorerCommand`、COM、`windows.comServer` 与 `windows.fileExplorerContextMenus`。

**不要假设所有菜单来自同一个注册表节点。**

### 17.1 第一阶段：发现，不修改

1. 枚举能安全读取的传统注册点。
2. 识别与 COM CLSID 对应的静态元信息。
3. 根据 Package/AppX/MSIX 注册元数据查找可能的现代菜单扩展。
4. 显示来源、菜单类型、相关进程或 DLL（仅在有依据时显示）。
5. 对无法确定的状态展示 `Unknown` / `ReadOnly`，不冒充准确的启用状态。
6. 与 Windows 11 实际 Explorer 菜单对照测试，记录“已发现但不出现”“出现但未发现”“不能修改”三类差异。

### 17.2 第二阶段：逐机制支持修改

每一种扩展机制需通过：

- 注册表/包声明正式文档或稳定可验证的事实依据。
- 作用范围及影响列表。
- 用户可理解的操作预览。
- 操作前备份方案。
- 至少一个可重复的正反向测试案例。
- 失败与卸载/升级冲突处理。

**禁止**：用“设置 Explorer 全局恢复旧右键菜单”的 hack 作为对所有新菜单的通用管理方式，更不可默认修改用户整台系统的菜单模式。

### 17.3 软件来源追踪

建议的详情信息：

- 可读名称、菜单来源机制和作用场景。
- CLSID、ProgID、包身份、对应注册路径。
- 关联可执行文件 / DLL / Shell 扩展文件。
- 发布者、文件版本、数字签名结果（如有可验证来源）。
- 文件不存在、注册信息不完整、32/64 位不一致等诊断。

不能根据一个 DLL 文件名胡乱推测发布者，更不能将“未签名”直接标记为“恶意”。

## 18. 搜索、过滤、来源诊断

### 18.1 搜索维度

- 菜单显示名称、描述、类别。
- CLI 命令和占位符（避免将敏感用户路径发送到网络）。
- CLSID、ProgID、包身份。
- 注册表路径、DLL/EXE 路径、文件名、发布者。
- 菜单状态与可操作能力。

### 18.2 诊断分级

- **正常**：解析和预期一致。
- **信息**：不影响功能的历史注册项或特殊状态。
- **警告**：缺失文件、访问拒绝、来源不明、数据异常。
- **错误**：操作失败、快照校验失败、权限恢复失败。

不自动删除“疑似孤儿项”，只有在用户明确选择并验证影响范围后才能修改。

## 19. 监控菜单重新出现（后续功能）

某些软件升级、启动或修复安装后会恢复自己的上下文菜单项。

可设计基于操作历史的差异检测：

1. 保存“用户曾经禁用的菜单稳定标识及原始来源”。
2. 用户打开程序时或用户明确开启后台监控时检查其状态。
3. 检测重新出现则通知，展示原因和当前注册信息。
4. 默认不执行永久后台自动禁用；如允许用户自定义规则，必须明确开关、范围、日志和退出路径。

避免创建持续监控全注册表的常驻高权限服务。性能、误报与第三方安装器冲突必须专门测试。

---
# 第七篇：功能增强、更新器与品牌迁移

## 20. 自定义命令编辑器：保留并加固

原项目允许创建自定义菜单和命令。4.0 应先保持行为，再增加校验：

- 确认对应上下文（文件、文件夹、目录背景、驱动器等）。
- 提示路径占位符使用场景，例：不同菜单注册上下文中的 `%1`、`%V` 等并不总是可以互换。
- 展示**保存前的完整命令模板**和引用规则；在测试样本中安全预览参数替换。
- 校验可执行文件是否存在；处理引号、带空格路径、环境变量、工作目录。
- 自定义图标路径和资源索引要能够可靠保存/还原。
- **“校验”不得直接执行命令**；执行测试必须由用户显式点击并提示程序的风险。
- 避免以 `cmd /c` 或 PowerShell 作为不必要的默认执行方式。
- “运行程序的用户输入”应按结构化参数传递，不要不必要地拼接成 Shell 命令。

## 21. 功能优先级与取舍表

| 功能 | 优先级 | 价值 | 难度 | 建议版本 |
|---|---|---|---|---|
| 扫描不改 ACL | P0 | 安全底线 | 中高 | 4.0 |
| 权限操作错误报告 | P0 | 安全底线 | 中 | 4.0 |
| .NET 10 + 自包含 | P0 | 核心迁移目标 | 中高 | 4.0 |
| 修改前备份与恢复 | P0 | 防止用户数据丢失 | 高 | 4.0 |
| 旧版全部核心功能回归 | P0 | 用户基础 | 高 | 4.0 |
| 分机制的操作能力判断 | P0 | 防止错误修改 | 中高 | 4.0 |
| 稳定 CI 与版本管理 | P0 | 可持续维护 | 中 | 4.0 |
| Windows 11 现代菜单只读发现 | P1 | 填补旧项目不足 | 高 | 4.0/4.1 |
| 菜单来源追踪 | P1 | 用户可解释性 | 中 | 4.1 |
| 全局搜索与筛选 | P1 | 便利性 | 低中 | 4.1 |
| 自定义命令验证 | P1 | 减少错误配置 | 中 | 4.1 |
| 更新器可靠性改造 | P1（若 4.0 继续内置更新则变 P0） | 发布安全 | 高 | 4.0/4.1 |
| 批量启用/禁用 | P2 | 效率 | 中，安全要求高 | 4.1+ |
| 导入导出/备份比较 | P2 | 高级使用 | 中高 | 4.1+ |
| 重新出现监控 | P2 | 抗重复注册 | 中高 | 4.2+ |
| 深色模式 | P2 | 体验 | 中 | 4.2+ |
| 新 UI 皮肤 | P3 | 视觉 | 高 | 暂不安排 |

**若进度不足：优先砍 P2/P3，不降低 P0 的安全标准。**

## 22. 更新系统与供应链安全

### 22.1 先评估旧更新器

审计内容：

- 旧更新 URL、`Update.ini`、版本解析规则、代理、TLS 和下载错误处理。
- 程序是不是覆盖自身、使用临时文件/外部进程完成替换。
- 下载文件是否验证大小、哈希、真实性。
- 是否依赖 .NET Framework 版本判断或旧 API。
- 是否需要管理员权限才能更新便携版。
- 旧版是否能安全迁移到新产品，还是应明确要求用户手动下载 4.0。

### 22.2 更新方案可选

**A：第一版只提供 GitHub Releases + 手动下载**（更简洁、降低自动更新安全风险）。

**B：提供应用内更新检查与下载**（需经过以下验证）：

- 仅接受明确授权的发布来源。
- HTTPS 传输 + SHA-256 完整性校验。
- 对高风险更新最好增加可靠的签名/签名清单校验。**仅从同一不受信任来源获取的哈希，不等于独立真实性保障。**
- 支持失败回滚和旧版本保留。
- 不在进程运行期间直接覆盖仍被使用的 EXE。
- 避免临时目录路径注入、可执行文件替换竞争、未验证文件提权执行。
- 取消更新、断网、空间不足时程序仍可正常使用。

**建议**：4.0 初期选择 A，除非经过审计确认旧更新器可以安全、低成本地迁移。

## 23. ContextMenuManager → ContextMenuX 品牌和兼容性迁移

### 23.1 改名顺序

1. 先确定对外显示名称和 README。
2. 检查 `AssemblyName`、`RootNamespace`、窗体资源、配置持久化、路径硬编码、更新版本信息。
3. 尽可能保留内部命名至运行时迁移成功，再逐阶段重命名。
4. 若更改配置目录，实现仅一次且幂等的配置迁移；不删除旧目录。
5. 发布时清楚说明新名称与旧项目关系。

### 23.2 用户配置迁移

- 首次启动时探测旧版配置，只读取、展示迁移摘要，且不可覆盖当前用户新配置。
- 提供迁移失败恢复路径；完成迁移后可保留旧文件作为备份。
- 旧版设置可能含文件路径、语言资源选择、窗口位置、菜单配置，不能只迁移一个主设置文件。
- 对超过当前权限的旧配置路径，不应为了迁移擅自提权。

### 23.3 GPL-3.0 与上游署名

原仓库标为 GPL-3.0；如果 ContextMenuX 是它的衍生作品，需要保留原版权与许可信息，并根据 GPL 要求处理完整相应源代码及分发义务。

- 不删除上游作者署名、原始 license 和有关版权说明。
- 新产品名称不意味着可以把原代码重新以闭源/不兼容协议分发。
- 修改文件和新增文件保留合适的版权/变更记录。
- 审核图标和其他第三方素材的单独许可，不自动假定全部资源都能不加条件改名复用。
- 如需要商业化分发或双重许可，先进行明确的许可证审查，不在 Agent 任务中做未经授权的法律结论。

此部分为工程合规提示，不代替法律意见。

---

# 第八篇：测试策略、CI、诊断与部署

## 24. 测试分层

### 24.1 Level 1：纯单元测试（可常规 CI）

必须涵盖：

- 菜单来源路径解析、注册表 Hive、RegistryView。
- CLSID/GUID 识别与未知状态。
- 菜单分类和功能能力计算。
- 操作计划的参数拒绝、关键目录拒绝。
- Shell 命令占位符、引号、文件路径测试。
- 备份序列化、反序列化、校验、旧 Schema 拒绝。
- 操作状态机、部分失败与回滚结果。
- 旧版设置迁移、失败幂等与冲突处理。

尽量使用假服务和内存模型，绝不依赖生产系统 Shell 注册表。

### 24.2 Level 2：HKCU 沙箱式注册表集成测试（专用执行）

测试键示例：

```text
HKEY_CURRENT_USER\Software\ContextMenuX\Tests\<run-id>\
```

规则：

- 每个测试运行拥有独立随机的 `<run-id>`。
- 只删除自己创建的键，禁止递归删 `Tests` 父目录或任意真实系统注册项。
- 路径必须受强校验，缺少 run-id 或与许可前缀不符时立即拒绝写操作。
- 不在系统 Shell 真实注册点执行集成写测试。
- 测试进程不需要管理员权限。
- 覆盖 REG_SZ、REG_EXPAND_SZ、REG_DWORD、REG_QWORD、REG_BINARY、REG_MULTI_SZ 等多种类型和空/非法路径。
- 测试失败时尽可能清理，但清理只限于该次运行的专用测试键。

### 24.3 Level 3：Windows 虚拟机手动/隔离测试

仅在快照可恢复的虚拟机、测试机中进行：

- HKLM 正常权限、访问拒绝、权限取消。
- 需要系统管理员权限的菜单操作。
- 受保护 Shell 项行为。
- COM 位数差异和 ShellEx 解析。
- Windows 11 一级菜单与“显示更多选项”的对照。
- Explorer 刷新与可选重启。
- 外部程序改回菜单导致的并发冲突。
- 撤销、失败回滚、故障恢复。

**任何涉及 TrustedInstaller 所有权、ACL 的破坏性实验均不可在开发者日用 Windows 上进行。**

### 24.4 Level 4：UI 回归与可访问性

- 所有旧功能分类可进入、加载并刷新。
- 菜单列表有滚动、图标、空状态、错误状态。
- 切换分类时正在进行的后台扫描不会串页。
- 创建/删除/启用/禁用操作有明确成功或失败反馈。
- 高 DPI 与多显示器移动不会导致控件溢出。
- 中文/英文长文本、最小窗口大小、窗口最大化。
- 键盘 Tab 顺序、快捷键、焦点状态、屏幕阅读器可识别的主要操作。

## 25. 测试矩阵

| 环境 | 运行/启动 | 读取扫描 | 允许的修改 | 发布验证 | 备注 |
|---|---|---|---|---|---|
| Windows 11 x64（官方支持版本） | 必须 | 必须 | 隔离环境必须 | 必须 | 首要目标 |
| Windows 10 Enterprise/LTSC（官方矩阵列出的版本） | 必须测试 | 必须测试 | 隔离环境抽样 | 建议 | 官方 .NET 10 矩阵范围内 |
| Windows 10 Home/Pro 22H2 | 尽力测试 | 尽力测试 | 隔离环境 | 可选 | 不承诺官方支持 |
| Windows 11 非管理员账户 | 必须 | 必须 | HKCU 必须 | 必须 | 验证按需提权 |
| Windows 11 管理员账户 | 必须 | 必须 | 隔离环境 | 必须 | UAC 开启 |
| 干净 Windows（未装 .NET Desktop Runtime） | 必须 | 基本验证 | 可选 | **必须** | 核心目标 |
| 125%/150%/200% DPI | 必须 | 可选 | 可选 | 可选 | WinForms 可用性 |
| Windows x86/Arm64 | 暂不要求 | 暂不要求 | 暂不要求 | 暂不要求 | 不属于首发承诺 |

## 26. 诊断与日志

### 26.1 日志内容

建议记录：

- 应用版本、Runtime 版本、OS Build、位数、当前权限状态。
- 扫描类别、项目数量、各类错误数量、耗时。
- 操作 ID、目标菜单 ID、操作类型、执行结果、备份位置。
- 权限不足、签名检查失败、文件缺失、非预期异常。

### 26.2 隐私与最小日志

- 日志默认本地保存。
- 用户主目录、命令实参、网络路径、个人文件名等可能敏感；导出日志时需要可选脱敏预览。
- 不在日志中保存注册表二进制秘密数据或完整用户环境变量。
- 不默认上传日志、扫描记录和系统详情。
- 诊断包需要用户明确选择导出，默认不发送到远程服务器。

### 26.3 错误反馈

禁止空 `catch` 和只报告“失败”的通用提示。失败结果至少包含：

- 哪个动作失败。
- 哪个项目、路径（按隐私策略显示）。
- 失败原因分类（权限不足、目标已变、无此项、备份失败、数据无效等）。
- 是否已发生部分修改。
- 是否已完成恢复，以及残留风险。
- 下一步安全操作建议。

## 27. CI/CD 与仓库约定

### 27.1 CI 最小工作流

GitHub Actions（Windows runner）可执行：

1. `dotnet restore`。
2. `dotnet build -c Release`。
3. `dotnet test` 执行纯单元测试。
4. 静态格式、nullable/编译警告扫描。
5. 在版本标记或专用发布任务上执行 `dotnet publish`。
6. 检查产物目录与文件大小，生成 SHA-256。
7. 把日志、测试报告、产物作为 CI artifacts 归档。

在普通 CI 上**不要**运行真正修改系统 Shell、TrustedInstaller/ACL 的测试。

### 27.2 分支与提交

```text
main
├─ audit/source-and-security
├─ migration/net10
├─ safety/read-only-scan
├─ safety/registry-journal
├─ refactor/windows-services
├─ feature/win11-discovery
└─ release/4.0
```

提交原则：

- 一个 PR 尽量只包含一个主题。
- 不将批量格式化、重命名、功能更改混合在同一 PR。
- 每次改动必须说明测试级别和未验证点。
- 大改之前有可恢复的 Git commit/tag。
- Git 检查/审查是 Agent 交付的一部分，不需要 Agent 自行合并到 main。

### 27.3 版本管理

- 产品版本遵循 `4.0.0`、`4.0.1`、`4.1.0` 等软件语义，而非 `10.0` 对应 Runtime。
- `AssemblyVersion`、`FileVersion`、`InformationalVersion` 经过规划后使用一致策略。
- Release Notes 分为：安全修复、功能、兼容性、已知限制、升级注意事项。
- 必须明确是否与原版 ContextMenuManager 配置兼容。

## 28. 便携版与单文件发布

### 28.1 顺序

**先跑通自包含目录版，再跑通单文件版。**

示例：

```powershell
# 先发布文件夹版（不需要终端用户安装 .NET）
dotnet publish .\src\ContextMenuX.App\ContextMenuX.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -p:PublishTrimmed=false

# 通过目录版运行和回归后，再尝试单文件
dotnet publish .\src\ContextMenuX.App\ContextMenuX.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false
```

命令以迁移后的项目实际路径为准；尚未创建上述目录前不要直接盲目运行。

### 28.2 单文件陷阱

必须审计：

- 旧代码是否通过 `Assembly.Location` 或当前工作目录定位资源；打包后含义可能改变。
- `.resx` 嵌入资源和语言文件是否能正确加载。
- 更新器是否尝试覆盖当前执行文件。
- 启动期间原生 DLL 提取路径和权限。
- 应用启动、二次启动、程序退出与残留临时文件。
- 自定义外部 XML/语言包/可编辑配置究竟是内置、旁置还是首次启动时释放。
- 杀毒误报、SmartScreen、数字签名和长期发布稳定性。

### 28.3 最终产物

建议：

```text
ContextMenuX-4.0.0-win-x64-portable.zip
ContextMenuX-4.0.0-win-x64-singlefile.zip  # 可选，验证通过才发布
SHA256SUMS.txt
CHANGELOG.md
LICENSE
README.md
```

如果单文件版仍需旁置语言包、配置模板或帮助文件，应如实说明；不为了形式上的单 EXE 强行将所有可变资源埋进程序内部。

### 28.4 发布验收（不得跳过）

- 在未安装全局 .NET Runtime 的全新 Windows 虚拟机运行。
- 验证启动不出现缺少 .NET 提示。
- 检查全部资源和语言包可用。
- 检查扫描及菜单管理功能。
- 检查旧版配置迁移或明确提供手动迁移说明。
- 测试断网、权限拒绝、删除备份后恢复等合理的失败场景。
- 用 `Get-FileHash` 计算 SHA-256，并公布下载产物校验信息。
- 更新所携带的 .NET 运行时补丁版本，审查已知漏洞。

---

# 第九篇：分阶段实施计划（Agent 可直接照单执行）

> 下列阶段“完成条件”是验收门槛，不是时间承诺。每次只交付一个阶段，禁止把所有阶段混成一次巨型 PR。

## 29. 阶段 0：全仓只读审计（第一项任务）

**目标：** 确定真实源码状态、迁移阻塞、安全风险及改造依赖。

**允许：** 读取代码、Git 信息、文档、官方 API 文档；编译原项目（仅在不会运行危险功能时）；撰写分析报告。

**不允许：** 改代码、修改实际系统注册表、自动修复权限、重写 UI、删除资源、升级整个解决方案。

**交付：** 第 5 节所列 9 份审计报告或等价结构化报告，包含代码证据和风险分级。

**通过标准：**

- [ ] 每个现有项目与主要模块有清晰清单。
- [ ] 已有功能清单覆盖 UI、菜单、系统操作和资源。
- [ ] 所有注册表写入/ACL/Owner/进程权限操作路径已检索并给出调用链。
- [ ] 已建立 .NET 10 不兼容 API 清单，确认哪些只是警告。
- [ ] 提供迁移先后依赖和最小可运行里程碑。
- [ ] 报告把“确认”与“未验证”分开。

## 30. 阶段 1：建立可复现的旧版基线

**任务：**

- [ ] 固定原版 tag/commit SHA。
- [ ] 创建 `migration/net10` 分支。
- [ ] 原工程旧工具链构建，记录限制；如无旧 targeting pack 则记录环境缺失而不是推测源码错误。
- [ ] 保存所有原始 DLL 依赖、资源目录、语言文件、应用图标与配置约定。
- [ ] 在隔离 Windows 测试机留存 UI 截图、基本运行路径和功能行为。
- [ ] 创建初步无副作用的单元测试工程（可随后接入）。

**通过标准：** 原始基线及无法测试的部分明确记录；后续回归有对照物。

## 31. 阶段 2：项目格式与运行时迁移

**任务：**

- [ ] 转换 SDK-style；明确源文件、嵌入资源和多语言文件包含规则。
- [ ] 改为 `net10.0-windows`、`UseWindowsForms=true`。
- [ ] 处理缺失的 Framework-only API 和程序集。
- [ ] 处理旧委托 BeginInvoke、WebClient、配置、manifest、COM Interop。
- [ ] 在 Windows 11 x64 编译并启动应用。
- [ ] 验证 UI 的主导航和所有基础页面可显示。
- [ ] 比对原版控件外观和资源，修复明显回归。

**通过标准：** Debug/Release 构建成功，程序能启动，不出现主页面缺失；已知未实现操作标出并禁用危险按钮，不宣称全功能完成。

## 32. 阶段 3：只读扫描安全整改（P0）

**任务：**

- [ ] 清除 `LoadItems` 等只读路径中的权限修改。
- [ ] 所有扫描通过只读接口，明确禁止 SetValue/Delete/SetAccessControl。
- [ ] 对权限拒绝实现返回部分结果和可解释状态。
- [ ] 为受限访问情形和 Owner/DACL 无变化写测试。
- [ ] 搜索全项目确认没有其他“读操作触发写入”的路径。

**通过标准：** 在隔离环境中扫描前后的 Registry values/Owner/DACL 保持不变；无因权限拒绝导致界面崩溃；缺失项不被错误地当作删除成功。

## 33. 阶段 4：安全写入、权限和补偿式恢复（P0）

**任务：**

- [ ] 引入 OperationPlan/OperationResult，明确操作能力与不允许的路径。
- [ ] 增加修改前类型化快照和校验机制。
- [ ] 重写/封装 `MoveTo`、`DeleteKeyTree` 等危险路径的调用方式。
- [ ] 实施操作日志、异常分类和失败恢复。
- [ ] 逐项验证禁用、启用、移动、删除、恢复。
- [ ] 评估按需提权及有权限的辅助进程方案。

**通过标准：** 完整通过 HKCU 隔离用例；受限项不被强行接管；失败的部分状态准确展示；没有无法确认是否成功的静默危险修改。

## 34. 阶段 5：整理 Core 与 Windows 层

**任务：**

- [ ] 提取菜单模型和适当的服务接口。
- [ ] 将 UI 的系统调用移入 Windows 服务层。
- [ ] 保留最少必要的向后兼容适配。
- [ ] 让单元测试可通过假服务验证所有核心规则。
- [ ] 逐步消除跨界面复制的 Registry API 逻辑。

**通过标准：** UI 层不直接负责 ACL、Owner 或未经规划的注册表删除；测试中可构造菜单并验证禁用/恢复计划而不碰注册表。

## 35. 阶段 6：Windows 11 识别和诊断

**任务：**

- [ ] 创建 ClassicVerb、ShellEx、ModernProvider 等可扩展发现逻辑。
- [ ] 识别现代菜单相关 COM/包元信息，并在界面明确标识机制。
- [ ] 对照真实 Windows 11 Explorer 的菜单清单，形成缺失/误判记录。
- [ ] 加入来源详情、CLSID、DLL/路径及未知状态。
- [ ] 未经验证的现代菜单统一只读，不实现猜测性禁用。

**通过标准：** 可以可靠展示已支持的现代/传统菜单来源；不能识别时明确标注；系统未被扫描过程修改。

## 36. 阶段 7：功能增强（按优先级选择）

建议从最小可见价值开始：

1. 全局搜索与状态筛选。
2. 菜单来源/缺失文件诊断。
3. 更清楚的操作预览和撤销入口。
4. 自定义命令占位符及引号校验。
5. 导入/导出、监控、深色模式等后续能力。

**通过标准：** 每个新功能有独立测试和回退方式，不能对原来的菜单类别或自定义命令造成回归。

## 37. 阶段 8：打包、安装环境与发布

**任务：**

- [ ] 目录版自包含通过干净 Windows 11 VM 测试。
- [ ] 单文件版通过所有相关功能回归后才作为额外产物。
- [ ] 发布版本、许可证、上游署名、配置迁移说明。
- [ ] GitHub Actions 自动构建、校验与生成制品。
- [ ] 建立兼容性矩阵、发布说明和已知限制。

**通过标准：** 终端用户不安装 .NET Runtime 也能使用；未测试的 Windows 版本不写入“已全面支持”；至少有一次完整人工回归记录。

---

# 第十篇：Agent 使用方式、任务模板与交付协议

## 38. 最重要的协作原则

本方案是**一次任务执行的规格输入**。不同 Agent 是否自动读取某个文件取决于使用的 IDE/插件。应在每轮任务中明确指定文件路径及当前阶段，而不是假定任何 Agent 会自动读取普通 `.md`。

开发者的推荐操作：

1. 在 Rider 打开解决方案并检查 Git status。
2. 把本方案文件加入仓库。
3. 在 Agent 对话里要求阅读此文件。
4. 第一次只执行阶段 0；审计结论经人工审查后再授权修改。
5. 以后每次只派一项任务/一个阶段，明确文件范围和不允许做的动作。
6. Agent 交付后用 Rider 运行编译、单测、查看 diff，再在隔离 Windows VM 做系统级测试。

## 39. 第一次给 Agent 的完整任务（可直接复制）

```text
项目：ContextMenuX
当前任务：阶段 0 —— 全仓只读审计，不做源码修改。

请先完整阅读：docs/ContextMenuX_开发与迁移实施方案.md
（如果文件不在 docs，请在仓库根目录寻找同名文件。）

上游项目：BluePointLilac/ContextMenuManager。
目标：保留 C# 和 WinForms，将项目安全迁移到 .NET 10，
最终实现无需最终用户安装 .NET Runtime 的 self-contained 发布。

要求：
1. 列出所有解决方案项目、依赖关系、主入口、WinForms 资源、
   配置文件、语言包以及原有功能分类。
2. 检查 Framework 4.0 → .NET 10 的兼容性阻塞，包括旧 API、
   COM、P/Invoke、系统管理程序集、资源加载、DPI 和更新器。
3. 重点审查 ShellList、RegTrustedInstaller、RegistryEx 及其
   所有调用点，建立完整的 registry owner / ACL / 写入操作调用图。
4. 明确判断：只读扫描、查看菜单、刷新是否会修改注册表 ACL/Owner。
5. 检查 HKCR 合并视图、HKCU/HKLM、x64/x86 RegistryView 处理。
6. 查找空 catch、错误吞噬、无法回滚的删除/移动、UI 主线程阻塞。
7. 明确 Windows 11 现代菜单与传统菜单的发现边界。
8. 列出全部功能和回归测试清单，给出具体迁移依赖顺序。

每一个发现均必须包含：
- 完整文件路径、类名、方法名和本地行号；
- 源码证据/调用链；
- 问题与可能用户影响；
- P0/P1/P2/P3 严重度；
- 最小范围修复建议；
- 可能产生的兼容性副作用；
- 建议测试；
- 确认/待验证状态。

绝对限制：
- 不修改程序源代码、工程文件、语言文件和资源；
- 不执行写注册表或改变 Owner/DACL 的操作；
- 不启动会自动修改真实系统菜单的程序；
- 不在开发者日常使用的 Windows 上做破坏性测试；
- 不升级所有依赖，不自行迁移代码；
- 不删除旧功能或改 UI；
- 不将猜测写成已确认的事实。

输出：按方案中的 9 份审计成果组织完整审计报告。
如果没有权限创建文件，直接在对话中输出对应章节。
最后列出下一阶段最小任务包，等待我批准后再修改源码。
```

## 40. 后续给 Agent 的小任务模板

```text
项目：ContextMenuX
先读：docs/ContextMenuX_开发与迁移实施方案.md
当前任务：[例如：SEC-001 只读扫描修复]

已有结论：[链接审计报告/代码位置]
本次允许修改：[文件或目录列表]
本次禁止修改：[文件或目录列表]
必须保留：[具体旧行为]
预期输出：[具体功能/测试/文档]

执行：
1. 说明理解的调用链和风险。
2. 给出最小修改方案。
3. 只修改本次任务相关文件。
4. 添加/更新针对性测试。
5. 构建与执行允许的测试，不运行危险系统级写操作。
6. 自查 Git diff，报告兼容性影响。
7. 按验收清单列出通过/未通过/未测试。

不要扩展任务范围，不要顺手重命名整个项目，不要修改真实 Shell 注册项。
```

## 41. Agent 每轮交付的固定格式

Agent 应在每轮结束给出：

```markdown
## 本轮任务
- 编号与目标：...
- 状态：已完成 / 部分完成 / 阻塞

## 修改文件
| 文件 | 修改原因 | 对旧行为影响 |
|---|---|---|

## 结果
- 已完成：...
- 未完成：...
- 已知风险：...

## 验证
- 构建命令与结果：...
- 单元测试命令、数量与结果：...
- 集成测试：已通过 / 未运行（说明原因）
- 手动 Windows 验证：已通过 / 未运行
- Git Diff 审核要点：...

## 下一步建议
- 最小后续任务：...
- 是否需要人工授权：...
```

禁止以下表述：

- 没有真正运行，却说“所有测试已通过”。
- 仅编译成功，就说“Windows 11 已完全兼容”。
- 未确认恢复流程，就说“任何修改都可撤销”。
- 代码里保留 `catch {}`，但说“异常处理完善”。
- 未验证安装环境，就说“完全单文件、免依赖”。

---

# 第十一篇：风险台账与决策原则

## 42. 风险台账（初始版）

| ID | 风险 | 影响 | 优先级 | 避免/缓解措施 | 验证方式 |
|---|---|---|---|---|---|
| R-001 | 扫描时变更 Registry Owner/DACL | 可能影响系统安装、更新和其他应用 | **P0** | 只读接口，禁止扫描接管权限 | ACL 快照对比与源码调用审查 |
| R-002 | 复制后删除导致数据残缺 | 用户菜单配置丢失 | **P0** | 先备份、验证目标，补偿式恢复 | 故障注入/模拟失败 |
| R-003 | 不同注册表视图产生误判 | 菜单漏检或改错位置 | P1 | RegistryView 显式建模 | x64/x86 测试 |
| R-004 | 旧 API 在 .NET 10 不兼容 | 不能编译或运行期异常 | P1 | 编译清单与针对性替换 | 构建/集成测试 |
| R-005 | COM/Shell 扩展线程模型错误 | 死锁、崩溃、状态异常 | P1 | 对特定 COM 对象保持 STA 等约束 | 隔离调用测试 |
| R-006 | Windows 11 现代菜单误分类 | UI 声称可操作但实际无效 | P1 | 机制识别+能力表 | Windows 11 对照测试 |
| R-007 | 项目改名导致配置丢失 | 升级体验倒退 | P1 | 幂等配置迁移、保留旧数据 | 跨版本升级测试 |
| R-008 | Single-file 资源定位失效 | 应用启动/图标/语言异常 | P1 | 先目录版，后单文件测试 | 干净 VM 启动 |
| R-009 | 更新文件未验证真实性 | 供应链/执行风险 | **P0**（若内置自动更新） | 签名或可信完整性机制；必要时暂关自动更新 | 篡改包拒绝测试 |
| R-010 | 终端用户需要额外运行时 | 核心产品目标未实现 | P0 | Self-contained 干净机验证 | 没有 .NET Runtime 的 VM |
| R-011 | Windows 10 支持声明过度承诺 | 技术/维护承诺错误 | P1 | 明确官方矩阵和实测兼容区别 | README 审查 |
| R-012 | GPL/资源许可处理不当 | 开源合规问题 | P1 | 保留上游许可署名，审查资源许可 | 发布清单审查 |
| R-013 | Agent 大规模无关修改 | 审查困难、引入回归 | P1 | 单阶段小 PR，严格 diff 审查 | PR 评审 |
| R-014 | 读取过程中加载未知第三方 COM | 崩溃/执行外部代码 | P1 | 元信息读取优先，避免实例化 | 静态扫描检查 |
| R-015 | 操作历史含敏感信息 | 隐私风险 | P1 | 日志最小化与导出脱敏 | 数据审查 |

## 43. 任务排序规则

每项任务按五因素评估：

1. **安全收益**：是否防止不可逆系统损坏？
2. **迁移必要性**：不做是否无法编译/运行？
3. **用户价值**：是否解决真实痛点？
4. **实现/维护成本**：是否依赖大量不稳定 Windows 内部机制？
5. **可验证性**：是否有可复现测试场景？

若两项冲突，先保证能证明无破坏性副作用，再追求功能增加。

## 44. 不接受的“优化”

- 把所有旧类机械改成现代 C# 语法但不解释收益。
- 为了通过测试而删除失败测试。
- 把 COM 对象全部改为线程池异步任务。
- 对所有未知 Shell 项提供“强制删除”按钮。
- 为了所谓轻量化开启不兼容的裁剪、混淆、AOT。
- 在程序启动时无条件获取 TrustedInstaller 权限。
- 在扫描过程中默默取得注册表所有权。
- 默认清理所谓“无效注册表项”。
- 为了品牌统一立刻更改所有命名空间、资源 ID、配置路径。
- 为了“新版本”而重写现有 WinForms 界面。

---

# 第十二篇：发布前总验收清单

## 45. 安全

- [ ] 扫描和查询不会修改注册表的值、所有者、DACL。
- [ ] 修改前有针对性备份且通过完整性校验。
- [ ] 禁用、删除、移动的逻辑与 UI 语义一致。
- [ ] 权限不足、用户取消 UAC、部分失败均能清楚处理。
- [ ] 高风险路径保护与 RegistryView 校验有效。
- [ ] 失败恢复真实可用，无法恢复时有明显提示。
- [ ] 没有会自动执行陌生 COM 对象的扫描机制。
- [ ] 没有隐蔽持久的高权限后台服务。

## 46. 功能

- [ ] 原版主要菜单分类和操作有对应回归记录。
- [ ] 旧语言资源、图标、自定义命令正常。
- [ ] Windows 11 新/旧菜单区分准确，未支持操作明确禁用。
- [ ] 搜索与刷新不锁死 UI，不会串页。
- [ ] 配置迁移可回退并保留原数据。

## 47. 兼容与工程

- [ ] `net10.0-windows`、WinForms 构建成功。
- [ ] Debug 与 Release 功能一致，原生 DLL 加载正常。
- [ ] x64 COM、注册表视图行为通过测试。
- [ ] 100/125/150/200% DPI 通过 UI 检查。
- [ ] README 说明 .NET 10 官方 Windows 10 支持范围。
- [ ] 无无效历史发布路径和意外开发者本机依赖。

## 48. 部署与合规

- [ ] 自包含目录版在无 .NET Runtime 的干净 Windows 系统运行。
- [ ] 单文件版仅在验证完整后发布。
- [ ] 能使用和保存外部配置/语言文件。
- [ ] 发布包附带 LICENSE、上游署名、变更说明。
- [ ] 下载文件生成 SHA-256；自动更新如存在则通过安全审计。
- [ ] 自包含 Runtime 采用所选 .NET 10 最新服务补丁重新构建。
- [ ] 不把未测试目标写成“完美兼容”。

## 49. 版本决策记录（填写模板）

| 项目 | 当前决策 | 记录/负责人 | 变更条件 |
|---|---|---|---|
| 产品名 | ContextMenuX | 项目负责人 | 重大品牌讨论 |
| 初始版本 | 4.0 | 项目负责人 | 版本发布规划调整 |
| 运行时 | .NET 10 LTS | 项目负责人 | 官方支持/兼容性变化 |
| UI | WinForms 原版延续 | 项目负责人 | 单独获批的大型 UI 项目 |
| 主架构 | win-x64 | 项目负责人 | 明确新增硬件目标 |
| 首发部署 | Self-contained 目录版 | 项目负责人 | Single-file 全面验证通过 |
| 首发安全条件 | 扫描只读、操作可追踪和恢复 | 强制 | 不可豁免 |
| Windows 11 新菜单 | 先识别，后有条件修改 | 项目负责人 | 每个机制均已验证 |

---

# 第十三篇：权威资料与可追溯来源

以下链接用于 Agent 在实际开发时进一步核对，不能代替本地代码审查。

## 50. 上游仓库

- 上游主页：<https://github.com/BluePointLilac/ContextMenuManager>
- README：<https://github.com/BluePointLilac/ContextMenuManager/blob/master/README.md>
- 原始工程：<https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/ContextMenuManager.csproj>
- 菜单扫描：<https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/Controls/ShellList.cs>
- RegistryEx：<https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/BluePointLilac.Methods/RegistryEx.cs>
- RegTrustedInstaller：<https://raw.githubusercontent.com/BluePointLilac/ContextMenuManager/master/ContextMenuManager/BluePointLilac.Methods/RegTrustedInstaller.cs>
- Release：<https://github.com/BluePointLilac/ContextMenuManager/releases>
- Issues：<https://github.com/BluePointLilac/ContextMenuManager/issues>
- 许可：<https://github.com/BluePointLilac/ContextMenuManager/blob/master/LICENSE>
- ACL 相关用户报告：<https://github.com/BluePointLilac/ContextMenuManager/issues/310>
- 现代菜单反馈：<https://github.com/BluePointLilac/ContextMenuManager/issues/298>
- 恢复功能需求：<https://github.com/BluePointLilac/ContextMenuManager/issues/297>
- 菜单重新出现：<https://github.com/BluePointLilac/ContextMenuManager/issues/280>

## 51. .NET 与 WinForms 官方文档

- .NET Windows 支持矩阵：<https://learn.microsoft.com/en-us/dotnet/core/install/windows>
- .NET 版本与支持策略：<https://learn.microsoft.com/en-us/dotnet/core/releases-and-support>
- WinForms 迁移指南：<https://learn.microsoft.com/en-us/dotnet/desktop/winforms/migration/>
- 单文件部署：<https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview>
- .NET 部署总览：<https://learn.microsoft.com/en-us/dotnet/core/deploying/>
- .NET 兼容性破坏性变更：<https://learn.microsoft.com/en-us/dotnet/core/compatibility/>
- .NET 10 下载：<https://dotnet.microsoft.com/download/dotnet/10.0>

## 52. Windows / Shell 官方文档

- Windows API：<https://learn.microsoft.com/en-us/windows/win32/api/>
- Windows Shell 文档：<https://learn.microsoft.com/en-us/windows/win32/shell/shell-entry>
- 现代 Explorer 菜单整合：<https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer>
- Windows 注册表概述：<https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry>
- 32/64 位注册表视图：<https://learn.microsoft.com/en-us/windows/win32/winprog64/registry-redirector>
- HKEY_CLASSES_ROOT：<https://learn.microsoft.com/en-us/windows/win32/sysinfo/hkey-classes-root-key>

---

# 最终执行摘要

**ContextMenuX 不做 C++/Qt 重写，不推翻原版 WinForms UI。** 采取受控、分阶段的 C#/.NET 10 升级路线。第一阶段必须完成源码和安全审计；发布前必须解决只读扫描中不应出现的注册表权限副作用。现代 Windows 11 菜单支持遵循“准确识别、谨慎修改”的原则。便携发行优先通过 Self-contained 目录版在干净 Windows 上验证，再视需要推出单文件版。

**立即开始的唯一任务：第 29 节“阶段 0：全仓只读审计”。** 等有真实源码证据、风险评估和实施顺序后，再授权 Agent 改动。

> ContextMenuX — 更可控、更安全的 Windows 右键菜单管理工具。
