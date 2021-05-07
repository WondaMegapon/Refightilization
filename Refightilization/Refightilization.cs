using BepInEx;
using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace Wonda
{
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(guid, modName, version)]
    public class Refightilization : BaseUnityPlugin
    {
        // Setting up variables and waking the program up.
        //

        // Cool info B)
        const string guid = "com.Wonda.Refightilization";
        const string modName = "Refightilization";
        const string version = "1.0.0";

        // Config
        private RefightilizationConfig _config;

        // Struct to make players easier to manage
        public class PlayerStorage {
            public NetworkUser user;
            public CharacterMaster master;
            public GameObject origPrefab;
            public bool isDead;
        }

        public List<PlayerStorage> playerStorage = new List<PlayerStorage>();

        public void Awake()
        {
            _config = new RefightilizationConfig(Config);
            SetupHooks();
            Logger.LogInfo("Loaded Refightilization!");
        }

        // Hook setup.
        //

        // See!
        private void SetupHooks()
        {
            On.RoR2.Run.Start += Run_Start;
            On.RoR2.GlobalEventManager.OnPlayerCharacterDeath += GlobalEventManager_OnPlayerCharacterDeath;
            On.RoR2.Run.OnServerSceneChanged += Run_OnServerSceneChanged;
            On.RoR2.Run.OnUserAdded += Run_OnUserAdded;
            On.RoR2.TeleporterInteraction.OnInteractionBegin += TeleporterInteraction_OnInteractionBegin;
            On.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
        }

        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);
            SetupPlayers(); // Gotta make sure players are properly stored once the run begins. (todo: What if players join after the run starts?)
        }

        private void GlobalEventManager_OnPlayerCharacterDeath(On.RoR2.GlobalEventManager.orig_OnPlayerCharacterDeath orig, GlobalEventManager self, DamageReport damageReport, NetworkUser victimNetworkUser)
        {
            orig(self, damageReport, victimNetworkUser);
            Invoke("RespawnCheck", _config.RespawnDelay); // Spawning in players shortly after a delay.
        }

        private void Run_OnServerSceneChanged(On.RoR2.Run.orig_OnServerSceneChanged orig, Run self, string sceneName)
        {
            orig(self, sceneName);
            if (self.stageClearCount == 0) return; // Kinda pointless to reset prefabs before the stage begins.
            ResetPrefabs(); // Gotta make sure players respawn as their desired class.
        }

        private void Run_OnUserAdded(On.RoR2.Run.orig_OnUserAdded orig, Run self, NetworkUser user)
        {
            orig(self, user);
            if(Run.instance.time > 1f)
                SetupPlayers(false); // For players who enter the game late.
        }

        private void TeleporterInteraction_OnInteractionBegin(On.RoR2.TeleporterInteraction.orig_OnInteractionBegin orig, TeleporterInteraction self, Interactor activator)
        {
            if (_config.EnableRefightilization) { 
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.master.GetBody().gameObject == activator.gameObject && player.isDead && playerStorage.Count > 1) return; // If there's multiple players, then dead ones won't be able to activate the teleporter.
                }
            }
            orig(self, activator);
        }

        private void GenericPickupController_AttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
            foreach(PlayerStorage player in playerStorage)
            {
                if(player.master.GetBody().gameObject == body.gameObject && player.isDead)
                    player.master.GetBody().teamComponent.teamIndex = TeamIndex.Player; // Apparently you have to be on the player team to pick up items???
            }
            orig(self, body);
            foreach (PlayerStorage player in playerStorage)
            {
                if(player.master.GetBody().gameObject == body.gameObject && player.isDead)
                    player.master.GetBody().teamComponent.teamIndex = (TeamIndex)_config.RespawnTeam; // Set them back for posterity.
            }
        }

        // Beginning the *actual* custom code.
        //

        // Adding every player into the playerStorage list so we can easily refer to them and their original prefabs.
        private void SetupPlayers(bool StageUpdate = true)
        {
            Logger.LogInfo("Setting up players...");
            if(StageUpdate) playerStorage.Clear();
            foreach (PlayerCharacterMasterController playerCharacterMaster in PlayerCharacterMasterController.instances)
            {
                if(!StageUpdate && playerStorage != null)
                {
                    bool flag = false;
                    foreach (PlayerStorage player in playerStorage)
                    {
                        if (player.master == playerCharacterMaster.master)
                            flag = true;
                    }
                    if(flag) continue;
                }
                PlayerStorage newPlayer = new PlayerStorage();
                if(playerCharacterMaster.networkUser) newPlayer.user = playerCharacterMaster.networkUser;
                if(playerCharacterMaster.master) newPlayer.master = playerCharacterMaster.master;
                if(playerCharacterMaster.master.bodyPrefab) newPlayer.origPrefab = playerCharacterMaster.master.bodyPrefab;
                playerStorage.Add(newPlayer);
                Logger.LogInfo(newPlayer.user.userName + " added to PlayerStorage!");
            }
            Logger.LogInfo("Setting up players finished.");
            if(!StageUpdate) Invoke("RespawnCheck", _config.RespawnDelay + Mathf.Epsilon);
        }

        // Checking for dead players, and respawning them if it seems like they're respawnable.
        private void RespawnCheck()
        {
            Logger.LogInfo("Checking players...");

            // Wait... Yea disable functionality if the mod is disabled.
            if (!_config.EnableRefightilization)
            {
                Logger.LogInfo("Nevermind. Mod is disabled via config.");
                return;
            }
            
            // Okay, prep a flag for if everybody has died and kill the function if PlayerStorage is null for some reason.
            bool isEverybodyDead = true;
            if (playerStorage == null)
            {
                Logger.LogError("PlayerStorage is null!");
                return;
            }

            // Iterate through each player and confirm whether or not they've died.
            foreach (PlayerStorage player in playerStorage)
            {
                if (player == null)
                {
                    playerStorage.Remove(player); // This was quickly spledged in and is untested. It'll probably break *everything* if a player leaves mid-game.
                    continue;
                }

                if (player.master.IsDeadAndOutOfLivesServer())
                {
                    Logger.LogInfo(player.user.userName + " passed spawn check!");
                    if(!player.isDead)
                    {
                        player.isDead = true;
                        player.master.teamIndex = (TeamIndex)_config.RespawnTeam;
                    }
                    Respawn(player.master); // Begin respawning the player.
                }
                else
                {
                    Logger.LogInfo(player.user.userName + " failed spawn check.");
                    if(!player.isDead) isEverybodyDead = false; // Alright, cool. We don't have to kill everybody.
                }
            }

            Logger.LogInfo("Checking players complete.");
            if (isEverybodyDead && _config.EndGameWhenEverybodyDead)
            {
                Logger.LogInfo("Everybody is dead. Forcibly ending the game.");
                ResetPrefabs();
                Run.instance.BeginGameOver(RoR2Content.GameEndings.StandardLoss);
            }
        }

        // Respawning that player.
        private void Respawn(CharacterMaster player)
        {
            Logger.LogInfo("Attempting player respawn!");

            // Grabbing a random spawncard from the monster selection.
            SpawnCard monsterCard = ClassicStageInfo.instance.monsterSelection.Evaluate(Random.Range(0f, 1f)).spawnCard;
            if(monsterCard == null)
            {
                Logger.LogError("Spawn card is null!");
                Respawn(player);
                return;
            }

            // Grabbing the prefab from that monster card.
            GameObject randomMonster = BodyCatalog.FindBodyPrefab(monsterCard.prefab.name.Replace("Master", "Body"));
            if (randomMonster == null)
            {
                Logger.LogError("Random monster is null!");
                Respawn(player);
                return;
            }

            // Checking to see if the configuration has disabled the selected monster.
            if ((randomMonster.GetComponent<CharacterBody>().isChampion && !_config.AllowBosses) || (randomMonster.name == "ScavengerBody" && !_config.AllowScavengers) || (CheckBlacklist(randomMonster.name)))
            {
                Logger.LogInfo(randomMonster.name + " is disabled!");
                Respawn(player);
                return;
            }

            Logger.LogInfo("Found body " + randomMonster.name + ".");

            // Assigning the player to the selected monster prefab.
            player.bodyPrefab = randomMonster;
            Logger.LogInfo("Player was assigned to " + player.bodyPrefab.name + ".");

            // Selecting a random position on the node graph and respawning the player there.
            SceneInfo.instance.groundNodes.GetNodePosition(new RoR2.Navigation.NodeGraph.NodeIndex(Random.Range(0, SceneInfo.instance.groundNodes.GetNodeCount() - 1)), out Vector3 newPos);
            player.Respawn(newPos, Quaternion.identity);
            Logger.LogInfo("Respawned " + player.name + "!");

            // Stat changes to allow players to not die instantly when they get into the game.
            player.GetBody().baseMaxHealth *= _config.RespawnHealthMultiplier;
            player.GetBody().baseDamage *= _config.RespawnDamageMultiplier;
            if(player.GetBody().GetComponent<DeathRewards>()) player.GetBody().GetComponent<DeathRewards>().goldReward *= (uint)_config.RespawnMoneyMultiplier;
            player.GetBody().baseRegen = 1f;
            player.GetBody().levelRegen = 0.2f;

            // Some fun stuff to allow players to easily get back into combat.
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.ArmorBoost, 30f);
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.CloakSpeed, 30f);

            // And Affixes if the player is lucky.
            if(Util.CheckRoll(_config.RespawnAffixChance, player.playerCharacterMasterController.master))
            {
                int i = Random.Range(0, 5);
                BuffDef selectedBuff;
                switch(i)
                {
                    case 0:
                        selectedBuff = RoR2Content.Buffs.AffixRed;
                        break;
                    case 1:
                        selectedBuff = RoR2Content.Buffs.AffixBlue;
                        break;
                    case 2:
                        selectedBuff = RoR2Content.Buffs.AffixWhite;
                        break;
                    case 3:
                        selectedBuff = RoR2Content.Buffs.AffixPoison;
                        break;
                    case 4:
                        selectedBuff = RoR2Content.Buffs.AffixHaunted;
                        break;
                    case 5:
                        selectedBuff = RoR2Content.Buffs.AffixLunar;
                        break;
                    default:
                        selectedBuff = RoR2Content.Buffs.AffixRed;
                        break;
                }
                player.GetBody().AddBuff(selectedBuff);
            }

            // Broadcasting it to everyone.
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage{ baseToken = "<style=cWorldEvent><sprite name=\"Skull\" tint=1> " + player.playerCharacterMasterController.networkUser.userName + " has inhabited the body of a " + player.GetBody().GetDisplayName() + "! <sprite name=\"Skull\" tint=1></style>" });
        }

        // Making sure that players are set back to their original prefabs.
        private void ResetPrefabs()
        {
            Logger.LogInfo("Resetting player prefabs...");

            // Same as always. Never know what'll cause an NRE.
            if(playerStorage == null)
            {
                Logger.LogError("PlayerStorage is null!");
                return;
            }

            // Iterating through every player and set all the things we've changed back.
            foreach (PlayerStorage player in playerStorage)
            {
                if(player.isDead)
                {
                    player.master.bodyPrefab = player.origPrefab;
                    player.master.preventGameOver = true;
                    player.master.teamIndex = TeamIndex.Player;
                    player.isDead = false;
                }
                Logger.LogInfo(player.user.userName + "'s prefab reset.");
            }
            SetupPlayers();
            Logger.LogInfo("Reset player prefabs!");
        }

        private bool CheckBlacklist(string name)
        {
            bool flag = false;
            foreach(string enemy in _config.BlacklistedEnemies)
            {
                if (name == enemy) flag = true;
            }
            return flag;
        }
    }
}
