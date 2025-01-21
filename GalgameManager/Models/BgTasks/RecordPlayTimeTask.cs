using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Services;
using GalgameManager.ViewModels;

namespace GalgameManager.Models.BgTasks;

public class RecordPlayTimeTask : BgTaskBase
{
    private const int ManuallySelectProcessSec = 15; //认定为需要手动选择游戏进程的时间阈值
    
    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }= DateTime.Now;
    public int CurrentPlayTime { get; set; } //本次游玩时间
    public override bool ProgressOnTrayIcon => true;

    public Galgame? Galgame;
    private Process? _process;

    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();
    
    public RecordPlayTimeTask(){}

    public RecordPlayTimeTask(Galgame game, Process process)
    {
        Debug.Assert(game.CheckExistLocal());
        if (process.HasExited) return;
        ProcessName = process.ProcessName;
        Galgame = game;
        _process = process;
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        if(_process is null || Galgame is null) return ;
        ChangeProgress(0, 1, "RecordPlayTimeTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
        Task t = Task.Run(async () =>
        {
            await _process.WaitForExitAsync();
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                GalgamePageParameter parma = new()
                {
                    Galgame = Galgame,
                    SelectProgress = DateTime.Now - StartTime < TimeSpan.FromSeconds(ManuallySelectProcessSec) 
                                     && Galgame.ProcessName is null
                };
                App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, parma);
                App.SetWindowMode(WindowMode.Normal);
                ChangeProgress(1, 1,
                    "RecordPlayTimeTask_Done".GetLocalized(Galgame.Name.Value ?? string.Empty,
                        TimeToDisplayTimeConverter.Convert(CurrentPlayTime)));
            });
            await (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!.SaveGalgamesAsync(Galgame);
            if(await App.GetService<ILocalSettingsService>().ReadSettingAsync<bool>(KeyValues.SyncGames))
                App.GetService<IPvnService>().Upload(Galgame, PvnUploadProperties.PlayTime);
        });
        
        _ = RecordPlayTimeAsync();

        await t;
    }

    private Task RecordPlayTimeAsync()
    {
        return Task.Run(() =>
        {
            var recordOnlyWhenForeground =
                _localSettingsService.ReadSettingAsync<bool>(KeyValues.RecordOnlyWhenForeground).Result;
            try
            {
                _localSettingsService.OnSettingChanged += OnSettingChanged;
                
                while (!_process!.HasExited)
                {
                    Thread.Sleep(1000 * 60);
                    if (_process.HasExited || 
                        (recordOnlyWhenForeground && (_process.IsMainWindowMinimized() || !_process.IsMainWindowActive())))
                        continue;
                    UiThreadInvokeHelper.Invoke(() =>
                    {
                        Galgame!.TotalPlayTime++;
                        CurrentPlayTime++;
                    });
                    var now = DateTime.Now.ToStringDefault();
                    if (!Galgame!.PlayedTime.TryAdd(now, 1))
                        Galgame.PlayedTime[now]++;
                }
            }
            finally
            {
                _localSettingsService.OnSettingChanged -= OnSettingChanged;
            }

            return;

            void OnSettingChanged(string key, object? value)
            {
                if(key != KeyValues.RecordOnlyWhenForeground || value is not bool b) return;
                recordOnlyWhenForeground = b;
            }
        });
    }

    public override string Title { get; } = "RecordPlayTimeTask_Title".GetLocalized();
}