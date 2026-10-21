# Microsoft 正版认证配置

启动器默认使用项目提供的公开 Client ID：`2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a`，不需要 Client Secret。首次启动或已有配置缺少 `microsoftClientId` 字段时使用此默认值；Settings 中显式清空或填写其他 ID 的选择会保留。仅配置 ID 不代表已验证应用审批或完成账户登录。

根据项目维护者于 **2026-10-21** 的确认，当前状态如下：

| 项目 | 状态 |
| --- | --- |
| Microsoft Entra 应用注册 | 已完成 |
| 桌面回调 `http://localhost` | 已配置 |
| Minecraft Services API 申请 | 尚未提交 |
| Minecraft Services API 审批 | 尚未获批 |

**当前项目按 Minecraft API 未获批处理。** Microsoft OAuth 应用注册与 Minecraft API 审批是两个步骤；浏览器授权成功也不能作为已获得 Minecraft API 权限的证明。Accounts 页面分别展示客户端配置和 API 审批记录。换用其他 Client ID 时显示“审核状态尚未验证”，不会自动认定其已获批。

**Accounts → 登录 Microsoft** 保留用于验证认证链路；当前不承诺能够完成 Minecraft 正版登录。若已有配置显式保存了空值，可在 Settings 中填入上述 ID 并保存。清空 ID 后，应用、版本管理、原版下载与离线模式仍可使用；Microsoft 登录会提示先配置，不会生成虚假的成功会话。

## 注册应用

1. 登录 [Microsoft Entra 管理中心](https://entra.microsoft.com/)，进入 **App registrations → New registration**。
2. 填写应用名称，例如 PIgHeAD Launcher。选择 **Personal Microsoft accounts only**，或包含个人 Microsoft 账户的多租户选项。本实现使用 `consumers` authority，只认证个人 Microsoft 账户。
3. 注册后记录 **Application (client) ID**。
4. 在 **Authentication → Add a platform → Mobile and desktop applications** 中添加 `http://localhost`。不要选择 Web 或 SPA 平台。MSAL 会在本机临时端口监听回调；Microsoft 对 localhost 回调支持动态端口匹配。
5. 配置应用为公开桌面客户端；按管理中心的选项启用 **Allow public client flows**。本项目使用授权码 + PKCE，不使用密码授权、客户端凭据授权或设备码流程。
6. 不创建 Client Secret，不把任何 Secret 写入启动器、配置文件或仓库。
7. 在启动器 **Settings → Microsoft 应用 Client ID** 中填写该 GUID 并保存，然后进入 **Accounts → 登录 Microsoft**。

注册步骤参考 [Microsoft 官方应用注册说明](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)。桌面回调和 PKCE 的实现依据 [MSAL 系统浏览器说明](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers)。

## Xbox 与 Minecraft 的额外要求

- 登录请求使用委托范围 `XboxLive.signin`；MSAL 管理用于会话刷新和 OpenID 的协议范围。无需为此登录额外申请 Microsoft Graph 的通讯录、邮件或文件权限。
- Microsoft OAuth 成功只证明身份认证完成。接下来还要交换 Xbox Live / XSTS 令牌，并完成 Minecraft Services 令牌、游戏所有权和 Java 角色资料验证。
- 第三方启动器的 Client ID 还需要得到 Minecraft API 的认可。Azure 应用注册成功并不表示 Minecraft 已允许该应用。按 [CmlLib 的 Client ID 指南](https://cmllib.github.io/CmlLib.Core-wiki/en/auth.microsoft/xboxauthnet.game.msal/clientid/) 和 [Minecraft 官方认证说明](https://help.minecraft.net/hc/en-us/articles/16254801392141) 办理 App ID 审核；官方审核入口为 [Minecraft App ID review](https://aka.ms/mce-reviewappid)。是否获批、所需材料和审核时间由 Microsoft/Mojang 决定。
- Minecraft Services 返回 `403 / Invalid app registration` 时，应检查应用审核状态，不应使用其他启动器的 Client ID，也不应把添加 Graph 权限当作修复方法。
- 账户必须有可用的 Minecraft Java 所有权与角色资料。缺少 Xbox 资料、家庭/年龄限制、游戏所有权或角色资料时，都可能失败；启动器会显示认证未完成，不会自动把正版请求改成离线启动。

## 申请 Minecraft Services API 访问权限

1. 保留现有 Entra 应用及 Client ID `2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a`，确认桌面重定向仍为 `http://localhost`，应用支持个人 Microsoft 账户。
2. 阅读 [Minecraft 官方认证说明](https://help.minecraft.net/hc/en-us/articles/16254801392141)，打开官方 [Minecraft App ID review 申请入口](https://aka.ms/mce-reviewappid)。该短链接目前跳转到 Microsoft Forms；需在浏览器中查看实际表单。
3. 按表单当时的要求填写项目与申请资料，并使用上述 **Application (client) ID**。可提前准备 PIgHeAD 启动器的名称、用途说明、项目地址和联系信息；具体必填字段以官方表单为准。本项目没有代填或提交申请。
4. 提交后保存申请记录，等待 Microsoft/Mojang 的结果；“已提交”仍按“未获批”处理。这里不承诺审批时长或一定获批，也无需为此在启动器中配置 Client Secret。
5. 收到批准后，核对通知对应的 Client ID 与 Settings 保存的值一致，再运行完整登录验证：Microsoft → Xbox Live → XSTS → Minecraft 令牌 → 所有权 → Java 角色资料。随后验证会话刷新和游戏启动。
6. 记录实际审批结果并更新本文及 Accounts 页的项目状态说明。界面文字仅为项目记录，不是权限开关；只有真实服务验证成功才能产生正版游戏会话。若仍返回 403，依据具体失败阶段继续排查。

[CmlLib 的 Client ID 指南](https://cmllib.github.io/CmlLib.Core-wiki/en/auth.microsoft/xboxauthnet.game.msal/clientid/) 明确要求 Azure 注册后另行办理 Minecraft API 的 Client ID 认可，以避免认证接口的 403。添加 Microsoft Graph 权限不能代替这项审批。

## 认证模块边界

- `Services/Abstractions/IAccountService.cs` 是 UI 使用的账户接口，提供登录、恢复和退出操作。
- `Services/Accounts/MicrosoftAccountService.cs` 封装 MSAL 系统浏览器授权码 + PKCE，以及 Xbox / Minecraft 验证；后续认证适配仍集中在该服务目录。
- `Services/Accounts/ProtectedTokenCache.cs` 负责当前 Windows 用户的 DPAPI 加密缓存。
- `Services/Accounts/AuthenticationHttpHandler.cs` 提供不含凭据的阶段诊断，不决定服务器是否授权。
- `ViewModels/AccountsViewModel.cs` 管理显示状态和命令，`Views/AccountsView.xaml` 通过绑定呈现；应用审批说明与实际账户会话分开显示。

当前审批说明不会阻止应用在缺少 Client ID 时启动，也不会让失败的认证通过。只有完整服务端验证完成才返回 `GameSession`；离线身份始终通过独立入口明确选择。

## 浏览器显示完成，但启动器提示 403

浏览器的 localhost 页面只表示 MSAL 收到了 Microsoft 授权回调，不表示 Xbox 或 Minecraft 已登录成功。启动器会继续验证，必须以 Accounts 页面最终结果为准。回调页现已明确说明这一点。

更新后，启动器按实际失败请求显示阶段和 HTTP 状态码，适用于交互登录、启动时恢复和游戏启动前刷新：

| 错误阶段 | 检查方法 |
| --- | --- |
| Minecraft 令牌交换，HTTP 403 | 优先核对当前 Client ID 的 Minecraft API 审核状态。只有 Azure 注册仍可能被拒绝。若已获批，核对 Settings 与获批 ID，并检查代理或服务访问限制。 |
| Xbox Live 认证 / Xbox XSTS 授权 | 确认登录的是正确的个人 Microsoft 账户，已建立 Xbox 玩家资料，并检查家庭、年龄与地区限制。不能把所有 Xbox 错误归因为 Minecraft 应用未审核。 |
| Minecraft 所有权检查 | 确认该账户有可用的 Java 版所有权或订阅，并检查服务访问状态。 |
| Minecraft 角色资料，HTTP 404 | 在 Minecraft 官网登录该账户，完成 Java 版角色名称设置。 |
| 无法连接 | 检查网络、系统时间、代理和服务可用性。 |

若尚未申请审核，打开 [Minecraft App ID review](https://aka.ms/mce-reviewappid)，按表单要求提交启动器及公开应用 ID `2d1fa88a-c9ad-41ef-a0e0-a6f661a7356a` 的资料。无需提供 Client Secret。应用审核通过后，确认 Settings 保存的仍是同一个 ID，再登录重试。审核由 Microsoft/Mojang 执行，修改启动器代码不能代替审批。

截图中的旧提示 `403: Forbidden` 没有包含失败阶段，单凭该提示不能确定是哪一个接口拒绝，也不能确认应用审核状态。若新版本仍失败，请提供 Accounts 显示的完整阶段提示；无需提供授权码、令牌或加密缓存。

诊断只保留预定义的阶段、HTTP 状态和建议，不读取或记录请求正文、响应正文、Authorization 头或包含授权信息的 URL；HTTP 原始响应仍交给认证库处理。网络取消仍按取消处理，任何失败均不会创建成功的游戏会话。

## 实际流程与缓存

1. MSAL 在系统默认浏览器中执行 Authorization Code Flow + PKCE，生成随机 code verifier/challenge，并管理 state 与 localhost 回调验证。
2. 公共桌面客户端使用授权码与 code verifier 换取令牌，不发送 Client Secret。
3. CmlLib 的内存账户管理器完成 Xbox / Minecraft 认证与所有权检查。只有完整链路成功后，UI 才切换到正版身份。
4. 游戏启动前重新执行静默刷新和 Minecraft 认证。需要重新交互登录时，明确提示用户返回 Accounts；不会静默切换到离线身份。
5. MSAL 缓存使用 Windows DPAPI 的 `CurrentUser` 范围加密，存放在 `%LOCALAPPDATA%\PIgHeAD\Launcher\auth\<client-id>.bin`。Xbox 与 Minecraft 令牌仅保存在内存，不写入设置 JSON。
6. DPAPI 无法使用或缓存解密失败时，认证会失败并显示错误；没有明文回退。缓存不能直接迁移给另一个 Windows 用户。
7. **退出并清除缓存**移除此启动器缓存中的 Microsoft 账户，不登出浏览器中的全部 Microsoft 账户。
8. 启动器不记录完整启动命令，写入的游戏控制台日志会遮盖当前 Minecraft access token。Minecraft 客户端协议仍需要把令牌传给 Java 进程，因此同一 Windows 用户下有权限检查进程参数的软件仍可能看到它。

## 当前验证边界

构建和无 Client ID 的启动路径可以验证；自动测试使用合成数据验证失败处理、加密缓存和进程参数。项目默认 Client ID 已配置，维护者确认尚未提交 Minecraft API 申请、尚未获批；真实账户端到端登录尚未成功验收。获批后应手工验证浏览器登录、拒绝授权、取消、会话刷新、没有游戏所有权的失败路径，以及真实正版服务器认证。
