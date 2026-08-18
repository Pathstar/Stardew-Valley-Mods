using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using Microsoft.Xna.Framework;
// namespace StardewModdingAPI.Events;

// //
// // Summary:
// //     Events raised when something changes in the world.
// public interface IWorldEvents
// {

// 游戏时间限制、每天次数限制、最短拍照间隔、改动次数累计

// 触发：进入农场时触发 
// 农场变化触发
// 

// 每个触发器包含以上限制
// 再加一个总控 无次数控制

// 截图位置 Farm、姜岛
// 是否展示位置名称当warp

// 保存 地点->changedtime->个数


namespace AutoFarmScreenshot; 

public class ModEntry : Mod {
    internal Config config;

    public override void Entry(IModHelper helper) {
        helper.Events.Player.Warped += OnWarped;

        // 监听大型地形特征变化
        helper.Events.World.LargeTerrainFeatureListChanged += OnLargeTerrainFeatureListChanged;

        // 监听普通地形特征变化
        helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
        config = helper.ReadConfig<Config>();

        AutoPhotoManager auto = new(config);
    }

    /// <summary>
    /// 打印 LargeTerrainFeatureListChanged 的信息
    /// </summary>
    private void OnLargeTerrainFeatureListChanged(object sender, LargeTerrainFeatureListChangedEventArgs e)
    {
        Monitor.Log($"[LargeTerrainFeatureListChanged] Location: {e.Location.Name}, IsCurrentLocation: {e.IsCurrentLocation}", LogLevel.Debug);

        // 打印新增的特征
        foreach (var feature in e.Added)
        {
            Monitor.Log($"  Added LargeTerrainFeature: {feature.GetType().Name}", LogLevel.Debug);
        }

        // 打印移除的特征
        foreach (var feature in e.Removed)
        {
            Monitor.Log($"  Removed LargeTerrainFeature: {feature.GetType().Name}", LogLevel.Debug);
        }
    }

    /// <summary>
    /// 打印 TerrainFeatureListChanged 的信息
    /// </summary>
    private void OnTerrainFeatureListChanged(object sender, TerrainFeatureListChangedEventArgs e)
    {
        Monitor.Log($"[TerrainFeatureListChanged] Location: {e.Location.Name}, IsCurrentLocation: {e.IsCurrentLocation}", LogLevel.Debug);

        // 打印新增的特征
        foreach (var kvp in e.Added)
        {
            Vector2 pos = kvp.Key;
            TerrainFeature feature = kvp.Value;
            Monitor.Log($"  Added TerrainFeature at ({pos.X},{pos.Y}): {feature.GetType().Name}", LogLevel.Debug);
        }

        // 打印移除的特征
        foreach (var kvp in e.Removed)
        {
            Vector2 pos = kvp.Key;
            TerrainFeature feature = kvp.Value;
            Monitor.Log($"  Removed TerrainFeature at ({pos.X},{pos.Y}): {feature.GetType().Name}", LogLevel.Debug);
        }
    }

    private void OnWarped(object sender, WarpedEventArgs e) {
        try {
            Monitor.Log($"玩家: {e.Player.Name}", LogLevel.Debug);
            Monitor.Log($"旧位置: {e.OldLocation.Name}", LogLevel.Debug);
            Monitor.Log($"新位置: {e.NewLocation.Name}", LogLevel.Debug);
            Monitor.Log($"是否本地玩家: {e.IsLocalPlayer}", LogLevel.Debug);
            if (e.NewLocation.Name == "Farm") {
                TakeFarmPanorama();
            }
        }
        catch (Exception exception) {
            Monitor.Log("xd Failed to use e on OnWarped. " + exception);
        }
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e) {
        TakeFarmPanorama();
    }


    private void TakeFarmPanorama(float? in_scale=0.25f, string screenshot_name="") {
        string screenshot_name2 = screenshot_name;
        if (string.IsNullOrWhiteSpace(screenshot_name)) {
            DateTime utcNow = DateTime.UtcNow;
            screenshot_name2 = $"{ToSafeFileNameStrict(Game1.GetSaveGameName())}_{Game1.year}-{Game1.season}-{Game1.dayOfMonth}-{Game1.timeOfDay}" + 
                $"_{utcNow.Month}-{utcNow.Day}-{utcNow.Year}_{(int)utcNow.TimeOfDay.TotalMilliseconds}";
        }
        Game1.game1.takeMapScreenshot(in_scale, screenshot_name2, () => {
            
        });
        Monitor.Log($"农场截图完成 {screenshot_name2}.png", LogLevel.Debug);
        // // public string takeMapScreenshot(float? in_scale, string screenshot_name, Action onDone)
        // // 学习资料：
        // // https://stardewvalleywiki.com/Modding:Modder_Guide/APIs
        // // https://stardewvalleywiki.com/Modding:Modder_Guide/Game_API

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
        if (result.Length > 32)
            result = result[..32];

        return result;
    }

}

