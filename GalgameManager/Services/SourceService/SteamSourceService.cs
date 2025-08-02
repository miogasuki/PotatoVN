using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using ValveKeyValue;

namespace GalgameManager.Services;

public class SteamSourceService : IGalgameSourceService
{
    //installdir->appmanifest_*.acf文件内容，做一个缓存，避免扫描游戏时每次都便利所有文件
    private readonly Dictionary<string, KVValue> _appManifests = new(); 
    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null) => 
        throw new PvnException("This source does not support move in operation");

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game) => 
        throw new PvnException("This source does not support move out operation");

    public async Task SaveMetaAsync(Galgame game, GalgameSourceBase? targetSource = null)
    {
        await Task.CompletedTask;
        if (targetSource is null || !targetSource.SaveMetaBackup) return;
        throw new NotImplementedException();
    }

    public async Task<Galgame?> LoadMetaAsync(string path)
    {
        // DirectoryInfo dir = new(path);
        await Task.CompletedTask;
        return null;
    }

    public Task RemoveMetaAsync(Galgame game) => throw new NotImplementedException();

    public Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source)
    {
        DriveInfo info = new(source.Path);
        return Task.FromResult((info.TotalSize, info.TotalSize - info.AvailableFreeSpace));
    }

    public Task AddListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public Task RemoveListenAsync(GalgameSourceBase source) => Task.CompletedTask;

    public string GetMoveInDescription(GalgameSourceBase target, string targetPath) => string.Empty; //不支持
    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame) => string.Empty; //不支持

    public async Task<string> GetSourcePathAsync(string gamePath)
    {
        await Task.CompletedTask;
        DirectoryInfo? dir = new(gamePath);
        while (dir is not null)
        {
            if (dir.Name == "steamapps") return dir.FullName;
            dir = dir.Parent;
        }
        throw new PvnException($"{gamePath} does not belong to any Steam library");
    }

    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame) =>
        "This source does not support move operation";
}