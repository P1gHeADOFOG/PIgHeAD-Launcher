# 首次使用、高级设置与组件下载

## 首次启动

程序在显示主窗口、恢复账户或读取游戏列表之前，检查启动器旁的 `PHL\consent.json`。协议窗口可切换中英文，内容校验覆盖两种语言。

- 没有有效记录时，显示内嵌的 [用户协议（草案）](User-Agreement.md) 和 [免责声明与隐私说明（草案）](Disclaimer.md)。发布者为 **PIgHeAD_OFOG**，联系邮箱 **p1gheadofog@gmail.com**。
- 两个复选框默认不勾选；必须分别同意并成功保存，才进入启动器。拒绝或关闭则退出。
- 记录包含协议版本、两个选择、UTC 同意时间和两份文档的 SHA-256 内容校验值。协议版本或内容不一致、记录损坏时重新确认，不会自动同意。
- 更新协议时同步更新 `UserConsentService.Revision` 与文档版本。文档作为程序集资源发布，不依赖旁边是否有 docs 文件夹。
- Settings 的“查看协议与免责声明”以只读方式显示文档，不改写同意记录。关闭程序并删除 consent.json 后，下次启动会重新确认；这是本地确认记录，不是电子签名或第三方许可凭证。
- 文本是根据当前功能拟定的开发草案，正式发布前应复核其与实际功能、适用法律及项目许可的一致性。项目协议不代替 [Minecraft EULA](https://www.minecraft.net/eula) 或第三方许可。

## 高级启动设置

旧配置缺少新字段时使用默认值。更改后点击“保存设置”才影响启动；“重新读取”丢弃草稿；“恢复默认草稿”只修改表单，仍需保存。下载、游戏运行或账户认证期间不能保存或重新读取配置。

| 设置 | 默认值 | 实际效果 |
| --- | --- | --- |
| 窗口类型 | 窗口化 | 支持窗口化、全屏、最大化窗口；最大化会在检测到游戏窗口后请求系统最大化，最多等待 20 秒 |
| 窗口尺寸 | 1280 × 720 | 宽 320–16384、高 240–16384；游戏与系统可能调整最终窗口尺寸 |
| 自动分配内存 | 关闭，手动 4096 MiB | 启动时读取物理内存，取总物理内存一半、可用内存减 2048 MiB、12288 MiB 的最小值，按 256 MiB 向下对齐；不足 512 MiB 时停止启动并提示 |
| 版本隔离 | 关闭 | 游戏工作目录与 `--gameDir` 改为 `<游戏根目录>/instances/<版本ID>`；资源、版本与依赖仍在根目录共享 |
| JVM / 游戏参数 | 空 | 支持双引号、含空格值与 Windows 路径，直接传入进程；不经 PowerShell / CMD；不允许覆盖账户、目录、窗口、classpath 和堆内存参数 |
| 进程优先级 | 正常 | 低于正常、正常、高于正常、高；系统拒绝设置时在游戏日志中说明并保留默认值，不终止已启动的游戏 |
| 游戏语言 | 保留原设置 | 每次启动可跟随系统，或指定简体中文、繁体中文、English；跟随模式识别常用系统语言，其他语言回退 en_us |
| 动画 | 开启 | 主界面加载与页面切换的淡入 / 位移动画；同时遵循 Windows 客户区动画开关；可在设置中关闭 |

隔离不会迁移已有存档、模组或配置。想沿用旧存档，请先备份，再把需要的内容手动复制到目标实例目录。选择语言时仅更新该实例 `options.txt` 的 `lang` 项，保留其他项，最近一次修改前的文件保存在 `options.txt.pighead-backup`。古老版本或特殊模组可能使用不同语言代码，此时可选“保留游戏设置”并在游戏中配置。

参数示例：`-XX:+UseG1GC`、`-Dexample.label="hello world"`。具体参数是否可用取决于游戏和 Java 版本；参数校验不表示适用于所有版本。自动内存是启动时的预算，不保证游戏实际内存占用上限或性能。

## 下载中心

1. 在 Settings 保存游戏根目录；首次读取列表不会下载游戏文件。
2. 进入 Downloads，启动时已自动读取正式原版版本；最近 6 个版本可直接点击版本号下载安装，更多版本在下拉框选择。
3. 选择组件。Forge、NeoForge、Fabric、Quilt 自动读取兼容构建，选择一个构建；稳定版优先展示，Forge 优先展示推荐版。网络失败时显示重试入口。
4. 点击“下载并安装”。共享任务状态会禁用冲突的启动、安装、账户切换与设置保存。成功后刷新 Versions 并选中安装的版本。
5. 当前任务可取消；等待操作实际结束后解锁。已完成的文件保留，重试同一版本时检查并补全。任务记录仅在本次会话保留，最多 30 条。部分版本元数据存在不代表安装完整，失败后不要把“本地”标记当作成功证明。

| 组件 | 当前入口 | 来源与边界 |
| --- | --- | --- |
| Minecraft 原版 | 自动下载 / 修复 | Mojang / Microsoft 分发服务，通过 CmlLib 安装游戏、资源和对应 Java |
| Forge | 构建列表与自动安装 | CmlLib.Core.Installer.Forge；可能打开 Forge 的支持页面；旧版本兼容性以安装模块和实际测试为准 |
| NeoForge | 构建列表与自动安装 | 同一安装模块内置的 NeoForge 适配，读取 NeoForged Maven，不额外引入重复包 |
| Fabric | 构建列表与自动安装 | [Fabric 官方 Meta API](https://github.com/FabricMC/fabric-meta)，取得匹配 profile 并安装继承依赖 |
| Quilt | 构建列表与自动安装 | Quilt 官方 `https://meta.quiltmc.org/v3` 元数据服务，取得 profile 后安装依赖 |
| OptiFine HD | 官网下载 + 运行本地官方安装器 | 通过 [作者官方下载页](https://optifine.net/downloads) 手动取得 JAR，避免自行转载作者文件；参阅 [作者版权说明](https://optifine.net/copyright) |

每次安装一种加载器，不将 Forge、NeoForge、Fabric、Quilt 混装，也不自动推断 OptiFine / Forge 组合兼容性。Fabric API、Quilt 的额外模组库和普通模组不在当前安装入口自动添加。

OptiFine 使用步骤：先安装对应原版，在 Settings 选择可用的 Java，然后在下载中心选 OptiFine HD → 打开组件官网 → 下载官方 `OptiFine_*.jar` → 运行本地安装器。请在其窗口核对游戏根目录并完成操作。启动器不会替作者安装器宣告成功；关闭安装器后刷新本地版本。等待期间保持任务锁，不能通过“取消当前任务”终止外部安装器；请在安装器自身窗口关闭。其数据行为及功能以作者提供的版本为准。

关闭启动器时会取消可取消的准备任务，并等待文件操作 / 认证结束，不终止已运行的游戏。若有外部 OptiFine 安装器，需先关闭该窗口；关闭等待期间标题会给出提示。

## 架构与验证

### 本地版本详情与模组

Versions 仅列出本地安装，不再提供在线版本列表或直接安装按钮；“前往下载中心”跳转到 Downloads。本地列表每一行提供“详情”和“删除”。

详情读取本地版本 JSON 及其继承链，显示 Minecraft 版本、加载器及版本号、Java 要求、版本类型、发布时间、版本目录大小、游戏根目录和实际运行目录；缺少的信息明确标为未声明。体积只统计所选版本目录，不代表整个游戏与共享资源的大小。

“查看模组”以本地版本元数据中的 Forge / NeoForge / Fabric / Quilt 加载器作为模组支持依据；纯原版不显示入口，不根据文件夹名称猜测。读取实际运行目录的 `mods` 中启用的 `.jar` 文件，遵循共享目录、版本隔离及导入实例布局。支持 `fabric.mod.json`、`quilt.mod.json`、`META-INF/mods.toml`、`META-INF/neoforge.mods.toml` 和旧版 `mcmod.info`，显示名称、ID、版本与文件名；无法识别或损坏的 JAR 显示文件名和读取状态。只读取压缩包资料，不执行 JAR，不需要外部模组服务的 API Key，也不验证实际兼容性。

点击“删除”先展示具体版本目录及删除范围，确认后移入 Windows 回收站。目录内的 saves / mods / config 会一起移动；共享 libraries / assets、游戏根目录数据与独立 instances 目录不在删除范围。若其他版本直接继承此版本，会阻止删除并指出依赖版本。删除拒绝路径越界和链接；游戏运行、安装或其他冲突操作期间不能删除。自动化测试使用临时目录和模拟回收操作，不删除用户游戏。

配置模型与参数校验在 Models；游戏环境和加载器适配在 Services/Minecraft；协议存储在 Services/Consent；系统选择器及浏览器入口通过 IDesktopService 隔离在 Services/Desktop。ViewModel 处理状态和命令，Views 负责绑定和纯界面动画。保留已有 IAccountService 与加密缓存模块，Microsoft API 审批仍按未获批处理。

自动测试使用临时目录、模拟 HTTP、受控的 Java 替身，不运行真实 Minecraft、不执行外部 JAR，也不登录真实 Microsoft 账户。已实际读取 Minecraft 1.21.4 的 Forge、NeoForge、Fabric、Quilt 目录，并验证 Fabric / Quilt 选中稳定构建的 profile 继承目标；目录数量随服务更新变化。完整游戏下载、安装器处理器、实际窗口最大化、各版本模组兼容性仍需目标环境验收。

## 本次文件清单

| 范围 | 文件 |
| --- | --- |
| 应用装配 | `PIgHeAD.Launcher/App.xaml.cs`、`Bootstrap/AppBootstrapper.cs`、`PIgHeAD.Launcher.csproj` |
| 模型 | `Models/LauncherSettings.cs`、新增 `AdvancedOptions.cs`、`LaunchArgumentParser.cs`、`DownloadComponent.cs` |
| 服务接口 | 新增 `Services/Abstractions/IDesktopService.cs`、`ILoaderService.cs` |
| 业务与系统服务 | `Services/Minecraft/MinecraftGameService.cs`、新增 `GameEnvironment.cs`、`ModLoaderService.cs`；新增 `Services/Consent/UserConsentService.cs`、`Services/Desktop/DesktopService.cs`；`Services/Settings/JsonSettingsService.cs` |
| 页面状态 | `ViewModels/SettingsViewModel.cs`、`HomeViewModel.cs`、`AccountsViewModel.cs`、`VersionsViewModel.cs`、`DownloadsViewModel.cs`、`MainViewModel.cs`；新增 `ConsentViewModel.cs`、`DownloadCatalogViewModel.cs` |
| 视图与样式 | `Views/MainWindow.xaml` / `.xaml.cs`、`SettingsView.xaml`、`DownloadsView.xaml`、`AccountsView.xaml`；新增 `ConsentWindow.xaml` / `.xaml.cs`、`Controls/AnimatedContentControl.cs`；`Resources/Styles/Controls.xaml` |
| 测试 | `tests/PIgHeAD.Launcher.Tests/ProcessAndTokenTests.cs`、`WpfCompositionTests.cs`；新增 `ConsentAndEnvironmentTests.cs`、`DownloadCatalogTests.cs` |
| 文档 | `README.md`、新增 `docs/User-Agreement.md`、`Disclaimer.md`、`Advanced-Features.md` |

表中未重复完整路径的文件位于同一范围目录；测试不会改写用户现有同意记录或设置。当前版本与发布记录见 [beta v0.2.2 更新说明](Update-Notes-v0.2.2.md)。
