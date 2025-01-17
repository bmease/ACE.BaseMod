﻿using ACE.Database;
using ACE.Database.Entity;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Managers;
using System;
using System.Runtime.CompilerServices;
using static ACE.Server.WorldObjects.Creature;

namespace QuestBonus;

[HarmonyPatch]
public class PatchClass
{
    #region Settings
    //private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(2);
    const int RETRIES = 10;

    public static Settings Settings = new();
    private static string settingsPath = Path.Combine(Mod.ModPath, "Settings.json");
    private static FileInfo settingsInfo = new(settingsPath);

    private static JsonSerializerOptions _serializeOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static void SaveSettings()
    {
        string jsonString = JsonSerializer.Serialize(Settings, _serializeOptions);

        if (!settingsInfo.RetryWrite(jsonString, RETRIES))
        {
            ModManager.Log($"Failed to save settings to {settingsPath}...", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
        }
    }

    private static void LoadSettings()
    {
        if (!settingsInfo.Exists)
        {
            ModManager.Log($"Creating {settingsInfo}...");
            SaveSettings();
        }
        else
            ModManager.Log($"Loading settings from {settingsPath}...");

        if (!settingsInfo.RetryRead(out string jsonString, RETRIES))
        {
            Mod.State = ModState.Error;
            return;
        }

        try
        {
            Settings = JsonSerializer.Deserialize<Settings>(jsonString, _serializeOptions);
        }
        catch (Exception)
        {
            ModManager.Log($"Failed to deserialize Settings: {settingsPath}", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
            return;
        }
    }
    #endregion

    #region Start/Shutdown
    public static void Start()
    {
        //Need to decide on async use
        Mod.State = ModState.Loading;
        LoadSettings();

        if (Mod.State == ModState.Error)
        {
            ModManager.DisableModByPath(Mod.ModPath);
            return;
        }

        //On reload recalculate QB
        UpdateIngamePlayers();

        Mod.State = ModState.Running;
    }

    public static void Shutdown()
    {
        //if (Mod.State == ModState.Running)
        // Shut down enabled mod...

        //If the mod is making changes that need to be saved use this and only manually edit settings when the patch is not active.
        //SaveSettings();

        if (Mod.State == ModState.Error)
            ModManager.Log($"Improper shutdown: {Mod.ModPath}", ModManager.LogLevel.Error);
    }
    #endregion

    [CommandHandler("qp", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Lists and updates quest bonuses")]
    public static void HandleQuestPoints(Session session, params string[] parameters)
    {
        var player = session.Player;
        var count = player.QuestManager.GetQuests().Where(x => x.HasSolves()).Count();
        var qp = player.GetProperty(FakeFloat.QuestBonus) ?? 0;
        var bonus = session.Player.QuestBonus();

        player.SendMessage($"{count} quests solved for {qp} QP and {session.Player.QuestBonus():P2} XP multiplier");
    }

    [CommandHandler("qb", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Lists and updates quest bonuses")]
    public static void HandleQuests(Session session, params string[] parameters)
    {
        var player = session.Player;
        var quests = player.QuestManager.GetQuests();

        var sb = new StringBuilder($"{player.QuestManager.GetQuests().Where(x => x.HasSolves()).Count()} count known to be solved for {session.Player.QuestBonus():P2} bonus.\nQuest Name/Completions/Points\n");
        foreach (var quest in quests)
        {
            if (!PatchClass.Settings.QuestBonuses.TryGetValue(quest.QuestName, out var points))
                points = PatchClass.Settings.DefaultPoints;

            sb.Append($"{quest.QuestName,-30}\n  {quest.NumTimesCompleted,-5} - {points}\n");
        }

        player.UpdateQuestPoints();
        player.SendMessage(sb.ToString());
    }

    //HasQuestSolves
    public static void UpdateIngamePlayers()
    {
        foreach(var player in PlayerManager.GetAllOnline())
            player.UpdateQuestPoints();
    }


    //Update QB on login
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostPlayerEnterWorld(ref Player __instance)
    {
        //Check a players quests
        __instance.CalculateQuestPoints();
    }

    //Check quest removal
    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Decrement), new Type[] { typeof(string), typeof(int) })]
    public static void PreDecrement(string quest, int amount, ref QuestManager __instance)
    {
        var questName = QuestManager.GetQuestName(quest);
        var qst = __instance.GetQuest(questName);
        if (qst.NumTimesCompleted == 1 && __instance.Creature is Player player)
        {
            player.IncQuestPoints(qst.Value());

            if(PatchClass.Settings.NotifyQuest)
            player.SendMessage($"Removed {qst.Value()} QP on removing {questName}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Erase), new Type[] { typeof(string) })]
    public static void PreErase(string questFormat, ref QuestManager __instance)
    {
        var questName = QuestManager.GetQuestName(questFormat);
        var qst = __instance.GetQuest(questName);
        if (qst.NumTimesCompleted == 1 && __instance.Creature is Player player)
        {
            player.IncQuestPoints(-1 * qst.Value());

            if (PatchClass.Settings.NotifyQuest)
                player.SendMessage($"Removed {qst.Value()} QP on removing {questName}");
        }
    }

    #region Update / SetQuestCompletions 
    //Prefixes use __state to pass solves from before to compare after the method
    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Update), new Type[] { typeof(string) })]
    public static void PreUpdate(string questFormat, ref QuestManager __instance, ref int __state)
    {
        __state = __instance.GetCurrentSolves(questFormat);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.SetQuestCompletions), new Type[] { typeof(string), typeof(int) })]
    public static void PreSetQuestCompletions(string questFormat, int questCompletions, ref QuestManager __instance, ref int __state)
    {
        __state = __instance.GetCurrentSolves(questFormat);
    }

    //Postfixes check for changes and update QP
    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Update), new Type[] { typeof(string) })]
    public static void PostUpdate(string questFormat, ref QuestManager __instance, ref int __state)
    {
        CheckQuestEligibilityChange(questFormat, __instance, __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.SetQuestCompletions), new Type[] { typeof(string), typeof(int) })]
    public static void PostSetQuestCompletions(string questFormat, int questCompletions, ref QuestManager __instance, ref int __state)
    {
        CheckQuestEligibilityChange(questFormat, __instance, __state);
    }

    private static void CheckQuestEligibilityChange(string questFormat, QuestManager __instance, int __state)
    {
        var solves = __instance.GetCurrentSolves(questFormat);

        //Add quest
        if (__state == 0 && solves != 0)
        {
            var quest = __instance.GetQuest(QuestManager.GetQuestName(questFormat));

            if (__instance.Creature is Player player)
            {
                player.IncQuestPoints(quest.Value());

                if (PatchClass.Settings.NotifyQuest)
                    player.SendMessage($"Added {quest.Value()} to QB from {questFormat}");
            }
        }

        //Remove quest?
        if (__state != 0 && solves == 0)
        {
            var quest = __instance.GetQuest(QuestManager.GetQuestName(questFormat));

            if (__instance.Creature is Player player)
            {
                player.IncQuestPoints(-1 * quest.Value());

                if (PatchClass.Settings.NotifyQuest)
                    player.SendMessage($"Subtracted {quest.Value()} from QB from {questFormat}");
            }
        }
    }
    #endregion

    #region Failures
    #region Getter -- Ugly way that looks through online players for matching CharacterId, fails because it isn't populated?
    //[HarmonyPrefix]
    //[HarmonyPatch(MethodType.Setter)]
    //[HarmonyPatch(typeof(CharacterPropertiesQuestRegistry), nameof(CharacterPropertiesQuestRegistry.NumTimesCompleted), new Type[] { typeof(int) })]
    //public static void PreSetNumTimesCompleted(int value, ref CharacterPropertiesQuestRegistry __instance)
    //{
    //    if (value != 0 && __instance.NumTimesCompleted == 0)
    //    {
    //        foreach (var player in PlayerManager.GetAllOnline())
    //        {
    //            if (player.Character.Id == __instance.CharacterId)
    //            {
    //                player.IncQuestPoints(__instance.Value());
    //                player.SendMessage($"Added {__instance.Value()} to QB");
    //                return;
    //            }
    //        }
    //    }
    //    else if(value == 0 && __instance.NumTimesCompleted != 0)
    //    {
    //        foreach (var player in PlayerManager.GetAllOnline())
    //        {
    //            if (player.Character.Id == __instance.CharacterId)
    //            {
    //                player.IncQuestPoints(-1 * __instance.Value());
    //                player.SendMessage($"Subtracted {__instance.Value()} from QB");
    //                return;
    //            }
    //        }
    //    }
    //}
    #endregion

    #region ContractManager.NotifyOfQuestUpdate -- no way of telling if the elligibility is changing
    ////From ContractManager?
    //[HarmonyPrefix]
    //[HarmonyPatch(typeof(ContractManager), nameof(ContractManager.NotifyOfQuestUpdate), new Type[] { typeof(string) })]
    //public static void PreNotifyOfQuestUpdate(string questName, ref ContractManager __instance, ref int __state)
    //{
    //    var quest = __instance.Player.QuestManager.GetQuest(questName);
    //    __state = quest is null ? 0 : quest.NumTimesCompleted;
    //}

    //[HarmonyPostfix]
    //[HarmonyPatch(typeof(ContractManager), nameof(ContractManager.NotifyOfQuestUpdate), new Type[] { typeof(string) })]
    //public static void PostNotifyOfQuestUpdate(string questName, ref ContractManager __instance, ref int __state)
    //{
    //    var quest = __instance.Player.QuestManager.GetQuest(questName);
    //    var solves = quest is null ? 0 : quest.NumTimesCompleted;
    //    var player = __instance.Player;

    //    if (__state == 0 && solves != 0)
    //    {
    //        player.IncQuestPoints(quest.Value());
    //        player.SendMessage($"Added {quest.Value()} to QB");
    //    }

    //    //Remove quest?
    //    if (__state != 0 && solves == 0)
    //    {
    //        player.IncQuestPoints(-1 * quest.Value());
    //        player.SendMessage($"Subtracted {quest.Value()} from QB");
    //    }
    //} 
    #endregion
    #endregion

    //Payloads.  Currently just adds XP
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.GrantXP), new Type[] { typeof(long), typeof(XpType), typeof(ShareType) })]
    public static void PreGrantXP(ref long amount, XpType xpType, ShareType shareType, ref Player __instance)
    {
        //Increment exp
        if (PatchClass.Settings.NotifyExp)
            __instance.SendMessage($"Boosting xp from {amount} by {__instance.QuestBonus():P2} to {(long)(amount * __instance.QuestBonus())}");

        amount = (long)(amount * __instance.QuestBonus());
    }
}
