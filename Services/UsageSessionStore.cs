using Microsoft.Data.Sqlite;
using MutsumiPet.Models;
using System.IO;

namespace MutsumiPet.Services;

public sealed class UsageSessionStore
{
    private readonly string _databasePath;

    /// <summary>
    /// 初始化使用会话存储并确保数据库结构存在。
    /// </summary>
    public UsageSessionStore(string databasePath)
    {
        _databasePath = databasePath;
        InitializeDatabase();
    }

    /// <summary>
    /// 获取数据库文件路径。
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// 写入一条已经结束的应用使用会话。
    /// </summary>
    public void InsertSession(UsageSessionRecord record)
    {
        if (record.TotalSeconds <= 0 || record.EndedAt <= record.StartedAt)
        {
            return;
        }

        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_sessions (
                process_name,
                app_category,
                window_title,
                start_time_ms,
                end_time_ms,
                active_seconds,
                idle_seconds,
                switch_count,
                created_at_ms
            ) VALUES (
                $process_name,
                $app_category,
                $window_title,
                $start_time_ms,
                $end_time_ms,
                $active_seconds,
                $idle_seconds,
                $switch_count,
                $created_at_ms
            );
            """;
        command.Parameters.AddWithValue("$process_name", record.ProcessName);
        command.Parameters.AddWithValue("$app_category", record.Category.ToString());
        command.Parameters.AddWithValue("$window_title", (object?)record.WindowTitle ?? DBNull.Value);
        command.Parameters.AddWithValue("$start_time_ms", record.StartedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$end_time_ms", record.EndedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$active_seconds", record.ActiveSeconds);
        command.Parameters.AddWithValue("$idle_seconds", record.IdleSeconds);
        command.Parameters.AddWithValue("$switch_count", record.SwitchCount);
        command.Parameters.AddWithValue("$created_at_ms", DateTimeOffset.Now.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 读取指定时间范围内的使用聚合摘要。
    /// </summary>
    public UsageSummary GetSummary(string title, DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    {
        using var connection = CreateConnection();
        connection.Open();
        var totals = ReadTotals(connection, rangeStart, rangeEnd);
        var topApps = ReadTopApps(connection, rangeStart, rangeEnd);

        return new UsageSummary
        {
            Title = title,
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            ActiveTime = TimeSpan.FromSeconds(totals.ActiveSeconds),
            IdleTime = TimeSpan.FromSeconds(totals.IdleSeconds),
            SessionCount = totals.SessionCount,
            SwitchCount = totals.SwitchCount,
            TopApps = topApps
        };
    }

    /// <summary>
    /// 初始化数据库表和索引。
    /// </summary>
    private void InitializeDatabase()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS app_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                process_name TEXT NOT NULL,
                app_category TEXT NOT NULL,
                window_title TEXT NULL,
                start_time_ms INTEGER NOT NULL,
                end_time_ms INTEGER NOT NULL,
                active_seconds INTEGER NOT NULL,
                idle_seconds INTEGER NOT NULL,
                switch_count INTEGER NOT NULL,
                created_at_ms INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_app_sessions_range
            ON app_sessions (start_time_ms, end_time_ms);

            CREATE INDEX IF NOT EXISTS idx_app_sessions_process
            ON app_sessions (process_name, app_category);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 创建 SQLite 连接。
    /// </summary>
    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        return new SqliteConnection(builder.ToString());
    }

    /// <summary>
    /// 读取指定时间范围内的总体统计。
    /// </summary>
    private static (int SessionCount, long ActiveSeconds, long IdleSeconds, int SwitchCount) ReadTotals(
        SqliteConnection connection,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*),
                COALESCE(SUM(active_seconds), 0),
                COALESCE(SUM(idle_seconds), 0),
                COALESCE(SUM(switch_count), 0)
            FROM app_sessions
            WHERE start_time_ms < $end_time_ms
              AND end_time_ms > $start_time_ms;
            """;
        command.Parameters.AddWithValue("$start_time_ms", rangeStart.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$end_time_ms", rangeEnd.ToUnixTimeMilliseconds());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return (0, 0, 0, 0);
        }

        return (
            reader.GetInt32(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt32(3));
    }

    /// <summary>
    /// 读取指定时间范围内活跃时间最高的应用。
    /// </summary>
    private static IReadOnlyList<UsageAppSummary> ReadTopApps(
        SqliteConnection connection,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                process_name,
                app_category,
                COALESCE(SUM(active_seconds), 0) AS active_total,
                COALESCE(SUM(idle_seconds), 0) AS idle_total,
                COUNT(*) AS session_count
            FROM app_sessions
            WHERE start_time_ms < $end_time_ms
              AND end_time_ms > $start_time_ms
            GROUP BY process_name, app_category
            ORDER BY active_total DESC, idle_total DESC
            LIMIT 5;
            """;
        command.Parameters.AddWithValue("$start_time_ms", rangeStart.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$end_time_ms", rangeEnd.ToUnixTimeMilliseconds());

        var results = new List<UsageAppSummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new UsageAppSummary
            {
                ProcessName = reader.GetString(0),
                Category = ParseCategory(reader.GetString(1)),
                ActiveTime = TimeSpan.FromSeconds(reader.GetInt64(2)),
                IdleTime = TimeSpan.FromSeconds(reader.GetInt64(3)),
                SessionCount = reader.GetInt32(4)
            });
        }

        return results;
    }

    /// <summary>
    /// 将数据库中的类别文本转换为枚举。
    /// </summary>
    private static AppCategory ParseCategory(string value)
    {
        return Enum.TryParse<AppCategory>(value, out var category) ? category : AppCategory.Other;
    }
}
