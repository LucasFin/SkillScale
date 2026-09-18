# SkillScale

Valheim assumes you have infinite free time.
SkillScale assumes you might not. You pick how fast each skill levels up, and how much you lose when you die.

## What it does

Pick an employment preset, or set your own rates:

| Preset | XP rate |
| --- | --- |
| Giga Unemployed | 0.75x |
| Unemployed | 1x (normal, default) |
| Part-time | 1.5x |
| Full-time | 2.5x |

**Global dial** sets the rate for every skill that is still on that value. **Per skill** is the final XP rate for that skill (not multiplied on top of global). Example: global `2.5x`, then set Farming to `5x` - Farming is `5x`, everything else stays `2.5x`. No math.

Death skill loss uses the same kind of multiplier (`1` = normal, `0` = none).

On a dedicated server, settings can sync to players and the config can be locked. Players without the mod are not kicked.

## How to change settings

**In-game panel (built in):** press **\\** (backslash, the `\` / `|` key) by default. The mouse unlocks while the panel is open. Backslash or Esc closes it; opening inventory or the pause menu also closes it so you do not click through. Hotkey is ignored while chat, console, map, or text prompts have focus. Change it under `6 - In-Game Menu` if you want.

**Config file:**

`BepInEx/config/com.ljindustries.valheim.skillscale.cfg`

The in-game panel covers presets, global, death loss, and a scrollable per-skill list. The `.cfg` shows the same values.

## Settings overview

**General** - lock config, enable scaling

**Rates** - employment preset, global dial

**Per Skill** - final XP rate per skill (default matches global)

**Death** - death skill loss multiplier (default `1`)

**Notifications** - optional XP progress toasts (throttled; Run skipped by default). Toggle in the in-game menu.

**In-Game Menu** - enable panel, choose hotkey (not synced; each player can pick their own)

## Note for existing installs

1.1.0 uses real multipliers (`1`, `1.5`, `2.5`) instead of the old percent-style numbers (`250` = +250%). Check your config after updating. Per-skill values are final rates, not stacked on global.
