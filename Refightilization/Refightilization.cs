using BepInEx;
using RoR2;
using RoR2.Orbs;
using RoR2.Navigation;
using R2API.Utils;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;

namespace Wonda
{
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(guid, modName, version)]
    public class Refightilization : BaseUnityPlugin
    {
        // Setting up variables and waking the program up.
        //

        // Cool info B)
        const string guid = "com.Wonda.Refightilization";
        const string modName = "Refightilization";
        const string version = "1.2.1";

        // Config
        private RefightilizationConfig _config;

        // Language Management Stuff
        private RefightilizationLanguage _language;

        // Class to make players easier to manage
        public class PlayerStorage
        {
            public NetworkUser user = null; // The network user of the player.
            public CharacterMaster master = null; // The character master of the player.
            public GameObject origPrefab = null; // The original surivor the player was playing as.
            public bool isDead = false; // A tracker for if the player was dead or not.
            public Inventory inventory = null; // The inventory the player originally had.
            public Inventory blacklistedInventory = null; // For storing any items that the player wasn't supposed to have.
            public bool giftedAffix = false; // A tracker for if the player has recieved an affix from the mod.
            public EquipmentIndex previousEquipment = EquipmentIndex.None; // The equipment the player previously had.
            public NetworkUser lastDamagedBy = null; // The network user that previously attacked this player.
            public float lastDamagedTime = 0; // Thevar time this player was last attacked by a network user.
            public bool isLoggedOut = false; // A tracker for if the player left the server prematurely.
            public int lastStage = 0; // The last stage the player was on. Used for checking if they need to be reset after logging out.
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
        public List<ItemIndex> currItemBlacklist = new List<ItemIndex>(); // It'll be so many foreach statements running every time somebody dies unless we store those items somewhere.
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
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.rune580.riskofoptions", out var riskOfOptionsPlugin)) _config.BuildRiskOfOptionsMenu();
        }

        // Hook setup.
        //

        // See!
        private void SetupHooks()
        {
            On.RoR2.Run.Start += Run_Start;
            On.RoR2.GlobalEventManager.OnPlayerCharacterDeath += GlobalEventManager_OnPlayerCharacterDeath;
            GlobalEventManager.onServerDamageDealt += GlobalEventManager_onServerDamageDealt;
            On.RoR2.Run.OnServerSceneChanged += Run_OnServerSceneChanged;
            On.RoR2.Run.OnUserAdded += Run_OnUserAdded;
            On.RoR2.Run.OnUserRemoved += Run_OnUserRemoved;
            On.RoR2.TeleporterInteraction.OnInteractionBegin += TeleporterInteraction_OnInteractionBegin;
            On.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
            On.RoR2.Inventory.GiveItem_ItemIndex_int += Inventory_GiveItem_ItemIndex_int;
            On.RoR2.CharacterMaster.Respawn += CharacterMaster_Respawn;
            On.RoR2.CharacterMaster.PickRandomSurvivorBodyPrefab += CharacterMaster_PickRandomSurvivorBodyPrefab;
            On.RoR2.CharacterMaster.IsDeadAndOutOfLivesServer += CharacterMaster_IsDeadAndOutOfLivesServer;
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
            if (_config.EnableRefightilization) StopCoroutine(RespawnCheck()); // Stop any current respawn checks.
            orig(self, sceneName);
            if (!_config.EnableRefightilization) return; // Kinda pointless to do anything if the mod is disabled.
            if (sceneName.Equals("moon") || sceneName.Equals("moon2")) moonDisabled = true; // Disabling everything if we're on the moon.
            if (self.stageClearCount > 0) ResetPrefabs(); // Gotta make sure players respawn as their desired class.
            Invoke("UpdateStageWhitelist", 1f); // Gotta make sure we have an accurate monster selection.
            Invoke("UpdateEliteWhitelist", 1f); // Gotta make sure we have an accurate elite selection.
            Invoke("UpdateBlacklistedItems", 1f); // New baby! Here to check up on all the blacklisted items, too.
            respawnTime = _config.RespawnDelay; // Setting our wacky respawn delay.
        }

        private void Run_OnUserAdded(On.RoR2.Run.orig_OnUserAdded orig, Run self, NetworkUser user)
        {
            orig(self, user);
            if (Run.instance.time > 1f && _config.EnableRefightilization)
            {
                bool playerWasLoggedOut = false; // Tracking if the player existed at one point.

                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.user.userName == user.userName)
                    {
                        player.user = user; // Resetting the current user.
                        player.master = user.master; // Also resetting their master.
                        player.isLoggedOut = false; // They're no longer logged out.
                        playerWasLoggedOut = true; // Confirming this for when the game wants to run SetupPlayers again.
                        Logger.LogDebug(user.userName + " returned. Marking them as logged in.");

                        if (player.lastStage < Run.instance.stageClearCount && user.master && Stage.instance)
                        {
                            Invoke("NaturalRespawnCharacter", 3f); // Invoking a respawn for the dead.
                        } else
                        {
                            player.isDead = true; // Making them dead so we can respawn them again in our funny loops.
                            StartCoroutine(RespawnCheck(player.master.transform.position, 3)); // Spawning in players shortly after a delay.
                        }

                        player.master.inventory.CopyItemsFrom(player.inventory); // Copying the inventory.
                        player.blacklistedInventory = new Inventory(); // Creating a blacklisted inventory, for storage.

                        break; // Getting out of this loop.
                    }
                }

                if (!playerWasLoggedOut) SetupPlayers(false); // For players who enter the game late.
            }
        }

        // For allowing us to respawn rejoined players.
        private void NaturalRespawnCharacter()
        {
            foreach (PlayerStorage player in playerStorage)
            {
                if(!player.isDead && !player.isLoggedOut && !player.master.GetBody())
                {
                    Stage.instance.RespawnCharacter(player.master); // Respawnning a logged in, non-dead player.
                }
            }
        }

        private void Run_OnUserRemoved(On.RoR2.Run.orig_OnUserRemoved orig, Run self, NetworkUser user)
        {
            if (Run.instance.time > 1f && _config.EnableRefightilization)
            {
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.user.userName == user.userName)
                    {
                        CleanPlayer(player); // Cleaning this player up.
                        player.inventory.CopyItemsFrom(user.master.inventory); // Storing all of their goodies.
                        break;
                    }
                }
            }
            orig(self, user);
            if (Run.instance.time > 1f && _config.EnableRefightilization)
            {
                foreach (PlayerStorage player in playerStorage)
                {
                    if (player.user.userName == user.userName)
                    {
                        player.isLoggedOut = true; // Marking the player as returned.
                        player.lastStage = Run.instance.stageClearCount; // Setting their last stage to the current clear count.
                        Logger.LogDebug(user.userName + " left. Marking them as logged out.");
                        break;
                    }
                }
            }
        }

        private void TeleporterInteraction_OnInteractionBegin(On.RoR2.TeleporterInteraction.orig_OnInteractionBegin orig, TeleporterInteraction self, Interactor activator)
        {
            if (_config.EnableRefightilization)
            {
                foreach (PlayerStorage player in playerStorage)
                {
                    if (self.isCharged) StopCoroutine(RespawnCheck());
                    if (player.master != null && player.master.GetBody() != null && player.master.GetBody().gameObject == activator.gameObject && player.isDead && player.master.teamIndex != TeamIndex.Player && playerStorage.Count > 1) return; 
                    // If there's multiple players, then dead ones won't be able to activate the teleporter.
                }
            }
            orig(self, activator);
        }

        // Whenever an item is attempted to be picked up.
        private void GenericPickupController_AttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
            if (_config.EnableRefightilization)
            {
                // Get the current player.
                PlayerStorage player = null; 
                if(body.master) player = FindPlayerStorage(body.master);

                // Pre-check.
                if (player != null && player.isDead && _config.ItemPickupToggle)
                {
                    // Apparently you have to be on the player team to pick up items???
                    player.master.GetBody().teamComponent.teamIndex = TeamIndex.Player;
                }

                // Orig
                orig(self, body);

                // Post-fix.
                if (player != null && player.isDead && _config.ItemPickupToggle)
                {
                    // Set them back for posterity.
                    player.master.GetBody().teamComponent.teamIndex = (TeamIndex)_config.RespawnTeam;

                }
            }
            else orig(self, body);
        }

        private void WhyDoActionsDoSillySillyThings(RoR2.Orbs.ItemTransferOrb orb) { }

        // Handling item grants.
        private void Inventory_GiveItem_ItemIndex_int(On.RoR2.Inventory.orig_GiveItem_ItemIndex_int orig, Inventory self, ItemIndex itemIndex, int count)
        {
            // Is the item in our blacklist? (And also Refight is running.)
            if (_config.EnableRefightilization && currItemBlacklist.Contains(itemIndex))
            {
                // Seeing if a valid PlayerStorage exists for this Character.
                CharacterMaster possiblePlayer = self.GetComponent<CharacterMaster>();
                PlayerStorage player = null;
                if (possiblePlayer != null) player = FindPlayerStorage(possiblePlayer);

                // If we can even find that this player exists and is dead, then increment their blacklisted items.
                if (player != null && player.isDead)
                {
                    // Incrementing the blacklisted items.
                    player.blacklistedInventory.itemStacks[(int)itemIndex] += count;

                    // Dummy steal effect.
                    RoR2.Orbs.ItemTransferOrb.DispatchItemTransferOrb(possiblePlayer.GetBody().footPosition, 
                        possiblePlayer.inventory, itemIndex, 0, new System.Action<RoR2.Orbs.ItemTransferOrb>(WhyDoActionsDoSillySillyThings));

                    string messageText = _language.ItemBlacklistWarning; // Grabbing a random revenge message.
                    messageText = messageText.Replace("{0}", player.user.userName); // Setting 0 to be the user losing the item.
                    messageText = messageText.Replace("{1}", ItemCatalog.itemNames[(int)itemIndex]); // Setting 1 to be the item itself.
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cStack>" + messageText }); 
                    return;
                }
            }
            
            orig(self, itemIndex, count);
        }

        // Handling characters respawning.
        private CharacterBody CharacterMaster_Respawn(On.RoR2.CharacterMaster.orig_Respawn orig, CharacterMaster self, Vector3 footPosition, Quaternion rotation, bool wasRevivedMidStage = false)
        {
            if (_config.EnableRefightilization)
            {
                string currMethod = new StackFrame(2).GetMethod().Name;
                if (!respawnMethodCheck.Exists(x => x.Equals(currMethod)) && (FindPlayerStorage(self) != null))
                {
                    CleanPlayer(FindPlayerStorage(self));
                }
            }
            return orig(self, footPosition, rotation, wasRevivedMidStage);
        }

        // Handling Metamorphosis.
        private GameObject CharacterMaster_PickRandomSurvivorBodyPrefab(On.RoR2.CharacterMaster.orig_PickRandomSurvivorBodyPrefab orig, Xoroshiro128Plus rng, NetworkUser networkUser, bool allowHidden)
        {
            // In-case Metamorphosis is enabled, we have to make sure that the monster is the one that respawns and not the survivor.
            if (_config.EnableRefightilization && _config.OverrideMetamorphosis) {
                string currMethod = new StackFrame(6).GetMethod().Name;
                if (currMethod == "RefightRespawn")
                {
                    return currEnemyWhitelist[Random.Range(0, currEnemyWhitelist.Count - 1)];
                }
            }
            return orig(rng, networkUser, allowHidden);
        }

        // Handling if somebody is dead and out of lives.
        private bool CharacterMaster_IsDeadAndOutOfLivesServer(On.RoR2.CharacterMaster.orig_IsDeadAndOutOfLivesServer orig, CharacterMaster self)
        {
            if(_config.EnableRefightilization)
            {
                PlayerStorage player = FindPlayerStorage(self); // Finding the current player.
                if (player != null && player.isDead) // If they exist and they *are* dead, by our standards...
                    return true; // They're dead.
            }
            return orig(self); // Perform the original check.
        }

        // Handling Game-Overs
        private void Run_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            if (!(_config.EnableRefightilization && !_config.EndGameWhenEverybodyDead)) {
                StopCoroutine(RespawnCheck()); // We no longer need to check for Respawns
                ResetPrefabs(); // Gotta clean up every player.
                orig(self, gameEndingDef);
            }
        }

        // Handling all enemies being defeated in a Simulacrum wave.
        private void InfiniteTowerRun_OnWaveAllEnemiesDefeatedServer(On.RoR2.InfiniteTowerRun.orig_OnWaveAllEnemiesDefeatedServer orig, InfiniteTowerRun self, InfiniteTowerWaveController wc)
        {
            foreach(PlayerStorage player in playerStorage)
            {
                if (player == null)
                {
                    Logger.LogDebug("Player is null! Continuing.");
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
            Logger.LogDebug("Setting up players...");
            if (StageUpdate) playerStorage.Clear();
            foreach (PlayerCharacterMasterController playerCharacterMaster in PlayerCharacterMasterController.instances)
            {
                // Skipping over Disconnected Players.
                if (playerStorage != null && playerCharacterMaster.networkUser == null)
                {
                    continue;
                }

                // If this is ran mid-game, just skip over existing players and add anybody who joined.
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
                if (playerCharacterMaster.master.inventory) newPlayer.blacklistedInventory = new Inventory();
                playerStorage.Add(newPlayer);
                Logger.LogDebug(newPlayer.user.userName + " added to PlayerStorage!");
            }
            Logger.LogDebug("Setting up players finished.");
            if (!StageUpdate) StartCoroutine(RespawnCheck(new Vector3(0, 0, 0), respawnTime));
        }

        // Checking for dead players, and respawning them if it seems like they're respawnable.
        IEnumerator RespawnCheck(Vector3 deathPos = new Vector3(), float waitTime = 1f)
        {
            Logger.LogDebug("Respawn called! Waiting " + waitTime + " seconds!");

            yield return new WaitForSeconds(waitTime);

            // ABORT ABORT THE GAME DOESN'T EXIST
            if (!Run.instance || !Run.instance.isActiveAndEnabled) yield break;

            // Wait... Yea disable functionality if the mod is disabled.
            if (!_config.EnableRefightilization)
            {
                Logger.LogDebug("Nevermind. Mod is disabled via config.");
                yield break;
            }

            // Gotta prevent a major issue with players respawning after a teleporter event, causing the game to be over.
            if (_config.NoRespawnsAfterTeleporter && TeleporterInteraction.instance && TeleporterInteraction.instance.isCharged)
            {
                Logger.LogDebug("Respawning after teleporter is disabled!");
                yield break;
            }

            // Aaand moon prevention.
            if (_config.DisableMoon && moonDisabled)
            {
                Logger.LogDebug("Respawn prevented due to current stage.");
                yield break;
            }

            Logger.LogDebug("Checking players...");

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
                    Logger.LogDebug("Player doesn't exist! Skipping...");
                    playerStorage.Remove(player); // This was quickly spledged in and is untested. It'll probably break *everything* if a player leaves mid-game... It probably does already.
                    RespawnCheck(deathPos, waitTime);
                    yield break;
                }

                if (player.master.IsDeadAndOutOfLivesServer() && !player.isLoggedOut)
                {
                    Logger.LogDebug(player.user.userName + " passed spawn check!");

                    // Hey! This player hasn't died before!
                    if (!player.isDead)
                    {
                        player.isDead = true; // They died!
                        player.inventory.CopyItemsFrom(player.master.inventory); // Copy all their items.
                    }

                    player.master.teamIndex = _config.RespawnTeam; // Moved out here in-case config is changed mid-game.
                    respawnLoops = 0; // Setting our loops in-case something breaks in RefightRespawn.
                    RefightRespawn(player.master, deathPos); // Begin respawning the player.
                }
                else
                {
                    Logger.LogDebug(player.user.userName + " failed spawn check.");
                    if (!player.isDead) isEverybodyDead = false; // Alright, cool. We don't have to kill everybody.
                }
            }

            Logger.LogDebug("Checking players complete.");
            if (isEverybodyDead && !(TeleporterInteraction.instance != null && TeleporterInteraction.instance.isInFinalSequence) && _config.EndGameWhenEverybodyDead)
            {
                Logger.LogDebug("Everybody is dead. Forcibly ending the game.");
                ResetPrefabs(); // Resetting all those prefabs so the game-end screen would be accurate.
                Run.instance.BeginGameOver(RoR2Content.GameEndings.StandardLoss); // Woooo! Begin that game over!
            }

            yield break;
        }

        // Respawning that player.
        private void RefightRespawn(CharacterMaster player, Vector3 deathPos)
        {
            Logger.LogDebug("Attempting player respawn!");

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
                Logger.LogDebug("Player is null!? Aborting Respawn.");
                return;
            }

            // Also chucking out any player who isn't currently connected to the server.
            if (player.playerCharacterMasterController.networkUser == null)
            {
                Logger.LogDebug("Player doesn't have a networkUser!? Aborting Respawn.");
                return;
            }

            // Was this player assigned an affix by us?
            if (FindPlayerStorage(player).giftedAffix)
            {
                Logger.LogDebug("Yoinking that Affix.");
                player.inventory.SetEquipmentIndex(FindPlayerStorage(player).previousEquipment);
                FindPlayerStorage(player).giftedAffix = false;
            }

            // Another optimization, to prevent the game from looping over several repeated monsters.
            List<GameObject> tempEnemyWhitelist = currEnemyWhitelist;
            if (Util.CheckRoll(5f, player.playerCharacterMasterController.master) && currSpecialEnemyWhitelist.Count >= 1) tempEnemyWhitelist = new List<GameObject>(currSpecialEnemyWhitelist);
            if (Util.CheckRoll(0.1f, player.playerCharacterMasterController.master) && finalBossWhitelist.Count >= 1) tempEnemyWhitelist = new List<GameObject>(finalBossWhitelist);

            if (_config.NoRepeatRespawns && tempEnemyWhitelist.Count > 1) tempEnemyWhitelist.Remove(tempEnemyWhitelist.Where(entity => entity.name == player.bodyPrefab.name).FirstOrDefault());

            GameObject randomMonster = tempEnemyWhitelist[Random.Range(0, tempEnemyWhitelist.Count - 1)];

            Logger.LogDebug("Found body " + randomMonster.name + ".");

            // Do we have a blacklisted item?
            TakeBlacklistedItems(player);

            // Assigning the player to the selected monster prefab.
            if (player.bodyPrefab && randomMonster)
            {
                player.bodyPrefab = randomMonster;
                Logger.LogDebug(player.playerCharacterMasterController.networkUser.userName + " was assigned to " + player.bodyPrefab.name + ".");
            }
            else
            {
                Logger.LogDebug(player.playerCharacterMasterController.networkUser.userName + " has no bodyPrefab!? Retrying...");
                RefightRespawn(player, deathPos);
                return;
            }

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
            Logger.LogDebug("Respawned " + player.playerCharacterMasterController.networkUser.userName + "!");

            // Stat changes to allow players to not die instantly when they get into the game.
            player.GetBody().baseMaxHealth *= _config.RespawnHealthMultiplier;
            player.GetBody().baseDamage *= _config.RespawnDamageMultiplier;
            if (player.GetBody().GetComponent<DeathRewards>()) player.GetBody().GetComponent<DeathRewards>().goldReward *= (uint)_config.RespawnMoneyMultiplier;
            player.GetBody().baseRegen = 1f;
            player.GetBody().levelRegen = 0.2f;
            Logger.LogDebug("Applied stats.");

            // Let's just double-check that the player has an Interactor.
            if (!player.GetBody().GetComponent<InteractionDriver>())
            {
                player.GetBody().gameObject.AddComponent<InteractionDriver>();
                Logger.LogDebug("Granting an interaction driver.");
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

                Logger.LogDebug("Modifying interaction distance.");
            }

            // Grabbing all of the minions of the player and changing their team.
            ChangeMinionsTeam(player);

            // Some fun stuff to allow players to easily get back into combat.
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.ArmorBoost, 15f);
            player.GetBody().AddTimedBuff(RoR2Content.Buffs.CloakSpeed, 30f);
            Logger.LogDebug("Applied buffs.");

            // And Affixes if the player is lucky.
            if (_config.RespawnAffixEnabled && (Util.CheckRoll(_config.RespawnAffixChance, player.playerCharacterMasterController.master) || RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.eliteOnlyArtifactDef)))
            {
                if (player.inventory.GetEquipmentIndex() != EquipmentIndex.None && !_config.ForceGrantAffix) return; // If the player already has an equipment, just skip over 'em.

                if (_config.ForceGrantAffix) FindPlayerStorage(player).previousEquipment = player.inventory.GetEquipmentIndex(); // Record their current equipment if ForceGrantAffix is enabled 
                int i = Random.Range(0, currEliteWhitelist.Count - 1); // Pick a random Elite index.
                player.inventory.SetEquipmentIndex(currEliteWhitelist[i]); // Apply that equipment.
                FindPlayerStorage(player).giftedAffix = _config.TakeAffix; // Set a variable to take it away.
                Logger.LogDebug("Gifted affix.");
            }

            // Oh and they can't prevent a gameover anymore.
            player.preventGameOver = false;

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
            Logger.LogDebug("Resetting player prefabs...");

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
                    Logger.LogDebug("Player is null! Continuing.");
                    continue;
                }
                if (player.isDead) CleanPlayer(player);
                Logger.LogDebug(player.user.userName + "'s prefab reset.");
            }
            //SetupPlayers();
            Logger.LogDebug("Reset player prefabs!");
        }

        // Removes items from players.
        private void CleanPlayer(PlayerStorage player)
        {
            player.master.bodyPrefab = player.origPrefab; // Resetting their prefab.
            player.master.teamIndex = TeamIndex.Player; // Putting them back on the player team.
            ChangeMinionsTeam(player.master);

            // Taking back that affix.
            if (player.giftedAffix)
            {
                player.master.inventory.SetEquipmentIndex(player.previousEquipment);
                player.giftedAffix = false;
            }

            // Refer to the function names.
            StartCoroutine(ReturnBlacklistedItemsWait(player.master, 1f));

            // Yay! They're no longer dead!
            player.isDead = false;
        }

        // Changing the teams of all of a player's minions.
        private void ChangeMinionsTeam(CharacterMaster player)
        {
            if (_config.ChangeMinionsTeam)
            {
                // Iterating over all of the minion groups.
                MinionOwnership.MinionGroup minionGroup = null;
                for (int i = 0; i < MinionOwnership.MinionGroup.instancesList.Count; i++)
                {
                    MinionOwnership.MinionGroup minionGroup2 = MinionOwnership.MinionGroup.instancesList[i];
                    if (MinionOwnership.MinionGroup.instancesList[i].ownerId == player.netId)
                    {
                        minionGroup = minionGroup2;
                        break;
                    }
                }

                // If we found a minion group that belongs to the player.
                if (minionGroup != null)
                {
                    // Iterating over each minion in the group.
                    foreach (var minion in minionGroup.members)
                    {
                        // If the minion has a character master, then we change its index.
                        if (minion && minion.GetComponent<CharacterMaster>())
                        {
                            minion.GetComponent<CharacterMaster>().teamIndex = player.teamIndex;
                            if (minion.GetComponent<CharacterMaster>().GetBody()) minion.GetComponent<CharacterMaster>().GetBody().teamComponent.teamIndex = player.teamIndex;
                            Logger.LogDebug("Changed " + minion.GetComponent<CharacterMaster>().name + "'s team.");
                        }
                    }
                }
            }
        }

        // Updating our Whitelist of monsters at stage generation.
        private void UpdateStageWhitelist()
        {
            Logger.LogDebug("Updating the stage whitelist.");

            // Clearing the old stage whitelist.
            currEnemyWhitelist.Clear();
            Logger.LogDebug("Whitelist cleared.");

            // If we have fixed pool enabled.
            if(_config.EnableFixedPool)
            {
                Logger.LogDebug("Fixed pool is enabled.");

                // Iterating over each enemy body in our pool.
                foreach (var enemyBody in _config.FixedPool)
                {
                    // Adding it to the whitelist.
                    currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab(enemyBody));
                    Logger.LogDebug("Adding " + enemyBody + " to the currWhitelist.");
                }

            // If fixed pool isn't enabled.
            } else {
                // If we have no monster choices available.
                if (ClassicStageInfo.instance == null || ClassicStageInfo.instance.monsterSelection == null || ClassicStageInfo.instance.monsterSelection.choices == null)
                {
                    Logger.LogDebug("There is no available monster selection!");

                    // But we are in the Voidling encounter...
                    if (VoidRaidGauntletController.instance)
                    {
                        Logger.LogDebug("Void raid detected! Forcing monster selection...");

                        currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("NullifierBody"));
                        currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("VoidJailerBody"));
                        currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("VoidMegaCrabBody"));
                    }
                }
                // If we do have monster selections available.
                else
                {
                    Logger.LogDebug("Selection isn't null.");

                    // Grabbing a reference liiist.
                    List<WeightedSelection<DirectorCard>.ChoiceInfo> list = ClassicStageInfo.instance.monsterSelection.choices.ToList();

                    // Beginning a nice lil' for loop.
                    foreach (var choice in list)
                    {
                        Logger.LogDebug("Testing choice.");

                        // First First, we check to see if choice and value are null. Never leave an NRE unturned.
                        if (choice.value == null) continue;

                        // First, we grab the SpawnCard of our monster.
                        SpawnCard currMonster = choice.value.spawnCard;
                        if (currMonster == null) continue;

                        // Then, we check to see if the monster has a body.
                        GameObject currMonsterBody = BodyCatalog.FindBodyPrefab(currMonster.prefab.name.Replace("Master", "Body"));
                        if (currMonsterBody == null) continue;
                        Logger.LogDebug("We have found " + currMonsterBody.name + ".");

                        // Nuking any unwanted Champions.
                        if (!(_config.AllowBosses && Run.instance.loopClearCount >= _config.BossRequiredLoopCount) && currMonsterBody.GetComponent<CharacterBody>().isChampion)
                        {
                            if (_config.AllowBosses) currSpecialEnemyWhitelist.Add(currMonsterBody); // We have to let a player have fun every *once* in a while.
                            continue;
                        }

                        // Nuking any unwanted Scavangers.
                        if (!(_config.AllowScavengers && Run.instance.loopClearCount >= _config.ScavangerRequiredLoopCount) && currMonsterBody.name == "ScavengerBody") continue;

                        // Is it in our Blacklist?
                        if (CheckBlacklist(currMonsterBody.name)) continue;

                        // Add that rad dude!
                        currEnemyWhitelist.Add(currMonsterBody);
                        Logger.LogDebug("Adding " + currMonsterBody.name + " to the currWhitelist.");
                    }
                }
            }

            // Populating the Final Bosses list.
            finalBossWhitelist.Add(BodyCatalog.FindBodyPrefab("BrotherBody"));
            finalBossWhitelist.Add(BodyCatalog.FindBodyPrefab("MiniVoidRaidCrabBodyBase"));
            finalBossWhitelist.Add(BodyCatalog.FindBodyPrefab("FalseSonBossBody"));

            // Bring out the backup dudes if nothing works.
            if (currEnemyWhitelist.Count <= 0)
            {
                Logger.LogDebug("No enemies were found, pulling out the backup enemies.");
                // Yes I'm pulling out this again.
                foreach (var enemyBody in _config.FixedPool)
                {
                    // Adding it to the whitelist.
                    currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab(enemyBody));
                    Logger.LogDebug("Adding " + enemyBody + " to the currWhitelist.");
                }

                // Okay, somebody didn't put in any backup enemies. **Now** we pull out the backup dude.
                if (currEnemyWhitelist.Count <= 0) currEnemyWhitelist.Add(BodyCatalog.FindBodyPrefab("LemurianBody"));
            }
            Logger.LogDebug("Done updating Whitelist.");
        }

        // Updating the *other* whitelist.
        private void UpdateEliteWhitelist()
        {
            Logger.LogDebug("Updating the Elite whitelist.");

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

                        Logger.LogDebug("Added " + refDef[i].eliteTypes[v].eliteEquipmentDef.nameToken + " to the pool.");
                    }
                }
            }
            Logger.LogDebug("Done updating Whitelist.");
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

        private void UpdateBlacklistedItems()
        {
            Logger.LogDebug("Updating the Item blacklist.");
            // Clearing our list.
            currItemBlacklist = new List<ItemIndex>();

            // Going over each blacklisted item.
            foreach(var item in _config.BlacklistedItems)
            {
                // Adding each blacklisted item to the major list. 
                ItemIndex foundItem = ItemCatalog.FindItemIndex(item);
                if (foundItem != ItemIndex.None)
                {
                    currItemBlacklist.Add(foundItem);
                    Logger.LogDebug("Added " + foundItem + "|" + item + " to the pool.");
                } else Logger.LogDebug("Couldn't find " + item + ".");
            }
            Logger.LogDebug("Done updating Blacklist.");
        }

        private void TakeBlacklistedItems(CharacterMaster player)
        {
            Logger.LogDebug("Taking blacklisted items.");
            // Perform code to remove banned items in the player's inventory.
            PlayerStorage playerStorage = FindPlayerStorage(player);

            // Iterating over each item in the blacklist. (Would this not be performant if they blacklisted a *lot* of items?)
            foreach(var item in currItemBlacklist)
            {
                // Bluuuh, my branchless soul doesn't want this, but it prevents excess shenanigans from happening.
                if (player.inventory.itemStacks[(int)item] == 0) continue;

                // Setting the blacklisted count to their current count.
                playerStorage.blacklistedInventory.itemStacks[(int)item] = player.inventory.itemStacks[(int)item];

                // Setting the player's stacks count to zero.
                player.inventory.RemoveItem(item, player.inventory.itemStacks[(int)item]);

                // Dummy steal effect.
                RoR2.Orbs.ItemTransferOrb.DispatchItemTransferOrb(player.GetBody().footPosition,
                    player.inventory, item, 0, new System.Action<RoR2.Orbs.ItemTransferOrb>(WhyDoActionsDoSillySillyThings));

                Logger.LogDebug("Took away an item at " + item);
            }
            Logger.LogDebug("Done checking for blacklisted items.");
        }

        private IEnumerator ReturnBlacklistedItemsWait(CharacterMaster player, float time)
        {
            yield return new WaitForSeconds(time);
            ReturnBlacklistedItems(player);
        }

        private void ReturnBlacklistedItems(CharacterMaster player)
        {
            Logger.LogDebug("Giving blacklisted items.");
            // Perform code to return banned items in the player's inventory.
            PlayerStorage playerStorage = FindPlayerStorage(player);

            // Iterating over each item in the blacklist. (Would this not be performant if they blacklisted a *lot* of items?)
            foreach (var item in currItemBlacklist)
            {
                // Bluuuh, my branchless soul doesn't want this, but it prevents excess shenanigans from happening.
                if (playerStorage.blacklistedInventory.itemStacks[(int)item] == 0) continue;

                // Restoring the player's inventory.
                RoR2.Orbs.ItemTransferOrb.DispatchItemTransferOrb(player.GetBody().footPosition, player.inventory, item, playerStorage.blacklistedInventory.itemStacks[(int)item]);
                // Getting rid of spare stacks.
                playerStorage.blacklistedInventory.itemStacks[(int)item] = 0;
                Logger.LogDebug("Gave an item at " + item);
            }
            Logger.LogDebug("Done checking for blacklisted items.");
        }
    }
}
