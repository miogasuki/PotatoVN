using GalgameManager.Contracts.Services;
using GalgameManager.Helpers.API.Steam;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
namespace GalgameManager.Services.AccountServices;

public class SteamService : ISteamService
{
    private const string key = "";
    private string steamId { get; set; }
    private ISteamApi steamApi = SteamAPi.GetApi();
    private GalgameCollectionService galgameCollectionService;


    public async Task InitAsync(string steamid)
    {
        var account = await steamApi.GetPlayerSummariesAsync(key, steamid);
        if (account.response?.players?.Count == 0)
        {
            throw new PvnException("请检查steamID是否正确");
        }
        steamId = steamid;
    }

    public async Task<SteamAccountDto> GetSteamAccountAsync()
    {
        var account = await steamApi.GetPlayerSummariesAsync(key, steamId);
        return account.response.players[0]; //这里有可能会有多个玩家但是先不考虑吧
    }

    public async Task<List<SteamGameDto>> GetSteamGameListResponseAsync()
    {
        SteamResponseDto<SteamGameListResponseDto> gameList = await steamApi.GetOwnedGamesAsync(key, steamId);
        return gameList.response.games;
    }

    public async Task<List<SteamGameDto>> GetGalgameListResponseAsync()
    {
        var gameList = await GetSteamGameListResponseAsync();
        List<SteamGameDto> galgameList = new List<SteamGameDto>();
        foreach (var game in gameList)
        {
            if (PhraseHelper.TryGetSteamIdAsync(game.name) != null)
            {
                galgameList.Add(game);
            }
        }
        return galgameList;
    }

    public async Task UpdateSteamGalGameAsync(List<SteamGameDto> list, Galgame galgame)
    {
        foreach (var game in list)
        {
            if (game.appid == galgame.appId)
            {
                galgame.TotalPlayTime = game.playtime_forever;
                galgame.LastPlayTime = DateTimeOffset.FromUnixTimeSeconds(game.rtime_last_played).LocalDateTime;
                await galgameCollectionService.SaveGalgameAsync(galgame);
                break;
            }

        }
    }
}
