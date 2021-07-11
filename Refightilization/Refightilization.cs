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
using VarianceAPI.Scriptables;

namespace Wonda
{
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rob.MonsterVariants", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Nebby.VarianceAPI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.ThinkInvisible.TILER2", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.ThinkInvisible.ClassicItems", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.DestroyedClone.AncientScepter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("com.kking117.ArtifactOfGrief")]
    [BepInPlugin(guid, modName, version)]
    public class Refightilization : BaseUnityPlugin
    {
        // Setting up variables and waking the program up.
        //

        // Cool info B)
        const string guid = "com.Wonda.Refightilization";
        const string modName = "Refightilization";
        const string version = "1.0.13";

        // Config
        private RefightilizationConfig _config;

        // Additional mods to hook into for balance reasons.
        private PluginInfo _monsterVariants;
        private PluginInfo _varianceAPI;
        private PluginInfo _classicItems;
        private PluginInfo _standaloneAncientScepter;

        // Class to make players easier to manage
        public class PlayerStorage
        {
            public NetworkUser user;
            public CharacterMaster master;
            public GameObject origPrefab;
            public bool isDead;
            public Inventory inventory;
            public bool giftedAffix;
            public bool hadAncientScepter;
        }

        // The actual class to use.
        public List<PlayerStorage> playerStorage = new List<PlayerStorage>();

        // Misc variables
        public float respawnTime; // For an added penalty per death.
        public bool metamorphosIsEnabled; // For tracking the artifact of the same name.
        private int respawnLoops; // Will break out of the function if it runs into too many of these.
        public List<GameObject> currWhitelist = new List<GameObject>(); // For optimization, keeping track of the current stage's whitelist.

        public void Awake()
        {
            _config = new RefightilizationConfig(Config);
            SetupHooks();
            Logger.LogInfo("Loaded Refightilization!");
        }

        public void Start()
        {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.rob.MonsterVariants", out var monsterVariantsPlugin)) _monsterVariants = monsterVariantsPlugin;
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
            On.RoR2.Run.OnServerSceneChanged += Run_OnServerSceneChanged;
            On.RoR2.Run.OnUserAdded += Run_OnUserAdded;
            On.RoR2.Run.OnUserRemoved += Run_OnUserRemoved;
            On.RoR2.TeleporterInteraction.OnInteractionBegin += TeleporterInteraction_OnInteractionBegin;
            On.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
            On.RoR2.CharacterMaster.Respawn += CharacterMaster_Respawn;
            if (_config.EndGameWhenEverybodyDead)
            {
                On.RoR2.Run.BeginGameOver += Run_BeginGameOver;
            }
            else
            {
                On.RoR2.Run.BeginGameOver += BlockGameOver;
            }
        }

        private void BlockGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            if (_config.EnableRefightilization) StopCoroutine(RespawnCheck());
        }

        private void Run_Start(On.RoR2.Run.orig_Start orig, Run self)
        {
            orig(self);
            if (!_config.EnableRefightilization) return;
            respawnTime = _config.RespawnDelay; // Making sure that this function exists on a per run basis.
            metamorphosIsEnabled = RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.randomSurvivorOnRespawnArtifactDef);
            SetupPlayers(); // Gotta make sure players are properly stored once the run begins.
        }

        private void GlobalEventManager_OnPlayerCharacterDeath(On.RoR2.GlobalEventManager.orig_OnPlayerCharacterDeath orig, GlobalEventManager self, DamageReport damageReport, NetworkUser victimNetworkUser)
        {
            if (_config.EnableRefightilization) RemoveMonsterVariantItems(victimNetworkUser.master); // Was player previously a monster variant? Gotta take away those items if the server owner wants that.
            orig(self, damageReport, victimNetworkUser);
            if (!_config.EnableRefightilization) return;
            if (_config.MurderRevive && FindPlayerStorage(damageReport.attackerMaster) != null && FindPlayerStorage(damageReport.attackerMaster).isDead && FindPlayerStorage(damageReport.victimMaster) != null && !FindPlayerStorage(damageReport.victimMaster).isDead)
            {
                CleanPlayer(FindPlayerStorage(damageReport.attackerMaster));
                damageReport.attackerMaster.RespawnExtraLife();
                damageReport.attackerMaster.inventory.RemoveItem(RoR2Content.Items.ExtraLifeConsumed);

                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cWorldEvent><sprite name=\"Skull\" tint=1> " + damageReport.attackerMaster.playerCharacterMasterController.networkUser.userName + " has killed " + damageReport.victimMaster.playerCharacterMasterController.networkUser.userName + "! They will live again! <sprite name=\"Skull\" tint=1></style>" });
            }
            StartCoroutine(RespawnCheck(victimNetworkUser.master.transform.position)); // Spawning in players shortly after a delay.
            respawnTime += _config.AdditionalRespawnTime;
        }

        private void Run_OnServerSceneChanged(On.RoR2.Run.orig_OnServerSceneChanged orig, Run self, string sceneName)
        {
            if (_config.EnableRefightilization) StopCoroutine(RespawnCheck());
            orig(self, sceneName);
            if (!_config.EnableRefightilization) return; // Kinda pointless to do anything if the mod is disabled.
            metamorphosIsEnabled = RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.randomSurvivorOnRespawnArtifactDef); // Double checking the current artifact
            if (self.stageClearCount > 0) ResetPrefabs(); // Gotta make sure players respawn as their desired class.
            Invoke("UpdateStageWhitelist", 1f); // Gotta make sure we have an accurate monster selection.
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

        private void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            if (_config.EnableRefightilization) StopCoroutine(RespawnCheck());
            orig(self, gameEndingDef);
        }

        private CharacterBody CharacterMaster_Respawn(On.RoR2.CharacterMaster.orig_Respawn orig, CharacterMaster self, Vector3 footPosition, Quaternion rotation)
        {
            if (_config.EnableRefightilization)
            {
                string currMethod = new StackFrame(2).GetMethod().Name;
                if (currMethod != "RefightRespawn" && (FindPlayerStorage(self) != null))
                {
                    CleanPlayer(FindPlayerStorage(self));
                }
            }
            return orig(self, footPosition, rotation);
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
            if (!StageUpdate) StartCoroutine(RespawnCheck(new Vector3(0, 0, 0)));
        }

        // Checking for dead players, and respawning them if it seems like they're respawnable.
        IEnumerator RespawnCheck(Vector3 deathPos = new Vector3())
        {
            Logger.LogInfo("Respawn called! Waiting " + respawnTime + " seconds!");

            yield return new WaitForSeconds(respawnTime);

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
                    RespawnCheck(deathPos);
                    yield break;
                }

                if (player.master.IsDeadAndOutOfLivesServer())
                {
                    Logger.LogInfo(player.user.userName + " passed spawn check!");
                    if (!player.isDead)
                    {
                        player.isDead = true;
                        player.master.teamIndex = (TeamIndex)_config.RespawnTeam;
                        player.inventory.CopyItemsFrom(player.master.inventory);
                        if (_config.RemoveAllItems) player.master.inventory.CopyItemsFrom(new Inventory());
                    }
                    respawnLoops = 0;
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
                ResetPrefabs();
                Run.instance.BeginGameOver(RoR2Content.GameEndings.StandardLoss);
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
                player.inventory.SetEquipmentIndex(EquipmentIndex.None);
                FindPlayerStorage(player).giftedAffix = false;
            }

            GameObject randomMonster = currWhitelist[Random.Range(0, currWhitelist.Count - 1)];

            // Allowing for an easy way to break out of this possible loop of not finding a proper monster.
            if (respawnLoops < _config.MaxRespawnTries)
            {
                // To prevent players from spawning in as the same monster twice in a row.
                if (randomMonster.name == player.bodyPrefab.name && _config.NoRepeatRespawns && currWhitelist.Count > 1)
                {
                    Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " was already " + randomMonster.name + ". Retrying Respawn.");
                    RefightRespawn(player, deathPos);
                    return;
                }
            }
            else
            {
                Logger.LogInfo("Too many retries! Forcibly assigning monster.");
            }

            Logger.LogInfo("Found body " + randomMonster.name + ".");

            // Is the Artifact of Metamorphosis enabled?
            if (metamorphosIsEnabled && player.inventory && RoR2Content.Items.InvadingDoppelganger && player.inventory.GetItemCount(RoR2Content.Items.InvadingDoppelganger) <= 0) player.inventory.GiveItem(RoR2Content.Items.InvadingDoppelganger);
            Logger.LogInfo("Checked for Metamorphosis.");

            // Do we have an Ancient Scepter?
            if (_classicItems != null)
                TakeScepterCI(player);
            if (_standaloneAncientScepter != null)
                TakeScepterSAS(player);
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

            // Attempting to find a position near a player.
            /*
            System.Random r = new System.Random();
            foreach (PlayerCharacterMasterController viableSpawn in PlayerCharacterMasterController.instances.OrderBy(x => r.Next()))
            {
                if (!viableSpawn.master.IsDeadAndOutOfLivesServer() && viableSpawn.master != null)
                { 
                    newPos = viableSpawn.master.transform.position + new Vector3(Random.Range(-5, 5), Random.Range(-5, 5), Random.Range(-2, 2));
                    break;
                }
            }
            */

            // If we can't find a position near a player, we'll try a position near the teleporter.
            if (newPos == deathPos && TeleporterInteraction.instance) newPos = TeleporterInteraction.instance.transform.position + new Vector3(Random.Range(-5, 5), Random.Range(-5, 5), Random.Range(-2, 2));

            // Respawning that player!
            player.Respawn(GrabNearestNodePosition(newPos), Quaternion.identity);
            Logger.LogInfo("Respawned " + player.playerCharacterMasterController.networkUser.userName + "!");

            // Catching Monster Variants if the host has it disabled.
            if (!_config.RespawnAsMonsterVariants) RemoveMonsterVariantItems(player);

            // Stat changes to allow players to not die instantly when they get into the game.
            player.GetBody().baseMaxHealth *= _config.RespawnHealthMultiplier;
            player.GetBody().baseDamage *= _config.RespawnDamageMultiplier;
            if (player.GetBody().GetComponent<DeathRewards>()) player.GetBody().GetComponent<DeathRewards>().goldReward *= (uint)_config.RespawnMoneyMultiplier;
            player.GetBody().baseRegen = 1f;
            player.GetBody().levelRegen = 0.2f;

            // Some fun stuff to allow players to easily get back into combat.
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.ArmorBoost, 15f);
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.CloakSpeed, 30f);

            // And Affixes if the player is lucky.
            if (Util.CheckRoll(_config.RespawnAffixChance, player.playerCharacterMasterController.master) || RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.eliteOnlyArtifactDef))
            {
                if (player.inventory.GetEquipmentIndex() != EquipmentIndex.None) return; // If the player already has an equipment, just skip over 'em.

                int i = Random.Range(0, EliteCatalog.eliteList.Count - 1); // Pick a random Elite index.
                EquipmentIndex selectedBuff = EliteCatalog.GetEliteDef(EliteCatalog.eliteList[i]).eliteEquipmentDef.equipmentIndex; // Grab the associated equipment.
                player.inventory.SetEquipmentIndex(selectedBuff); // Apply that equipment.
                FindPlayerStorage(player).giftedAffix = _config.TakeAffix; // Set a variable to take it away.
            }

            // Resetting the amount of loops that we've done. 
            respawnLoops = 0;

            // Broadcasting it to everyone.
            if (_config.AnnounceRespawns) Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cWorldEvent><sprite name=\"Skull\" tint=1> " + player.playerCharacterMasterController.networkUser.userName + " has inhabited the body of a " + player.GetBody().GetDisplayName() + "! <sprite name=\"Skull\" tint=1></style>" });
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
            player.master.bodyPrefab = player.origPrefab;
            player.master.teamIndex = TeamIndex.Player;

            if (player.giftedAffix)
            {
                player.master.inventory.SetEquipmentIndex(EquipmentIndex.None);
                player.giftedAffix = false;
            }

            if (metamorphosIsEnabled && player.inventory.GetItemCount(RoR2Content.Items.InvadingDoppelganger) > 0) player.inventory.RemoveItem(RoR2Content.Items.InvadingDoppelganger);

            RemoveMonsterVariantItems(player.master);

            if (_classicItems != null)
                GiveScepterCI(player.master);
            if (_standaloneAncientScepter != null)
                GiveScepterSAS(player.master);

            if (_config.RemoveAllItems && _config.ReturnItemsOnStageChange) player.master.inventory.AddItemsFrom(player.inventory);
            if (_config.ForceItemRestoration) player.master.inventory.CopyItemsFrom(player.inventory);

            player.isDead = false;
        }

        // Updating our Whitelist of monsters at stage generation.
        private void UpdateStageWhitelist()
        {
            Logger.LogInfo("Updating the stage whitelist.");

            // Clearing the old stage whitelist.
            currWhitelist.Clear();

            // Grabbing a reference liiist.
            var list = ClassicStageInfo.instance.monsterSelection.choices.ToList();

            // Beginning a nice lil' for loop.
            foreach (var choice in list)
            {
                Logger.LogInfo("Testing choice.");

                // First, we grab the SpawnCard of our monster.
                SpawnCard currMonster = choice.value.spawnCard;
                if (currMonster == null) continue;

                // Then, we check to see if the monster has a body.
                GameObject currMonsterBody = BodyCatalog.FindBodyPrefab(currMonster.prefab.name.Replace("Master", "Body"));
                if (currMonsterBody == null) continue;
                Logger.LogInfo("We have found " + currMonsterBody.name + ".");

                // Nuking any unwanted Champions.
                if (!(_config.AllowBosses && Run.instance.loopClearCount >= 2) && currMonsterBody.GetComponent<CharacterBody>().isChampion) continue;

                // Nuking any unwanted Scavangers.
                if (!(_config.AllowScavengers && Run.instance.loopClearCount >= 5) && currMonsterBody.name == "ScavengerBody") continue;

                // Is it in our Blacklist?
                if (CheckBlacklist(currMonsterBody.name)) continue;

                // Add that rad dude!
                currWhitelist.Add(currMonsterBody);
                Logger.LogInfo("Adding " + currMonsterBody.name + " to the currWhitelist.");
            }

            // Bring out the backup dude if nothing works.
            if (currWhitelist.Count <= 0) currWhitelist.Add(BodyCatalog.FindBodyPrefab("LemurianBody"));
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

        // Code for handling other mods.
        //

        // One-upping myself by having a master function that can call sub-functions.
        private void RemoveMonsterVariantItems(CharacterMaster player)
        {
            if (_monsterVariants != null) { RemoveMonsterVariantItemsMV(player); }
            if (_varianceAPI != null) { RemoveMonsterVariantItemsAPI(player); }
        }

        // Stolen coooode. It takes the AddItems function from MonsterVariants and does everything in reverse... Except it no longer does thaaaat.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void RemoveMonsterVariantItemsMV(CharacterMaster player)
        {
            if (player != null && player.GetBody() && player.GetBody().GetComponent<MonsterVariants.Components.VariantHandler>() && player.GetBody().GetComponent<MonsterVariants.Components.VariantHandler>().isVariant && _config.RemoveMonsterVariantItems)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " is a Monster Variant. Attempting to remove their items.");
                Logger.LogError("Oh no! MonsterVariants is depreciated!? You'd better switch to VarianceAPI if you want proper functionality. :)"); // It was my idea, not Nebby's. :P
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cDeath> Oh no! The server owner forgot to uninstall MonsterVariants! </style>" });
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cDeath> They'd better switch to VarianceAPI! </style>" });
                player.inventory.GiveItem(RoR2Content.Items.HealthDecay, 15); // Ok this part was Nebby's idea.
            }
        }

        // Amazing code provided by Nebby. Thank you so much.
        private void RemoveMonsterVariantItemsAPI(CharacterMaster Player)
        {
            CharacterBody playerBody = Player.GetBody(); //Grab the body, since the components are stored in its gameobject.
            if ((bool)playerBody)
            {
                Logger.LogInfo("Found " + Player.playerCharacterMasterController.networkUser.userName + "'s Body!");

                //We're using LinQ for this. the magic thing about Linq is using the Where method.
                //It allows us to get only the components that match with our conditional. in this case, we're getting all the VariantHandler components the body has that are active, AKA: isVariant == true.
                List<VariantHandler> playerVariantHandlers = playerBody.gameObject.GetComponents<VariantHandler>().Where(VH => VH.isVariant == true).ToList();
                Logger.LogInfo("Obtained Active VariantHandler components from body!");

                //We're going to iterate thru these active variantHandler components and check if their inventory is null or not.
                foreach (VariantHandler currentVariantHandler in playerVariantHandlers)
                {
                    Logger.LogInfo("Checking for variant inventory in " + currentVariantHandler.identifierName);

                    if (currentVariantHandler.inventory == null)
                    {
                        //Inventory is null? then continue to the next element in the array..
                        Logger.LogInfo(currentVariantHandler.identifierName + "Has no inventory.");

                        //This method checks if the variantHandler's tier is greater or equal to uncommon, if so, it removes the purple healthbar.
                        CheckForPurpleHealthBar(playerBody.inventory, currentVariantHandler);

                        //We use continue to go to the next item in playerVariantHandlers
                        continue;
                    }
                    else
                    {
                        Logger.LogInfo(currentVariantHandler.identifierName + " Has an inventory! Removing items!");

                        //Inventory isnt null, store the inventory in a new temporary variable for ease of access.
                        VariantInventory variantInventory = currentVariantHandler.inventory;

                        //Iterate thru the inventory's arrays. since both variantInventory.counts & variantInventory.ItemStrings are of the same size, we can use the same iteration for both.
                        for (int i = 0; i < variantInventory.counts.Length; i++)
                        {
                            //Store the current iteration for ease of access.
                            var currentItemString = variantInventory.itemStrings[i];
                            var currentItemCount = variantInventory.counts[i];

                            //ItemCatalog can be used to get both the index of an item & the itemDef of an item using its itemString.                  
                            var itemDef = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(currentItemString));

                            //Inventory has its own method for removing items thankfully, but it needs an itemDef, ergo the existence of the line above!
                            Player.inventory.RemoveItem(itemDef, currentItemCount);
                            Logger.LogInfo("Removed " + currentItemCount + " " + itemDef.name + " from " + Player.playerCharacterMasterController.networkUser.userName + "'s Inventory");
                        }
                        CheckForPurpleHealthBar(playerBody.inventory, currentVariantHandler);
                    }
                    //We're not going to destroy any variantHandler components.
                }
            }
        }

        //Removes the purple healthbar if the tier is greater than common.
        private void CheckForPurpleHealthBar(Inventory inventory, VariantHandler variantHandler)
        {
            var purpleHealthBar = VarianceAPI.ContentPackProvider.contentPack.itemDefs.Find("VAPI_PurpleHealthbar");
            if (variantHandler.tier >= VariantTier.Uncommon)
            {
                Logger.LogInfo(variantHandler.identifierName + "'s tier is Uncommon or Greater! Removing PurpleHealthBar.");
                inventory.RemoveItem(purpleHealthBar, inventory.GetItemCount(purpleHealthBar));
            }
            else
            {
                Logger.LogInfo("Finished " + variantHandler.identifierName + ". Proceeding to next item in the list.");
            }
        }

        // This one is for Classic Items. It takes the player's Scepter.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void TakeScepterCI(CharacterMaster player)
        {
            if (player.inventory.GetItemCount(ThinkInvisible.ClassicItems.Scepter.instance.itemDef) > 0)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " has a Scepter. Taking it.");
                FindPlayerStorage(player).hadAncientScepter = true;
                player.inventory.RemoveItem(ThinkInvisible.ClassicItems.Scepter.instance.itemDef);
            }
        }

        // This one is for Classic Items. It gives the player's Scepter.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void GiveScepterCI(CharacterMaster player)
        {
            if (FindPlayerStorage(player).hadAncientScepter)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " had a Scepter. Returning it.");
                player.inventory.GiveItem(ThinkInvisible.ClassicItems.Scepter.instance.itemDef);
                FindPlayerStorage(player).hadAncientScepter = false;
            }
        }

        // This one is for Standalone AS. Same as before.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void TakeScepterSAS(CharacterMaster player)
        {
            if (player.inventory.GetItemCount(AncientScepter.AncientScepterItem.instance.ItemDef) > 0)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " has a Scepter. Taking it.");
                FindPlayerStorage(player).hadAncientScepter = true;
                player.inventory.RemoveItem(AncientScepter.AncientScepterItem.instance.ItemDef);
            }
        }

        // This one is for Standalone AS. Same as before.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void GiveScepterSAS(CharacterMaster player)
        {
            if (FindPlayerStorage(player).hadAncientScepter)
            {
                Logger.LogInfo(player.playerCharacterMasterController.networkUser.userName + " had a Scepter. Returning it.");
                player.inventory.GiveItem(AncientScepter.AncientScepterItem.instance.ItemDef);
                FindPlayerStorage(player).hadAncientScepter = false;
            }
        }
    }
}
