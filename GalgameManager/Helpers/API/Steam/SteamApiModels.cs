
using System.Text.Json.Serialization;

namespace GalgameManager.Helpers.API.Steam;
public class SteamResponseDto<T>
{
    [JsonPropertyName("response")]
    public required T response { get; set; }
}

public class SteamAccountResponseDto
{
    [JsonPropertyName("players")]
    public List<SteamAccountDto> players { get; set; } = new List<SteamAccountDto>();
}

public class SteamAccountDto
{
    [JsonPropertyName("steamid")]
    public string steamId { get; set; } = string.Empty;

    [JsonPropertyName("communityvisibilitystate")]
    public int communityVisibilityState { get; set; }

    [JsonPropertyName("profilestate")]
    public int profileState { get; set; }

    [JsonPropertyName("personaname")]
    public string personaName { get; set; } = string.Empty;

    [JsonPropertyName("commentpermission")]
    public int commentPermission { get; set; }

    [JsonPropertyName("profileurl")]
    public string profileUrl { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = string.Empty;

    [JsonPropertyName("avatarmedium")]
    public string avatarMedium { get; set; } = string.Empty;

    [JsonPropertyName("avatarfull")]
    public string avatarFull { get; set; } = string.Empty;

    [JsonPropertyName("avatarhash")]
    public string avatarHash { get; set; } = string.Empty;

    [JsonPropertyName("lastlogoff")]
    public int LastLogOff { get; set; }

    [JsonPropertyName("personastate")]
    public int PersonaState { get; set; }

    [JsonPropertyName("primaryclanid")]
    public string PrimaryClanId { get; set; } = string.Empty;

    [JsonPropertyName("timecreated")]
    public int TimeCreated { get; set; }

    [JsonPropertyName("personastateflags")]
    public int PersonaStateFlags { get; set; }

    [JsonPropertyName("loccountrycode")]
    public string? LocCountryCode { get; set; }
}
public class SteamGameListResponseDto
{
    [JsonPropertyName("game_count")]
    public int gameCount { get; set; }

    [JsonPropertyName("games")]
    public List<SteamGameDto> games { get; set; } = new List<SteamGameDto>();
}
public class SteamGameDto
{
    [JsonPropertyName("appid")]
    public int appid { get; set; }

    [JsonPropertyName("name")]
    public string name { get; set; } = string.Empty;

    [JsonPropertyName("playtime_2weeks")]
    public int playtime_2weeks { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int playtime_forever { get; set; }

    [JsonPropertyName("img_icon_url")] 
    public string img_icon_url { get; set; } = string.Empty;
}