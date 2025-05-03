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
    public  bool CheckSteamAccountResponseDto(string steamid) => throw new NotImplementedException();


    public SteamAccountDto GetSteamAccount(string steamid) => throw new NotImplementedException();
    public SteamGameDto GetSteamGameDto(string appid) => throw new NotImplementedException();
    public SteamGameListResponseDto GetSteamGameListResponse(string steamid) => throw new NotImplementedException();
    public Task InitAsync(string steamid) => throw new NotImplementedException();
}
