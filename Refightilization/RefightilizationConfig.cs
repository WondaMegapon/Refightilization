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

        private readonly ConfigEntry<bool> _endGameWhenEverybodyDead;

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

        public bool EndGameWhenEverybodyDead { get => _endGameWhenEverybodyDead.Value; }

        // Here's the fancy ol' struct.
        public RefightilizationConfig(ConfigFile config)
        {
            _enableRefightilization = config.Bind("Master", "EnableRefightilization", true, "Enables/Disables the entire mod.");

            _allowBosses = config.Bind("Monster Categories", "AllowBosses", false, "Allows players to spawn as bosses.");
            _allowScavengers = config.Bind("Monster Categories", "AllowScavengers", false, "Allows players to spawn as Scavengers.");
            _blacklistedEnemies = config.Bind("Monster Categories", "BlacklistedEnemies", "BeetleBody, JellyfishBody, WispBody", "Sets enemies to prevent players from spawning as.");

            _respawnDelay = config.Bind("Respawn Settings", "RespawnDelay", 5f, "Sets the delay until the player can respawn.");
            _respawnTeam = config.Bind("Respawn Settings", "RespawnTeam", 0, "Sets the team of the respawned players. (-1 None / 0 Neutral / 1 Player / 2 Monster / 3 Lunar / 4 Count)");
            _respawnHealthMultiplier = config.Bind("Respawn Settings", "RespawnHealthMultiplier", 2f, "Multiplies the health a player spawns with.");
            _respawnDamageMultiplier = config.Bind("Respawn Settings", "RespawnDamageMultiplier", 10f, "Multiplies the damage a player spawns with.");
            _respawnMoneyMultiplier = config.Bind("Respawn Settings", "RespawnMoneyMultiplier", 2f, "Multiplies the money rewarded for killing a player.");
            _respawnAffixChance = config.Bind("Respawn Settings", "RespawnAffixChance", 40f, "Sets the chance that a respawned player will have an affix.");

            _endGameWhenEverybodyDead = config.Bind("Debug", "EndGameWhenEverybodyDead", true, "Ends the round when everybody is dead. Keep this on.");
        }
    }
}
