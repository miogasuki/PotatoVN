using System.Diagnostics;
using LiteDB;

namespace GalgameManager.Test.Upgrade;

[TestFixture]
[NonParallelizable]
[Category("E2E")]
public sealed class UnpackagedAppStartupHealthCheckTest
{
    [TestCase("1.7.2")]
    [TestCase("1.8.0")]
    public async Task UnpackagedApp_ShouldStartAndMigrateOldData(string version)
    {
        var workspaceRoot = UpgradeTestData.WorkspaceRoot;
        var configuration = InferConfigurationFromTestOutput();

        var exePath = Path.Combine(
            workspaceRoot,
            "GalgameManager",
            "bin",
            "x64",
            configuration,
            "net8.0-windows10.0.22621.0",
            "win-x64",
            "GalgameManager.exe");

        Assert.That(File.Exists(exePath), Is.True, $"未找到客户端 exe: {exePath}");

        var fixtureDir = UpgradeTestData.GetFixtureDir(version);
        Assert.That(Directory.Exists(fixtureDir), Is.True, $"未找到旧数据夹具目录: {fixtureDir}");

        var expectedCount = UpgradeTestData.ReadExpectedGalgameCount(fixtureDir);
        Assert.That(expectedCount, Is.GreaterThan(0), $"夹具版本 {version} 的游戏数应 > 0");

        var runRoot = Path.Combine(Path.GetTempPath(), "PotatoVN.E2E", version, Guid.NewGuid().ToString("N"));
        var localDataPath = Path.Combine(runRoot, "LocalData");
        var tempPath = Path.Combine(runRoot, "Temp");
        var dbPath = Path.Combine(localDataPath, "pvn_data.db");

        Directory.CreateDirectory(localDataPath);
        Directory.CreateDirectory(tempPath);

        UpgradeTestData.CopyJsonOnly(fixtureDir, localDataPath);

        ProcessStartInfo psi = new()
        {
            FileName = exePath,
            Arguments = "--healthcheck",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            Environment =
            {
                ["POTATOVN_LOCALDATA_PATH"] = localDataPath,
                ["POTATOVN_TEMP_PATH"] = tempPath,
                ["POTATOVN_PORTABLE"] = "0",
            },
        };

        using Process? process = Process.Start(psi);
        Assert.That(process, Is.Not.Null, "启动进程失败");

        TimeSpan timeout = TimeSpan.FromSeconds(45);
        var exited = await WaitForExitAsync(process!, timeout);
        if (!exited)
        {
            try
            {
                process!.Kill(entireProcessTree: true);
            }
            catch
            {
                //ignore
            }

            Assert.Fail($"客户端在 {timeout.TotalSeconds}s 内未退出（healthcheck 模式应快速退出）");
        }

        Assert.That(process!.ExitCode, Is.EqualTo(0), $"healthcheck 模式应正常退出，ExitCode={process.ExitCode}");

        Assert.That(File.Exists(dbPath), Is.True, $"未生成 LiteDB 数据库文件: {dbPath}");

        var actualCount = await ReadGalgameCountWithRetryAsync(dbPath, timeout: TimeSpan.FromSeconds(10));
        Assert.That(actualCount, Is.EqualTo(expectedCount), $"[{version}] LiteDB 游戏数应与旧数据一致");

        try
        {
            if (Directory.Exists(runRoot)) Directory.Delete(runRoot, recursive: true);
        }
        catch
        {
            // ignore
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<int> ReadGalgameCountWithRetryAsync(string dbPath, TimeSpan timeout)
    {
        Stopwatch start = Stopwatch.StartNew();
        Exception? last = null;
        while (start.Elapsed < timeout)
        {
            try
            {
                using LiteDatabase db = new(new ConnectionString
                {
                    Filename = dbPath,
                    ReadOnly = true,
                });

                ILiteCollection<BsonDocument>? col = db.GetCollection<BsonDocument>("galgame");
                return col.Count();
            }
            catch (Exception e)
            {
                last = e;
                await Task.Delay(200);
            }
        }

        throw new AssertionException($"在 {timeout.TotalSeconds}s 内无法打开 LiteDB：{dbPath}，最后错误：{last}");
    }

    private static string InferConfigurationFromTestOutput()
    {
        // 例如 ...\bin\Release\net8.0-... 或 ...\bin\Debug\...
        var baseDir = AppContext.BaseDirectory;
        return baseDir.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
    }
}
