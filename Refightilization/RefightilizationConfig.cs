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

        private readonly ConfigEntry<float> _respawnDelay;
        private readonly ConfigEntry<int> _respawnTeam;
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
        private readonly ConfigEntry<bool> _removeAllItems;
        private readonly ConfigEntry<bool> _returnItemsOnStageChange;
        private readonly ConfigEntry<bool> _takeAffix;

        private readonly ConfigEntry<bool> _murderRevive;

        private readonly ConfigEntry<bool> _endGameWhenEverybodyDead;
        private readonly ConfigEntry<int> _maxRespawnTries;
        private readonly ConfigEntry<bool> _forceItemRestoration;

        // Making the public variables
        public bool EnableRefightilization { get => _enableRefightilization.Value; }

        public bool AllowBosses { get => _allowBosses.Value; }
        public bool AllowScavengers { get => _allowScavengers.Value; }
        public string[] BlacklistedEnemies { get => _blacklistedEnemies.Value.Replace(" ", "").Split(','); }

        public float RespawnDelay { get => _respawnDelay.Value; }
        public int RespawnTeam { get => _respawnTeam.Value; }
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
        public bool RemoveAllItems { get => _removeAllItems.Value; }
        public bool ReturnItemsOnStageChange { get => _returnItemsOnStageChange.Value; }
        public bool TakeAffix { get => _takeAffix.Value; }

        public bool MurderRevive { get => _murderRevive.Value; }

        public bool EndGameWhenEverybodyDead { get => _endGameWhenEverybodyDead.Value; }
        public int MaxRespawnTries { get => _maxRespawnTries.Value; }
        public bool ForceItemRestoration { get => _forceItemRestoration.Value; }

        // Here's the fancy ol' initialization function.
        public RefightilizationConfig(ConfigFile config)
        {
            _enableRefightilization = config.Bind("Master", "EnableRefightilization", true, "Enables/Disables the entire mod.");

            _allowBosses = config.Bind("Monster Categories", "AllowBosses", true, "Allows players to spawn as bosses.");
            _allowScavengers = config.Bind("Monster Categories", "AllowScavengers", false, "Allows players to spawn as Scavengers.");
            _blacklistedEnemies = config.Bind("Monster Categories", "BlacklistedEnemies", "BeetleBody, JellyfishBody, WispBody", "Sets monsters to prevent players from spawning as. A list of bodies can be grabbed by using body_list in the console.");

            _respawnDelay = config.Bind("Respawn Settings", "RespawnDelay", 5f, "Sets the delay until the player can respawn.");
            _respawnTeam = config.Bind("Respawn Settings", "RespawnTeam", 0, "Sets the team of the respawned players. (-1 None / 0 Neutral / 1 Player / 2 Monster / 3 Lunar / 4 Count)");
            _respawnHealthMultiplier = config.Bind("Respawn Settings", "RespawnHealthMultiplier", 1.1f, "Multiplies the health a player spawns with.");
            _respawnDamageMultiplier = config.Bind("Respawn Settings", "RespawnDamageMultiplier", 5f, "Multiplies the damage a player spawns with.");
            _respawnMoneyMultiplier = config.Bind("Respawn Settings", "RespawnMoneyMultiplier", 1.1f, "Multiplies the money rewarded for killing a player.");
            _respawnAffixChance = config.Bind("Respawn Settings", "RespawnAffixChance", 60f, "Sets the chance that a respawned player will have an affix.");
            _additionalRespawnTime = config.Bind("Respawn Settings", "AdditionalRespawnTime", 0.2f, "Sets how much respawn time will increase per player death. This will effect everyone.");
            _respawnAsMonsterVariants = config.Bind("Respawn Settings", "RespawnAsMonsterVariants", true, "Allows players to respawn as Monster Variants.");
            _noRespawnsAfterTeleporter = config.Bind("Respawn Settings", "NoRespawnsAfterTeleporter", true, "Disables respawning after the Teleporter event concludes.");
            _noRepeatRespawns = config.Bind("Respawn Settings", "NoRepeatRespawns", true, "Will attempt to prevent the player from respawning as the same monster twice in a row.");

            _itemPickupToggle = config.Bind("Item Settings", "ItemPickupToggle", true, "Allows monster players to pick up items off the ground. (Disabling won't work if RespawnTeam is set to 1.)");
            _removeMonsterVariantItems = config.Bind("Item Settings", "RemoveMonsterVariantItems", true, "Will remove items given to players by Monster Variants on respawn.");
            _removeAllItems = config.Bind("Item Settings", "RemoveAllItems", false, "Will remove all items in a player's inventory when they respawn.");
            _returnItemsOnStageChange = config.Bind("Item Settings", "ReturnItemsOnStageChange", true, "If RemoveAllItems is enabled, will allow removed items to be returned when the stage changes.");
            _takeAffix = config.Bind("Item Settings", "TakeAffix", true, "Will take away granted affixes upon respawning.");

            _murderRevive = config.Bind("Behavior", "MurderRevive", true, "Will respawn a dead player as a survivor if they kill another player.");

            _forceItemRestoration = config.Bind("Item Settings", "ForceItemRestoration", false, "Will reset a player's inventory to the state it was before they died. (Overrides ReturnItemsOnStageChange.)");

            _endGameWhenEverybodyDead = config.Bind("Debug", "EndGameWhenEverybodyDead", true, "Ends the round when everybody is dead. (Keep this on.)");
            _maxRespawnTries = config.Bind("Debug", "MaxRespawnTries", 25, "The maximum attempts the game will make to retry spawning a player.");
        }
    }
}
