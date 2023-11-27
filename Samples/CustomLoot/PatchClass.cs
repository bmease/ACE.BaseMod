﻿using ACE.Database.Models.World;
using ACE.Server.Command;
using ACE.Server.Factories.Entity;
using ACE.Server.Network;
using ACE.Server.WorldObjects;
using CustomLoot.Enums;
using HarmonyLib;
using System.Text;

namespace CustomLoot;

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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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

        SetupFeatures();
        SetupMutators();

        Mod.State = ModState.Running;
    }

    public static void Shutdown()
    {
        ShutdownMutators();
        Mod.Harmony.UnpatchAll();

        if (Mod.State == ModState.Error)
            ModManager.Log($"Improper shutdown: {Mod.ModPath}", ModManager.LogLevel.Error);
    }
    #endregion

    private static readonly Dictionary<MutationEvent, List<Mutator>> mutators = new();
    /// <summary>
    /// Adds additional features to ACE that may be needed by custom loot
    /// </summary>
    private static void SetupFeatures()
    {
        foreach (var feature in PatchClass.Settings.Features)
        {
            Mod.Harmony.PatchCategory(feature.ToString());

            if (PatchClass.Settings.Verbose)
                ModManager.Log($"Enabled feature: {feature}");
        }
    }
    private static void SetupMutators()
    {
        //enabledPatches.Clear();
        mutators.Clear();
        mutators[MutationEvent.Loot] = new();
        mutators[MutationEvent.Corpse] = new();
        mutators[MutationEvent.Generator] = new();

        foreach (var mutatorOptions in Settings.Mutators)
        {
            if (!mutatorOptions.Enabled)
                continue;

            try
            {
                var mutator = mutatorOptions.CreateMutator();
                mutator.Start();

                //enabledPatches.Add(mutator);
                if (mutator.IsLootMutator)
                    mutators[MutationEvent.Loot].Add(mutator);
                if (mutator.IsCorpseMutator)
                    mutators[MutationEvent.Corpse].Add(mutator);
                if (mutator.IsGeneratorMutator)
                    mutators[MutationEvent.Generator].Add(mutator);


                if (PatchClass.Settings.Verbose)
                    ModManager.Log($"Enabled mutator: {mutatorOptions.PatchType}");
            }
            catch (Exception ex)
            {
                if (PatchClass.Settings.Verbose)
                    ModManager.Log($"Failed to patch {mutatorOptions.PatchType}: {ex.Message}", ModManager.LogLevel.Error);
            }
        }
    }
    private static void ShutdownMutators()
    {
        //if (Mod.State == ModState.Running)

        //Shutdown/unpatch everything on settings change to support repatching by category
        foreach (var eventType in mutators.Values)
        {
            //Todo: Prevent duplicate shutdowns..?
            HashSet<Mutator> encountered = new();

            foreach (var mutator in eventType)
            {
                if (encountered.Contains(mutator))
                    continue;

                //Shut down the mutator / remember it
                encountered.Add(mutator);
                mutator.Shutdown();
            }
        }
    }

    /// <summary>
    /// Entry point for mutation.  After loot is generated it is passed to mutators to try to change
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(LootGenerationFactory), nameof(LootGenerationFactory.CreateAndMutateWcid), new Type[] { typeof(TreasureDeath), typeof(TreasureRoll), typeof(bool) })]
    public static void PostCreateAndMutateWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll, bool isMagical, ref WorldObject __result)
    {
        if (treasureDeath is null) return;

        //Keeps track of what mutations have been applied
        HashSet<Mutation> mutations = new();

        foreach (var mutator in mutators[MutationEvent.Loot])
        {
            //Check for elligible item type
            if (!mutator.MutatesLoot(mutations, treasureDeath, treasureRoll, __result))
                continue;

            //If an item was mutated add the type
            if (mutator.TryMutateLoot(mutations, treasureDeath, treasureRoll, __result))
                mutations.Add(mutator.MutationType);
        }

        if (PatchClass.Settings.Verbose && mutations.Count > 0)
            ModManager.Log($"{__result.Name} was mutated with: {String.Join(", ", mutations)}");

    }

    /// <summary>
    /// After creature dies
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.GenerateTreasure), new Type[] { typeof(DamageHistoryInfo), typeof(Corpse) })]
    public static void PostGenerateTreasure(DamageHistoryInfo killer, Corpse corpse, ref Creature __instance, ref List<WorldObject> __result)
    {
        //Todo: look at skipping based on container
        //!!DROPPED ITEMS in __result, the rest are moved to corpse?!!
        //foreach (var item in __result)
        foreach (var item in corpse.Inventory.Values)
        {
            //Keeps track of what mutations have been applied
            HashSet<Mutation> mutations = new();

            foreach (var mutator in mutators[MutationEvent.Corpse])
            {
                if (!mutator.MutatesCorpse(mutations, __instance, killer, corpse, item))
                    continue;

                //If an item was mutated add the type
                if (mutator.TryMutateCorpse(mutations, __instance, killer, corpse, item))
                    mutations.Add(mutator.MutationType);

                if (PatchClass.Settings.Verbose && mutations.Count > 0)
                    ModManager.Log($"{item.Name} was mutated with: {String.Join(", ", mutations)}");
            }
        }
    }

    /// <summary>
    /// Container/treasure generator?
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GeneratorProfile), nameof(GeneratorProfile.TreasureGenerator))]
    public static void PostTreasureGenerator(ref GeneratorProfile __instance, ref List<WorldObject> __result)
    {
        //Todo: look at skipping based on container
        //Loop through each item
        foreach (var item in __result)
        {
            //Keeps track of what mutations have been applied
            HashSet<Mutation> mutations = new();

            foreach (var mutator in mutators[MutationEvent.Generator])
            {
                if (!mutator.MutatesGenerator(mutations, __instance, item))
                    continue;

                //If an item was mutated add the type
                if (mutator.TryMutateGenerator(mutations, __instance, item))
                    mutations.Add(mutator.MutationType);

                if (PatchClass.Settings.Verbose && mutations.Count > 0)
                    ModManager.Log($"{item.Name} was mutated with: {String.Join(", ", mutations)}");
            }
        }
    }
}
