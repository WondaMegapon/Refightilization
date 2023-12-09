using BepInEx;
using RoR2;
using RoR2.Navigation;
using R2API.Utils;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using UnityEngine;
using VarianceAPI;
using VarianceAPI.Components;
using VarianceAPI.ScriptableObjects;

namespace Wonda
{
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.Nebby.VarianceAPI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.ThinkInvisible.TILER2", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.ThinkInvisible.ClassicItems", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.DestroyedClone.AncientScepter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(guid, modName, version)]
    public class Refightilization : BaseUnityPlugin
    {
        // Setting up variables and waking the program up.
        //

        // Cool info B)
        const string guid = "com.Wonda.Refightilization";
        const string modName = "Refightilization";
        const string version = "1.2.0";

        // Config
        private RefightilizationConfig _config;

        // Language Management Stuff
        private RefightilizationLanguage _language;

        // Additional mods to hook into for balance reasons.
        private PluginInfo _varianceAPI;
        private PluginInfo _classicItems;
        private PluginInfo _standaloneAncientScepter;

        // Class to make players easier to manage
        public class PlayerStorage
        {
            public NetworkUser user = null;
            public CharacterMaster master = null;
            public GameObject origPrefab = null;
            public bool isDead = false;
            public Inventory inventory = null;
            public bool giftedAffix = false;
            public bool hadAncientScepter = false;
            public EquipmentIndex previousEquipment = EquipmentIndex.None;
            public NetworkUser lastDamagedBy = null;
            public float lastDamagedTime = 0;
        }

        // The actual class to use.
        public List<PlayerStorage> playerStorage = new List<PlayerStorage>();

        // Internal, hardcoded monster lists.
        private List<GameObject> voidWhitelist = new List<GameObject>();
        private List<GameObject> finalBossWhitelist = new List<GameObject>();

        // Hard-coded Functions for Respawn to not clear the player prefab.
        private List<string> respawnMethodCheck = new List<string>() { "RefightRespawn" };

        // Misc variables
        public float respawnTime; // For an added penalty per death.
        private int respawnLoops; // Will break out of the function if it runs into too many of these.
        public List<GameObject> currEnemyWhitelist = new List<GameObject>(); // For optimization, keeping track of the current stage's whitelist.
        public List<GameObject> currSpecialEnemyWhitelist = new List<GameObject>(); // For optimization, keeping track of the current stage's whitelist.
        public List<EquipmentIndex> currEliteWhitelist = new List<EquipmentIndex>(); // Another optimization, keeping track of the current stage's elites.
        public bool moonDisabled; // For disabling spawning on the moon due to softlocks.

        public void Awake()
        {
            _config = new RefightilizationConfig(Config);
            SetupHooks();
            respawnMethodCheck.AddRange(_config.PreventPrefabResetMethods);
            Logger.LogInfo("Loaded Refightilization!");
        }

        public void Start()
        {
            // Setting up all the mod compatibility.
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.Nebby.VarianceAPI", out var varianceAPI)) _varianceAPI = varianceAPI;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.ThinkInvisible.ClassicItems", out var classicItemsPlugin)) _classicItems = classicItemsPlugin;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.DestroyedClone.AncientScepter", out var standaloneAncientScepterPlugin)) _standaloneAncientScepter = standaloneAncientScepterPlugin;
        }

        // Hook setup.
        //

        // See!
        private void SetupHooks()
        {
            On.RoR2.Run.Start += Run_Start;
            On.RoR2.GlobalEventManager.OnPlayerCharacterDeath += GlobalEventManager_OnPlayerCharacterDeath;
            RoR2.GlobalEventManager.onServerDamageDealt += GlobalEventManager_onServerDamageDealt;
            On.RoR2.Run.OnServerSceneChanged += Run_OnServerSceneChanged;
            On.RoR2.Run.OnUserAdded += Run_OnUserAdded;
            On.RoR2.Run.OnUserRemoved += Run_OnUserRemoved;
            On.RoR2.TeleporterInteraction.OnInteractionBegin += TeleporterInteraction_OnInteractionBegin;
            On.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
            On.RoR2.CharacterMaster.Respawn += CharacterMaster_Respawn;
            On.RoR2.CharacterMaster.PickRandomSurvivorBodyPrefab += CharacterMaster_PickRandomSurvivorBodyPrefab;
            On.RoR2.Run.BeginGameOver += Run_BeginGameOver;
            On.RoR2.InfiniteTowerRun.OnWaveAllEnemiesDefeatedServer += InfiniteTowerRun_OnWaveAllEnemiesDefeatedServer;
        }

        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);
            if (!_config.EnableRefightilization) return;
            respawnTime = _config.RespawnDelay; // Making sure that this function exists on a per run basis.
            SetupPlayers(); // Gotta make sure players are properly stored once the run begins.
            SetupLang(); // For all of our wacky lines we need said.
            moonDisabled = false; // Moonless
        }

        private void GlobalEventManager_OnPlayerCharacterDeath(On.RoR2.GlobalEventManager.orig_OnPlayerCharacterDeath orig, GlobalEventManager self, DamageReport damageReport, NetworkUser victimNetworkUser)
        {
            if (_config.EnableRefightilization) RemoveMonsterVariantItems(victimNetworkUser.master); // Was player previously a monster variant? Gotta take away those items if the server owner wants that.
            orig(self, damageReport, victimNetworkUser);
            if (!_config.EnableRefightilization) return;

            // Handling PvP and if a monster successfully kills a player.
            if (_config.MurderRevive)
            {
                PlayerStorage victimStorage = FindPlayerStorage(victimNetworkUser.master);
                PlayerStorage attackerStorage = null;
                if (victimStorage != null && victimStorage.lastDamagedBy != null && FindPlayerStorage(victimStorage.lastDamagedBy.master) != null) attackerStorage = FindPlayerStorage(victimStorage.lastDamagedBy.master);

                // It feels *awful* nesting if statements, but this is the best I can do.
                if (victimStorage != null && !victimStorage.isDead && // Handling the Victim
                attackerStorage != null && attackerStorage.isDead && // Handling the Attacker
                Time.time <= victimStorage.lastDamagedTime + _config.MurderWindow) // Handling the time window
                {
                    CleanPlayer(attackerStorage);
                    attackerStorage.master.RespawnExtraLife(); // Respawning the attacker with the extra life effect.
                    attackerStorage.master.inventory.RemoveItem(RoR2Content.Items.ExtraLifeConsumed); // Immediately removing the item that the extra life effect spawns.

                    string messageText = _language.RevengeMessages[Random.Range(0, _language.RevengeMessages.Count - 1)]; // Grabbing a random revenge message.
                    messageText = messageText.Replace("{0}", attackerStorage.master.playerCharacterMasterController.networkUser.userName); // Setting 0 to be the attacker.
                    messageText = messageText.Replace("{1}", victimStorage.master.playerCharacterMasterController.networkUser.userName); // Setting 1 to be the victim.

                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cWorldEvent><sprite name=\"Skull\" tint=1> " + messageText + " <sprite name=\"Skull\" tint=1></style>" });
                }
            }

            StartCoroutine(RespawnCheck(victimNetworkUser.master.transform.position, respawnTime)); // Spawning in players shortly after a delay.
            respawnTime += _config.AdditionalRespawnTime;
        }

        private void GlobalEventManager_onServerDamageDealt(DamageReport report)
        {
            // The usual "if enabled" check.
            if (!_config.EnableRefightilization && !_config.MurderRevive) return;

            // Confirming that the damage report contains network users.
            if (FindPlayerStorage(report.attackerMaster) != null && FindPlayerStorage(report.victimMaster) != null)
            {
                PlayerStorage victim = FindPlayerStorage(report.victimMaster); // Grabbing the victim.
                victim.lastDamagedBy = FindPlayerStorage(report.attackerMaster).user; // Overriding the victim's lastDamagedBy to be the attacker.
                victim.lastDamagedTime = Time.time; // Setting the time they were last damaged to now.
            }
        }

        private void Run_OnServerSceneChanged(On.RoR2.Run.orig_OnServerSceneChanged orig, Run self, string sceneName)
        {
            if (_config.EnableRefightilization) StopCoroutine(RespawnCheck());
            orig(self, sceneName);
            if (!_config.EnableRefightilization) return; // Kinda pointless to do anything if the mod is disabled.
            if (sceneName.Equals("moon") || sceneName.Equals("moon2")) moonDisabled = true;
            if (self.stageClearCount > 0) ResetPrefabs(); // Gotta make sure players respawn as their desired class.
            Invoke("UpdateStageWhitelist", 1f); // Gotta make sure we have an accurate monster selection.
            Invoke("UpdateEliteWhitelist", 1f); // Gotta make sure we have an accurate elite selection.
            respawnTime = _config.RespawnDelay; // Setting our wacky respawn delay.
        }

        private void Run_OnUserAdded(On.RoR2.Run.orig_OnUserAdded orig, Run self, NetworkUser user)
        {
            orig(self, user);
            if (Run.instance.time > 1f && _config.EnableRefightilization)
                SetupPlayers(false); // For players who enter the game late.
        }

        private void Run_OnUserRemoved(On.RoR2.Run.orig_OnUserRemoved orig, Run self, NetworkUser user)
        {
            orig(self, user);
            if (Run.instance.time > 1f && _config.EnableRefightilization)
            {
                PlayerStorage target = null;
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.user.userName == user.userName)
                    {
                        target = player;
                        Logger.LogInfo(user.userName + " left. Removing them from PlayerStorage.");
                        break;
                    }
                }
                playerStorage.Remove(target);
            }
        }

        private void TeleporterInteraction_OnInteractionBegin(On.RoR2.TeleporterInteraction.orig_OnInteractionBegin orig, TeleporterInteraction self, Interactor activator)
        {
            if (_config.EnableRefightilization)
            {
                foreach (PlayerStorage player in playerStorage)
                {
                    if (self.isCharged) RemoveMonsterVariantItems(player.master);
                    if (self.isCharged) StopCoroutine(RespawnCheck());
                    if (player.master != null && player.master.GetBody() != null && player.master.GetBody().gameObject == activator.gameObject && player.isDead && player.master.teamIndex != TeamIndex.Player && playerStorage.Count > 1) return; 
                    // If there's multiple players, then dead ones won't be able to activate the teleporter.
                }
            }
            orig(self, activator);
        }

        private void GenericPickupController_AttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
            if (_config.EnableRefightilization)
            {
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.master.GetBody() && player.master.GetBody().gameObject == body.gameObject && player.isDead && _config.ItemPickupToggle)
                        player.master.GetBody().teamComponent.teamIndex = TeamIndex.Player; // Apparently you have to be on the player team to pick up items???
                }
                orig(self, body);
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.master.GetBody() && player.master.GetBody().gameObject == body.gameObject && player.isDead && _config.ItemPickupToggle)
                        player.master.GetBody().teamComponent.teamIndex = (TeamIndex)_config.RespawnTeam; // Set them back for posterity.
                }
            }
            else orig(self, body);
        }

        private CharacterBody CharacterMaster_Respawn(On.RoR2.CharacterMaster.orig_Respawn orig, CharacterMaster self, Vector3 footPosition, Quaternion rotation)
        {
            if (_config.EnableRefightilization)
            {
                string currMethod = new StackFrame(2).GetMethod().Name;
                if (!respawnMethodCheck.Exists(x => x.Equals(currMethod)) && (FindPlayerStorage(self) != null))
                {
                    CleanPlayer(FindPlayerStorage(self));
                }
            }
            return orig(self, footPosition, rotation);
        }

        private GameObject CharacterMaster_PickRandomSurvivorBodyPrefab(On.RoR2.CharacterMaster.orig_PickRandomSurvivorBodyPrefab orig, Xoroshiro128Plus rng, NetworkUser networkUser, bool allowHidden)
        {
            // In-case Metamorphosis is enabled, we have to make sure that the monster is the one that respawns and not the survivor.
            if (_config.EnableRefightilization) {
                string currMethod = new StackFrame(6).GetMethod().Name;
                if (currMethod == "RefightRespawn")
                {
                    return currEnemyWhitelist[Random.Range(0, currEnemyWhitelist.Count - 1)];
                }
            }
            return orig(rng, networkUser, allowHidden);
        }

        private void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            if (!(_config.EnableRefightilization && !_config.EndGameWhenEverybodyDead)) {
                StopCoroutine(RespawnCheck()); // We no longer need to check for Respawns
                ResetPrefabs(); // Gotta clean up every player.
                orig(self, gameEndingDef);
            }
        }

        private void InfiniteTowerRun_OnWaveAllEnemiesDefeatedServer(On.RoR2.InfiniteTowerRun.orig_OnWaveAllEnemiesDefeatedServer orig, InfiniteTowerRun self, InfiniteTowerWaveController wc)
        {
            foreach(PlayerStorage player in playerStorage)
            {
                if (player == null)
                {
                    Logger.LogInfo("Player is null! Continuing.");
                    continue;
                }
                if (player.isDead)
                {
                    player.master.TrueKill();
                }
            }
            orig(self, wc);
        }

        // Not yet the end of hooks, but up here are setup functions, and I need a place to shuffle through language info when neccesary.
        private void SetupLang()
        {
            _language = new RefightilizationLanguage();
        }

        // Beginning the *actual* custom code.
        //

        // Adding every player into the playerStorage list so we can easily refer to them and their original prefabs.
        private void SetupPlayers(bool StageUpdate = true)
        {
            Logger.LogInfo("Setting up players...");
            if (StageUpdate) playerStorage.Clear();
            foreach (PlayerCharacterMasterController playerCharacterMaster in PlayerCharacterMasterController.instances)
            {
                // Skipping over Disconnected Players.
                if (playerStorage != null && playerCharacterMaster.networkUser == null)
                {
                    Logger.LogInfo("A player disconnected! Skipping over what remains of them...");
                    continue;
                }

                // If this is ran mid-stage, just skip over existing players and add anybody who joined.
                if (!StageUpdate && playerStorage != null)
                {
                    // Skipping over players that are already in the game.
                    bool flag = false;
                    foreach (PlayerStorage player in playerStorage)
                    {
                        if (player.master == playerCharacterMaster.master)
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (flag) continue;
                }
                PlayerStorage newPlayer = new PlayerStorage();
                if (playerCharacterMaster.networkUser) newPlayer.user = playerCharacterMaster.networkUser;
                if (playerCharacterMaster.master) newPlayer.master = playerCharacterMaster.master;
                if (playerCharacterMaster.master.bodyPrefab) newPlayer.origPrefab = playerCharacterMaster.master.bodyPrefab;
                if (playerCharacterMaster.master.inventory) newPlayer.inventory = new Inventory();
                playerStorage.Add(newPlayer);
                Logger.LogInfo(newPlayer.user.userName + " added to PlayerStorage!");
            }
            Logger.LogInfo("Setting up players finished.");
            if (!StageUpdate) StartCoroutine(RespawnCheck(new Vector3(0, 0, 0), respawnTime));
        }

        // Checking for dead players, and respawning them if it seems like they're respawnable.
        IEnumerator RespawnCheck(Vector3 deathPos = new Vector3(), float waitTime = 1f)
        {
            Logger.LogInfo("Respawn called! Waiting " + waitTime + " seconds!");

            yield return new WaitForSeconds(waitTime);

            // ABORT ABORT THE GAME DOESN'T EXIST
            if (!Run.instance.isActiveAndEnabled) yield break;

            // Wait... Yea disable functionality if the mod is disabled.
            if (!_config.EnableRefightilization)
            {
                Logger.LogInfo("Nevermind. Mod is disabled via config.");
                yield break;
            }

            // Gotta prevent a major issue with players respawning after a teleporter event, causing the game to be over.
            if (_config.NoRespawnsAfterTeleporter && TeleporterInteraction.instance && TeleporterInteraction.instance.isCharged)
            {
                Logger.LogInfo("Respawning after teleporter is disabled!");
                yield break;
            }

            // Aaand moon prevention.
            if (moonDisabled)
            {
                Logger.LogInfo("Respawn prevented due to current stage.");
                yield break;
            }

            Logger.LogInfo("Checking players...");

            // Okay, prep a flag for if everybody has died and kill the function if PlayerStorage is null for some reason.
            bool isEverybodyDead = true;
            if (playerStorage == null)
            {
                Logger.LogError("PlayerStorage is null!");
                yield break;
            }

            // Iterate through each player and confirm whether or not they've died.

            foreach (PlayerStorage player in playerStorage)
            {
                if (player == null)
                {
                    Logger.LogInfo("Player doesn't exist! Skipping...");
                    playerStorage.Remove(player); // This was quickly spledged in and is untested. It'll probably break *everything* if a player leaves mid-game... It probably does already.
                    RespawnCheck(deathPos, waitTime);
                    yield break;
                }

                if (player.master.IsDeadAndOutOfLivesServer())
                {
                    Logger.LogInfo(player.user.userName + " passed spawn check!");

                    // Testing to see if the player is preventing us from having a game-over. (Possible endless loop if we aren't careful?)
                    if (player.master.preventGameOver)
                    {
                        Logger.LogInfo("...except " + player.user.userName + " is preventing a game over, so we'll check again in a few seconds.");
                        isEverybodyDead = false; // Gotta respect the prevented game over.
                        StartCoroutine(RespawnCheck(new Vector3(0, 0, 0), (waitTime + 1) * 2)); // Prepping our next check, but a lil' later in case of a loop.
                        continue; // Skipping right over anything else in-case it's important.
                    }

                    // Hey! This player hasn't died before!
                    if (!player.isDead)
                    {
                        player.isDead = true; // They died!
                        player.inventory.CopyItemsFrom(player.master.inventory); // Copy all their items.
                    }

                    player.master.teamIndex = (TeamIndex)_config.RespawnTeam; // Moved out here in-case config is changed mid-game.
                    ResetMinionTeam(player.master);
                    respawnLoops = 0; // Setting our loops in-case something breaks in RefightRespawn.
                    RefightRespawn(player.master, deathPos); // Begin respawning the player.
                }
                else
                {
                    Logger.LogInfo(player.user.userName + " failed spawn check.");
                    if (!player.isDead) isEverybodyDead = false; // Alright, cool. We don't have to kill everybody.
                }
            }

            Logger.LogInfo("Checking players complete.");
            if (isEverybodyDead && !(TeleporterInteraction.instance != null && TeleporterInteraction.instance.isInFinalSequence) && _config.EndGameWhenEverybodyDead)
            {
                Logger.LogInfo("Everybody is dead. Forcibly ending the game.");
                ResetPrefabs(); // Resetting all those prefabs so the game-end screen would be accurate.
                Run.instance.BeginGameOver(RoR2Content.GameEndings.StandardLoss); // Woooo! Begin that game over!
            }

            yield break;
        }

        // Respawning that player.
        private void RefightRespawn(CharacterMaster player, Vector3 deathPos)
        {
            Logger.LogInfo("Attempting player respawn!");

            // Fun fact: I pushed a build without this. This is vital to anything working and it completly slipped my mind. It helps us track how many loops we're in :).
            respawnLoops++;

            // Catching if we're in the middle of an infinite loop.
            if (respawnLoops > 99)
            {
                Logger.LogError("INFINITE LOOP CAUGHT! Please file a bug report for Refightilization! This is not intended behavior.");
                return;
            }

            // Apparently there's an NRE that can happen within this method, so I'm prepping for that possible event.
            if (player == null)
            {
                Logger.LogInfo("Player is null!? Aborting Respawn.");
                return;
            }

            // Also chucking out any player who isn't currently connected to the server.
            if (player.playerCharacterMasterController.networkUser == null)
            {
                Logger.LogInfo("Player doesn't have a networkUser!? Aborting Respawn.");
                return;
            }

            // Was this player assigned an affix by us?
            if (FindPlayerStorage(player).giftedAffix)
            {
                Logger.LogInfo("Yoinking that Affix.");
                player.inventory.SetEquipmentIndex(FindPlayerStorage(player).previousEquipment);
                FindPlayerStorage(player).giftedAffix = false;
            }

            // Another optimization, to prevent the game from looping over several repeated monsters.
            List<GameObject> tempEnemyWhitelist = currEnemyWhitelist;
            if (Util.CheckRoll(5f, player.playerCharacterMasterController.master) && currSpecialEnemyWhitelist.Count >= 1) tempEnemyWhitelist = new List<GameObject>(currSpecialEnemyWhitelist);
            if (Util.CheckRoll(0.1f, player.playerCharacterMasterController.master) && finalBossWhitelist.Count >= 1) tempEnemyWhitelist = new List<GameObject>(finalBossWhitelist);

            if (_config.NoRepeatRespawns && tempEnemyWhitelist.Count > 1) tempEnemyWhitelist.Remove(tempEnemyWhitelist.Where(entity => entity.name == player.bodyPrefab.name).FirstOrDefault());

            GameObject randomMonster = tempEnemyWhitelist[Random.Range(0, tempEnemyWhitelist.Count - 1)];

            Logger.LogInfo("Found body " + randomMonster.name + ".");

            // Do we have an Ancient Scepter?
            TakeScepter(player);
            Logger.LogInfo("Checked for Ancient Scepter.");

            // Assigning the player to the selected monster prefab.
            if (player.bodyPrefab && randomMonster)
            {
                player.bodyPrefab = randomMonster;
            }
            else
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " has no bodyPrefab!? Retrying...");
                RefightRespawn(player, deathPos);
                return;
            }
            Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " was assigned to " + player.bodyPrefab.name + ".");

            // Grabbing a viable position for the player to spawn in.
            Vector3 newPos = deathPos; // Starting with where the player dies, if all else fails.

            // Radius for our spawning.
            Vector3 spawnRadius = new Vector3(Random.Range(-5, 5), Random.Range(-5, 5), Random.Range(-2, 2));

            // Grabbing a random active player's location.
            System.Random r = new System.Random();
            foreach (PlayerCharacterMasterController viableSpawn in PlayerCharacterMasterController.instances.OrderBy(x => r.Next()))
            {
                if (!viableSpawn.master.IsDeadAndOutOfLivesServer() && viableSpawn.master != null)
                { 
                    newPos = viableSpawn.master.transform.position + spawnRadius;
                    break;
                }
            }

            // If we can't find a position near a player, we'll try a position near the teleporter.
            if (newPos == deathPos && TeleporterInteraction.instance) newPos = TeleporterInteraction.instance.transform.position + spawnRadius;

            // Grabbing a safe location nearby that location.
            //newPos = TeleportHelper.FindSafeTeleportDestination(newPos, player.bodyPrefab.GetComponent<CharacterBody>(), RoR2Application.rng) ?? newPos;

            // Respawning that player!
            player.Respawn(GrabNearestNodePosition(newPos), Quaternion.identity);
            Logger.LogInfo("Respawned " + player.playerCharacterMasterController.networkUser.userName + "!");

            // Catching Monster Variants if the host has it disabled.
            if(!_config.RespawnAsMonsterVariants) RemoveMonsterVariantItems(player);

            // Stat changes to allow players to not die instantly when they get into the game.
            player.GetBody().baseMaxHealth *= _config.RespawnHealthMultiplier;
            player.GetBody().baseDamage *= _config.RespawnDamageMultiplier;
            if (player.GetBody().GetComponent<DeathRewards>()) player.GetBody().GetComponent<DeathRewards>().goldReward *= (uint)_config.RespawnMoneyMultiplier;
            player.GetBody().baseRegen = 1f;
            player.GetBody().levelRegen = 0.2f;
            Logger.LogInfo("Applied stats.");

            // Let's just double-check that the player has an Interactor.
            if (!player.GetBody().GetComponent<InteractionDriver>())
            {
                player.GetBody().gameObject.AddComponent<InteractionDriver>();
            }

            // Oh! And fixing up their interactor so they can reach things that are just a *little* outta reach*.
            if (player.GetBody().GetComponent<Interactor>())
            {
                Interactor interactor = player.GetBody().GetComponent<Interactor>();

                // Getting the player's model size and using that to base the selection size off of.
                Vector3 vec = Vector3.Scale(player.GetBody().modelLocator.modelTransform.GetComponent<CharacterModel>().mainSkinnedMeshRenderer.bounds.size, player.GetBody().modelLocator.transform.localScale);
                float var = Mathf.Max(Mathf.Max(vec.x, vec.y), vec.z);
                interactor.maxInteractionDistance = var;

                // Next, we give flying enemies extra distance.
                if (player.GetBody().isFlying)
                    interactor.maxInteractionDistance *= 1.5f;
            }

            // Some fun stuff to allow players to easily get back into combat.
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.ArmorBoost, 15f);
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.CloakSpeed, 30f);
            Logger.LogInfo("Applied buffs.");

            // And Affixes if the player is lucky.
            if (Util.CheckRoll(_config.RespawnAffixChance, player.playerCharacterMasterController.master) || RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.eliteOnlyArtifactDef))
            {
                if (player.inventory.GetEquipmentIndex() != EquipmentIndex.None && !_config.ForceGrantAffix) return; // If the player already has an equipment, just skip over 'em.

                if (_config.ForceGrantAffix) FindPlayerStorage(player).previousEquipment = player.inventory.GetEquipmentIndex(); // Record their current equipment if ForceGrantAffix is enabled 
                int i = Random.Range(0, currEliteWhitelist.Count - 1); // Pick a random Elite index.
                player.inventory.SetEquipmentIndex(currEliteWhitelist[i]); // Apply that equipment.
                FindPlayerStorage(player).giftedAffix = _config.TakeAffix; // Set a variable to take it away.
                Logger.LogInfo("Gifted affix.");
            }

            // Resetting the amount of loops that we've done. 
            respawnLoops = 0;

            // Broadcasting it to everyone.
            if (_config.AnnounceRespawns) {
                string messageText = _language.ReviveMessages[Random.Range(0, _language.ReviveMessages.Count - 1)];
                messageText = messageText.Replace("{0}", player.playerCharacterMasterController.networkUser.userName);
                messageText = messageText.Replace("{1}", player.GetBody().GetDisplayName());

                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cWorldEvent><sprite name=\"Skull\" tint=1> " + messageText + " <sprite name=\"Skull\" tint=1></style>" });
            } 
        }

        // Making sure that players are set back to their original prefabs.
        private void ResetPrefabs()
        {
            Logger.LogInfo("Resetting player prefabs...");

            // Same as always. Never know what'll cause an NRE.
            if (playerStorage == null)
            {
                Logger.LogError("PlayerStorage is null!");
                return;
            }

            // Iterating through every player and set all the things we've changed back.
            foreach (PlayerStorage player in playerStorage)
            {
                if (player == null)
                {
                    Logger.LogInfo("Player is null! Continuing.");
                    continue;
                }
                if (player.isDead) CleanPlayer(player);
                Logger.LogInfo(player.user.userName + "'s prefab reset.");
            }
            SetupPlayers();
            Logger.LogInfo("Reset player prefabs!");
        }

        // Removes items from players.
        private void CleanPlayer(PlayerStorage player)
        {
            player.master.bodyPrefab = player.origPrefab; // Resetting their prefab.
            player.master.teamIndex = TeamIndex.Player; // Putting them back on the player team.
            ResetMinionTeam(player.master);

            // Taking back that affix.
            if (player.giftedAffix)
            {
                player.master.inventory.SetEquipmentIndex(player.previousEquipment);
                player.giftedAffix = false;
            }

            // Refer to the function names.
            RemoveMonsterVariantItems(player.master);
            StartCoroutine(GiveScepterWait(player.master, 1f));

            // Yay! They're no longer dead!
            player.isDead = false;
        }

        // Updating our Whitelist of monsters at stage generation.
        private void UpdateStageWhitelist()
        {
            Logger.LogInfo("Updating the stage whitelist.");

            // Clearing the old stage whitelist.
            currEnemyWhitelist.Clear();
            Logger.LogInfo("Whitelist cleared.");

            if(ClassicStageInfo.instance == null || ClassicStageInfo.instance.monsterSelection == null || ClassicStageInfo.instance.monsterSelection.choices == null)
            {
                Logger.LogInfo("There is no available monster selection!");
                if(VoidRaidGauntletController.instance)
                {
                    Logger.LogInfo("Void raid detected! Forcing monster selection...");

                    currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("NullifierBody"));
                    currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("VoidJailerBody"));
                    currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("VoidMegaCrabBody"));
                }
            }
            else
            {
                Logger.LogInfo("Selection isn't null.");

                // Grabbing a reference liiist.
                List<WeightedSelection<DirectorCard>.ChoiceInfo> list = ClassicStageInfo.instance.monsterSelection.choices.ToList();

                // Beginning a nice lil' for loop.
                foreach (var choice in list)
                {
                    Logger.LogInfo("Testing choice.");

                    // First First, we check to see if choice and value are null. Never leave an NRE unturned.
                    if (choice.value == null) continue;

                    // First, we grab the SpawnCard of our monster.
                    SpawnCard currMonster = choice.value.spawnCard;
                    if (currMonster == null) continue;

                    // Then, we check to see if the monster has a body.
                    GameObject currMonsterBody = BodyCatalog.FindBodyPrefab(currMonster.prefab.name.Replace("Master", "Body"));
                    if (currMonsterBody == null) continue;
                    Logger.LogInfo("We have found " + currMonsterBody.name + ".");

                    // Nuking any unwanted Champions.
                    if (!(_config.AllowBosses && Run.instance.loopClearCount >= _config.BossRequiredLoopCount) && currMonsterBody.GetComponent<CharacterBody>().isChampion) 
                    {
                        if(_config.AllowBosses) currSpecialEnemyWhitelist.Add(currMonsterBody); // We have to let a player have fun every *once* in a while.
                        continue;
                    }

                    // Nuking any unwanted Scavangers.
                    if (!(_config.AllowScavengers && Run.instance.loopClearCount >= _config.ScavangerRequiredLoopCount) && currMonsterBody.name == "ScavengerBody") continue;

                    // Is it in our Blacklist?
                    if (CheckBlacklist(currMonsterBody.name)) continue;

                    // Add that rad dude!
                    currEnemyWhitelist.Add(currMonsterBody);
                    Logger.LogInfo("Adding " + currMonsterBody.name + " to the currWhitelist.");
                }
            }

            // Populating the Final Bosses list.
            finalBossWhitelist.Add(BodyCatalog.FindBodyPrefab("BrotherBody"));
            finalBossWhitelist.Add(BodyCatalog.FindBodyPrefab("MiniVoidRaidCrabBodyBase"));

            // Bring out the backup dude if nothing works.
            if (currEnemyWhitelist.Count <= 0) currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("LemurianBody"));
            Logger.LogInfo("Done updating Whitelist.");
        }

        // Updating the *other* whitelist.
        private void UpdateEliteWhitelist()
        {
            Logger.LogInfo("Updating the Elite whitelist.");

            // Checking to see if we even have a combat director in the first place.
            if(CombatDirector.instancesList == null && CombatDirector.instancesList[0] == null) return;

            // Clearing the old whitelist.
            currEliteWhitelist.Clear();

            // Grabbing our reference to EliteTiers
            CombatDirector.EliteTierDef[] refDef = CombatDirector.eliteTiers;

            // A final lil' check to see if our EliteTiers are null.
            if(refDef == null || refDef.Length <= 0) return;

            // The big bad loop. First iterating over all the main tiers.
            for (var i = 0; i < refDef.Length; i++)
            {
                // If the tier is available, then we chuck it in.
                if (refDef[i].isAvailable(SpawnCard.EliteRules.Default) || refDef[i].isAvailable(SpawnCard.EliteRules.Lunar))
                {
                    // Iterating over each individual elite.
                    for (var v = 0; v < refDef[i].eliteTypes.Length; v++)
                    {
                        // Checking to see if the elite type is even valid in the first place because there are inexplicable voids in the game.
                        if (refDef[i].eliteTypes[v] == null) continue;

                        // Checking to see if the elite has any valid equipment.
                        if (refDef[i].eliteTypes[v].eliteEquipmentDef == null) continue;

                        // Checking to see if it has a valid icon (it's the only real way I can tell if it's meant to be implemented or not)
                        if (refDef[i].eliteTypes[v].eliteEquipmentDef.pickupIconSprite == null) continue;

                        // Adding the index to our list.
                        currEliteWhitelist.Add(refDef[i].eliteTypes[v].eliteEquipmentDef.equipmentIndex);

                        Logger.LogInfo("Added " + refDef[i].eliteTypes[v].eliteEquipmentDef.nameToken + " to the pool.");
                    }
                }
            }
            Logger.LogInfo("Done updating Whitelist.");
        }

        // Utility Methods
        //

        // Mooooore stolen coooode. Using RoR's ambush generation code to grab the spawn position for players.
        private Vector3 GrabNearestNodePosition(Vector3 startPos)
        {
            NodeGraph groundNodes = SceneInfo.instance.groundNodes;
            NodeGraph.NodeIndex nodeIndex = groundNodes.FindClosestNode(startPos, HullClassification.BeetleQueen);
            NodeGraphSpider nodeGraphSpider = new NodeGraphSpider(groundNodes, HullMask.BeetleQueen);
            nodeGraphSpider.AddNodeForNextStep(nodeIndex);

            List<NodeGraphSpider.StepInfo> list = new List<NodeGraphSpider.StepInfo>();
            int num = 0;
            List<NodeGraphSpider.StepInfo> collectedSteps = nodeGraphSpider.collectedSteps;
            while (nodeGraphSpider.PerformStep() && num < 8)
            {
                num++;
                for (int i = 0; i < collectedSteps.Count; i++)
                {
                    list.Add(collectedSteps[i]);
                }
                collectedSteps.Clear();
            }
            groundNodes.GetNodePosition(list[Random.Range(0, list.Count - 1)].node, out Vector3 outPos);
            return outPos;
        }

        // A cheap and dirty way of checking to see if a string is in the blacklist.
        private bool CheckBlacklist(string name)
        {
            bool flag = false;
            foreach (string enemy in _config.BlacklistedEnemies)
            {
                if (name == enemy)
                {
                    flag = true;
                    break;
                }
            }
            return flag;
        }

        // Grabbing a certain player in the playerStorage object.
        private PlayerStorage FindPlayerStorage(CharacterMaster cMaster)
        {
            foreach (PlayerStorage player in playerStorage)
            {
                if (player.master == cMaster)
                {
                    return player;
                }
            }

            return null;
        }

        // For resetting the minions a character owns.
        private void ResetMinionTeam(CharacterMaster player)
        {
            if (player == null || player.minionOwnership == null || player.minionOwnership.group == null || player.minionOwnership.group.members.Length <= 0) return;

            MinionOwnership[] minionOwnership = FindObjectsOfType<MinionOwnership>();

            foreach (MinionOwnership minion in minionOwnership)
            {
                if (minion.ownerMaster == player) {
                    minion.GetComponent<CharacterMaster>().teamIndex = player.teamIndex;
                    minion.GetComponent<CharacterMaster>().Respawn(minion.transform.position, minion.transform.rotation);
                }
            }
        }

        // Code for handling other mods.
        //

        // One-upping myself by having a master function that can call sub-functions.
        private void RemoveMonsterVariantItems(CharacterMaster player)
        {
            if (_varianceAPI != null) { RemoveMonsterVariantItemsAPI(player); }
        }

        // Amazing code provided by Nebby. Thank you so much.
        private void RemoveMonsterVariantItemsAPI(CharacterMaster Player)
        {
            CharacterBody playerBody = Player.GetBody();
            if (!playerBody) return;

            VariantHandler variantHandler = playerBody.GetComponent<VariantHandler>();
            if (variantHandler)
            {
                List<VariantInfo> activeVariantInfos = variantHandler.VariantInfos.ToList();
                foreach (VariantInfo variantInfo in activeVariantInfos)
                {
                    if (variantInfo.variantInventory == null)
                    {
                        CheckForPurpleHealthBar(playerBody.inventory, variantInfo);
                        continue;
                    }
                    else if (variantInfo.variantInventory.ItemInventory.Length > 0)
                    {
                        VariantInventoryInfo.VariantInventory[] itemInventory = variantInfo.variantInventory.ItemInventory;
                        for (int i = 0; i < itemInventory.Length; i++)
                        {
                            var current = itemInventory[i];
                            ItemDef toRemove = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(current.itemDefName));
                            Player.inventory.RemoveItem(toRemove, current.amount);
                        }
                        CheckForPurpleHealthBar(playerBody.inventory, variantInfo);
                    }
                }
            }
        }

        //Removes the purple healthbar if the tier is greater than common.
        private void CheckForPurpleHealthBar(Inventory inventory, VariantInfo variantInfo)
        {
            var purpleHealthBar = VarianceAPI.Assets.VAPIAssets.LoadAsset<ItemDef>("PurpleHealthbar");
            if (variantInfo.variantTier >= VarianceAPI.VariantTier.Uncommon)
            {
                inventory.RemoveItem(purpleHealthBar, inventory.GetItemCount(purpleHealthBar));
            }
        }

        private void TakeScepter(CharacterMaster player)
        {
            if (_classicItems != null) TakeScepterCI(player);
            if (_standaloneAncientScepter != null) TakeScepterSAS(player);
        }

        private IEnumerator GiveScepterWait(CharacterMaster player, float time)
        {
            yield return new WaitForSeconds(time);
            GiveScepter(player);
        }

        private void GiveScepter(CharacterMaster player)
        {
            if (_classicItems != null) GiveScepterCI(player);
            if (_standaloneAncientScepter != null) GiveScepterSAS(player);
        }

        // This one is for Classic Items. It takes the player's Scepter.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void TakeScepterCI(CharacterMaster player)
        {
            /*if (player.inventory.GetItemCount(ThinkInvisible.ClassicItems.Scepter.instance.itemDef) > 0)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " has a Scepter. Taking it.");
                FindPlayerStorage(player).hadAncientScepter = true;
                player.inventory.RemoveItem(ThinkInvisible.ClassicItems.Scepter.instance.itemDef);
            }*/
        }

        // This one is for Classic Items. It gives the player's Scepter.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void GiveScepterCI(CharacterMaster player)
        {
            /*if (FindPlayerStorage(player).hadAncientScepter)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " had a Scepter. Returning it.");
                player.inventory.GiveItem(ThinkInvisible.ClassicItems.Scepter.instance.itemDef);
                FindPlayerStorage(player).hadAncientScepter = false;
            }*/
        }

        // This one is for Standalone AS. Same as before.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void TakeScepterSAS(CharacterMaster player)
        {
            /*if (player.inventory.GetItemCount(AncientScepter.AncientScepterItem.instance.ItemDef) > 0)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " has a Scepter. Taking it.");
                FindPlayerStorage(player).hadAncientScepter = true;
                player.inventory.RemoveItem(AncientScepter.AncientScepterItem.instance.ItemDef);
            }*/
        }

        // This one is for Standalone AS. Same as before.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void GiveScepterSAS(CharacterMaster player)
        {
            /*if (FindPlayerStorage(player).hadAncientScepter)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " had a Scepter. Returning it.");
                player.inventory.GiveItem(AncientScepter.AncientScepterItem.instance.ItemDef);
                FindPlayerStorage(player).hadAncientScepter = false;
            }*/
        }
    }
}
