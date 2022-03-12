using System;
using BepInEx;
using BepInEx.Configuration;

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

        private readonly ConfigEntry<float> _respawnDelay;
        private readonly ConfigEntry<RoR2.TeamIndex> _respawnTeam;
        private readonly ConfigEntry<float> _respawnHealthMultiplier;
        private readonly ConfigEntry<float> _respawnDamageMultiplier;
        private readonly ConfigEntry<float> _respawnMoneyMultiplier;
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
        private readonly ConfigEntry<bool> _announceRespawns;
        private readonly ConfigEntry<bool> _disableMoon;

        private readonly ConfigEntry<bool> _endGameWhenEverybodyDead;
        private readonly ConfigEntry<int> _maxRespawnTries;

        // Making the public variables
        public bool EnableRefightilization { get => _enableRefightilization.Value; }

        public bool AllowBosses { get => _allowBosses.Value; }
        public bool AllowScavengers { get => _allowScavengers.Value; }
        public string[] BlacklistedEnemies { get => _blacklistedEnemies.Value.Replace(" ", "").Split(','); }
        public int BossRequiredLoopCount { get => _bossRequiredLoopCount.Value; }
        public int ScavangerRequiredLoopCount { get => _scavangerRequiredLoopCount.Value; }

        public float RespawnDelay { get => _respawnDelay.Value; }
        public RoR2.TeamIndex RespawnTeam { get => _respawnTeam.Value; }
        public float RespawnHealthMultiplier { get => _respawnHealthMultiplier.Value; }
        public float RespawnDamageMultiplier { get => _respawnDamageMultiplier.Value; }
        public float RespawnMoneyMultiplier { get => _respawnMoneyMultiplier.Value; }
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
        public bool AnnounceRespawns { get => _announceRespawns.Value; }
        public bool DisableMoon { get => _disableMoon.Value; }

        public bool EndGameWhenEverybodyDead { get => _endGameWhenEverybodyDead.Value; }
        public int MaxRespawnTries { get => _maxRespawnTries.Value; }

        // Here's the fancy ol' initialization function.
        public RefightilizationConfig(ConfigFile config)
        {
            _enableRefightilization = config.Bind("Master", "EnableRefightilization", true, "Enables/Disables the entire mod.");

            _allowBosses = config.Bind("Monster Categories", "AllowBosses", true, "Allows players to spawn as bosses.");
            _allowScavengers = config.Bind("Monster Categories", "AllowScavengers", false, "Allows players to spawn as Scavengers.");
            _blacklistedEnemies = config.Bind("Monster Categories", "BlacklistedEnemies", "BeetleBody, JellyfishBody, WispBody, MinorConstructBody, VoidBarnacleBody", "Sets monsters to prevent players from spawning as. A list of bodies can be grabbed by using body_list in the console.");
            _bossRequiredLoopCount = config.Bind("Monster Categories", "BossRequiredLoopCount", 2, "The required amount of loops before a player can spawn as a boss.");
            _scavangerRequiredLoopCount = config.Bind("Monster Categories", "ScavangerRequiredLoopCount", 5, "The required amount of loops before a player can spawn as a scavanger.");

            _respawnDelay = config.Bind("Respawn Settings", "RespawnDelay", 5f, "Sets the delay until the player can respawn.");
            _respawnTeam = config.Bind("Respawn Settings", "RespawnTeam", RoR2.TeamIndex.Neutral, "Sets the team of the respawned players.");
            _respawnHealthMultiplier = config.Bind("Respawn Settings", "RespawnHealthMultiplier", 1.1f, "Multiplies the health a player spawns with.");
            _respawnDamageMultiplier = config.Bind("Respawn Settings", "RespawnDamageMultiplier", 5f, "Multiplies the damage a player spawns with.");
            _respawnMoneyMultiplier = config.Bind("Respawn Settings", "RespawnMoneyMultiplier", 1.1f, "Multiplies the money rewarded for killing a player.");
            _respawnAffixChance = config.Bind("Respawn Settings", "RespawnAffixChance", 60f, "Sets the chance that a respawned player will have an affix.");
            _additionalRespawnTime = config.Bind("Respawn Settings", "AdditionalRespawnTime", 0.2f, "Sets how much respawn time will increase per player death. This will effect everyone.");
            _respawnAsMonsterVariants = config.Bind("Respawn Settings", "RespawnAsMonsterVariants", true, "Allows players to respawn as Monster Variants.");
            _noRespawnsAfterTeleporter = config.Bind("Respawn Settings", "NoRespawnsAfterTeleporter", true, "Disables respawning after the Teleporter event concludes.");
            _noRepeatRespawns = config.Bind("Respawn Settings", "NoRepeatRespawns", true, "Will attempt to prevent the player from respawning as the same monster twice in a row.");

            _itemPickupToggle = config.Bind("Item Settings", "ItemPickupToggle", true, "Allows monster players to pick up items off the ground. (Disabling won't work if RespawnTeam is set to Player.)");
            _removeMonsterVariantItems = config.Bind("Item Settings", "RemoveMonsterVariantItems", true, "Will remove items given to players by Monster Variants on respawn.");
            _takeAffix = config.Bind("Item Settings", "TakeAffix", true, "Will take away granted affixes upon respawning.");
            _forceGrantAffix = config.Bind("Item Settings", "ForceGrantAffix", false, "Will forcibly give the player an aspect, even if they have an equipment item.");

            _murderRevive = config.Bind("Behavior", "MurderRevive", true, "Will respawn a dead player as a survivor if they kill another player.");
            _announceRespawns = config.Bind("Behavior", "AnnounceRespawns", true, "Will announce a player respawning in the chat.");
            _disableMoon = config.Bind("Behavior", "DisableMoon", true, "Prevents players from respawning in Commencement.");

            _endGameWhenEverybodyDead = config.Bind("Debug", "EndGameWhenEverybodyDead", true, "Ends the round when everybody is dead. (Keep this on.)");
            _maxRespawnTries = config.Bind("Debug", "MaxRespawnTries", 5, "The maximum attempts the game will make to retry spawning a player.");
        }
    }
}
