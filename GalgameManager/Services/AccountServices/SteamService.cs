using System.Threading.Tasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers.API.Steam;

namespace GalgameManager.Services.AccountServices;

public class SteamService : ISteamService
{
    private string key;
    private string steamId { get; set; }
    private ISteamApi steamApi = SteamAPi.GetApi();
    public SteamAccountDto steamAccountDto;
    public SteamService(string key)
    {
        this.key = key;
    }
    public SteamService(string key, string steamId)
    {
        this.key = key;
        this.steamId = steamId;
    }
    public async Task<bool> CheckSteamAccountResponseDto(string steamid)
    {
        if (steamId == null)
        {
            return false; //这里到时候加报错 没有steamid
        }
        var response = await steamApi.GetPlayerSummariesAsync(key, steamId);
        if(response)
    }
    public SteamAccountDto GetSteamAccount(string steamid) => throw new NotImplementedException();
    public SteamGameDto GetSteamGameDto(string appid) => throw new NotImplementedException();
    public SteamGameListResponseDto GetSteamGameListResponse(string steamid) => throw new NotImplementedException();
    public Task InitAsync(string steamid) => throw new NotImplementedException();
}
