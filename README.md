# AbilityRealizer

Name is very temporary.

## What It Currently Does

Provide support for modding the ability tree and abilities without requiring modders to completely redo all of the PilotDefs, as well as providing a mechanism for updating pilots that are already stored in saves.

* Keeps all pilots/pilot defs up-to-date with the current state of the ability tree (stored in SimGameConstants)

* Prevent crashes/save game loss from changing the ability tree

* Changes the barracks UI to show tooltips for passive abilities that are not primary abilities

* Can add abilities based on Faction or Tag

* Can swap abilities for the AI (until adding to the AI is added)

### Ignoring Pilots By Tag

Pilots that have any of these tags will be ignored

```json
"IgnorePilotsWithTags": [ "pilot_release_skirmish", "pilot_release_ksbeta" ]
```

### Adding Abilities based on Faction/Tag

Add to the `FactionAbilities` or `TagAbilities` in the settings

```json
"FactionAbilities": {
    "AuriganPirates": [ "MyAbilityDef1", "MyAbilityDef2" ]
},
```

```json
"TagAbilities": {
    "commander_career_soldier": [ "MyAbilityDef3", "MyAbilityDef4" ]
},
```

### Swapping AI Abilities

Add to the `SwapAIAbilities` in the settings

```json
"SwapAIAbilities": {
    "AbilityDefG8": "AbilityDefG8AI"
},
```

## Future Plans

* Provide some sort of mechanism for allowing AI pilots to use active abilities that have been modded in

* Update all save information to remove/update effects from abilities that have been removed/updated (low prio)

