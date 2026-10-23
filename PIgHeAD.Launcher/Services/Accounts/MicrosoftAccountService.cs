using System.IO;
using System.Net.Http;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Microsoft.Identity.Client;
using PIgHeAD.Launcher.Models;
using PIgHeAD.Launcher.Services.Abstractions;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;

namespace PIgHeAD.Launcher.Services.Accounts;

public sealed class MicrosoftAccountService(string? authDirectory = null) : IAccountService
{
    private static readonly string[] Scopes = ["XboxLive.signin"];
    private IPublicClientApplication? application;
    private string? applicationId;

    public async Task<GameSession> SignInAsync(string clientId, CancellationToken cancellationToken)
    {
        var app = GetApplication(clientId);
        // MSAL uses authorization code + PKCE and validates state on its loopback listener.
        var result = await app.AcquireTokenInteractive(Scopes)
            .WithUseEmbeddedWebView(false)
            .WithSystemWebViewOptions(new SystemWebViewOptions
            {
                HtmlMessageSuccess = "<html><head><meta charset=\"utf-8\"><title>PIgHeAD 登录</title></head>"
                    + "<body><h2>已收到 Microsoft 授权回调</h2><p>请返回 PIgHeAD 启动器查看最终结果。"
                    + "启动器还需验证 Xbox 和 Minecraft，当前页面不代表游戏账户登录成功。</p>"
                    + "<p>可以关闭此浏览器标签页。</p></body></html>"
            })
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
        var session = await ExchangeForMinecraftAsync(app, result, cancellationToken).ConfigureAwait(false);
        foreach (var account in await app.GetAccountsAsync().ConfigureAwait(false))
        {
            if (account.HomeAccountId.Identifier != result.Account.HomeAccountId.Identifier)
            {
                await app.RemoveAsync(account).ConfigureAwait(false);
            }
        }
        return session;
    }

    public async Task<GameSession?> RestoreAsync(string clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }
        var app = GetApplication(clientId);
        var account = (await app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();
        if (account is null)
        {
            return null;
        }
        try
        {
            var result = await app.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return await ExchangeForMinecraftAsync(app, result, cancellationToken).ConfigureAwait(false);
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    public async Task SignOutAsync(string clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }
        var app = GetApplication(clientId);
        foreach (var account in await app.GetAccountsAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await app.RemoveAsync(account).ConfigureAwait(false);
        }
    }

    private IPublicClientApplication GetApplication(string clientId)
    {
        if (!Guid.TryParse(clientId, out var id))
        {
            throw new InvalidOperationException("请先在 Settings 中填写并保存你注册的 Microsoft Client ID。无需 Client Secret。");
        }
        var normalizedId = id.ToString();
        if (application is not null && normalizedId == applicationId)
        {
            return application;
        }
        var cache = new ProtectedTokenCache(Path.Combine(authDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PIgHeAD", "Launcher", "auth"), normalizedId + ".bin"));
        var app = PublicClientApplicationBuilder.Create(normalizedId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
            .WithRedirectUri("http://localhost")
            .Build();
        app.UserTokenCache.SetBeforeAccess(args =>
        {
            var data = cache.Read();
            if (data is not null)
            {
                args.TokenCache.DeserializeMsalV3(data);
            }
        });
        app.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
            {
                cache.Write(args.TokenCache.SerializeMsalV3());
            }
        });
        applicationId = normalizedId;
        application = app;
        return app;
    }

    private static async Task<GameSession> ExchangeForMinecraftAsync(
        IPublicClientApplication app, AuthenticationResult result, CancellationToken cancellationToken)
    {
        // The Xbox/Minecraft tokens stay in memory; only the encrypted MSAL cache is persisted.
        using var diagnostics = new AuthenticationHttpHandler(new HttpClientHandler());
        using var http = new HttpClient(diagnostics);
        var handler = new JELoginHandlerBuilder()
            .WithHttpClient(http)
            .WithOAuthProvider(new MsalCodeFlowProvider(app))
            .WithAccountManager(new InMemoryXboxGameAccountManager(JEGameAccount.FromSessionStorage))
            .Build();
        var authenticator = handler.CreateAuthenticatorWithNewAccount(cancellationToken);
        authenticator.AddMsalOAuth(app, oauth => oauth.FromResult(result));
        authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
        authenticator.AddForceJEAuthenticator(builder => builder.WithGameOwnershipChecker().Build());
        MSession session;
        try
        {
            session = await authenticator.ExecuteForLauncherAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            && diagnostics.FailureMessage is not null)
        {
            // Never surface raw HTTP payloads, request URIs, or token-bearing inner exceptions.
            throw new InvalidOperationException(diagnostics.FailureMessage);
        }
        if (!session.CheckIsValid() || string.IsNullOrWhiteSpace(session.Username)
            || string.IsNullOrWhiteSpace(session.UUID) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            throw new InvalidOperationException("未能取得有效的 Minecraft Java 正版资料，请检查游戏所有权与应用权限。");
        }
        return new GameSession(session.Username, session.UUID, session.AccessToken, true, session.Xuid ?? "");
    }
}
