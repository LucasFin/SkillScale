# Development

## Build

```bash
dotnet build SkillScale/SkillScale.csproj -c Release
```

Output: `package/SkillScale.dll`

```bash
dotnet build SkillScale/SkillScale.csproj -c Release -p:ValheimFolder="/path/to/Valheim"
```

## Harvest bug notes

`Pickable.Interact` grants farming XP before it finishes the pick. Notification code in that
path must never throw, or bushes/crops raise farming without giving the item.

## Testing

Install through r2modman. Do not copy DLLs into a profile by hand.

- Pick raspberries / blueberries / a grown crop with notifications on
- Pick a stone and a branch
- Confirm farming XP moves and the plant actually picks
- Die with death penalty at 0, -100, and 50

## Publish

Thunderstore team: `LJIndustries`. GitHub: LucasFin.
GitHub Actions secret: `THUNDERSTORE_API_KEY`
