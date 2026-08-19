using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoFarmScreenshot;

public class Config
{
    // =========================
    // 原始配置
    // =========================
    public bool enabled { get; set; }
    public float photoCooldown { get; set; }
    public bool printLocationNameWhenEntering { get; set; }
    public TriggerConfig locationEntryTrigger { get; set; }
    public TriggerConfig changeTrigger { get; set; }

    public Config()
    {
        enabled = true;
        photoCooldown = 30.0f;
        printLocationNameWhenEntering = false;
        locationEntryTrigger = new TriggerConfig{
            enabled = true,
            locations = "Farm, IslandWest",
            triggerCooldown = 60f,
            allowedTimeRanges = "",
            triggersBeforeTakingPhoto = 1,
            dailyPhotoLimit = 1
        };
        changeTrigger = new TriggerConfig{
            enabled = true,
            locations = "Farm, IslandWest",
            triggerCooldown = 1f,
            allowedTimeRanges = "",
            triggersBeforeTakingPhoto = 16,
            dailyPhotoLimit = 0
        };
    }
    // =========================
    // 运行时配置
    // =========================

    internal void Compile()
    {
        photoCooldown = Math.Max(0, photoCooldown);

        locationEntryTrigger.Compile();
        changeTrigger.Compile();
    }
}

public class TriggerConfig
{
    public bool enabled { get; set; } = true;

    public string locations { get; set; } = "";

    public float triggerCooldown { get; set; }

    public string allowedTimeRanges { get; set; } = "";

    public int triggersBeforeTakingPhoto { get; set; }

    public int dailyPhotoLimit { get; set; }

    // =========================
    // 编译后的运行时数据
    // =========================
    internal HashSet<string> locationSet { get; private set; }
        = new(StringComparer.Ordinal);

    internal TimeRange[] timeRanges { get; private set; }
        = Array.Empty<TimeRange>();

    internal List<Func<TriggerContext, bool>> filters { get; private set; }
        = new();


    internal void Compile()
    {
        triggerCooldown = Math.Max(0, triggerCooldown);
        triggersBeforeTakingPhoto = Math.Max(1, triggersBeforeTakingPhoto);
        dailyPhotoLimit = Math.Max(0, dailyPhotoLimit);

        locationSet = locations
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        timeRanges = ParseTimeRanges(allowedTimeRanges);

        filters.Clear();

        // 地点限制始终存在，而且最先执行。挪走到最先
        // filters.Add((ctx) => locationSet.Contains(ctx.State.location));

        // 下面三个限制，只有真正有限制时才加入策略链。
        if (dailyPhotoLimit > 0)
        {
            filters.Add((ctx) => ctx.State.photosToday < dailyPhotoLimit);
        }

        if (timeRanges.Length > 0)
        {
            filters.Add((ctx) => timeRanges.Any(x => x.Contains(ctx.TimeOfDay)));
        }

        if (triggerCooldown > 0)
        {
            filters.Add((ctx) => 
                !ctx.State.lastTriggerTime.HasValue ||
                ctx.GameTimeNow - ctx.State.lastTriggerTime.Value >= triggerCooldown);
        }
    }

    private static TimeRange[] ParseTimeRanges(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<TimeRange>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x =>
            {
                string[] parts = x.Split('-', 2);

                int start = ParseTime(parts[0]);
                int end = ParseTime(parts[1]);

                return new TimeRange(start, end);
            })
            .ToArray();
    }

    private static int ParseTime(string value)
    {
        value = value.Trim();

        int colon = value.IndexOf(':');

        if (colon < 0)
            return int.Parse(value);

        int hour = int.Parse(value[..colon]);
        int minute = int.Parse(value[(colon + 1)..]);

        return hour * 100 + minute;
    }
}

public readonly record struct TimeRange(int start, int end)
{
    internal bool Contains(int timeOfDay)
    {
        // 例如 06:00-22:00
        if (start <= end)
            return timeOfDay >= start && timeOfDay <= end;

        // 跨午夜，例如 22:00-02:00
        return timeOfDay >= start || timeOfDay <= end;
    }
}


public sealed class TriggerContext
{
    public TriggerRule Rule { get; }
    public TriggerState State { get; }
    public int TimeOfDay { get; }
    public double GameTimeNow { get; }
    public bool IsCurrentLocation { get; }
    
    public TriggerContext(TriggerRule rule, TriggerState state, int timeOfDay, double now, bool isCurrentLocation)
    {
        Rule = rule;
        State = state;
        TimeOfDay = timeOfDay;
        GameTimeNow = now;
        IsCurrentLocation = isCurrentLocation;
    }
}

// ========================================================================
// TriggerRule
// ========================================================================

public sealed class TriggerRule
{
    private readonly List<Func<TriggerContext, bool>> filters;

    internal readonly int triggersBeforeTakingPhoto;

    internal TriggerRule(TriggerConfig config)
    {
        filters = config.filters;
        triggersBeforeTakingPhoto = config.triggersBeforeTakingPhoto;
    }

    internal bool Accept(TriggerContext ctx)
    {
        foreach (var filter in filters)
        {
            if (!filter(ctx))
                return false;
        }
        return true;
    }


}

// ========================================================================
// TriggerState
// ========================================================================


public sealed class TriggerState
{
    internal readonly string location;

    // 第 5 步：累计多少个符合条件的触发。
    internal int triggerCount;

    // 第 4 步：这个地点上一次合格触发的时间。
    internal double? lastTriggerTime;

    // 第 6 步：正在等待拍照冷却 / 等待进入地点。
    internal bool pendingPhoto;

    // 每个游戏日实际拍了多少张。
    internal int photosToday;

    internal string type = "Unknown";

    internal TriggerState(string location)
    {
        this.location = location;
    }
}