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

        var expectedGalgameCount = UpgradeTestData.ReadExpectedGalgameCount(fixtureDir);
        var expectedCategoryGroupCount = UpgradeTestData.ReadExpectedCategoryGroupCount(fixtureDir);
        var expectedCategoryCount = UpgradeTestData.ReadExpectedCategoryCount(fixtureDir);
        var expectedSourceCount = UpgradeTestData.ReadExpectedGalgameSourceCount(fixtureDir);
        
        Assert.That(expectedGalgameCount, Is.GreaterThan(0), $"夹具版本 {version} 的游戏数应 > 0");

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

        // 验证 LiteDB 中的数据迁移
        var dbTimeout = TimeSpan.FromSeconds(10);
        
        // 验证游戏数量
        var actualGalgameCount = await ReadCollectionCountWithRetryAsync(dbPath, "galgame", dbTimeout);
        Assert.That(actualGalgameCount, Is.EqualTo(expectedGalgameCount), $"[{version}] LiteDB 游戏数应与旧数据一致");
        
        // 验证分类组数量（如果夹具中有分类数据）
        if (expectedCategoryGroupCount > 0)
        {
            // 真实 LiteDB 集合名以 snake_case 存储（见 CategoryService）
            var actualCategoryGroupCount = await ReadCollectionCountWithRetryAsync(dbPath, "category_group", dbTimeout);

            // 升级过程中可能会补齐内置分组（例如“游玩状态”），因此这里只保证“不少于旧数据”
            Assert.That(actualCategoryGroupCount, Is.GreaterThanOrEqualTo(expectedCategoryGroupCount),
                $"[{version}] LiteDB 分类组数不应少于旧数据（升级可能会新增内置分组）");

            var groupTypes = await ReadIntFieldSetWithRetryAsync(dbPath, "category_group", "Type", dbTimeout);
            Assert.That(groupTypes.Contains(1), Is.True, $"[{version}] 升级后应存在 Status 分类组(Type=1)");
        }
        
        // 验证分类数量
        if (expectedCategoryCount > 0)
        {
            var actualCategoryCount = await ReadCollectionCountWithRetryAsync(dbPath, "category", dbTimeout);

            // 升级过程中会补齐缺失的状态分类（固定 GUID），因此分类总数可能增加；这里只保证不丢失旧分类。
            Assert.That(actualCategoryCount, Is.GreaterThanOrEqualTo(expectedCategoryCount),
                $"[{version}] LiteDB 分类数不应少于旧数据（升级可能会新增缺失的内置分类）");

            var expectedCategories = UpgradeTestData.ReadExpectedCategories(fixtureDir);
            var (actualCategoryIds, actualCategoryNames) = await ReadCategoryIdAndNameSetsWithRetryAsync(dbPath, dbTimeout);
            foreach (var (id, name) in expectedCategories)
            {
                if (id is { } guid)
                    Assert.That(actualCategoryIds.Contains(guid), Is.True, $"[{version}] 升级后应保留分类 Id={guid}");
                else
                    Assert.That(actualCategoryNames.Contains(name), Is.True, $"[{version}] 升级后应保留分类 Name={name}");
            }
        }
        
        // 验证游戏库数量
        if (expectedSourceCount > 0)
        {
            // 真实 LiteDB 集合名（见 GalgameSourceCollectionService）
            var actualSourceCount = await ReadCollectionCountWithRetryAsync(dbPath, "source", dbTimeout);

            // 升级过程中可能会基于游戏路径补齐缺失的源（例如老版本记录的是游戏路径而不是库路径），因此这里也只保证不减少。
            Assert.That(actualSourceCount, Is.GreaterThanOrEqualTo(expectedSourceCount),
                $"[{version}] LiteDB 游戏库数不应少于旧数据（升级可能会补齐缺失的源）");
        }

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

    private static async Task<int> ReadCollectionCountWithRetryAsync(string dbPath, string collectionName, TimeSpan timeout)
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

                ILiteCollection<BsonDocument>? col = db.GetCollection<BsonDocument>(collectionName);
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

    private static async Task<HashSet<int>> ReadIntFieldSetWithRetryAsync(
        string dbPath,
        string collectionName,
        string fieldName,
        TimeSpan timeout)
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

                var col = db.GetCollection<BsonDocument>(collectionName);
                var set = new HashSet<int>();
                foreach (var doc in col.FindAll())
                {
                    if (doc.TryGetValue(fieldName, out var v) && v.IsInt32)
                        set.Add(v.AsInt32);
                }

                return set;
            }
            catch (Exception e)
            {
                last = e;
                await Task.Delay(200);
            }
        }

        throw new AssertionException($"在 {timeout.TotalSeconds}s 内无法读取集合 {collectionName} 字段 {fieldName}：{dbPath}，最后错误：{last}");
    }

    private static async Task<(HashSet<Guid> Ids, HashSet<string> Names)> ReadCategoryIdAndNameSetsWithRetryAsync(
        string dbPath,
        TimeSpan timeout)
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

                var col = db.GetCollection<BsonDocument>("category");
                var ids = new HashSet<Guid>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var doc in col.FindAll())
                {
                    if (doc.TryGetValue("_id", out var idVal) && idVal.IsGuid)
                        ids.Add(idVal.AsGuid);
                    if (doc.TryGetValue("Name", out var nameVal) && nameVal.IsString)
                        names.Add(nameVal.AsString);
                }

                return (ids, names);
            }
            catch (Exception e)
            {
                last = e;
                await Task.Delay(200);
            }
        }

        throw new AssertionException($"在 {timeout.TotalSeconds}s 内无法读取 LiteDB 分类集合：{dbPath}，最后错误：{last}");
    }

    private static string InferConfigurationFromTestOutput()
    {
        // 例如 ...\bin\Release\net8.0-... 或 ...\bin\Debug\...
        var baseDir = AppContext.BaseDirectory;
        return baseDir.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
    }
}
