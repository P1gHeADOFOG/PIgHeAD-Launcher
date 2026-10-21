using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PIgHeAD.Launcher.Models;

namespace PIgHeAD.Launcher.Services.Accounts;

public static class OfflineSessionFactory
{
    public static GameSession Create(string name)
    {
        if (!Regex.IsMatch(name, "^[A-Za-z0-9_]{3,16}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("离线玩家名须为 3–16 位英文字母、数字或下划线。");
        }
        // Java UUID.nameUUIDFromBytes compatibility; MD5 is an identity format here, not a security primitive.
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new GameSession(name, Convert.ToHexString(bytes).ToLowerInvariant(), "0", false);
    }
}
