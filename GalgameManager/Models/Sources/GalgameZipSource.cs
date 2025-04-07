using System.Text.RegularExpressions;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using SystemPath = System.IO.Path;


namespace GalgameManager.Models.Sources;


public class GalgameZipSource : GalgameSourceBase
{
    public static string FxRegex = @"(?<=\\)(?<name>[^\.\\]+)(?:(\.part1)?\.(zip|rar|7z))$";
    public override GalgameSourceType SourceType => GalgameSourceType.LocalZip;

    public GalgameZipSource(string path): base(path)
    {
    }

    public GalgameZipSource()
    {
        
    }

    public override bool IsInSource(string path)
    {
        return SystemPath.GetFullPath(path).StartsWith(SystemPath.GetFullPath(Path)) ;
    }

    public async override IAsyncEnumerable<(string?, string)> ScanAllGalgames()
    {
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        

        Queue<string> pathToCheck = new();
        pathToCheck.Enqueue(Path);
        while (pathToCheck.Count > 0)
        {
            var currentPath = pathToCheck.Dequeue();
            
            foreach (var f in Directory.GetFiles(currentPath))
            {
                Match m = Regex.Match(f, FxRegex);
                if (m.Success)
                {
                    yield return (new (f), $"successfully add {f}\n");
                }
                else
                {
                    yield return (null, $"{f} is not zip\n");
                }
            }
            
            // 删除深度检查，持续扫描所有子目录
            foreach (var subPath in Directory.GetDirectories(currentPath))
                pathToCheck.Enqueue(subPath);
        }
    }
}
