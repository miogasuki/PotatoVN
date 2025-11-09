using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using GalgameManager.Helpers;

namespace GalgameManager.Models.BgTasks;

public class GameSaveDetectorTask : BgTaskBase
{
    public Galgame? Galgame { get; set; }
    public List<string> DetectedSavePaths { get; set; } = new();
    public List<string> MonitoredPaths { get; set; } = new();
    public bool IsMonitoring { get; set; }
    public int SaveOperationCount { get; set; }

    private List<FileSystemWatcher> _watchers = new();
    private readonly List<string> _candidatePaths = new();
    private DateTime _monitorStartTime;

    public GameSaveDetectorTask() { } // For serialization

    public GameSaveDetectorTask(Galgame game)
    {
        Galgame = game;
        InitializeCandidatePaths();
    }

    private void InitializeCandidatePaths()
    {
        if (Galgame == null) return;

        // 游戏安装目录（如果是本地的）
        if (!string.IsNullOrEmpty(Galgame.LocalPath))
        {
            _candidatePaths.Add(Galgame.LocalPath);
        }

        // 用户文档目录
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _candidatePaths.Add(documentsPath);
        _candidatePaths.Add(Path.Combine(documentsPath, "My Games"));
        _candidatePaths.Add(Path.Combine(documentsPath, "Saved Games"));

        // AppData 目录
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localLowPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("Local", "LocalLow");

        _candidatePaths.Add(appDataPath);
        _candidatePaths.Add(localAppDataPath);
        _candidatePaths.Add(localLowPath);

        // 基于游戏名称和开发者的启发式路径
        var gameKeywords = ExtractGameKeywords();
        foreach (var keyword in gameKeywords)
        {
            if (!string.IsNullOrEmpty(keyword))
            {
                _candidatePaths.Add(Path.Combine(appDataPath, keyword));
                _candidatePaths.Add(Path.Combine(localAppDataPath, keyword));
                _candidatePaths.Add(Path.Combine(documentsPath, keyword));
            }
        }
    }

    private List<string> ExtractGameKeywords()
    {
        var keywords = new List<string>();

        if (Galgame == null) return keywords;

        // 从游戏名称提取关键字
        if (Galgame.Name?.Value != null)
        {
            keywords.Add(Galgame.Name.Value);
        }

        if (!string.IsNullOrEmpty(Galgame.ChineseName))
        {
            keywords.Add(Galgame.ChineseName);
        }

        if (!string.IsNullOrEmpty(Galgame.OriginalName?.Value))
        {
            keywords.Add(Galgame.OriginalName.Value);
        }

        // 从开发者名称提取关键字
        if (!string.IsNullOrEmpty(Galgame.Developer))
        {
            keywords.Add(Galgame.Developer);
        }

        // 从分类中提取关键字
        if (Galgame.Categories != null)
        {
            foreach (var category in Galgame.Categories)
            {
                keywords.Add(category.Name);
            }
        }

        return keywords;
    }

    protected override Task RecoverFromJsonInternal()
    {
        // 重新初始化候选路径
        InitializeCandidatePaths();
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        if (Galgame == null) return;

        ChangeProgress(0, 1, "GameSaveDetectorTask_Starting".GetLocalized(Galgame.Name.Value!));

        // 启动文件系统监听
        StartFileSystemMonitoring();

        // 指导用户进行存档操作
        ChangeProgress(0, 1, "GameSaveDetectorTask_GuideUser".GetLocalized());
        await Task.Delay(2000); // 给用户时间阅读提示

        // 监听指定时间或直到检测到足够的存档操作
        _monitorStartTime = DateTime.Now;
        var maxMonitorTime = TimeSpan.FromMinutes(5); // 最多监听5分钟

        while (IsMonitoring && (DateTime.Now - _monitorStartTime) < maxMonitorTime)
        {
            ChangeProgress((int)(DateTime.Now - _monitorStartTime).TotalSeconds,
                          (int)maxMonitorTime.TotalSeconds,
                          $"GameSaveDetectorTask_Monitoring".GetLocalized() + $" ({DetectedSavePaths.Count} paths found)");

            await Task.Delay(1000);
        }

        // 停止监听
        StopFileSystemMonitoring();

        // 过滤和验证检测到的路径
        var finalPaths = FilterDetectedPaths();

        if (finalPaths.Count > 0 && Galgame != null)
        {
            var saveDirectory = FindBestSaveDirectory(finalPaths);

            if (!string.IsNullOrEmpty(saveDirectory))
            {
                Galgame.SavePosition = saveDirectory;

                // 开发状态：使用 ChangeProgress 显示所有可能的存档目录
                #if DEBUG
                try
                {
                    // 获取所有候选文件的目录并去重
                    var allDirectories = finalPaths.Select(Path.GetDirectoryName)
                                                  .Where(dir => !string.IsNullOrEmpty(dir))
                                                  .Distinct()
                                                  .ToList();

                    var dirList = allDirectories.Select((dir, index) => $"{index + 1}.{dir}").ToList();
                    var finalMsg = $"All save dirs ({allDirectories.Count}): {string.Join(" | ", dirList)} | Final: {saveDirectory}";

                    ChangeProgress(1, 1, finalMsg, false);

                    // 也输出最终结果单独一行，方便复制
                    ChangeProgress(1, 1, $"FINAL SAVE DIRECTORY: {saveDirectory}", false);
                }
                catch
                {
                    // 备用方案
                    ChangeProgress(1, 1, $"Save Directory: {saveDirectory}", false);
                }
                #endif
            }
            else
            {
                // 如果没有找到一致的父级目录，使用最高评分文件的目录
                var bestSavePath = finalPaths[0];
                var fallbackDirectory = Path.GetDirectoryName(bestSavePath);
                if (!string.IsNullOrEmpty(fallbackDirectory))
                {
                    Galgame.SavePosition = fallbackDirectory;

                    #if DEBUG
                try
                {
                    ChangeProgress(1, 1, $"FALLBACK SAVE DIRECTORY: {fallbackDirectory}", false);
                }
                catch
                {
                    // 备用方案
                    ChangeProgress(1, 1, $"Save Directory: {fallbackDirectory}", false);
                }
                #endif
                }
            }
        }

        ChangeProgress(1, 1, $"GameSaveDetectorTask_Completed".GetLocalized() + $" ({finalPaths.Count} save paths detected)", false);
    }

    private void StartFileSystemMonitoring()
    {
        IsMonitoring = true;

        foreach (var path in _candidatePaths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    var watcher = new FileSystemWatcher(path)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };

                    // 只监听创建和修改事件
                    watcher.Created += OnFileSystemChanged;
                    watcher.Changed += OnFileSystemChanged;

                    _watchers.Add(watcher);
                    MonitoredPaths.Add(path);
                }
                catch (Exception ex)
                {
                    // 记录错误但继续处理其他路径
                    Debug.WriteLine($"Failed to watch path {path}: {ex.Message}");
                }
            }
        }
    }

    private void StopFileSystemMonitoring()
    {
        IsMonitoring = false;

        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing watcher: {ex.Message}");
            }
        }

        _watchers.Clear();
        MonitoredPaths.Clear();
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsMonitoring) return;

        try
        {
            // 检查文件是否可能是存档文件
            if (IsPotentialSaveFile(e.FullPath))
            {
                lock (DetectedSavePaths)
                {
                    if (!DetectedSavePaths.Contains(e.FullPath))
                    {
                        DetectedSavePaths.Add(e.FullPath);
                        SaveOperationCount++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error handling file system change: {ex.Message}");
        }
    }

    private bool IsPotentialSaveFile(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            // 检查文件扩展名（常见的存档文件扩展名）
            var saveExtensions = new[] { ".sav", ".dat", ".save", ".sfs", ".rpgsave", ".rvdata", ".rvdata2" };
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (saveExtensions.Contains(extension))
                return true;

            // 检查文件名是否包含存档相关关键词
            var saveKeywords = new[] { "save", "sav", "slot", "data", "record", "progress", "file" };
            var fileNameLower = fileName.ToLowerInvariant();

            if (saveKeywords.Any(keyword => fileNameLower.Contains(keyword)))
                return true;

            // 使用启发式关键字匹配
            return MatchesHeuristicKeywords(directory);
        }
        catch
        {
            return false;
        }
    }

    private bool MatchesHeuristicKeywords(string path)
    {
        if (Galgame == null) return false;

        var gameKeywords = ExtractGameKeywords();
        var pathLower = path.ToLowerInvariant();

        foreach (var keyword in gameKeywords)
        {
            if (!string.IsNullOrEmpty(keyword) && pathLower.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private List<string> FilterDetectedPaths()
    {
        var filteredPaths = new List<string>();

        lock (DetectedSavePaths)
        {
            // 去重和排序
            var uniquePaths = DetectedSavePaths.Distinct().ToList();

            // 开发状态：打印所有检测到的文件
            #if DEBUG
            try
            {
                Console.WriteLine($"=== Detected {uniquePaths.Count} potential save files ===");
                foreach (var path in uniquePaths)
                {
                    Console.WriteLine($"Detected: {path}");
                }
            }
            catch (Exception ex)
            {
                // 尝试使用 Trace.WriteLine 作为备选
                System.Diagnostics.Trace.WriteLine($"[SAVE DETECTOR ERROR] Failed to write debug output: {ex.Message}");
                System.Diagnostics.Trace.WriteLine($"[SAVE DETECTOR INFO] Found {uniquePaths.Count} potential save files");
            }
            #endif

            // 按文件大小和修改时间排序（最新的和较大的文件更可能是存档）
            var fileInfoList = uniquePaths.Select(path => new
            {
                Path = path,
                Info = new FileInfo(path),
                Score = CalculateSaveFileScore(path)
            })
            .OrderByDescending(x => x.Score)
            .Take(10) // 只返回前10个最可能的路径
            .ToList();

            filteredPaths.AddRange(fileInfoList.Select(x => x.Path));

            // 开发状态：使用 ChangeProgress 显示候选文件信息
            #if DEBUG
            try
            {
                // 将文件信息转换为进度消息
                var fileInfo = fileInfoList.Take(5).Select((item, index) =>
                    $"{index + 1}. Score:{item.Score} Dir:{System.IO.Path.GetDirectoryName(item.Path)}").ToList();

                if (fileInfo.Count > 0)
                {
                    var message = $"Found {fileInfoList.Count} candidates: " + string.Join(" | ", fileInfo);
                    ChangeProgress(0, 1, message, false);
                }
            }
            catch { }
            #endif
        }

        return filteredPaths;
    }

    private string FindBestSaveDirectory(List<string> paths)
    {
        if (paths.Count == 0) return string.Empty;

        // 获取所有路径的父级目录
        var parentDirectories = paths.Select(Path.GetDirectoryName).Where(dir => !string.IsNullOrEmpty(dir)).ToList();

        if (parentDirectories.Count == 0) return string.Empty;

        // 统计每个父级目录出现的次数
        var directoryCount = new Dictionary<string, int>();
        foreach (var dir in parentDirectories)
        {
            var normalizedDir = dir!.ToLowerInvariant().TrimEnd('\\', '/');
            if (directoryCount.ContainsKey(normalizedDir))
                directoryCount[normalizedDir]++;
            else
                directoryCount[normalizedDir] = 1;
        }

        // 找出出现次数最多的父级目录
        var mostCommonDirectory = directoryCount.OrderByDescending(kvp => kvp.Value).First();
        var maxCount = mostCommonDirectory.Value;
        var bestDirectory = mostCommonDirectory.Key;

        // 开发状态：使用 ChangeProgress 显示目录分析
        #if DEBUG
        try
        {
            var sortedDirs = directoryCount.OrderByDescending(kvp => kvp.Value).Take(3).ToList();
            var dirInfo = sortedDirs.Select((kvp, index) =>
                $"{index + 1}.({kvp.Value}files){kvp.Key}").ToList();

            var analysisMsg = $"Directory analysis: {string.Join(" | ", dirInfo)} | Most common: {bestDirectory} ({maxCount}/{paths.Count})";
            ChangeProgress(0, 1, analysisMsg, false);
        }
        catch { }
        #endif

        // 如果超过一半的文件都在同一个目录中，认为这是存档目录
        if (maxCount >= Math.Ceiling(paths.Count * 0.5))
        {
            return bestDirectory;
        }
        return string.Empty;
    }

    private double CalculateSaveFileScore(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var score = 0.0;

            // 文件大小适中的文件更可能是存档（1KB - 10MB）
            if (fileInfo.Length > 1024 && fileInfo.Length < 10 * 1024 * 1024)
                score += 30;
            else if (fileInfo.Length > 100 && fileInfo.Length < 100 * 1024 * 1024)
                score += 20;

            // 最近修改的文件更可能是存档
            var timeDiff = DateTime.Now - fileInfo.LastWriteTime;
            if (timeDiff.TotalMinutes < 10)
                score += 40;
            else if (timeDiff.TotalHours < 1)
                score += 30;
            else if (timeDiff.TotalDays < 1)
                score += 20;

            // 路径匹配启发式关键字
            if (MatchesHeuristicKeywords(filePath))
                score += 30;

            return score;
        }
        catch
        {
            return 0;
        }
    }

    public override string Title => "GameSaveDetectorTask_Title".GetLocalized();

    // 提供获取用户指导信息的方法
    public string GetUserGuidance()
    {
        return "GameSaveDetectorTask_UserGuidance".GetLocalized() +
               "\n1. " + "GameSaveDetectorTask_Guidance1".GetLocalized() +
               "\n2. " + "GameSaveDetectorTask_Guidance2".GetLocalized() +
               "\n3. " + "GameSaveDetectorTask_Guidance3".GetLocalized();
    }
}