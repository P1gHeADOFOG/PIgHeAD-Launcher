# PIgHeAD v0.1.4beta

## 界面与下载体验改进（源码更新）

- 复选框统一主题色圆角、勾选 / 半选图形与键盘焦点环，浅色模式主页改为白天风景。
- Versions 仅管理本地版本，提供“前往下载中心”跳转。每个版本新增详情与删除按钮：基本信息包含 Minecraft、加载器、Java、发布时间、目录和体积；识别到 Forge / NeoForge / Fabric / Quilt 才显示模组入口，从实际运行目录读取模组本地资料。
- 删除先显示目标目录供确认，再移入回收站；保留共享资源及独立实例目录，阻止删除仍被其他版本依赖的基础版本。目录内的存档与模组会随目录移动，确认界面明确展示范围。详见 [本地版本详情与模组](Advanced-Features.md#本地版本详情与模组)。

- 主窗口与输入控件统一圆角；Windows 11 使用系统圆角及原生窗口阴影，保留拖动、缩放和最大化。
- 标题栏和 Settings 提供深色 / 浅色与中文 / English 切换，立即生效，保存到启动器旁的 `PHL/appearance.json`，跨游戏目录共享。客户端页面、提示、文件选择标签及首次使用协议均提供英文；README 顶部可切换至 [English](../README.en.md)。
- 开屏使用猪头图标缩放淡入、像素点与淡出；遵循已保存的启动器动画选项和 Windows 动画设置。开屏不会等待网络请求完成。
- 下载中心在启动时自动读取正式版本，最近 6 个版本支持点击版本号立即下载安装；切换加载器或游戏版本自动读取兼容构建，拒绝已过期请求的结果，网络失败保留重试入口。安装仍需用户主动点击，OptiFine 保持官方外部安装器流程。
- Home 移除滚动容器，风景区域随可用高度伸缩，启动按钮、下载选项及底部状态始终可见。最小窗口为 1000 × 640 WPF 逻辑像素，已验证 1135 × 720 与 1280 × 820；其他功能页继续支持滚动。
- 英文协议嵌入客户端，内容校验覆盖两种语言；协议翻译变化也会要求重新同意。界面语言不影响 Minecraft 游戏语言。

这些内容需重新构建客户端；已存在的 GitHub 发布资产不会自动更新。

## 下载与运行

GitHub 预发布入口：[v0.1.4beta](https://github.com/P1gHeADOFOG/PIgHeAD-Launcher/releases/tag/v0.1.4beta)。下载 `PIgHeAD-v0.1.4beta-win-x64.zip`，完整解压到可写目录，运行 `PIgHeAD.Launcher.exe`。该包包含 .NET 10 Windows x64 运行时，无需另外安装 .NET；游戏的 Java 运行时仍由游戏设置与下载流程管理。可使用同页的 `SHA256SUMS.txt` 核对下载文件。

这是 beta 预发布：协议仍为草案，Minecraft Services API 仍未申请 / 未获批，不能保证 Microsoft 正版认证完成。完整大型整合包、各加载器和真实游戏窗口仍需实际环境验收。

本地重新生成发布包：先构建和测试，然后运行 `powershell -File tools/Publish-Release.ps1`。脚本仅写入 artifacts，生成独立运行 ZIP、构建提交记录和 SHA-256；从 NuGet 缓存及对应项目来源收集第三方许可证，因此需要网络。已有输出不会被覆盖。

### 单文件客户端

若需要单独移动 EXE，运行 `powershell -File tools/Publish-Release.ps1 -SingleFile -OutputDirectory artifacts\standalone`。生成的 `artifacts\standalone\PIgHeAD-v0.1.4beta-win-x64.exe` 已包含 .NET 10、托管和本机依赖、图标、协议、第三方许可证与说明文档，只复制该 EXE 即可。旁边的 SHA-256 文件用于核验；其他目录是打包中间文件，无需携带。

单文件使用 `SingleFile.pubxml` 配置：自包含、内置本机库自动释放、压缩程序集、关闭裁剪和外置调试符号，仅保留中英文卫星资源。运行时自动使用用户临时目录下的 `.net` 缓存；首次启动需等待缓存建立。新增便携模式会在启动器和游戏文件夹生成 `PHL` 保存数据，因此这些位置需可写。优先识别 EXE 旁的 `.minecraft` 及同级 Java，并支持 Versions 顶部切换已登记的游戏目录。移动整个整合包（包含 PHL）后会恢复本地相对路径；外部游戏仍按原绝对路径访问，DPAPI 认证不能跨 Windows 用户迁移。详见 [便携模式与游戏目录](Portable-Workspaces.md)。

添加 `-FrameworkDependent` 可生成带 `-lite.exe` 后缀的精简版，要求目标电脑安装 .NET 10 Desktop Runtime x64；这不是免运行时版本。完整版与精简版均为单个 EXE。

## 导入游戏

在 Versions 点击“导入压缩整合包”或“接入游戏目录”，也可以将一个 ZIP、`.mrpack` 或游戏文件夹拖到主窗口任意位置。拖放后自动切到 Versions。操作期间禁用冲突任务，Downloads 显示进度与取消入口。

支持范围：

- 完整 ZIP：根部含 `versions`，或根部含 `.minecraft/versions`，允许外面多套一层整合包文件夹。压缩包包含多个游戏根目录时会拒绝自动猜测。
- Modrinth `.mrpack`：读取 Minecraft 和 Forge / NeoForge / Fabric / Quilt 固定依赖，安装所需版本，补全模组清单，逐个校验大小、SHA-1 和 SHA-512。客户端 `unsupported` 文件不安装，`optional` 客户端文件默认包含。先应用通用 overrides，再应用 client-overrides，不应用 server-overrides。打包提供的 overrides 文件优先于远程同路径文件。
- 已有目录：直接接入所选目录，自动识别其 `.minecraft`，不复制或移动原游戏；会添加本启动器的实例布局记录，勾选补全时检查并补全所选版本。

ZIP / 目录可以先关闭“导入后校验并补全文件”，导入后再选择“修复游戏”；`.mrpack` 导入需要开启补全并联网。仅包含 CurseForge `manifest.json`、没有完整游戏文件的导出 ZIP 暂不支持；RAR / 7z 本版不支持。

压缩包导入到 `启动器目录\PHL\imports\pack-时间-随机标识`。每次建立独立目录，不合并或覆盖当前游戏。解压、补全成功后才登记为新游戏根目录，在对应游戏的 PHL 中保存设置，并加入游戏文件夹列表，后续可切换；原压缩包保留。若只完成解压而设置保存失败，独立目录仍保留，可再次通过目录入口接入。

取消或失败时尝试清理本次临时目录，保持当前活动目录不变；如文件被第三方进程占用，未激活的 `.pending-*` 目录可能暂留，可关闭程序后检查。目录接入模式的修复可能已经补全部分文件，但不会移动或删除原目录与存档。

压缩导入拒绝目录穿越、链接、Windows 同名冲突和流路径；支持最多 100000 个 ZIP 条目、累计 16 GiB 的声明解压大小。Modrinth 下载也按清单大小限制，并使用限定的 HTTPS 下载来源（Modrinth CDN、GitHub、GitLab 及已知 GitHub 资源域名）。不符合当前支持范围时显示失败，不跳过校验并报告成功。具体格式依据 [Modrinth 官方格式说明](https://support.modrinth.com/en/articles/8802351-modrinth-modpack-format-mrpack)。

## 目录发现与布局

没有本位置的 PHL 记录时，优先在 EXE 目录查找 `.minecraft/versions`，与终端工作目录及旧版本全局配置无关；没有本地游戏时默认使用 EXE 旁的 `.minecraft`。已有本位置的目录选择记录优先恢复。选择外层目录后，设置和版本服务也会识别其 `.minecraft`。直接放入 PCL 等整合包时，也识别已有 `versions/版本ID`、`instances/版本ID` 下的 mods / config / saves 布局。

导入包保留自己的实例布局：根目录下的 mods / config / saves 留在根目录；位于 `versions/<版本ID>` 或 `instances/<版本ID>` 下的实例文件按原位置启动。`.pighead-import.json` 记录对应关系。该记录优先于一般“按版本隔离”设置，避免导入后看不到模组、存档。导入和普通下载的版本可以在 Versions 中手动切换；下次刷新首先尝试恢复导入时保存的版本。

## 修复游戏

在 Versions 选择本地版本，点击“修复游戏”：

1. 对导入 `.mrpack` 的保留清单，重新校验并下载缺失或损坏的远程模组文件。未修改的文件不重新下载。
2. 使用 CmlLib 的大小与 checksum 检查，补全有版本元数据支持的游戏核心、库、资源和 Java 文件。
3. 保留存档、配置、额外模组及资源包；不重置用户数据，不自动删除未列入清单的文件。

修复只能依照已有元数据和可用来源恢复文件。普通 ZIP 中没有清单的任意模组无法凭空恢复；版本 JSON 本身损坏、定制客户端缺少下载地址或加载器不兼容时，会显示失败并建议重新安装对应加载器 / 重新导入，不能保证“一键修复”所有问题。基于清单的修复也无法替代实际启动与模组兼容性检查。

## 版本与交互

- 显示版本为 `v0.1.4beta`，位于侧栏和 Settings；文件版本与程序集版本为 `0.1.4.0`，包版本为 `0.1.4-beta`。
- Java 启动参数中的启动器版本使用同一程序集信息来源，避免旧的硬编码版本。
- 按钮、导航、版本列表、复选框、下拉选项及下载分类增加按压 / 回弹反馈，支持鼠标和键盘操作。继续遵循 Settings 动画开关和 Windows 动画偏好；关闭动画时清除动画状态。
- 中文标题为 `PIgHeAD - 猪头启动器`，切换英文后显示 `PIgHeAD - Minecraft Launcher`。

## 验证与文件清单

自动测试使用临时 ZIP / mrpack、模拟下载和受控进程；覆盖目录探测、分层布局、压缩路径拒绝、取消清理、依赖选择、模组哈希与修复、设置注册失败、版本号、页面绑定和关闭动画。测试不下载完整 Minecraft，不登录真实 Microsoft 账户，也不替用户同意协议。

| 范围 | 本次新增或修改 |
| --- | --- |
| 模型与版本 | `Models/LauncherVersion.cs`、`GameImportResult.cs`、`LauncherSettings.cs`、`PIgHeAD.Launcher.csproj` |
| 服务接口 | 新增 `IGameImportService.cs`，扩展 `IGameService.cs` |
| 导入服务 | 新增 `GameImportService.cs`、`ModrinthPackInstaller.cs`、`SafeZipExtractor.cs`、`SafeGamePath.cs`、`GameRootLocator.cs`、`ImportedGameLayout.cs`，均位于 `Services/Minecraft` |
| 游戏与设置服务 | `Services/Minecraft/MinecraftGameService.cs`、`GameEnvironment.cs`；`Services/Settings/JsonSettingsService.cs` |
| 页面与装配 | `ViewModels/VersionsViewModel.cs`、`SettingsViewModel.cs`、`MainViewModel.cs`、`HomeViewModel.cs`；`Bootstrap/AppBootstrapper.cs` |
| 视图与动画 | `Views/VersionsView.xaml`、`MainWindow.xaml` / `.xaml.cs`、`SettingsView.xaml`、`DownloadsView.xaml`；新增 `Views/Controls/InteractionAnimation.cs`；`Resources/Styles/Controls.xaml` |
| 测试 | 新增 `GameImportTests.cs`，更新 `LauncherTestContext.cs`、`WpfCompositionTests.cs` |
| 文档 | `README.md`、本文件 |

除已完整指定的路径外，源文件位于 `PIgHeAD.Launcher`，测试位于 `tests/PIgHeAD.Launcher.Tests`。发布打包脚本为 `tools/Publish-Release.ps1`。生成的 obj 文件不作为源配置提交。
