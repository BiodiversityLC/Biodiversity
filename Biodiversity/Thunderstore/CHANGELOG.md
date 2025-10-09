# Version 0.2.7. - v73 Compatability and a rocky road ahead.

## Ogopogo:

- Added Ogo to a bunch of new moons!
- Added static spawn position config.
- Made Vermin super rare in preparation for its rework (they will be changed once that's out).
- Fixed Ogopogo cheese.

## Aloe:

- Fixed Visor issue.
- Fixed Double body bug.
- Added a few configs, check em out!

## Coil-crabs:

- Fixed weights on modded moons.

## Items:

- Fixed Nethersome duck.
- Added Iron dog.
- Added a dev item for Ccode.
- Added a dev item for JacuC.
- Added a dev item for Zipka.

## Misc:

- Added French translation, thanks to Ziggy (Zigzag Awaka)!
- Made Coil-crabs and Boom birds less common on snow moons.
- 60%
- 10%


# Version 0.2.6. - THE HAPPY BIRTHDAY ZIGGY UPDATE!

- Happy birthday ziggy haha hoohoo

## Version 0.2.5. - v70 Compatability!

#### Boom bird:

- Fixed them freaking out if another naturally spawned Boom bird is killed through unusual means.
- EnhancedRadarBooster is now compatible with Boom birds!
- Fixed the radar malfunction's sound being audible from the interior (it is still audible from the exterior, which I see no issue with so if you don't like it cope and seethe idk)

#### Coil-crab:

- Made their bodies render properly when inside the ship if the door is closed.
- Fixed rotation of Coil-shell item so that it's held properly.

#### Ogopogo:

- Fixed strange log spam when using radar boosters when an Ogopogo is active.

#### Misc:

- Mod is now compatible with v70 of LC.
- Progress on the Wax Soldier has started.

## Version 0.2.1. - THE NOT WAX SOLDIER UPDATE

#### Boom bird:

- Made them able to be killed by Earth Leviathans.
- Made it so they can get hurt by all damage sources.
- Added flight animation for when the ship takes off.
- Added a fallback for when Boom birds become stuck upon spawning (they will now despawn after 2 seconds with an animation).
- Added sounds to the ship light malfunctions.
- Optimized a shit ton of code. 

##### Coil-crab:

- Changed scan node to "Coil-Crab"
- Made it so they're targettable by Old birds.
- Made it so they're killable by Earth Leviathans.

#### Ogopogo:

- Added an extra check for if the player is inside.

#### Aloe:

- Fixed whack ass footstep bug with the Aloe.
- Made it so player ragdolls don't trigger landmines when being carried by the Aloe.
- Made some slight performance improvements to its code.

#### Misc:

- Fixed Nethersome duck weight bug.
- Made all items compatible with Runtime Icons.

## Version 0.2.0. - THE NOT WAX SOLDIER UPDATE

# WE ARE OFFICIAL OUT OF THE PUBLIC BETA TEST PHASE! FEEL FREE TO RESET YOUR CONFIGS AS THERE HAVE BEEN A TON OF BALANCE CHANGES SINCE 0.1.3.! WE APPRECIATE YOUR PATIENCE, AND WE HOPE YOU ENJOY THIS UPDATE.

#### Aloe:

- Finally fixed the sound issues.
- KNOWN ISSUE (?): The player's visor hud appearing in front of the player's view during the Aloe's dragging, maybe, i think? idk :P !!!! Also other players may still be able to see duplicate bodies when you're getting dragged maybee

## Version 0.1.92. - THE NOT WAX SOLDIER UPDATE (BETA)

# THIS VERSION IS A PUBLIC BETA TEST!!! IF YOU DON'T WANT THE FEATURES ADDED YOU SHOULD DOWNGRADE TO 0.1.5., 0.1.4. OR 0.1.3. IMMEDIATELY!!!

#### Misc.
- Finally Prototax global sound (Thank you Ccode!).
- Fixed the Leaf boys' scream sound being desynced.
- Some other cool fixes by Figo.
- Added Kanie to the Ogopogo's defaults.
- Added a new config for Ogopogo.
- Removed peak mod PhysicsAPI as a dependency.
- Removed Wax soldier.

## Version 0.1.91. - THE NOT WAX SOLDIER UPDATE (BETA)

# THIS VERSION IS A PUBLIC BETA TEST!!! IF YOU DON'T WANT THE FEATURES ADDED YOU SHOULD DOWNGRADE TO 0.1.5., 0.1.4. OR 0.1.3. IMMEDIATELY!!!

#### Misc.
- Fixed networking issues.
- Fixed typo in the Boom bird bestiary.
- Removed Wax soldier.

KNOWN ISSUES I FORGOT TO WRITE ABOUT LAST TIME:
- Aloe's terminal video files are brokey atm.
- There's a duplicate helmet (the player's hud) that appears when the Aloe is dragging/healing the player.


## Version 0.1.9. - THE NOT WAX SOLDIER UPDATE (BETA)

# THIS VERSION IS A PUBLIC BETA TEST!!! IF YOU DON'T WANT THE FEATURES ADDED YOU SHOULD DOWNGRADE IMMEDIATELY!!!

#### Aloe:
- Added config for its healing to deal damage instead.
- Added config to change the max speed of the Aloe during all phases.
- Added config to set whether the Aloe will detonate vanilla landmines and Surfaced seamines while kidnapping a player.
- Removed the view width and view range configs from the Aloe because they are very misleading, and people shouldnt be touching them.
- Fixed the Aloe's global fear effect.
- The Aloe sounds now have a very very slight (random) variation in pitch.
- The Aloe's map dot shouldn't be pink anymore.

KNOWN ISSUES: The Aloe's sounds are brokey right now

#### Upcoming changes for Leaf boys:
- Reworked wandering.
- Added spawn animation.
- Added the following configs for the Leafboy: player forget time; scared speed multiplier; base movement speed; scary player distance.

KNOWN ISSUES: The Leaf boy's "yell" sound effects are host-only.

#### Upcoming changes for Prototax:
- Reworked wandering.
- Added spawn and idle animation.
- Fixed global sound effect.

KNOWN ISSUES: The Prototax's "spew" sound is global.

#### Ogopogo:
- Added a way to cheese the Ogopogo.
- Fixed bug where it would wander under the ground due to elevation changes in areas with water.
 
#### Items:
- Added new sound effects to the rubber duck.
- Added a config for developer items.

#### Misc:
- Added Boom birds.
- Added Coil-crabs.
- Removed Wax soldier.
- Changed 99% of all debug logs to verbose logs which need the verbose logging setting in the config to be turned on to use.
- Added spanish, german and russian translations.
- Added another quote to the quotes list.
- Fixed bug where the mod would produce an error if the Surfaced mod wasn't installed.

## Version 0.1.5.

- Readme update (hah)
- Bug fixes and new content will come soon, I promise. For the time being, these are things I (Monty) am able to change from the github, and so I figured I'd drop this, I hope it's enough for the time being.
- Balance changes to weights and damage values.
- Added PhysicsAPI as a dependency to help on development of future enemies and items.

## Version 0.1.4.

- Added 0.1.3. changelog (fuck you power company)

## Version 0.1.3.

#### Ogopogo:
- Implemented blacklist to prevent wandering on certain moons (Thanks Ccode).
- Fixed bug where Ogopogo would be able to grab people who are inside the ship. We implemented a check to "block" the grab if the grab box touches a ship-bound player.
- Fixed a bug where Ogopogo would soft-lock players when the grab box touched two people at the same time.
- Change default lose range from 70 to 60.
- A buncha other bugfixes idk

##### Vermin:
- Fixed them entirely.
- Made them spawn on Ogopogo as opposed to their water origin.
- Implemented Vermin moon blacklist, included Adamance and Dine by default since they "un-flood"
- You can enable em in the defaults now :thumbsup:

##### Aloe:
- Fixed neck IK script breaking like crazy and causing her to spaz out at times, sorry 'bout that!
- Fixed typo caused by usage of a different english type which lead to logspam about Aloe's skin color.
- Changed heal animation to make the Aloe look down.

## Version 0.1.2.

##### Ogopogo:
- Fixed Ogopogo spawning inside interiors with water, finally.

##### Aloe:
- Fixed Aloe heads twitching a lot during the healing animation.
- Increased the minimum health needed for Aloes to kidnap players.
- Attempted a fix for health HUD bugs related to Aloe healing.
- Made Aloe avoid players better during stalking phases.

##### Misc:
- Made changes to the readme.
- Removed debug feature that allowed the knife to harm the user.

## Version 0.1.1.

##### Ogopogo:
- Implemented a bandaid fix to prevent them from bothering people inside the facility. They now are unable to target players inside.

##### Vermin:
- Fixed log spam for Vermin on flooded weather.
- Disabled Vermin by default through the config file.

##### Prototax:
- Fixed model clipping into the ground slightly on certain terrain.
- Made hit and spore spew sound effect trigger eyeless dogs.
- Fixed them spawning only at night (they now spawn during the day).

##### Leaf boy:
- Fixed model floating above the ground.
- Made all of its sound effects trigger eyeless dogs.
- Fixed them spawning only at night (they now spawn during the day).

##### Added scrap item:
- Nethersome's rubber duck.

## Version 0.1.0.

Updated description to remove typo, whoops!

## Version 0.0.1.

Released.
