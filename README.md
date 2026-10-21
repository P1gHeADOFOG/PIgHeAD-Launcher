# PIgHeAD - 猪头启动器 · v0.1.4beta
本启动器仅供作者与朋友使用，请不要二次分发，在中国大陆境内禁止使用

使用 C#、WPF、.NET 10 与 MVVM 构建的 Minecraft Java Edition 启动器。

下载：[v0.1.4beta Windows x64 预发布](https://github.com/P1gHeADOFOG/PIgHeAD-Launcher/releases/tag/v0.1.4beta)。ZIP 包已包含 .NET 10，完整解压后运行 `PIgHeAD.Launcher.exe`，无需自行安装 .NET。此版本的 Minecraft API 尚未获批，真实正版登录仍待验收。

本次图标与启动接入的完整文件清单见 [改动记录](docs/Launch-Integration-Changes.md)。
首次使用协议、高级设置、加载器下载与本次文件清单见 [高级功能说明](docs/Advanced-Features.md)。
ZIP / mrpack 导入、目录发现、游戏修复和交互动画见 [v0.1.4beta 说明与文件清单](docs/Release-v0.1.4beta.md)。

## 当前功能

- 深色主窗口、Home / Versions / Downloads / Accounts / Settings 五项导航，默认进入 Home。
- 使用用户提供的猪头原图作为侧栏、窗口和程序图标；PNG 与多尺寸 ICO 已嵌入项目。
- 扫描配置目录下的本地版本，读取官方原版正式版本列表，下载/修复游戏、依赖和匹配的 Java 运行时。
- 用保存的内存与目录设置启动选定版本，检查 Java 版本，显示准备进度、运行状态和退出码，保存游戏控制台日志。
- 支持明确标识的离线身份，以及 Microsoft 系统浏览器授权码 + PKCE 正版认证。
- 已配置项目默认 Client ID：`2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a`。Settings 中仍可更换或清空；清空后保留版本管理、下载和离线启动。
- 可取消下载或启动准备。取消不会终止已启动的游戏；游戏运行期间阻止重复启动和文件安装。
- 首次使用须分别同意用户协议和免责声明（草案）；发布者 PIgHeAD_OFOG，联系邮箱 p1gheadofog@gmail.com。协议变化后重新确认，设置页可随时查看。
- 高级设置包括窗口模式与尺寸、自动内存、版本隔离、JVM / 游戏参数、进程优先级、游戏语言和页面动画。
- 下载中心提供原版、Forge、NeoForge、Fabric、Quilt 自动安装入口，以及 OptiFine 官网下载与本地官方安装器入口；显示兼容构建和本次会话任务记录。
- 支持拖入 ZIP 完整整合包、Modrinth `.mrpack` 和游戏文件夹，识别 `.minecraft`，保留实例布局并补全文件；Versions 提供独立的“修复游戏”入口。
- 侧栏与 Settings 显示 `v0.1.4beta`，常用操作加入可关闭的按压 / 回弹反馈。

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
2. 在 Versions 点击“读取在线原版列表”，选择正式版本。
3. 点击“下载 / 修复所选版本”，或在 Home 允许补下载后直接启动。
4. 在 Downloads 查看文件进度并按需取消。重新执行安装会检查已存在文件，补全缺失内容。

下载能力通过 CmlLib 访问其默认的 Mojang/Microsoft 分发服务，不添加未经配置的第三方镜像。Downloads 可读取原版列表、选择加载器、读取对应构建并安装；成功后刷新本地版本并选中新版本。每次安装一种加载器，不自动组合不同加载器。OptiFine 通过作者官网手动取得安装器，再由用户在安装窗口核对目标目录；退出安装器不等于安装成功。详见 [高级功能说明](docs/Advanced-Features.md)。

## Microsoft 登录

参阅 [Microsoft 应用注册与认证说明](docs/Microsoft-Authentication.md)。

- 默认使用项目提供的公开桌面应用 Client ID；如需独立使用其他应用，可在 Settings 中更改并保存。已有配置缺少该字段时自动使用默认值，显式留空或自定义值会保留。
- 使用系统默认浏览器执行 Authorization Code + PKCE，不需要 Client Secret，也不在启动器中输入密码。
- Microsoft、Xbox、Minecraft 所有权和角色资料验证全部成功后才显示正版身份。
- Microsoft 登录失效会提示重新登录，不会自动降级成离线身份。
- 项目 Client ID 已完成 Entra 注册和桌面回调配置，但尚未提交 Minecraft Services API 申请、尚未获批（维护者于 2026-10-21 确认）。Accounts 页显示该状态；Microsoft OAuth 注册不授予 Minecraft API 访问权限。申请步骤见上述认证说明。

## 配置与日志

设置文件：`%LOCALAPPDATA%\PIgHeAD\Launcher\settings.json`。

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
- 游戏日志存于 `%LOCALAPPDATA%\PIgHeAD\Launcher\logs`，最近一次路径显示在 Downloads 中。
- Microsoft 缓存按 Client ID 分文件，以 Windows 当前用户 DPAPI 加密。不会在设置 JSON 中写入令牌或密码。

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
