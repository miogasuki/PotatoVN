using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Refit;

namespace GalgameManager.Helpers.API.Steam;

public interface ISteamApi
{
    [Get("/ISteamUser/GetPlayerSummaries/v0002/?key={key}&steamids={steamids}")]
    Task<SteamResponseDto<SteamAccountResponseDto>> GetPlayerSummariesAsync(string key, string steamids);

    [Get("/IPlayerService/GetOwnedGames/v0001/?key={key}&steamid={steamid}&include_appinfo={include_appinfo}")]
    Task<SteamResponseDto<SteamGameListResponseDto>> GetOwnedGamesAsync(string key, string steamid, bool include_appinfo = true);
}
