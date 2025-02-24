using System.IO.Compression;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Models.BgTasks;

public class ExportTask (string targetPath) : BgTaskBase
{
    public string TargetPath = targetPath; // 导出zip文件的文件夹路径
    private readonly ILocalSettingsService _settingService = App.GetService<ILocalSettingsService>();
    private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
    private readonly IGalgameSourceCollectionService _sourceService = App.GetService<IGalgameSourceCollectionService>();
    private readonly ICategoryService _categoryService = App.GetService<ICategoryService>();
    private readonly IFileService _fileService = App.GetService<IFileService>();
    private readonly string _fileName = $"PotatoVN_{DateTime.Now:yy-MM-dd}.pvnExport.zip";
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;
    
    protected async override Task RunInternal()
    {
        if (!Utils.IsPathWritable(TargetPath))
            throw new UnauthorizedAccessException("ExportTask_PathNotWritable".GetLocalized(TargetPath));
        if (File.Exists(OutputFilePath))
            throw new InvalidOperationException("ExportTask_FileExist".GetLocalized(OutputFilePath));
        try
        {
            await (await _settingService.GetTmpExportFolder()).DeleteAsync(); // 防止某些情况下临时文件夹未被删除
            StorageFolder tmp = await _settingService.GetTmpExportFolder();
            
            // 导出游戏信息
            await _gameService.ExportAsync((msg, current, total) => { ChangeProgress(current, total, msg); });
            // 导出游戏库
            await _sourceService.ExportAsync((msg, current, total) => { ChangeProgress(current, total, msg); });
            // 导出分类（组）
            await _categoryService.ExportAsync((msg, current, total) => { ChangeProgress(current, total, msg); });
            // 导出数据状态
            LocalSettingStatus status = await _settingService
                .ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true) ?? new();
            status = status.Clone(); // 防止修改原数据
            status.SetToExport();
            await _settingService.AddToExportAsync(KeyValues.DataStatus, status);
            // 导出主页设置
            await _settingService.AddToExportDirectlyAsync(KeyValues.MultiStreamPageList);
            
            await _fileService.WaitForWriteFinishAsync();

            // 压缩
            ChangeProgress(0, 1, "ExportTask_Compressing".GetLocalized());
            ZipFile.CreateFromDirectory(tmp.Path, OutputFilePath, CompressionLevel.Optimal, false);
        }
        finally
        {
            await (await _settingService.GetTmpExportFolder()).DeleteAsync();
        }
        
        ChangeProgress(1, 1, "ExportTask_Success".GetLocalized(OutputFilePath));
    }

    public override string Title => "ExportTask_Title".GetLocalized();
    
    public override bool OnSearch(string key) => true;
    
    private string OutputFilePath => $"{TargetPath}\\{_fileName}";
}