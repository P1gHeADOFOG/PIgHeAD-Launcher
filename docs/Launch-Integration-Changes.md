# 图标与启动接入改动清单

本清单仅记录本次图标与启动功能的改动，不包含此前已存在的工作区改动。

## 功能

- 原图转换为 PNG 和七种尺寸的 ICO，接入窗口、侧栏与可执行程序。
- 添加本地与官方版本目录、文件安装、Java 检查、启动参数、运行监控与日志。
- 添加 Microsoft 系统浏览器授权码 + PKCE、所有权验证、静默刷新与 Windows 用户加密缓存；Client ID 可留空。
- 用实际功能页面替换 Versions / Downloads / Accounts 的展示占位页，并同步主页状态。
- 扩展测试及配置文档。

## 新增或修改（46 个文件）

- [PIgHeAD.Launcher/App.xaml.cs](../PIgHeAD.Launcher/App.xaml.cs)
- [PIgHeAD.Launcher/Bootstrap/AppBootstrapper.cs](../PIgHeAD.Launcher/Bootstrap/AppBootstrapper.cs)
- [PIgHeAD.Launcher/Models/GameOperation.cs](../PIgHeAD.Launcher/Models/GameOperation.cs)
- [PIgHeAD.Launcher/Models/GameSession.cs](../PIgHeAD.Launcher/Models/GameSession.cs)
- [PIgHeAD.Launcher/Models/GameVersionInfo.cs](../PIgHeAD.Launcher/Models/GameVersionInfo.cs)
- [PIgHeAD.Launcher/Models/LauncherSettings.cs](../PIgHeAD.Launcher/Models/LauncherSettings.cs)
- [PIgHeAD.Launcher/PIgHeAD.Launcher.csproj](../PIgHeAD.Launcher/PIgHeAD.Launcher.csproj)
- [PIgHeAD.Launcher/Resources/Images/pighead.ico](../PIgHeAD.Launcher/Resources/Images/pighead.ico)
- [PIgHeAD.Launcher/Resources/Images/pighead.png](../PIgHeAD.Launcher/Resources/Images/pighead.png)
- [PIgHeAD.Launcher/Resources/Styles/Controls.xaml](../PIgHeAD.Launcher/Resources/Styles/Controls.xaml)
- [PIgHeAD.Launcher/Resources/Styles/Icons.xaml](../PIgHeAD.Launcher/Resources/Styles/Icons.xaml)
- [PIgHeAD.Launcher/Services/Abstractions/IAccountService.cs](../PIgHeAD.Launcher/Services/Abstractions/IAccountService.cs)
- [PIgHeAD.Launcher/Services/Abstractions/IGameService.cs](../PIgHeAD.Launcher/Services/Abstractions/IGameService.cs)
- [PIgHeAD.Launcher/Services/Accounts/MicrosoftAccountService.cs](../PIgHeAD.Launcher/Services/Accounts/MicrosoftAccountService.cs)
- [PIgHeAD.Launcher/Services/Accounts/OfflineSessionFactory.cs](../PIgHeAD.Launcher/Services/Accounts/OfflineSessionFactory.cs)
- [PIgHeAD.Launcher/Services/Accounts/ProtectedTokenCache.cs](../PIgHeAD.Launcher/Services/Accounts/ProtectedTokenCache.cs)
- [PIgHeAD.Launcher/Services/Minecraft/JavaRuntimeProbe.cs](../PIgHeAD.Launcher/Services/Minecraft/JavaRuntimeProbe.cs)
- [PIgHeAD.Launcher/Services/Minecraft/MinecraftGameService.cs](../PIgHeAD.Launcher/Services/Minecraft/MinecraftGameService.cs)
- [PIgHeAD.Launcher/ViewModels/AccountsViewModel.cs](../PIgHeAD.Launcher/ViewModels/AccountsViewModel.cs)
- [PIgHeAD.Launcher/ViewModels/DownloadsViewModel.cs](../PIgHeAD.Launcher/ViewModels/DownloadsViewModel.cs)
- [PIgHeAD.Launcher/ViewModels/HomeViewModel.cs](../PIgHeAD.Launcher/ViewModels/HomeViewModel.cs)
- [PIgHeAD.Launcher/ViewModels/MainViewModel.cs](../PIgHeAD.Launcher/ViewModels/MainViewModel.cs)
- [PIgHeAD.Launcher/ViewModels/SettingsViewModel.cs](../PIgHeAD.Launcher/ViewModels/SettingsViewModel.cs)
- [PIgHeAD.Launcher/ViewModels/VersionsViewModel.cs](../PIgHeAD.Launcher/ViewModels/VersionsViewModel.cs)
- [PIgHeAD.Launcher/Views/AccountsView.xaml](../PIgHeAD.Launcher/Views/AccountsView.xaml)
- [PIgHeAD.Launcher/Views/AccountsView.xaml.cs](../PIgHeAD.Launcher/Views/AccountsView.xaml.cs)
- [PIgHeAD.Launcher/Views/DownloadsView.xaml](../PIgHeAD.Launcher/Views/DownloadsView.xaml)
- [PIgHeAD.Launcher/Views/DownloadsView.xaml.cs](../PIgHeAD.Launcher/Views/DownloadsView.xaml.cs)
- [PIgHeAD.Launcher/Views/HomeView.xaml](../PIgHeAD.Launcher/Views/HomeView.xaml)
- [PIgHeAD.Launcher/Views/MainWindow.xaml](../PIgHeAD.Launcher/Views/MainWindow.xaml)
- [PIgHeAD.Launcher/Views/SettingsView.xaml](../PIgHeAD.Launcher/Views/SettingsView.xaml)
- [PIgHeAD.Launcher/Views/VersionsView.xaml](../PIgHeAD.Launcher/Views/VersionsView.xaml)
- [PIgHeAD.Launcher/Views/VersionsView.xaml.cs](../PIgHeAD.Launcher/Views/VersionsView.xaml.cs)
- [PIgHeAD.sln](../PIgHeAD.sln)
- [README.md](../README.md)
- [docs/Launch-Integration-Changes.md](../docs/Launch-Integration-Changes.md)
- [docs/Microsoft-Authentication.md](../docs/Microsoft-Authentication.md)
- [tests/PIgHeAD.Launcher.ProcessProbe/PIgHeAD.Launcher.ProcessProbe.csproj](../tests/PIgHeAD.Launcher.ProcessProbe/PIgHeAD.Launcher.ProcessProbe.csproj)
- [tests/PIgHeAD.Launcher.ProcessProbe/Program.cs](../tests/PIgHeAD.Launcher.ProcessProbe/Program.cs)
- [tests/PIgHeAD.Launcher.Tests/LaunchWorkflowTests.cs](../tests/PIgHeAD.Launcher.Tests/LaunchWorkflowTests.cs)
- [tests/PIgHeAD.Launcher.Tests/LauncherTestContext.cs](../tests/PIgHeAD.Launcher.Tests/LauncherTestContext.cs)
- [tests/PIgHeAD.Launcher.Tests/PIgHeAD.Launcher.Tests.csproj](../tests/PIgHeAD.Launcher.Tests/PIgHeAD.Launcher.Tests.csproj)
- [tests/PIgHeAD.Launcher.Tests/ProcessAndTokenTests.cs](../tests/PIgHeAD.Launcher.Tests/ProcessAndTokenTests.cs)
- [tests/PIgHeAD.Launcher.Tests/SettingsViewModelTests.cs](../tests/PIgHeAD.Launcher.Tests/SettingsViewModelTests.cs)
- [tests/PIgHeAD.Launcher.Tests/WpfCompositionTests.cs](../tests/PIgHeAD.Launcher.Tests/WpfCompositionTests.cs)
- [tools/Set-LauncherIcon.ps1](../tools/Set-LauncherIcon.ps1)

## 删除的过时占位文件

- `PIgHeAD.Launcher/Models/PreviewItem.cs`
- `PIgHeAD.Launcher/ViewModels/PlaceholderPageViewModel.cs`
- `PIgHeAD.Launcher/Views/PlaceholderPageView.xaml`
- `PIgHeAD.Launcher/Views/PlaceholderPageView.xaml.cs`

## 验证

Debug / Release 构建通过，均为 0 警告、0 错误；42 项自动测试通过。已读取官方版本清单。受控进程测试验证中文及含空格路径、Java 检查、启动参数、日志令牌遮盖和退出处理；真实服务的空 Client ID 初始化也已验证。

没有下载完整游戏资源、打开真实 Minecraft 窗口或登录真实 Microsoft 账户。正版端到端验收需先配置有效且获准访问 Minecraft API 的 Client ID。
