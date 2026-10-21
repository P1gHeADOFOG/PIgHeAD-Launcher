using System.Net;
using System.Net.Http;
using PIgHeAD.Launcher.Services.Accounts;

namespace PIgHeAD.Launcher.Tests;

public sealed class AuthenticationDiagnosticsTests
{
    [Theory]
    [InlineData("https://api.minecraftservices.com/authentication/login_with_xbox", 403, "Minecraft 令牌交换", true)]
    [InlineData("https://user.auth.xboxlive.com/user/authenticate", 401, "Xbox Live 认证", false)]
    [InlineData("https://xsts.auth.xboxlive.com/xsts/authorize", 403, "Xbox XSTS 授权", false)]
    [InlineData("https://api.minecraftservices.com/entitlements/mcstore", 403, "Minecraft 所有权检查", false)]
    [InlineData("https://api.minecraftservices.com/minecraft/profile", 404, "Minecraft 角色资料", false)]
    public async Task RejectedRequestsIdentifyStageAndOnlySuggestAppReviewAtTokenExchange(
        string endpoint, int status, string expectedStage, bool suggestsReview)
    {
        using var diagnostics = new AuthenticationHttpHandler(new StubHandler((HttpStatusCode)status));
        using var http = new HttpClient(diagnostics);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + "?token=private-query");
        request.Headers.Authorization = new("Bearer", "private-header");
        using var response = await http.SendAsync(request);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Contains(expectedStage, diagnostics.FailureMessage);
        Assert.Contains($"HTTP {status}", diagnostics.FailureMessage);
        Assert.Equal(suggestsReview, diagnostics.FailureMessage!.Contains("https://aka.ms/mce-reviewappid"));
        Assert.DoesNotContain("private-", diagnostics.FailureMessage);
        // The library must still be able to parse the original response.
        Assert.Equal("private-response", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SuccessClearsPreviousFailureAndRetainsNoResponseData()
    {
        using var stub = new StubHandler(HttpStatusCode.Forbidden);
        using var diagnostics = new AuthenticationHttpHandler(stub);
        using var http = new HttpClient(diagnostics);
        using var failure = await http.GetAsync("https://api.minecraftservices.com/authentication/login_with_xbox");
        Assert.NotNull(diagnostics.FailureMessage);

        stub.Status = HttpStatusCode.OK;
        using var success = await http.GetAsync("https://api.minecraftservices.com/authentication/login_with_xbox");

        Assert.Null(diagnostics.FailureMessage);
    }

    [Fact]
    public async Task NetworkFailureDoesNotExposeRawExceptionOrRequestUri()
    {
        using var diagnostics = new AuthenticationHttpHandler(new StubHandler(HttpStatusCode.OK)
        {
            Error = new HttpRequestException("private-token https://private-host/path")
        });
        using var http = new HttpClient(diagnostics);

        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync("https://xsts.auth.xboxlive.com/xsts/authorize"));

        Assert.Contains("Xbox XSTS 授权无法连接", diagnostics.FailureMessage);
        Assert.DoesNotContain("private", diagnostics.FailureMessage);
    }

    [Fact]
    public async Task CancellationIsNotReportedAsAuthenticationFailure()
    {
        using var diagnostics = new AuthenticationHttpHandler(new StubHandler(HttpStatusCode.OK)
        {
            Error = new OperationCanceledException()
        });
        using var http = new HttpClient(diagnostics);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => http.GetAsync("https://xsts.auth.xboxlive.com/xsts/authorize"));

        Assert.Null(diagnostics.FailureMessage);
    }

    [Fact]
    public async Task StageDiagnosticReachesAccountsWithoutCreatingLaunchSession()
    {
        using var diagnostics = new AuthenticationHttpHandler(new StubHandler(HttpStatusCode.Forbidden));
        using var http = new HttpClient(diagnostics);
        using var response = await http.PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox", null);
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        context.Accounts.UseMicrosoft = true;
        context.AccountService.SignInError = new InvalidOperationException(diagnostics.FailureMessage);

        await context.Accounts.SignInCommand.ExecuteAsync(null);
        await context.Home.LaunchGameCommand.ExecuteAsync(null);

        Assert.Contains("Minecraft 令牌交换失败（HTTP 403）", context.Accounts.Status);
        Assert.Contains("https://aka.ms/mce-reviewappid", context.Accounts.Status);
        Assert.Null(context.Accounts.MicrosoftSession);
        Assert.False(context.Accounts.IsBusy);
        Assert.Equal(0, context.GameService.LaunchCalls);
    }

    [Fact]
    public async Task UnverifiedMicrosoftSelectionDoesNotClaimLicensedAccount()
    {
        var context = new LauncherTestContext();
        await context.Main.InitializeAsync(CancellationToken.None);
        context.Accounts.UseMicrosoft = true;

        Assert.Contains("尚未认证", context.Accounts.Description);
        Assert.DoesNotContain("已验证", context.Accounts.Description);
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = status;
        public Exception? Error { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Error is null
                ? Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent("private-response") })
                : Task.FromException<HttpResponseMessage>(Error);
    }
}
