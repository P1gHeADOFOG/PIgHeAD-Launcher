namespace PIgHeAD.Launcher.Models;

// Do not use a record: its generated ToString would expose credentials.
public sealed class GameSession(string userName, string uuid, string accessToken, bool isMicrosoft, string xuid = "")
{
    public string UserName { get; } = userName;
    public string Uuid { get; } = uuid;
    public string AccessToken { get; } = accessToken;
    public bool IsMicrosoft { get; } = isMicrosoft;
    public string Xuid { get; } = xuid;

    public override string ToString() => UserName;
}
