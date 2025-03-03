using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Helpers.API.Ymgal;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;

// ReSharper disable ClassNeverInstantiated.Global

namespace GalgameManager.Helpers.Phrase;

public class YmgalPhraser: IGalInfoPhraser
{
    private IYmgalApi _ymgalApi;
    private Task<IYmgalApi>? _apiInitTask;

    public YmgalPhraser()
    {
        // 初始化一个未认证的API实例
        _ymgalApi = YmgalApi.GetApi();
        // 后台任务获取认证的API实例
        _apiInitTask = YmgalApi.GetAuthenticatedApiAsync();
    }

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        // 确保先初始化API
        await EnsureApiInitialized();

        var name = galgame.Name.Value ?? "";
        int? id;
        try
        {
            if (galgame.RssType != RssType.Ymgal) throw new Exception();
            id = Convert.ToInt32(galgame.Id ?? "");
        }
        catch (Exception)
        {
            try
            {
                var searchResponse = await ExecuteWithTokenRefreshAsync(async () => 
                    await _ymgalApi.SearchGameAsync(name));
                    
                if (!searchResponse.Success || searchResponse.Data?.Result.Count == 0) 
                    return null;
                
                id = searchResponse.Data?.Result?.FirstOrDefault()?.Id;
            }
            catch (Exception)
            {
                return null;
            }
        }

        try
        {
            var gameResponse = await ExecuteWithTokenRefreshAsync(async () => 
                await _ymgalApi.GetGameAsync(id ?? throw new InvalidOperationException("ID cannot be null")));
                
            if (!gameResponse.Success || gameResponse.Data?.Game == null)
                return null;
                
            var g = gameResponse.Data.Game;
            Galgame result = new()
            {
                Name = g.Name,
                CnName = g.ChineseName ?? "",
                Description = g.Introduction,
                ReleaseDate = IGalInfoPhraser.GetDateTimeFromString(g.ReleaseDate) ?? DateTime.MinValue, 
                ImageUrl = g.MainImg,
                Id = g.Gid != 0 ? g.Gid.ToString() : g.Id.ToString()
            };
            
            try
            {
                var developerResponse = await ExecuteWithTokenRefreshAsync(async () => 
                    await _ymgalApi.GetOrganizationAsync(g.DeveloperId));
                    
                if (developerResponse.Success && developerResponse.Data?.Org != null)
                {
                    result.Developer = developerResponse.Data.Org.Name;
                }
                else
                {
                    result.Developer = Galgame.DefaultString;
                }
            }
            catch
            {
                result.Developer = Galgame.DefaultString;
            }
            
            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 确保API已初始化
    private async Task EnsureApiInitialized()
    {
        if (_apiInitTask != null)
        {
            _ymgalApi = await _apiInitTask;
            _apiInitTask = null; // 初始化完成后清除任务引用
        }
    }

    // 带有token刷新逻辑的API调用执行器
    private async Task<T> ExecuteWithTokenRefreshAsync<T>(Func<Task<T>> apiCall, int retryCount = 0)
    {
        // 最大重试次数为2，防止死循环
        const int maxRetries = 2;
        
        try
        {
            // 确保API已初始化
            await EnsureApiInitialized();
            
            // 执行API调用
            return await apiCall();
        }
        catch (ApiException ex) when ((ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                                      ex.StatusCode == System.Net.HttpStatusCode.Forbidden) &&
                                      retryCount < maxRetries)
        {
            // 如果是401(Unauthorized)或403(Forbidden)，且未超过最大重试次数，尝试刷新token
            _ymgalApi = await YmgalApi.GetAuthenticatedApiAsync();
            
            // 递增重试计数，并重试API调用
            return await ExecuteWithTokenRefreshAsync(apiCall, retryCount + 1);
        }
    }

    public RssType GetPhraseType() => RssType.Ymgal;
}







