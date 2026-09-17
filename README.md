# SkillScale

Valheim assumes you have infinite free time.
SkillScale assumes you might not. You pick how fast each skill levels up, and how much you lose when you die.

## What it does

Each skill has its own gain setting:

- `50` = 50% more XP
- `-50` = 50% less XP
- `0` = normal
- `-100` = no XP for that skill

Death penalty uses the same numbers.

On a dedicated server, settings can sync to players and the config can be locked.

## Settings

F1 Configuration Manager, or:

`BepInEx/config/com.ljindustries.valheim.skillscale.cfg`

**General**

- Lock Configuration: only server admins can change settings (default on)

**Skills**

- Change the skill gain factor: master on/off (default on)
- Display notifications for skills gained: top-left XP messages (default on)
- Should running skill notifications be ignored: hide Run spam (default on)
- Skill Notification Text Size: default 14
- One gain setting per skill. Default 0
- Death Penalty Factor Multiplier: default 0
