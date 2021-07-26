
# Info

Allows players to respawn as a monster when they die. Highly configurable. Only the server host needs the mod.

# Video

[![Youtube Teaser](https://img.youtube.com/vi/4BdSJ4V8CPI/0.jpg)](https://www.youtube.com/watch?v=4BdSJ4V8CPI)

# Configuring for Single Player

If you ever want to mess with this mod while in single player, there are some config options to change. However, this mod is intended for multiplayer use, so you may still end up encountering issues or you may even get a game over.

| Key | Value | Reason |
|---|---|---|
|EndGameWhenEverybodyDead|false|This will allow the game to not end after death.|
|RespawnDelay|0.0|It's boring to wait.|
|RespawnTeam|Player|The run would be softlocked because you can't charge the teleporter.|
|AdditionalRespawnTime|0.0|Once again, it's *very* boring to wait.|
|NoRespawnsAfterTeleporter|false|If you somehow die after the teleporter event finishes, you won't be able to respawn.|

# Issues

Issues are tracked on the [Github](https://github.com/WondaMegapon/Refightilization/issues). Feel free to leave any suggestions there as well.

# Contact

The [Github](https://github.com/WondaMegapon/Refightilization/issues) is the best place to leave any issues or suggestions. However, if you have any general feedback, feel free to message me on Discord. You can find me at `Wonda#2183` in the modding server.

# Credits

Thanks to Nova and Arky for helpin' me scheme up the name for this thing.

Thanks to Fantab and Loke for inspiring this project to exist. 

Thanks to Cebe, Joe, JC, and PurpleKid for helping me test this mod out prior to release.

Thanks to Lux, EnderGrimm and Gaforb for providing tons of feedback to help make this mod better.

Thanks to Nebby for giving advice on making the mod better compatible with other mods, and for helping with some errors with VarianceAPI.

Thanks to Shower and Infinityhero for coming up with amazing revive messages.

# Changelog

1.0.18 - Gave larger monsters a bigger interaction radius. Fixed returning Ancient Scepter to players. Improved monster selection.

1.0.17 - Fixed Artifact of Metamorphosis not respawning players as monsters.

1.0.16 - Added config option for forcibly granting affixes. Respawning now respects preventGameOver (allowing for compatibility with Aetherium's Void Heart).

1.0.15 - Added config options for Champion and Scavanger respawn requirements. Overhauled Affixes, now they accurately reflect the current stage's elite pool.

1.0.14 - Updated README. Added multiple revive messages. Reverted incompatibility with ArtifactOfGrief.

1.0.13 - Optimized monster selection and enabled support for modded aspects.

1.0.12 - Depreciated support with MonsterVariants. Improved Support with VarianceAPI. (Special thanks to Nebby)

1.0.11 - Hotfixed a softlock that occurs when a user disconnects from a server! Prevented a NRE when attempting to respawn a disconnected player. Added minor performance improvements.

1.0.10 - Added config toggle for preventing spawning as the same monster twice in a row, added config toggle for respawn announcements, added catch for players leaving mid-game, hotfixed boss and scavanger spawning, and hotfixed the previous hotfix.

1.0.9 - Implemented hotfix for infinite loops while selecting monsters, added incompatibility with [ArtifactOfGrief](https://thunderstore.io/package/kking117/ArtifactOfGrief/).

1.0.8 - Added compatibility with [VarianceAPI](https://thunderstore.io/package/Nebby/VarianceAPI/), implemented hotfix for players keeping items granted by MonsterVariants, implemented hotfix for game-overs after using the teleporter.

1.0.7 - Teleporter interaction hotfixes, possible NRE catch, fixed respawn behavior to allow for better mod compatibility, adjusted spawn rules for champions and scavangers.

1.0.6 - Caught edge case of player respawning after all players teleporting, fixed compatibility with mods that respawn players, and added feature where dead players can kill alive players to respawn.

1.0.5 - Updated Readme, attempted to fix MonsterVariants compatibility, and adjusted player teleporter interactions.

1.0.4 - Fixed compatibility with ClassicItems and StandaloneAncientScepter, and added config for Aspects.

1.0.3 - Added item configs, added config for MonsterVariants, added scaling respawn timer, prevented respawning as a recently spawned monster, caught bugs regarding disabling the mod, adjusted spawning to prevent softlocks, altered Affix code to give players Elite Aspects instead of buffing them, adjusted spawning to prevent being unable to pick up items, added functionality with some artifacts.

1.0.2 - Caught some NREs, fixed R2API dependencies.

1.0.1 - Updated README to provide some useful hints for using the mod.

1.0.0 - Initial Release