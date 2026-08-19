using System;
using System.Collections.Generic;
// using System.Linq;
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

    private string saveGameName = "";

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
        // 当前只在本地玩家触发，但也判断一下
        if (!e.IsLocalPlayer)
            return;

        string location = e.NewLocation.Name;
        TriggerConfig trigger = config.locationEntryTrigger;
        // 挪到是否监听 √
        // if (!trigger.enabled)
        //     return;

        if (!trigger.locationSet.Contains(location))
            return;

        // event maybe √
        // ResetDailyStateIfNeeded();

        TriggerState state = GetState(locationEntryStates, location, "LocationEntry");
        // 进入地点时，优先兑现这个地点的待拍照请求。
        if (TryCompletePendingPhoto(state))
            return;
        TryHandleTrigger(
            new TriggerContext(
                locationEntryRule, 
                state, 
                Game1.timeOfDay, 
                GameTimeNow, 
                true
        ));
    }

    // ============================================================
    // OnTerrainFeatureListChanged
    // ============================================================
    // public void OnTerrainFeatureListChanged(object sender, TerrainFeatureListChangedEventArgs e)

    public void OnSceneChanged(string location, bool isCurrentLocation)
    {

        TriggerConfig trigger = config.changeTrigger;
        // 挪到是否监听 √
        // if (!trigger.enabled)
        //     return;

        if (!trigger.locationSet.Contains(location))
            return;

        // event maybe √
        // ResetDailyStateIfNeeded();

        TriggerState state = GetState(changeStates, location, "Change");
        TryHandleTrigger(
            new TriggerContext(
                changeRule, 
                state, 
                Game1.timeOfDay, 
                GameTimeNow, 
                isCurrentLocation
        ));
    }

    // ============================================================
    // 每个 UpdateTicked 检查一次待拍照状态
    // ============================================================

    // public void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
    // {
    //     ResetDailyStateIfNeeded();

    //     if (!IsPhotoCooldownFinished())
    //         return;

    //     TryCompletePendingPhoto(Game1.currentLocation?.Name);
    // }

    // ============================================================
    // 核心触发流程
    // ============================================================

    private void TryHandleTrigger(TriggerContext ctx)
    {
        //         TriggerRule rule,
        // TriggerState state,
        // int timeOfDay,
        // bool isCurrentLocation)
        // 已经进入“等待拍照”阶段：
        // 在拍照完成之前，新的触发一律拒绝，不累计次数。
        TriggerState state = ctx.State;
        if (state.pendingPhoto)
            return;

        TriggerRule rule = ctx.Rule;
        // 是否已经是溢满状态
        if (state.triggerCount >= rule.triggersBeforeTakingPhoto) {
            TryCompletePendingPhoto(state);
            return;
        }
        // 4.是否应当触发
        double gameTimeNow = ctx.GameTimeNow;
        //独立计算每个触发器的每个地点 1. 每日拍照限制 / 2. 游戏时间 / 3. 触发冷却
        if (!rule.Accept(ctx))
            return;

        // 5. 累计触发次数，记录上次触发时间
        state.triggerCount++;
        state.lastTriggerTime = gameTimeNow;

        if (state.triggerCount < rule.triggersBeforeTakingPhoto)
            return;

        // 进入拍照阶段
        state.pendingPhoto = true;

        // 6. 拍照阶段
        //
        // 当前就在这个地点：
        //   - 冷却结束 -> 立即拍
        //
        // 当前不在这个地点：
        //   - 不拍，等 OnWarped 进入该地点。
        TryCompletePendingPhoto(state);
    }

    // ============================================================
    // 待拍照处理
    // ============================================================

    private bool TryCompletePendingPhoto(TriggerState state)
    {
        // if (string.IsNullOrEmpty(location))
        //     return false;
        if (!state.pendingPhoto)
            return false;

        // 确认玩家目前是否在目标地点。
        if (Game1.currentLocation?.Name != state.location)
            return false;

        if (!IsPhotoCooldownFinished())
            return false;

        RequestPhoto(state);

        return true;
    }

    // private bool TryCompletePendingPhoto(string location)
    // {
    //     // if (string.IsNullOrEmpty(location))
    //     //     return false;

    //     if (!IsPhotoCooldownFinished())
    //         return false;

    //     // 每种触发器分别检查。
    //     if (TryCompletePendingPhoto(locationEntryStates, location))
    //         return true;

    //     return TryCompletePendingPhoto(changeStates, location);
    // }

    // private bool TryCompletePendingPhoto(
    //     Dictionary<string, TriggerState> states,
    //     string location)
    // {
    //     if (!states.TryGetValue(location, out TriggerState? state))
    //         return false;

    //     if (!state.pendingPhoto)
    //         return false;

    //     // 重新确认玩家目前确实在目标地点。
    //     if (Game1.currentLocation?.Name != location){
    //         ModEntry.Instance.Monitor.Log($"The player is no longer in the location when he is about to take a picture", LogLevel.Debug);
    //         return false;
    //     }

    //     // 真正进入拍照阶段。
    //     state.pendingPhoto = false;
    //     state.photosToday++;

    //     lastPhotoTime = GameTimeNow;

    //     RequestPhoto(state);

    //     return true;
    // }

    // ============================================================
    // 状态
    // ============================================================

    private static TriggerState GetState(Dictionary<string, TriggerState> states, string location, string type="")
    {
        if (!states.TryGetValue(location, out TriggerState state))
        {
            state = new TriggerState(location) { type = type };
            states.Add(location, state);
        }

        return state;
    }

    // ============================================================
    // 每日重置
    // ============================================================
    public void ResetDailyState()
    {
        foreach (TriggerState state in locationEntryStates.Values)
            state.photosToday = 0;

        foreach (TriggerState state in changeStates.Values)
            state.photosToday = 0;
    }

    public void ResetDailyStateIfNeeded()
    {
        string dayKey = GetDayKey();

        if (dayKey == currentDayKey)
            return;

        currentDayKey = dayKey;
        ResetDailyState();
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

        return GameTimeNow - lastPhotoTime.Value >= config.photoCooldown;
    }

    private static double GameTimeNow =>
        Game1.currentGameTime.TotalGameTime.TotalSeconds;

    // ============================================================
    // 实际拍照入口
    // ============================================================

    private void RequestPhoto(TriggerState state)
    {
        state.pendingPhoto = false;
        state.photosToday++;
        state.triggerCount = 0;
        lastPhotoTime = GameTimeNow;
        DateTime utcNow = DateTime.UtcNow;
        string location = state.location;
        if (location.Length > 32)
            location = location[..32];
        string screenshot_name = $"{GetSaveGameName()}_{utcNow.Month}-{utcNow.Day}-{utcNow.Year}" + 
                $"_{Game1.year}-{Game1.season}-{Game1.dayOfMonth}-{Game1.timeOfDay}_{location}_{state.type}_{(int)utcNow.TimeOfDay.TotalMilliseconds}";
        TakeFarmPanorama(screenshot_name);
    }

    
    private static void TakeFarmPanorama(float? in_scale=0.25f, string screenshot_name="") {
        string screenshot_name2 = screenshot_name;
        if (string.IsNullOrWhiteSpace(screenshot_name)) {
        DateTime utcNow = DateTime.UtcNow;
            string saveGameName = ToSafeFileNameStrict(Game1.GetSaveGameName());
            if (saveGameName.Length > 32)
                saveGameName = saveGameName[..32];
            screenshot_name2 = $"{saveGameName}_{utcNow.Month}-{utcNow.Day}-{utcNow.Year}" + 
                $"_{Game1.year}-{Game1.season}-{Game1.dayOfMonth}-{Game1.timeOfDay}_{(int)utcNow.TimeOfDay.TotalMilliseconds}";
        }
        Game1.game1.takeMapScreenshot(in_scale, screenshot_name2, () => {
            ModEntry.Instance.Monitor.Log($"[AutoFarmScreenshot] Triggered: {screenshot_name2}.png", LogLevel.Info);
        });
        // // public string takeMapScreenshot(float? in_scale, string screenshot_name, Action onDone)
        // // 学习资料：
        // // https://stardewvalleywiki.com/Modding:Modder_Guide/APIs
        // // https://stardewvalleywiki.com/Modding:Modder_Guide/Game_API

    }
    private string GetSaveGameName()
    {
        if (string.IsNullOrWhiteSpace(saveGameName)){
            saveGameName = ToSafeFileNameStrict(Game1.GetSaveGameName());
            if (saveGameName.Length > 32)
                saveGameName = saveGameName[..32];
        }
        return saveGameName;
    }
    
    private static void TakeFarmPanorama(string screenshot_name="")
    {
        TakeFarmPanorama(0.25f, screenshot_name);
    }

    public static string ToSafeFileNameStrict(string input) {
        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();

        var builder = new System.Text.StringBuilder(input.Length);
        foreach (char c in input)
        {
            // 去掉控制字符
            if (char.IsControl(c))
                continue;

            // 替换非法字符
            if (Array.IndexOf(invalidChars, c) >= 0)
                builder.Append('_');
            else
                builder.Append(c);
        }

        string result = builder.ToString().Trim();

        // Windows 文件名最长 255 字符
        // if (result.Length > 32)
        //     result = result[..32];

        return result;
    }

}

