using System.Net;
using System.Net.Http;

namespace PIgHeAD.Launcher.Services.Accounts;

/// <summary>Collects safe diagnostics without retaining credentials or response bodies.</summary>
public sealed class AuthenticationHttpHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    public string? FailureMessage { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stage = GetStage(request.RequestUri);
        FailureMessage = null;
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            FailureMessage = $"{stage}无法连接。请检查网络、系统时间和代理设置后重试。";
            throw;
        }
        if (!response.IsSuccessStatusCode)
        {
            var guidance = (stage, response.StatusCode) switch
            {
                ("Minecraft 令牌交换", HttpStatusCode.Forbidden) =>
                    "Microsoft 浏览器授权已完成，但 Minecraft 服务拒绝访问。请确认 Settings 中的 Client ID 已通过 Minecraft App ID 审核：https://aka.ms/mce-reviewappid 。仅在 Azure 注册应用还不够；若已获批，请核对获批的 ID 与 Settings 一致，并检查网络代理。",
                ("Xbox Live 认证" or "Xbox XSTS 授权", _) =>
                    "请确认此 Microsoft 账户已有 Xbox 玩家资料，并检查 Xbox 家庭、年龄与地区限制。此阶段失败不能直接判断为 Minecraft 应用审核问题。",
                ("Minecraft 角色资料", HttpStatusCode.NotFound) =>
                    "未找到 Minecraft Java 角色。请使用拥有 Java 版的账户，在 Minecraft 官网完成角色名称设置。",
                (_, HttpStatusCode.Unauthorized) =>
                    "认证凭据未被接受，请重新登录 Microsoft。",
                ("Minecraft 所有权检查" or "Minecraft 角色资料", _) =>
                    "请检查账户的 Minecraft Java 所有权、角色资料和服务访问状态；此阶段失败不能直接判断为应用未获批。",
                _ => "请稍后重试，并检查认证服务和网络代理状态。"
            };
            FailureMessage = $"{stage}失败（HTTP {(int)response.StatusCode}）。{guidance}";
        }
        // Let the authentication library parse its response and preserve its normal failure behavior.
        return response;
    }

    private static string GetStage(Uri? uri) => (uri?.Host.ToLowerInvariant(), uri?.AbsolutePath) switch
    {
        ("user.auth.xboxlive.com", "/user/authenticate") => "Xbox Live 认证",
        ("xsts.auth.xboxlive.com", "/xsts/authorize") => "Xbox XSTS 授权",
        ("api.minecraftservices.com", "/authentication/login_with_xbox") => "Minecraft 令牌交换",
        ("api.minecraftservices.com", "/entitlements/mcstore") => "Minecraft 所有权检查",
        ("api.minecraftservices.com", "/minecraft/profile") => "Minecraft 角色资料",
        _ => "游戏账户认证"
    };
}
