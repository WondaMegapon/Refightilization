using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using RiskOfOptions;

namespace Wonda
{
    class RefightilizationConfig
    {
        // Making the private variables
        private readonly ConfigEntry<bool> _enableRefightilization;

        private readonly ConfigEntry<bool> _allowBosses;
        private readonly ConfigEntry<bool> _allowScavengers;
        private readonly ConfigEntry<string> _blacklistedEnemies;
        private readonly ConfigEntry<int> _bossRequiredLoopCount;
        private readonly ConfigEntry<int> _scavangerRequiredLoopCount;
        private readonly ConfigEntry<bool> _enableFixedPool;
        private readonly ConfigEntry<string> _fixedPool;

        private readonly ConfigEntry<float> _respawnDelay;
        private readonly ConfigEntry<RoR2.TeamIndex> _respawnTeam;
        private readonly ConfigEntry<float> _respawnHealthMultiplier;
        private readonly ConfigEntry<float> _respawnDamageMultiplier;
        private readonly ConfigEntry<float> _respawnMoneyMultiplier;
        private readonly ConfigEntry<bool> _respawnAffixEnabled;
        private readonly ConfigEntry<float> _respawnAffixChance;
        private readonly ConfigEntry<float> _additionalRespawnTime;
        private readonly ConfigEntry<bool> _respawnAsMonsterVariants;
        private readonly ConfigEntry<bool> _noRespawnsAfterTeleporter;
        private readonly ConfigEntry<bool> _noRepeatRespawns;

        private readonly ConfigEntry<bool> _itemPickupToggle;
        private readonly ConfigEntry<bool> _removeMonsterVariantItems;
        private readonly ConfigEntry<bool> _takeAffix;
        private readonly ConfigEntry<bool> _forceGrantAffix;

        private readonly ConfigEntry<bool> _murderRevive;
        private readonly ConfigEntry<float> _murderWindow;
        private readonly ConfigEntry<bool> _announceRespawns;
        private readonly ConfigEntry<bool> _disableMoon;
        private readonly ConfigEntry<bool> _overrideMetamorphosis;
        private readonly ConfigEntry<bool> _changeMinionsTeam;

        private readonly ConfigEntry<bool> _endGameWhenEverybodyDead;
        private readonly ConfigEntry<int> _maxRespawnTries;
        private readonly ConfigEntry<string> _preventPrefabResetMethods;

        // Making the public variables
        public bool EnableRefightilization { get => _enableRefightilization.Value; }

        public bool AllowBosses { get => _allowBosses.Value; }
        public bool AllowScavengers { get => _allowScavengers.Value; }
        public string[] BlacklistedEnemies { get => _blacklistedEnemies.Value.Replace(" ", "").Split(','); }
        public int BossRequiredLoopCount { get => _bossRequiredLoopCount.Value; }
        public int ScavangerRequiredLoopCount { get => _scavangerRequiredLoopCount.Value; }
        public bool EnableFixedPool { get => _enableFixedPool.Value; }
        public string[] FixedPool { get => _fixedPool.Value.Replace(" ", "").Split(','); }

        public float RespawnDelay { get => _respawnDelay.Value; }
        public RoR2.TeamIndex RespawnTeam { get => _respawnTeam.Value; }
        public float RespawnHealthMultiplier { get => _respawnHealthMultiplier.Value; }
        public float RespawnDamageMultiplier { get => _respawnDamageMultiplier.Value; }
        public float RespawnMoneyMultiplier { get => _respawnMoneyMultiplier.Value; }
        public bool RespawnAffixEnabled { get => _respawnAffixEnabled.Value; }
        public float RespawnAffixChance { get => _respawnAffixChance.Value; }
        public float AdditionalRespawnTime { get => _additionalRespawnTime.Value; }
        public bool RespawnAsMonsterVariants { get => _respawnAsMonsterVariants.Value; }
        public bool NoRespawnsAfterTeleporter { get => _noRespawnsAfterTeleporter.Value; }
        public bool NoRepeatRespawns { get => _noRepeatRespawns.Value; }

        public bool ItemPickupToggle { get => _itemPickupToggle.Value; }
        public bool RemoveMonsterVariantItems { get => _removeMonsterVariantItems.Value; }
        public bool TakeAffix { get => _takeAffix.Value; }
        public bool ForceGrantAffix { get => _forceGrantAffix.Value; }

        public bool MurderRevive { get => _murderRevive.Value; }
        public float MurderWindow { get => _murderWindow.Value; }
        public bool AnnounceRespawns { get => _announceRespawns.Value; }
        public bool DisableMoon { get => _disableMoon.Value; }
        public bool OverrideMetamorphosis { get => _overrideMetamorphosis.Value; }
        public bool ChangeMinionsTeam { get => _changeMinionsTeam.Value; }

        public bool EndGameWhenEverybodyDead { get => _endGameWhenEverybodyDead.Value; }
        public int MaxRespawnTries { get => _maxRespawnTries.Value; }
        public string[] PreventPrefabResetMethods { get => _preventPrefabResetMethods.Value.Replace(" ", "").Split(','); }

        // Here's the fancy ol' initialization function.
        public RefightilizationConfig(ConfigFile config)
        {
            _enableRefightilization = config.Bind("Master", "EnableRefightilization", true, "Enables/Disables the entire mod.");

            _allowBosses = config.Bind("Monster Categories", "AllowBosses", true, "Allows players to spawn as bosses.");
            _allowScavengers = config.Bind("Monster Categories", "AllowScavengers", false, "Allows players to spawn as Scavengers.");
            _blacklistedEnemies = config.Bind("Monster Categories", "BlacklistedEnemies", "BeetleBody, JellyfishBody, WispBody, MinorConstructBody, VoidBarnacleBody, ClayBossBody", "Sets monsters to prevent players from spawning as. A list of bodies can be grabbed by using body_list in the console.");
            _bossRequiredLoopCount = config.Bind("Monster Categories", "BossRequiredLoopCount", 2, "The required amount of loops before a player can spawn as a boss.");
            _scavangerRequiredLoopCount = config.Bind("Monster Categories", "ScavangerRequiredLoopCount", 5, "The required amount of loops before a player can spawn as a scavanger.");
            _enableFixedPool = config.Bind("Monster Categories", "EnableFixedPool", false, "Forces players to respawn from a pre-defined list of monsters, instead of the current stage's monsters.");
            _fixedPool = config.Bind("Monster Categories", "FixedPool", "LemurianBody", "Provides a fixed list of monsters that the player could spawn as with EnableFixedPool enabled. This list will also be used if there are no available monsters to spawn as.");

            _respawnDelay = config.Bind("Respawn Settings", "RespawnDelay", 5f, "Sets the delay until the player can respawn.");
            _respawnTeam = config.Bind("Respawn Settings", "RespawnTeam", RoR2.TeamIndex.Neutral, "Sets the team of the respawned players.");
            _respawnHealthMultiplier = config.Bind("Respawn Settings", "RespawnHealthMultiplier", 1.1f, "Multiplies the health a player spawns with.");
            _respawnDamageMultiplier = config.Bind("Respawn Settings", "RespawnDamageMultiplier", 5f, "Multiplies the damage a player spawns with.");
            _respawnMoneyMultiplier = config.Bind("Respawn Settings", "RespawnMoneyMultiplier", 1.1f, "Multiplies the money rewarded for killing a player.");
            _respawnAffixEnabled = config.Bind("Respawn Settings", "RespawnAffixEnabled", true, "Allows players to spawn with affixes if they have an empty equipment slot.");
            _respawnAffixChance = config.Bind("Respawn Settings", "RespawnAffixChance", 60f, "Sets the chance that a respawned player will have an affix.");
            _additionalRespawnTime = config.Bind("Respawn Settings", "AdditionalRespawnTime", 0.2f, "Sets how much respawn time will increase per player death. This will effect everyone.");
            _noRespawnsAfterTeleporter = config.Bind("Respawn Settings", "NoRespawnsAfterTeleporter", true, "Disables respawning after the Teleporter event concludes.");
            _noRepeatRespawns = config.Bind("Respawn Settings", "NoRepeatRespawns", true, "Will attempt to prevent the player from respawning as the same monster twice in a row.");

            _itemPickupToggle = config.Bind("Item Settings", "ItemPickupToggle", true, "Allows monster players to pick up items off the ground. (Disabling won't work if RespawnTeam is set to Player.)");
            _takeAffix = config.Bind("Item Settings", "TakeAffix", true, "Will take away granted affixes upon respawning.");
            _forceGrantAffix = config.Bind("Item Settings", "ForceGrantAffix", false, "Will forcibly give the player an aspect, even if they have an equipment item.");

            _murderRevive = config.Bind("Behavior Settings", "MurderRevive", true, "Will respawn a dead player as a survivor if they kill another player.");
            _murderWindow = config.Bind("Behavior Settings", "MurderWindow", 15f, "The amount of time that a player has after damaging a player to respawn as them.");
            _announceRespawns = config.Bind("Behavior Settings", "AnnounceRespawns", true, "Will announce a player respawning in the chat.");
            _disableMoon = config.Bind("Behavior Settings", "DisableMoon", true, "Prevents players from respawning in Commencement.");
            _overrideMetamorphosis = config.Bind("Behavior Settings", "OverrideMetamorphosis", true, "Allows Refightilization to override Artifact of Metamorphosis's behavior.");
            _changeMinionsTeam = config.Bind("Behavior Settings", "ChangeMinionsTeam", true, "Minions will have their team changed to match the player.");

            _respawnAsMonsterVariants = config.Bind("Compatibility Settings", "RespawnAsMonsterVariants", true, "Allows players to respawn as Monster Variants.");
            _removeMonsterVariantItems = config.Bind("Compatibility Settings", "RemoveMonsterVariantItems", true, "Will remove items given to players by Monster Variants on respawn.");

            _endGameWhenEverybodyDead = config.Bind("Debug", "EndGameWhenEverybodyDead", true, "Ends the round when everybody is dead. (Keep this on.)");
            _maxRespawnTries = config.Bind("Debug", "MaxRespawnTries", 5, "The maximum attempts the game will make to retry spawning a player.");
            _preventPrefabResetMethods = config.Bind("Debug", "PreventPrefabResetMethods", "SwapCharacters, RevertCharacter, CCSpawnAs", "Manually set methods to not be affected by Refight's BodyPrefab-Resetting behavior. (Don't touch this unless you know what you're doing.)");
        }

        // For Risk of Options.
        public void BuildRiskOfOptionsMenu()
        {
            // Grabbing our icon.
            byte[] iconData = File.ReadAllBytes(System.Reflection.Assembly.GetExecutingAssembly().Location.Replace("Refightilization.dll", "") + "icon.png");

            // Some flavor.
            UnityEngine.Texture2D modIconSprite = new UnityEngine.Texture2D(256, 256);
            UnityEngine.ImageConversion.LoadImage(modIconSprite, iconData);
            UnityEngine.Sprite modIcon = UnityEngine.Sprite.Create(modIconSprite, new UnityEngine.Rect(0, 0, modIconSprite.width, modIconSprite.height), UnityEngine.Vector2.zero);

            ModSettingsManager.SetModIcon(modIcon);
            ModSettingsManager.SetModDescriptionToken("REFIGHT_MOD_DESCRIPTION");

            // For all of our settings:
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_enableRefightilization));

            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_allowBosses));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_allowScavengers));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(_blacklistedEnemies));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(_bossRequiredLoopCount));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(_scavangerRequiredLoopCount));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_enableFixedPool));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(_fixedPool));

            ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(_respawnDelay, new RiskOfOptions.OptionConfigs.StepSliderConfig() { min = 0, max = 60, increment = 1f } ));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.ChoiceOption(_respawnTeam));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(_respawnHealthMultiplier, new RiskOfOptions.OptionConfigs.StepSliderConfig() { min = 0, max = 2, increment = 0.1f }));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(_respawnDamageMultiplier, new RiskOfOptions.OptionConfigs.StepSliderConfig() { min = 0, max = 10, increment = 0.2f }));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(_respawnMoneyMultiplier, new RiskOfOptions.OptionConfigs.StepSliderConfig() { min = 0, max = 2, increment = 0.1f }));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_respawnAffixEnabled));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(_respawnAffixChance));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(_additionalRespawnTime, new RiskOfOptions.OptionConfigs.StepSliderConfig() { min = 0, max = 2, increment = 0.1f }));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_noRespawnsAfterTeleporter));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_noRepeatRespawns));

            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_itemPickupToggle));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_takeAffix));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_forceGrantAffix));

            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_murderRevive));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(_murderWindow, new RiskOfOptions.OptionConfigs.StepSliderConfig() { min = 1, max = 60, increment = 1f }));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_announceRespawns));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_disableMoon));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_overrideMetamorphosis));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_changeMinionsTeam));

            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_respawnAsMonsterVariants));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_removeMonsterVariantItems));

            ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(_endGameWhenEverybodyDead));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(_maxRespawnTries));
            ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(_preventPrefabResetMethods, new RiskOfOptions.OptionConfigs.InputFieldConfig() { restartRequired = true }));
        }
    }
}
