using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.BgTasks;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class BgTaskService : IBgTaskService
{
    public event Action<BgTaskBase>? BgTaskAdded;
    public event Action<BgTaskBase>? BgTaskRemoved;

    private const string FileName = "bgTasks.json";

    private readonly List<BgTaskBase> _bgTasks = new();
    private readonly object _bgTasksLock = new();
    private readonly Dictionary<Type,string> _bgTasksString = new();
    private readonly IInfoService _infoService;
    private readonly List<JsonConverter> _converters = new();

    public BgTaskService(IInfoService infoService)
    {
        _infoService = infoService;

        _bgTasksString[typeof(RecordPlayTimeTask)] = "-record";
        _bgTasksString[typeof(GetGalgameInSourceTask)] = "-getGalInSource";
        _bgTasksString[typeof(UnpackGameTask)] = "-unpack";
        _bgTasksString[typeof(SourceMoveTask)] = "-sourceMove";
        _bgTasksString[typeof(GetGalgameCharactersFromRssTask)] = "-getGalChar";
        _bgTasksString[typeof(CallMagpieTask)] = "-callMagpie";
        _bgTasksString[typeof(GameMuteTask)] = "-gameMute";
        _bgTasksString[typeof(KeyMappingTask)] = "-keyMap";
        _bgTasksString[typeof(GameSaveDetectorTask)] = "-saveDetector";

        _converters.Add(new GalgameAndUidConverter());
    }

    public void SaveBgTasksString()
    {
        var result = string.Empty;
        BgTaskBase[] snapshot;
        lock (_bgTasksLock)
        {
            snapshot = _bgTasks.ToArray();
        }

        foreach (BgTaskBase bgTask in snapshot)
        {
            //转换为json，再转换为base64（避免参数解析困难），再加上前缀
            if (_bgTasksString.TryGetValue(bgTask.GetType(), out var str))
                result += str + $" {JsonConvert.SerializeObject(bgTask, _converters.ToArray()).ToBase64()} ";
        }
        FileHelper.Save(FileName, result);
    }

    public async Task ResolvedBgTasksAsync()
    {
        var argStrings = FileHelper.Load<string>(FileName)?.Split() ?? Array.Empty<string>();
        for (var i = 0; i < argStrings.Length; i++)
        {
            if(argStrings[i].StartsWith("-") == false) continue;
            Type? bgTaskType = _bgTasksString.FirstOrDefault(x => x.Value == argStrings[i]).Key;
            if (bgTaskType == null) continue;
            if (JsonConvert.DeserializeObject(Utils.FromBase64(argStrings[++i]), bgTaskType, _converters.ToArray()) is not BgTaskBase bgTask)
                continue;
            await bgTask.RecoverFromJson();
            _ = AddTaskInternal(bgTask);
        }
        FileHelper.Delete(FileName);
    }

    public Task AddBgTask(BgTaskBase bgTask) => AddTaskInternal(bgTask);

    public IEnumerable<BgTaskBase> GetBgTasks()
    {
        lock (_bgTasksLock)
        {
            return _bgTasks.ToArray();
        }
    }

    public T? GetBgTask<T>(string key) where T : BgTaskBase
    {
        BgTaskBase[] snapshot;
        lock (_bgTasksLock)
        {
            snapshot = _bgTasks.ToArray();
        }

        return snapshot.FirstOrDefault(t => t is T && t.OnSearch(key)) as T;
    }

    private Task AddTaskInternal(BgTaskBase bgTask)
    {
        try
        {
            lock (_bgTasksLock)
            {
                _bgTasks.Add(bgTask);
            }

            if (bgTask.ProgressOnTrayIcon)
                bgTask.OnProgress += UpdateTrayIconLabel;

            UiThreadInvokeHelper.Invoke(() => BgTaskAdded?.Invoke(bgTask));

            Task runTask = bgTask.Run();

            return HandleBgTaskCompletionAsync(bgTask, runTask);
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
                "BgTaskService_TaskFailed".GetLocalized(bgTask.Title), e);
            TryRemoveBgTask(bgTask);
            return Task.CompletedTask;
        }
    }

    private async Task HandleBgTaskCompletionAsync(BgTaskBase bgTask, Task runTask)
    {
        Exception? exception = null;
        try
        {
            try
            {
                await runTask.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                exception = e;
            }

            await Task.Delay(500).ConfigureAwait(false);

            if (exception is not null)
            {
                _infoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Warning,
                    "BgTaskService_TaskFailed".GetLocalized(bgTask.Title), exception);
            }
            else if (bgTask.CurrentProgress.NotifyWhenSuccess && bgTask.CurrentProgress.Current > 0)
            {
                _infoService.Event(EventType.BgTaskSuccessEvent, InfoBarSeverity.Success,
                    "BgTaskService_TaskSuccess".GetLocalized(bgTask.Title), msg: bgTask.CurrentProgress.Message,
                    callbackAction: bgTask.EventAction, callbackButtonText: bgTask.EventActionText);
            }
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: "HandleBgTaskCompletionAsync Failed", e: e);
        }
        finally
        {
            var removed = TryRemoveBgTask(bgTask);
            if (removed)
            {
                UiThreadInvokeHelper.Invoke(() => BgTaskRemoved?.Invoke(bgTask));
            }

            if (bgTask.ProgressOnTrayIcon)
            {
                bgTask.OnProgress -= UpdateTrayIconLabel;
                UpdateTrayIconLabel(new Progress());
            }
        }
    }

    private bool TryRemoveBgTask(BgTaskBase bgTask)
    {
        lock (_bgTasksLock)
        {
            return _bgTasks.Remove(bgTask);
        }
    }

    private void UpdateTrayIconLabel(Progress _)
    {
        try
        {
            BgTaskBase[] snapshot;
            lock (_bgTasksLock)
            {
                snapshot = _bgTasks.ToArray();
            }

            UiThreadInvokeHelper.Invoke(() =>
            {
                try
                {
                    var label = $"{"AppDisplayName".GetLocalized()}\n";
                    foreach (BgTaskBase bgTask in snapshot)
                    {
                        if (bgTask.ProgressOnTrayIcon)
                            label += $"{bgTask.CurrentProgress.Message}\n";
                    }

                    label = label.TrimEnd('\n'); //去掉最后一个换行符
                    if (App.SystemTray is not null)
                        App.SystemTray.ToolTipText = label;
                }
                catch (Exception e) // 更新托盘这个事情就算挂了也不影响整个软件的运行，所以这里不需要抛出异常炸掉整个软件
                {
                    _infoService.DeveloperEvent(msg: "Update TrayIcon Label Failed", e: e);
                }
            });
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: "Update TrayIcon Label Failed", e: e); // 不太可能发生，为了保险起见还是加上
        }
    }
}
