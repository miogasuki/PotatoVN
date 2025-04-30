
namespace GalgameManager.Models.Accounts;

public class SteamAccount
{
    public string? steamid { get; set; }
    public string? personaname { get; set; }
    public int communityvisibilitystate { get; set; } //由这个来判断是否公开社区
}
