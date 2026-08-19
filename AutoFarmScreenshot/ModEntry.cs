// using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
// using StardewValley;
// using StardewValley.TerrainFeatures;
// using Microsoft.Xna.Framework;
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
    public static ModEntry Instance { get; private set; }
    public static AutoPhotoManager autoPhotoManager { get; private set; }
    public override void Entry(IModHelper helper) {
        Instance = this;
        config = helper.ReadConfig<Config>();
        if (!config.enabled)
            return;

        if (config.locationEntryTrigger.enabled)
            helper.Events.Player.Warped += OnWarped;

        if (config.printLocationNameWhenEntering)
            helper.Events.Player.Warped += OnWarpedPrint;
            
        // 监听地形特征变化
        if (config.changeTrigger.enabled)
        {
            helper.Events.World.LargeTerrainFeatureListChanged += OnLargeTerrainFeatureListChanged;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
            helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
        }

        helper.Events.GameLoop.DayStarted += OnDayStarted;
        autoPhotoManager = new AutoPhotoManager(config);
    }

    private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        // Monitor.Log($"[ObjectListChanged] Location: {e.Location.Name}, IsCurrentLocation: {e.IsCurrentLocation}", LogLevel.Debug);
        // foreach (var feature in e.Added){
        //     Monitor.Log($"  Added Object: {feature.GetType().Name}", LogLevel.Debug);
        // }
        // foreach (var feature in e.Removed){
        //     Monitor.Log($"  Removed Object: {feature.GetType().Name}", LogLevel.Debug);
        // }
        autoPhotoManager.OnSceneChanged(e.Location.Name, e.IsCurrentLocation);
    }


    /// <summary>
    /// 打印 LargeTerrainFeatureListChanged 的信息
    /// </summary>
    private void OnLargeTerrainFeatureListChanged(object sender, LargeTerrainFeatureListChangedEventArgs e)
    {
        autoPhotoManager.OnSceneChanged(e.Location.Name, e.IsCurrentLocation);
        // Monitor.Log($"[LargeTerrainFeatureListChanged] Location: {e.Location.Name}, IsCurrentLocation: {e.IsCurrentLocation}", LogLevel.Debug);

        // // 打印新增的特征
        // foreach (var feature in e.Added)
        // {
        //     Monitor.Log($"  Added LargeTerrainFeature: {feature.GetType().Name}", LogLevel.Debug);
        // }

        // // 打印移除的特征
        // foreach (var feature in e.Removed)
        // {
        //     Monitor.Log($"  Removed LargeTerrainFeature: {feature.GetType().Name}", LogLevel.Debug);
        // }
    }

    /// <summary>
    /// 打印 TerrainFeatureListChanged 的信息
    /// </summary>
    private void OnTerrainFeatureListChanged(object sender, TerrainFeatureListChangedEventArgs e)
    {
        autoPhotoManager.OnSceneChanged(e.Location.Name, e.IsCurrentLocation);

        // Monitor.Log($"[TerrainFeatureListChanged] Location: {e.Location.Name}, IsCurrentLocation: {e.IsCurrentLocation}", LogLevel.Debug);

        // // 打印新增的特征
        // foreach (var kvp in e.Added)
        // {
        //     Vector2 pos = kvp.Key;
        //     TerrainFeature feature = kvp.Value;
        //     Monitor.Log($"  Added TerrainFeature at ({pos.X},{pos.Y}): {feature.GetType().Name}", LogLevel.Debug);
        // }

        // // 打印移除的特征
        // foreach (var kvp in e.Removed)
        // {
        //     Vector2 pos = kvp.Key;
        //     TerrainFeature feature = kvp.Value;
        //     Monitor.Log($"  Removed TerrainFeature at ({pos.X},{pos.Y}): {feature.GetType().Name}", LogLevel.Debug);
        // }
    }

    private void OnWarped(object sender, WarpedEventArgs e) {
        autoPhotoManager.OnWarped(sender, e);
        // try {
        //     Monitor.Log($"玩家: {e.Player.Name}", LogLevel.Debug);
        //     Monitor.Log($"旧位置: {e.OldLocation.Name}", LogLevel.Debug);
        //     Monitor.Log($"新位置: {e.NewLocation.Name}", LogLevel.Debug);
        //     Monitor.Log($"是否本地玩家: {e.IsLocalPlayer}", LogLevel.Debug);
        //     if (e.NewLocation.Name == "Farm") {
        //         TakeFarmPanorama();
        //     }
        // }
        // catch (Exception exception) {
        //     Monitor.Log("xd Failed to use e on OnWarped. " + exception);
        // }
    }

    private void OnWarpedPrint(object sender, WarpedEventArgs e)
    {
        Monitor.Log($"[AutoFarmScreenshot] New Location: {e.NewLocation.Name}, Old Location: {e.OldLocation.Name}", LogLevel.Debug);
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e) {
        // Monitor.Log($"[DayStarted]", LogLevel.Debug);
        autoPhotoManager.ResetDailyState();
        // TakeFarmPanorama();
    }

}

