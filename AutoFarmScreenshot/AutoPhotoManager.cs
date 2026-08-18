using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace AutoFarmScreenshot;

public class AutoPhotoManager
{
    private readonly Config config;

    private readonly TriggerRule locationEntryRule;
    private readonly TriggerRule changeRule;

    // 每个「触发类型 + 地点」独立维护状态。
    private readonly Dictionary<string, TriggerState> locationEntryStates = new();
    private readonly Dictionary<string, TriggerState> changeStates = new();

    // 所有地点、所有触发类型共享的实际拍照冷却。
    private double? lastPhotoTime;

    // $"{Game1.year}-{Game1.currentSeason}-{Game1.dayOfMonth}";
    private string currentDayKey = "";

    public AutoPhotoManager(Config config)
    {
        this.config = config;
        config.Compile();
        locationEntryRule = new TriggerRule(config.locationEntryTrigger);
        changeRule = new TriggerRule(config.changeTrigger);

        currentDayKey = GetDayKey();
    }

    // ============================================================
    // OnWarped
    // ============================================================

    public void OnWarped(object sender, WarpedEventArgs e)
    {
        string location = e.NewLocation.Name;

        // todo event maybe
        ResetDailyStateIfNeeded();

        // 进入地点时，优先兑现这个地点的待拍照请求。
        if (TryCompletePendingPhoto(location))
            return;

        // todo 挪到监听
        if (!config.locationEntryTrigger.enabled)
            return;

        TriggerState state = GetState(locationEntryStates, location);

        TryHandleTrigger(
            rule: locationEntryRule,
            state: state,
            timeOfDay: Game1.timeOfDay,
            isCurrentLocation: true
        );
    }

    // ============================================================
    // OnTerrainFeatureListChanged
    // ============================================================

    public void OnTerrainFeatureListChanged(
        object sender,
        TerrainFeatureListChangedEventArgs e)
    {
        ResetDailyStateIfNeeded();

        if (!config.changeTrigger.enabled)
            return;

        TriggerState state =
            GetState(changeStates, e.Location.Name);

        TryHandleTrigger(
            rule: changeRule,
            state: state,
            timeOfDay: Game1.timeOfDay,
            isCurrentLocation: e.IsCurrentLocation);
    }

    // ============================================================
    // 每个 UpdateTicked 检查一次待拍照状态
    // ============================================================

    public void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
    {
        ResetDailyStateIfNeeded();

        if (!IsPhotoCooldownFinished())
            return;

        TryCompletePendingPhoto(Game1.currentLocation?.Name);
    }

    // ============================================================
    // 核心触发流程
    // ============================================================

    private void TryHandleTrigger(
        TriggerRule rule,
        TriggerState state,
        int timeOfDay,
        bool isCurrentLocation)
    {
        // 已经进入“等待拍照”阶段：
        // 在拍照完成之前，新的触发一律拒绝，不累计次数。
        if (state.pendingPhoto)
            return;

        double now = Now;

        // 1. 地点 / 2. 每日拍照限制 / 3. 时间 / 4. 触发冷却
        if (!rule.Accept(state, timeOfDay, now))
            return;

        // 5. 累计触发次数
        state.triggerCount++;
        state.lastTriggerTime = now;

        if (state.triggerCount < rule.triggersBeforeTakingPhoto)
            return;

        // 达到次数后立即清零。
        // 后续若拍照正在等待，则由于 pendingPhoto=true，
        // 新触发不会继续累计。
        state.triggerCount = 0;
        state.pendingPhoto = true;

        // 6. 拍照阶段
        //
        // 当前就在这个地点：
        //   - 冷却结束 -> 立即拍
        //   - 冷却未结束 -> 等 UpdateTicked
        //
        // 当前不在这个地点：
        //   - 不拍，等 OnWarped 进入该地点。
        if (isCurrentLocation)
            TryCompletePendingPhoto(state.location);
    }

    // ============================================================
    // 待拍照处理
    // ============================================================

    private bool TryCompletePendingPhoto(string location)
    {
        if (string.IsNullOrEmpty(location))
            return false;

        if (!IsPhotoCooldownFinished())
            return false;

        // 每种触发器分别检查。
        if (TryCompletePendingPhoto(locationEntryStates, location))
            return true;

        return TryCompletePendingPhoto(changeStates, location);
    }

    private bool TryCompletePendingPhoto(
        Dictionary<string, TriggerState> states,
        string location)
    {
        if (!states.TryGetValue(location, out TriggerState? state))
            return false;

        if (!state.pendingPhoto)
            return false;

        // 重新确认玩家目前确实在目标地点。
        if (Game1.currentLocation?.Name != location)
            return false;

        // 真正进入拍照阶段。
        state.pendingPhoto = false;
        state.photosToday++;

        lastPhotoTime = Now;

        RequestPhoto(state);

        return true;
    }

    // ============================================================
    // 状态
    // ============================================================

    private TriggerState GetState(Dictionary<string, TriggerState> states, string location)
    {
        if (!states.TryGetValue(location, out TriggerState? state))
        {
            state = new TriggerState(location);
            states.Add(location, state);
        }

        return state;
    }

    // ============================================================
    // 每日重置
    // ============================================================

    private void ResetDailyStateIfNeeded()
    {
        string dayKey = GetDayKey();

        if (dayKey == currentDayKey)
            return;

        currentDayKey = dayKey;

        foreach (TriggerState state in locationEntryStates.Values)
            state.photosToday = 0;

        foreach (TriggerState state in changeStates.Values)
            state.photosToday = 0;
    }

    private static string GetDayKey()
    {
        return $"{Game1.year}-{Game1.currentSeason}-{Game1.dayOfMonth}";
    }

    // ============================================================
    // 拍照冷却
    // ============================================================

    private bool IsPhotoCooldownFinished()
    {
        if (config.photoCooldown <= 0)
            return true;

        if (!lastPhotoTime.HasValue)
            return true;

        return Now - lastPhotoTime.Value >= config.photoCooldown;
    }

    private static double Now =>
        Game1.currentGameTime.TotalGameTime.TotalSeconds;

    // ============================================================
    // 实际拍照入口
    // ============================================================

    private void RequestPhoto(TriggerState state)
    {
        // 这里只保留拍照阶段，不实现具体拍照逻辑。
        //
        // 在这里调用你的截图 / SMAPI API / 自定义拍照代码即可。
        //
        // state.location
        // state.photosToday
        //
        // 均已经更新完成。
    }
}

// ========================================================================
// TriggerRule
// ========================================================================

internal sealed class TriggerRule
{
    private readonly List<Func<TriggerState, int, double, bool>> filters;

    internal readonly int triggersBeforeTakingPhoto;

    internal TriggerRule(TriggerConfig config)
    {
        filters = config.filters;
        triggersBeforeTakingPhoto = config.triggersBeforeTakingPhoto;
    }

    internal bool Accept(
        TriggerState state,
        int timeOfDay,
        double now)
    {
        foreach (var filter in filters)
        {
            if (!filter(state, timeOfDay, now))
                return false;
        }

        return true;
    }
}

// ========================================================================
// TriggerState
// ========================================================================

internal sealed class TriggerState
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

    internal TriggerState(string location)
    {
        this.location = location;
    }
}