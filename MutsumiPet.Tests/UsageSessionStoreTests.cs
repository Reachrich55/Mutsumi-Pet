using MutsumiPet.Models;
using MutsumiPet.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MutsumiPet.Tests;

public sealed class UsageSessionStoreTests
{
    /// <summary>
    /// 验证会话写入后能按时间范围读取聚合摘要。
    /// </summary>
    [Fact]
    public void GetSummary_InsertSession_ReturnsAggregateData()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"mutsumi-test-{Guid.NewGuid():N}.db");
        try
        {
            var store = new UsageSessionStore(databasePath);
            var now = DateTimeOffset.Now;
            store.InsertSession(new UsageSessionRecord
            {
                ProcessName = "Code",
                Category = AppCategory.Focus,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now,
                ActiveSeconds = 240,
                IdleSeconds = 60,
                SwitchCount = 1
            });

            var summary = store.GetSummary("测试摘要", now.AddHours(-1), now.AddHours(1));

            Assert.True(summary.HasData);
            Assert.Equal(1, summary.SessionCount);
            Assert.Equal(TimeSpan.FromSeconds(240), summary.ActiveTime);
            Assert.Equal(TimeSpan.FromSeconds(60), summary.IdleTime);
            Assert.Equal(1, summary.SwitchCount);
            Assert.Single(summary.TopApps);
            Assert.Equal("Code", summary.TopApps[0].ProcessName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
