using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public class LocalFolderSourceService : IGalgameSourceService
{
    private readonly Dictionary<GalgameFolderSource, FileSystemWatcher> _watchers = new();
    private readonly IInfoService _infoService;
    private readonly IFileService _fileService;

    public LocalFolderSourceService(IInfoService infoService, IFileService fileService)
    {
        _infoService = infoService;
        _fileService = fileService;
        App.OnAppClosing += () =>
        {
            foreach (FileSystemWatcher watcher in _watchers.Values)
            {
                try
                {
                    watcher.Dispose();
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        };
    }

    public BgTaskBase MoveInAsync(GalgameSourceBase target, Galgame game, string? targetPath = null)
    {
        if (targetPath is null) throw new PvnException("targetPath is null");
        if (target is not GalgameFolderSource) throw new ArgumentException("target is not GalgameFolderSource");
        return new LocalFolderSourceMoveInTask(game, targetPath);
    }

    public BgTaskBase MoveOutAsync(GalgameSourceBase target, Galgame game)
    {
        return new LocalFolderSourceMoveOutTask(game, target);
    }

    public async Task SaveMetaAsync(Galgame game)
    {
        if (!game.CheckExistLocal()) return;
        foreach (GalgameFolderSource source in game.Sources.OfType<GalgameFolderSource>())
        {
            var folderPath = source.GetPath(game)!;
            var metaPath = Path.Combine(folderPath, ".PotatoVN");
            if (!Directory.Exists(metaPath)) Directory.CreateDirectory(metaPath);
            Galgame meta = game.DeepClone();
            // 备份图片
            if (Utils.IsImageValid(meta.ImagePath.Value))
            {
                CopyImg(meta.ImagePath.Value, metaPath);
                meta.ImagePath.ForceSet(Path.Combine(".", Path.GetFileName(meta.ImagePath.Value!)));
            }
            foreach (GalgameCharacter character in meta.Characters)
            {
                if (Utils.IsImageValid(character.ImagePath))
                {
                    CopyImg(character.ImagePath, metaPath);
                    character.ImagePath = Path.Combine(".", Path.GetFileName(character.ImagePath));
                }
                if (Utils.IsImageValid(character.PreviewImagePath))
                {
                    CopyImg(character.PreviewImagePath, metaPath);
                    character.PreviewImagePath = Path.Combine(".", Path.GetFileName(character.PreviewImagePath));
                }
            }
            _fileService.Save(metaPath, "meta.json", meta);
        }

        await Task.CompletedTask;
    }

    public async Task<Galgame?> LoadMetaAsync(string path)
    {
        await Task.CompletedTask;
        var metaFolderPath = Path.Combine(path, ".PotatoVN");
        if (!Directory.Exists(metaFolderPath)) return null; // 不存在备份文件夹
        Galgame meta = _fileService.Read<Galgame>(metaFolderPath, "meta.json")!;
        if (meta is null) throw new PvnException("meta.json not exist");
        meta.ImagePath.ForceSet(LoadImg(meta.ImagePath.Value, metaFolderPath));
        foreach (GalgameCharacter character in meta.Characters)
        {
            character.ImagePath = LoadImg(character.ImagePath, metaFolderPath)!;
            character.PreviewImagePath = LoadImg(character.PreviewImagePath, metaFolderPath)!;
        }
        meta.ExePath = LoadImg(meta.ExePath, metaFolderPath, defaultReturn: null);
        meta.SavePath = Directory.Exists(meta.SavePath) ? meta.SavePath : null; //检查存档路径是否存在并设置SavePosition字段
        meta.FindSaveInPath();
        return meta;
    }

    public Task RemoveMetaAsync(Galgame game)
    {
        return Task.Run(() =>
        {
            foreach (GalgameFolderSource source in game.Sources.OfType<GalgameFolderSource>())
            {
                try
                {
                    var folderPath = source.GetPath(game)!;
                    var metaPath = Path.Combine(folderPath, ".PotatoVN");
                    if (!Directory.Exists(metaPath)) return;
                    Directory.Delete(metaPath, true);
                    _infoService.Log(msg: $"[LocalFolderSourceService] remove meta folder {metaPath}");
                }
                catch (Exception e)
                {
                    _infoService.DeveloperEvent(msg: $"failed to remove meta folder with exception: {e}");
                }
            }
        });
    }

    public async Task<(long total, long used)> GetSpaceAsync(GalgameSourceBase source)
    {
        await Task.CompletedTask;
        try
        {
            DriveInfo? info = GetDriveInfo(source.Path);
            if (info is null) return (-1, -1);
            return (info.TotalSize, info.TotalSize - info.AvailableFreeSpace);
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: $"failed to get drive info with exception: {e}");
            return (-1, -1);
        }
    }

    public async Task AddListenAsync(GalgameSourceBase source)
    {
        if (source is not GalgameFolderSource folderSource)
            throw new ArgumentException($"source {source.Path} is not GalgameFolderSource");
        FileSystemWatcher watcher = new(folderSource.Path);
        watcher.NotifyFilter = NotifyFilters.DirectoryName;
        watcher.Filter = "*";
        watcher.EnableRaisingEvents = true;
        if (folderSource.DetectFolderAdd) watcher.Created += OnFolderCreated;
        if (folderSource.DetectFolderRemove) watcher.Deleted += OnFolderDelete;
        _watchers.Add(folderSource, watcher);
        await Task.CompletedTask;
    }

    public async Task RemoveListenAsync(GalgameSourceBase source)
    {
        if (source is not GalgameFolderSource folderSource) 
            throw new ArgumentException($"source {source.Path} is not GalgameFolderSource");
        if (_watchers.TryGetValue(folderSource, out FileSystemWatcher? watcher))
        {
            watcher.Dispose();
            _watchers.Remove(folderSource);
        }
        await Task.CompletedTask;
    }

    public string GetMoveInDescription(GalgameSourceBase target, string targetPath)
    {
        return "LocalFolderSourceService_MoveInDescription".GetLocalized(targetPath);
    }

    public string GetMoveOutDescription(GalgameSourceBase target, Galgame galgame)
    {
        var path = target.GetPath(galgame) ?? string.Empty;
        return "LocalFolderSourceService_MoveOutDescription".GetLocalized(path);
    }

    public string? CheckMoveOperateValid(GalgameSourceBase? moveIn, GalgameSourceBase? moveOut, Galgame galgame)
    {
        if (moveIn?.SourceType == GalgameSourceType.LocalFolder)
            return moveOut?.SourceType == GalgameSourceType.LocalFolder
                ? null
                : "LocalFolderSourceService_MoveOutError".GetLocalized();
        return null;
    }

    private static void CopyImg(string? src, string metaPath)
    {
        if (!Utils.IsImageValid(src) || src is null) return;
        var target = Path.Combine(metaPath, Path.GetFileName(src));
        if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(src).Length) return; //文件已存在且大小相同就不复制
        File.Copy(src, target, true);
    }

    private static string? LoadImg(string? target, string path, string defaultTarget = Galgame.DefaultImagePath, 
        string? defaultReturn = Galgame.DefaultImagePath)
    {
        if (string.IsNullOrEmpty(target) || target == defaultTarget) return defaultReturn;
        var targetPath = Path.GetFullPath(Path.Combine(path, target));
        return File.Exists(targetPath) ? targetPath : defaultReturn;
    }
    
    private static DriveInfo? GetDriveInfo(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is null ? null : new DriveInfo(root);
    }

    private void OnFolderCreated(object sender, FileSystemEventArgs e)
    {
        if (!Directory.Exists(e.FullPath)) return;
        UiThreadInvokeHelper.Invoke(async () =>
        {
            try
            {
                IGalgameCollectionService gameService = App.GetService<IGalgameCollectionService>();
                await gameService.AddGameAsync(GalgameSourceType.LocalFolder, e.FullPath, true, false);
                _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Success,
                    "LocalFolderSourceService_OnFolderCreated".GetLocalized(), msg: e.FullPath);
            }
            catch (Exception exception)
            {
                _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                    "LocalFolderSourceService_OnFolderCreatedError".GetLocalized(), exception);
            }
        });
    }

    private void OnFolderDelete(object sender, FileSystemEventArgs e)
    {
        try
        {
            DirectoryInfo? sourceDir = new DirectoryInfo(e.FullPath).Parent;
            if (sourceDir is null)
            {
                Log($"Failed to get parent directory of {e.FullPath}", InfoBarSeverity.Warning);
                return;
            }
            IGalgameSourceCollectionService sourceService = App.GetService<IGalgameSourceCollectionService>();
            GalgameSourceBase? source =
                sourceService.GetGalgameSource(GalgameSourceType.LocalFolder, sourceDir.FullName);
            if (source is null)
            {
                Log($"Failed to get source from {sourceDir.FullName}");
                return;
            }

            GalgameAndPath? game = source.Galgames.FirstOrDefault(g => Utils.ArePathsEqual(g.Path, e.FullPath));
            if (game is null)
            {
                Log($"Failed to get game from {source.Path}");
                return;
            }

            sourceService.MoveOutOperate(source, game.Galgame);

            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Success,
                "LocalFolderSourceService_OnFolderDelete".GetLocalized(), msg: e.FullPath);
            Log($"Game {game.Galgame.Name} moved out from {source.Path}");
        }
        catch (Exception exception)
        {
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning,
                "LocalFolderSourceService_OnFolderDeleteError".GetLocalized(), exception);
        }

        return;

        void Log(string msg, InfoBarSeverity severity = InfoBarSeverity.Informational) =>
            _infoService.Log(severity, msg: $"[OnFolderDelete] {msg}");
    }
}