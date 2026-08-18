using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoFarmScreenshot;

public class Config
{
    // =========================
    // 原始配置
    // =========================

    public int photoCooldown { get; set; } = 30;

    public TriggerConfig locationEntryTrigger { get; set; } = new();

    public TriggerConfig changeTrigger { get; set; } = new();

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
    public bool enabled { get; set; }

    public string locations { get; set; } = "";

    public int triggerCooldown { get; set; }

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

    internal List<Func<TriggerState, int, double, bool>> filters { get; private set; }
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

        // 地点限制始终存在，而且最先执行。
        filters.Add((state, _, _) =>
            locationSet.Contains(state.location));

        // 下面三个限制，只有真正有限制时才加入策略链。
        if (dailyPhotoLimit > 0)
        {
            filters.Add((state, _, _) =>
                state.photosToday < dailyPhotoLimit);
        }

        if (timeRanges.Length > 0)
        {
            filters.Add((_, timeOfDay, _) =>
                timeRanges.Any(x => x.Contains(timeOfDay)));
        }

        if (triggerCooldown > 0)
        {
            filters.Add((state, _, now) =>
                !state.lastTriggerTime.HasValue ||
                now - state.lastTriggerTime.Value >= triggerCooldown);
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

internal readonly record struct TimeRange(int start, int end)
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