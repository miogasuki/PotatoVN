namespace GalgameManager.Contracts.Services;

public interface IUpdateService
{
    /// <summary>
    /// 是否应该显示更新内容(每个版本只显示一次)
    /// </summary>
    public bool ShouldDisplayUpdateContent();
    
    /// <summary>
    /// 检查更新
    /// </summary>
    /// <returns>是否有更新</returns>
    public Task<bool> CheckUpdateAsync();
    
    /// <summary>
    /// 更新更行提醒的小蓝点
    /// </summary>
    public Task UpdateSettingsBadgeAsync();
    
    public event Action<bool>? SettingBadgeEvent; 
}