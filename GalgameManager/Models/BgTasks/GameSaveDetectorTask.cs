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

    private readonly List<FileSystemWatcher> _watchers = new();
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
                var combinedAppDataPath = Path.Combine(appDataPath, keyword);
                var combinedLocalAppDataPath = Path.Combine(localAppDataPath, keyword);
                var combinedDocumentsPath = Path.Combine(documentsPath, keyword);

                _candidatePaths.Add(combinedAppDataPath);
                _candidatePaths.Add(combinedLocalAppDataPath);
                _candidatePaths.Add(combinedDocumentsPath);
            }
        }
    }

    private List<string> ExtractGameKeywords()
    {
        var keywords = new List<string>();

        if (Galgame == null) return keywords;

        // 从游戏名称提取关键字
        if (Galgame.Name?.Value is { } gameNameValue)
        {
            keywords.Add(gameNameValue);
        }

        if (!string.IsNullOrEmpty(Galgame.ChineseName))
        {
            keywords.Add(Galgame.ChineseName);
        }

        if (Galgame.OriginalName?.Value is { } originalNameValue)
        {
            keywords.Add(originalNameValue);
        }

        // 从开发者名称提取关键字
        if (!string.IsNullOrEmpty(Galgame.Developer?.Value))
        {
            keywords.Add(Galgame.Developer.Value);
        }

        // 从分类中提取关键字
        if (Galgame.Categories != null)
        {
            foreach (var category in Galgame.Categories)
            {
                if (category.Name != null)
                {
                    keywords.Add(category.Name);
                }
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

        StartFileSystemMonitoring();

        // 监听最多1分钟
        var maxMonitorTime = TimeSpan.FromMinutes(1);
        var earlyStopThreshold = 3;
        var confidenceThreshold = 2;

        _monitorStartTime = DateTime.Now;
        while (IsMonitoring && (DateTime.Now - _monitorStartTime) < maxMonitorTime)
        {
            if (ShouldStopEarly(DetectedSavePaths, earlyStopThreshold, confidenceThreshold))
            {
                break;
            }
            await Task.Delay(500);
        }

        StopFileSystemMonitoring();

        // 设置存档目录
        var finalPaths = FilterDetectedPaths();
        if (finalPaths.Count > 0 && Galgame != null)
        {
            var saveDirectory = FindBestSaveDirectory(finalPaths);
            if (!string.IsNullOrEmpty(saveDirectory))
            {
                Galgame.DetectedSavePosition = saveDirectory;
            }
            else
            {
                var fallbackDirectory = Path.GetDirectoryName(finalPaths[0]);
                if (!string.IsNullOrEmpty(fallbackDirectory))
                {
                    Galgame.DetectedSavePosition = fallbackDirectory;
                }
            }
        }
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
                        EnableRaisingEvents = true,
                        // 减少缓冲区大小以提高响应速度
                        InternalBufferSize = 4096,
                        // 通知所有变化
                        NotifyFilter = NotifyFilters.FileName |
                                      NotifyFilters.LastWrite |
                                      NotifyFilters.Size |
                                      NotifyFilters.Attributes
                    };

                    // 监听所有相关事件
                    watcher.Created += OnFileSystemChanged;
                    watcher.Changed += OnFileSystemChanged;
                    watcher.Renamed += OnFileSystemChanged;

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
            // 处理重命名事件
            if (e is RenamedEventArgs renamedArgs)
            {
                if (IsPotentialSaveFile(renamedArgs.FullPath))
                {
                    lock (DetectedSavePaths)
                    {
                        if (!DetectedSavePaths.Contains(renamedArgs.FullPath))
                        {
                            DetectedSavePaths.Add(renamedArgs.FullPath);
                            SaveOperationCount++;
                        }
                    }
                }
            }
            else
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

    private bool ShouldStopEarly(List<string> detectedPaths, int fileThreshold, int confidenceThreshold)
    {
        if (detectedPaths.Count < fileThreshold) return false;

        // 统计每个目录的文件数量
        var directoryCounts = new Dictionary<string, int>();
        foreach (var path in detectedPaths)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                var normalizedDir = directory.ToLowerInvariant().TrimEnd('\\', '/');
                if (directoryCounts.ContainsKey(normalizedDir))
                    directoryCounts[normalizedDir]++;
                else
                    directoryCounts[normalizedDir] = 1;
            }
        }

        // 如果有目录包含足够的文件，可以早停
        return directoryCounts.Any(kvp => kvp.Value >= confidenceThreshold);
    }

    private List<string> FilterDetectedPaths()
    {
        var filteredPaths = new List<string>();

        lock (DetectedSavePaths)
        {
            // 去重和排序
            var uniquePaths = DetectedSavePaths.Distinct().ToList();

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
        }

        return filteredPaths;
    }

    private string FindBestSaveDirectory(List<string> paths)
    {
        if (paths.Count == 0) return string.Empty;

        // 计算每个目录的综合评分
        var directoryScores = new Dictionary<string, double>();

        foreach (var path in paths)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) continue;

            var normalizedDir = directory.ToLowerInvariant().TrimEnd('\\', '/');

            if (!directoryScores.ContainsKey(normalizedDir))
                directoryScores[normalizedDir] = 0;

            // 基础分数：文件数量
            directoryScores[normalizedDir] += 10;

            // 额外分数：文件质量评分
            directoryScores[normalizedDir] += CalculateSaveFileScore(path) * 0.1;

            // 额外分数：启发式匹配
            if (MatchesHeuristicKeywords(directory))
                directoryScores[normalizedDir] += 20;

            // 额外分数：路径深度（更具体的路径更可能是存档目录）
            var depth = directory.Count(c => c == '\\' || c == '/');
            if (depth >= 3 && depth <= 6)
                directoryScores[normalizedDir] += 15 * (depth / 6.0);
        }

        if (directoryScores.Count == 0) return string.Empty;

        // 按评分排序，获取最佳目录
        var bestDirectoryEntry = directoryScores.OrderByDescending(kvp => kvp.Value).First();
        var bestDirectory = bestDirectoryEntry.Key;
        var bestScore = bestDirectoryEntry.Value;

        // 如果最佳目录评分足够高，直接返回
        if (bestScore >= 25) // 降低阈值，更快锁定目录
        {
            return bestDirectory;
        }

        // 如果没有明显的最佳目录，返回评分最高的目录
        return bestDirectory;
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
}