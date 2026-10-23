# PIgHeAD - 猪头启动器 · beta v0.2.2

**简体中文** | [English](README.en.md)

本启动器尚处于开发阶段目前仅供作者与朋友使用，请不要二次分发

使用 C#、WPF、.NET 10 与 MVVM 构建的 Minecraft Java Edition 启动器。

下载：[beta v0.2.2 · Windows x64](https://github.com/P1gHeADOFOG/PIgHeAD-Launcher/releases/tag/v0.2.2-beta)。完整版 `PIgHeAD-v0.2.2-beta-win-x64.exe` 自带 .NET 10，下载后直接运行；精简版 `-lite.exe` 需要安装 .NET 10 Desktop Runtime x64。可使用发布页的 `SHA256SUMS.txt` 校验文件。根据当前项目记录，Minecraft API 尚未获批，真实正版登录仍待验收。

更新说明：[简体中文](docs/Update-Notes-v0.2.2.md) | [English](docs/Update-Notes-v0.2.2.en.md)。本次包含主题与双语界面、自动下载目录、开屏动画、本地版本详情 / 模组查看 / 删除，以及便携目录改进。

本次图标与启动接入的完整文件清单见 [改动记录](docs/Launch-Integration-Changes.md)。
首次使用协议、高级设置、加载器下载与本次文件清单见 [高级功能说明](docs/Advanced-Features.md)。
ZIP / mrpack 导入、目录发现、游戏修复和交互动画见 [v0.1.4beta 说明与文件清单](docs/Release-v0.1.4beta.md)。

## 当前功能

- 圆角主窗口，深色 / 浅色主题；标题栏和 Settings 可立即切换外观及中英文，自动保存到启动器旁的 `PHL/appearance.json`。
- 复选框使用主题色圆角与统一勾选图形；浅色模式主页显示白天风景，深色模式显示暮色风景。
- Home / Versions / Downloads / Accounts / Settings 五项导航，默认进入 Home。主页按可用高度伸缩，无需滚动即可看到启动按钮、下载选项和底部状态；最小窗口为 1000 × 640（WPF 逻辑像素）。
- 开屏包含猪头图标缩放、淡入和像素点动画；可在 Settings 关闭，同时遵循 Windows 动画偏好。
- 使用用户提供的猪头原图作为侧栏、窗口和程序图标；PNG 与多尺寸 ICO 已嵌入项目。
- 扫描配置目录下的本地版本，读取官方原版正式版本列表，下载/修复游戏、依赖和匹配的 Java 运行时。
- 用保存的内存与目录设置启动选定版本，检查 Java 版本，显示准备进度、运行状态和退出码，保存游戏控制台日志。
- 支持明确标识的离线身份，以及 Microsoft 系统浏览器授权码 + PKCE 正版认证。
- 已配置项目默认 Client ID：`2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a`。Settings 中仍可更换或清空；清空后保留版本管理、下载和离线启动。
- 可取消下载或启动准备。取消不会终止已启动的游戏；游戏运行期间阻止重复启动和文件安装。
- 首次使用须分别同意用户协议和免责声明（草案）；发布者 PIgHeAD_OFOG，联系邮箱 p1gheadofog@gmail.com。协议变化后重新确认，设置页可随时查看。
- 高级设置包括窗口模式与尺寸、自动内存、版本隔离、JVM / 游戏参数、进程优先级、游戏语言和页面动画。
- 下载中心启动时预载原版版本；最近 6 个正式版本支持点击版本号立即下载安装，更多版本通过下拉框选择。切换加载器或 Minecraft 版本时自动读取兼容构建，失败后可重试；版本读取不占用游戏安装任务锁。
- 下载中心提供原版、Forge、NeoForge、Fabric、Quilt 自动安装入口，以及 OptiFine 官网下载与本地官方安装器入口；显示兼容构建和本次会话任务记录。
- 支持拖入 ZIP 完整整合包、Modrinth `.mrpack` 和游戏文件夹，识别 `.minecraft`，保留实例布局并补全文件；Versions 提供独立的“修复游戏”入口。
- Versions 仅管理本地版本，通过“前往下载中心”安装新版本。每个版本可查看 Minecraft 版本、加载器、Java 要求、目录和体积；检测到 Forge / NeoForge / Fabric / Quilt 时显示“查看模组”，读取实际运行目录中的模组名称、ID、版本及文件名，原版隐藏此入口。
- 删除版本需先核对具体目录并确认，再移入 Windows 回收站。版本目录内的数据随之移动；共享 libraries / assets、根目录存档及独立 instances 目录保留。存在依赖版本时阻止删除基础版本。
- 侧栏与 Settings 显示 `beta v0.2.2`，常用操作加入可关闭的按压 / 回弹反馈。

## 构建与运行

需要 Windows、支持 .NET 10 WPF 的 IDE，以及符合 `global.json` 的 .NET 10 SDK。首次还原依赖需要 NuGet 网络访问。

```powershell
dotnet restore PIgHeAD.sln
dotnet build PIgHeAD.sln --no-restore
dotnet test PIgHeAD.sln --no-build
dotnet run --project PIgHeAD.Launcher
```

解决方案只保留 Debug / Release 的 Any CPU 配置。发布时再按目标系统配置 RID。
若 Debug 输出被正在运行的程序占用，可关闭旧程序后构建，或使用 `dotnet build PIgHeAD.sln -c Release`，再运行 `dotnet run --project PIgHeAD.Launcher -c Release --no-build`。

需要只复制一个 EXE 的 Windows x64 客户端时，使用单文件发布：

```powershell
powershell -File tools/Publish-Release.ps1 -SingleFile -OutputDirectory artifacts\standalone
```

输出为 `artifacts\standalone\PIgHeAD-v0.2.2-beta-win-x64.exe`，只需移动这个 EXE；不需要旁边的 DLL、安装 .NET 或携带构建中间目录。脚本将第三方许可证、双语说明文档和构建信息嵌入程序，并生成 SHA-256 校验文件；输出目录需使用尚未构建过的新路径。发布配置位于 `PIgHeAD.Launcher/Properties/PublishProfiles/SingleFile.pubxml`。

启动时内置的本机运行库自动释放到 Windows 用户临时目录的 `.net` 缓存，首次打开可能稍慢。将 EXE 放在整合包根目录，与 `.minecraft`、`java21` 并列；首次启动优先识别旁边的游戏，自动创建 `PHL`。游戏目录列表、协议记录与 DPAPI 加密认证缓存存放在启动器旁的 `PHL`，每个游戏的设置和日志存放在该游戏的 `PHL`。在 Versions 顶部选择已导入的游戏目录并点击“切换文件夹”，可恢复该目录自己的设置。

精简版构建命令为 `powershell -File tools/Publish-Release.ps1 -SingleFile -FrameworkDependent -OutputDirectory artifacts\standalone-lite`，输出文件名带 `-lite.exe`。精简版只携带应用依赖，需要预先安装 **.NET 10 Desktop Runtime x64**；完整版包含运行时，适合无需额外安装的使用场景。两者都关闭裁剪，仅保留中英文卫星资源；完整版另外启用单文件压缩（SDK 不支持对依赖框架的精简版启用该压缩）。目录布局、迁移和隐私说明见 [便携模式与游戏目录](docs/Portable-Workspaces.md)。

## 启动已有游戏

1. 在 Settings 中填写实际的游戏根目录，保存设置。根目录应包含 `versions`、`libraries` 和 `assets`，不要选择单个版本文件夹。
2. 如需使用官方启动器已有的目录，可在资源管理器中打开 `%APPDATA%\.minecraft`，复制展开后的绝对路径到设置中。
3. Java 路径可留空；启动器先查找对应的已安装运行时，再检查 JAVA_HOME 与 PATH。自定义时填写 `java.exe` 或 `javaw.exe` 的完整路径。
4. 进入 Versions，点击“刷新本地版本”，选择版本。
5. 在 Accounts 中设置离线玩家名，或完成正版登录并选择 Microsoft 身份。
6. 回到 Home，点击 Launch Game。
7. 已完整安装且需要避免补下载时，关闭“允许下载缺失文件及对应 Java”。关闭后使用本地版本加载器，不请求在线版本列表；正版账户的会话刷新仍需要网络。

“本地”表示已发现版本元数据，不代表全部资源已完整。文件缺失或游戏异常退出时，可允许补下载，或查看日志排查。Java 检查发生在实际启动时；主页不会将未验证的 Java 配置伪装成检测成功。

## 下载原版

1. 保存目标游戏根目录。
2. 进入 Downloads，版本列表已在启动时自动读取；网络较慢时显示加载进度，失败时可点击重试。
3. 点击最近版本的版本号立即安装，或在下拉框选择其他版本，再点击“下载并安装”。也可在 Home 允许补下载后启动已选择的版本。
4. 在 Downloads 查看文件进度并按需取消。重新执行安装会检查已存在文件，补全缺失内容。

下载能力通过 CmlLib 访问其默认的 Mojang/Microsoft 分发服务，不添加未经配置的第三方镜像。Downloads 自动读取原版列表与所选加载器的兼容构建，成功安装后刷新本地版本并选中新版本。每次安装一种加载器，不自动组合不同加载器。OptiFine 通过作者官网手动取得安装器，再由用户在安装窗口核对目标目录；退出安装器不等于安装成功。详见 [高级功能说明](docs/Advanced-Features.md)。

## Microsoft 登录

参阅 [Microsoft 应用注册与认证说明](docs/Microsoft-Authentication.md)。

- 默认使用项目提供的公开桌面应用 Client ID；如需独立使用其他应用，可在 Settings 中更改并保存。已有配置缺少该字段时自动使用默认值，显式留空或自定义值会保留。
- 使用系统默认浏览器执行 Authorization Code + PKCE，不需要 Client Secret，也不在启动器中输入密码。
- Microsoft、Xbox、Minecraft 所有权和角色资料验证全部成功后才显示正版身份。
- Microsoft 登录失效会提示重新登录，不会自动降级成离线身份。
- 项目 Client ID 已完成 Entra 注册和桌面回调配置，但尚未提交 Minecraft Services API 申请、尚未获批（维护者于 2026-10-21 确认）。Accounts 页显示该状态；Microsoft OAuth 注册不授予 Minecraft API 访问权限。申请步骤见上述认证说明。

## 配置与日志

游戏设置文件：`游戏文件夹\PHL\settings.json`；若游戏根目录名为 `.minecraft`，PHL 位于其上一层。启动器旁的 `PHL\launcher.json` 保存最后使用的设置，`PHL\library.json` 保存游戏目录列表。

```json
{
  "gameDirectory": "C:\\Games\\Minecraft",
  "javaPath": "",
  "maximumMemoryMb": 4096,
  "microsoftClientId": "2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a"
}
```

- 路径必须为绝对路径，内存范围为 512–65536 MiB；设置范围不代表已检测到机器具备对应内存。
- 配置缺失时使用默认值。配置损坏时保留原文件并创建备份，保存时通过同目录临时文件替换。
- 切换页面保留设置草稿；主页使用已保存配置。更改游戏目录后，版本列表会失效，请重新刷新。
- 离线玩家名当前只在本次应用会话中保存。
- 游戏日志存于对应游戏的 `PHL\logs`，最近一次路径显示在 Downloads 中。
- Microsoft 缓存按 Client ID 分文件，以 Windows 当前用户 DPAPI 加密。不会在设置 JSON 中写入令牌或密码。
- 启动器界面语言与主题存于 `PHL/appearance.json`，与每个游戏的设置分开保存。界面语言切换不会修改 Minecraft 的游戏语言；游戏语言仍由 Settings 中的独立选项控制。

## 项目结构

```text
PIgHeAD.Launcher/
  Bootstrap/                 装配服务和 ViewModel
  Models/                    设置、版本、会话及操作结果
  Services/
    Abstractions/            可替换的设置、账户和游戏服务接口
    Accounts/                PKCE 认证、加密缓存与离线身份
    Consent/                 内嵌协议、内容校验与首次同意记录
    Desktop/                 文件选择、官方链接与协议窗口
    Minecraft/               原版/加载器安装、高级启动环境与进程日志
    Settings/                JSON 设置存储
  ViewModels/                页面状态、异步命令和共享操作状态
  Views/                     WPF 窗口和五个功能页
  Resources/
    Images/                  原图转换的 PNG 与 ICO
    Styles/                  配色、控件样式、矢量图标与风景
tests/
  PIgHeAD.Launcher.Tests/     单元测试与 WPF 页面组合测试
  PIgHeAD.Launcher.ProcessProbe/
                             测试用 Java 替身，验证真实进程管道
tools/Set-LauncherIcon.ps1    从图片重新生成 PNG / ICO
docs/Microsoft-Authentication.md
                             公开客户端注册、权限与缓存说明
```

View 通过绑定调用 ViewModel；游戏和账户业务服务不依赖窗口或控件，系统对话框集中在 Desktop 服务。CmlLib 处理 Minecraft 格式和依赖规则，适配代码集中在 Services，后续可替换。依赖包括 CmlLib.Core、CmlLib.Core.Auth.Microsoft、XboxAuthNet.Game.Msal 及 CmlLib.Core.Installer.Forge（包含 NeoForge）；Fabric / Quilt 使用官方 Meta API，没有额外 UI 框架。

## 验证范围

测试包括设置读写、损坏恢复、五项导航与绑定、版本/账户状态、失败与取消处理、重复启动保护、Java 输出解析、离线 UUID、DPAPI 缓存，以及受控测试进程的启动参数、日志与退出处理。测试不会打开 Minecraft，也不会登录真实账户。

DPAPI 测试需要加载当前 Windows 用户配置文件；某些沙箱/服务账户环境不可用时，应在普通用户上下文执行，不能将加密降级为明文。

设置 `PIGHEAD_TEST_ARTIFACTS` 可导出主页、下载、账户、设置及协议窗口渲染图。新增测试覆盖首次同意记录失效、自动内存、隔离目录、参数和语言实际传递、加载器目录解析、下载取消与操作互斥。真实游戏图形界面、完整加载器安装与正版服务器认证仍需手工验收；项目 Client ID 已配置，Minecraft API 尚未申请、尚未获批，真实登录尚未成功验收。




在中国大陆境内禁止使用
