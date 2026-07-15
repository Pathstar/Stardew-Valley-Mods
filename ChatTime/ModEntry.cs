using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
// for ChatBox
using HarmonyLib;

namespace ChatTime;

public class ModEntry : Mod {
    public static ModEntry Instance;
    internal Config config;

    public override void Entry(IModHelper helper) {
        Instance = this;
        config = helper.ReadConfig<Config>();
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(ChatMessage), nameof(ChatMessage.parseMessageForEmoji))]
    public static class ChatMessagePatch
    {
        public static void Prefix(ChatMessage __instance, string messagePlaintext)
        {
            LocalizedContentManager.LanguageCode lang = __instance.language != default
                ? __instance.language
                : LocalizedContentManager.CurrentLanguageCode;

            __instance.message.Add(
                new ChatSnippet($"{DateTime.Now.ToString(Instance.config.timeFormat)} ", lang)
            );
        }
    }
}
