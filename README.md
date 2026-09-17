# SkillScale

Control how fast each Valheim skill levels up, and how much skill you lose when you die.

Built as a maintained replacement for AzuSkillTweaks. Same idea, updated for current Valheim,
including a fix so picking bushes, berries, and crops works again.

**Turn off or uninstall AzuSkillTweaks before using this.** Do not run both at once.

## What it does

Each skill has its own gain setting in the config:

- `50` = 50% more XP
- `-50` = 50% less XP
- `0` = normal
- `-100` = no XP for that skill

There is also a setting for how much skill you lose on death, using the same numbers.

If you put the mod on your dedicated server too, those settings sync to everyone and the
server can lock the config.

## Install

Use r2modman or Thunderstore Mod Manager. For a manual install, put `SkillScale.dll` in
`BepInEx/plugins/`.

Works in single player. For multiplayer with synced settings, install it on the server as
well and use the same version.

## Settings

Open the config with F1, or edit:

`BepInEx/config/com.ljindustries.valheim.skillscale.cfg`

**General**

- Lock Configuration: only server admins can change settings (default on)

**Skills**

- Change the skill gain factor: master on/off switch (default on)
- Display notifications for skills gained: small top-left XP messages (default on)
- Should running skill notifications be ignored: hide Run spam (default on)
- Skill Notification Text Size: default 14
- One gain setting per skill (Sword, Knives, Clubs, and so on). Default 0
- Death Penalty Factor Multiplier: default 0

## Credits

Based on the skill settings from OdinsQOL / AzuSkillTweaks by Azumatt (AGPL). Uses
ServerSync by Tykea (MIT).
