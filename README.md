# LATimelineReminderSync

A Windows background service that syncs [TimelineReminders (Liquid)](https://www.curseforge.com/wow/addons/timeline-reminders) addon profiles from GitHub to your local WoW SavedVariables. Built for the **Liberty & Allegiance** raid guild to share boss reminder configs across the roster.

## How It Works

1. Polls a GitHub repo for a `manifest.json` listing encounter reminder files (Lua snippets).
2. Downloads each encounter file and merges it into `LiquidRemindersSaved.lua` under a dedicated `"Liberty & Allegiance"` profile, keyed by encounter ID and difficulty.
3. Detects WoW launching via WMI process events and triggers an immediate sync (with a cooldown to avoid spamming).
4. Content is hashed so unchanged files are skipped — only real updates touch your SavedVariables.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows (WMI process watching and `sc.exe` service registration are Windows-only)

## Building

Dev build:

```bash
dotnet build
```

Self-contained Windows executable:

```bash
dotnet publish src/LATimelineReminderSync/LATimelineReminderSync.csproj -c Release -r win-x64 --self-contained
```

Output lands in `src/LATimelineReminderSync/bin/Release/net8.0/win-x64/publish/`.

## Configuration

Edit `appsettings.json` (next to the executable):

```json
{
  "SyncService": {
    "SourceUrl": "https://raw.githubusercontent.com/jordan24obe/LATimelineReminderSync/main/reminders",
    "ManifestFileName": "manifest.json",
    "ProfileName": "Liberty & Allegiance",
    "AddonDataFolder": "C:\\Program Files (x86)\\World of Warcraft\\_retail_\\WTF\\Account\\YOUR_ACCOUNT\\SavedVariables",
    "PollIntervalSeconds": 300,
    "WoWLaunchCooldownSeconds": 30,
    "LogLevel": "Information"
  }
}
```

| Field | Description |
|---|---|
| `SourceUrl` | Base URL for the raw GitHub directory containing the manifest and `.lua` files. |
| `ManifestFileName` | Name of the JSON manifest file at `{SourceUrl}/{ManifestFileName}`. Default: `manifest.json`. |
| `ProfileName` | Profile name written into `LiquidRemindersSaved.lua`. Default: `Liberty & Allegiance`. |
| `AddonDataFolder` | Path to your WoW `SavedVariables` folder. Update `YOUR_ACCOUNT` to match your account name. |
| `PollIntervalSeconds` | How often (in seconds) the service polls GitHub for updates. Default: `300` (5 min). |
| `WoWLaunchCooldownSeconds` | Minimum seconds between WoW-launch-triggered syncs. Default: `30`. |
| `LogLevel` | Serilog minimum level: `Debug`, `Information`, `Warning`, `Error`, `Fatal`. Default: `Information`. |

## CLI Commands

```
LATimelineReminderSync <verb> [options]
```

| Verb | Description |
|---|---|
| `run` | Run in foreground/console mode (not as a Windows service). Good for testing. |
| `install` | Register as a Windows service via `sc.exe` (requires admin). |
| `uninstall` | Remove the Windows service registration. |
| `start` | Start the registered Windows service. |
| `stop` | Stop the registered Windows service. |
| `merge` | One-shot manual merge for testing (see below). |

### merge

Manually merge a single Lua reminder file into a SavedVariables file:

```bash
LATimelineReminderSync merge --file <SavedVariables.lua> --profile <reminder.lua> --encounter <id> --difficulty <index>
```

Example:

```bash
LATimelineReminderSync merge \
  --file LiquidRemindersSaved.lua \
  --profile reminders/The-Voidspire/mythic/Imperator-Averzian.lua \
  --encounter 3176 \
  --difficulty 2
```

Creates a `.bak` backup of the target file before writing.

## Running Tests

```bash
dotnet test
```

## Repo Structure for Reminders

Reminder files live in the `reminders/` directory, organized by raid tier, difficulty, and boss:

```
reminders/
├── manifest.json
├── The-Voidspire/
│   ├── mythic/
│   │   ├── Imperator-Averzian.lua
│   │   ├── Alleria-Windrunner.lua
│   │   └── ...
│   ├── heroic/
│   │   └── ...
│   └── normal/
│       └── ...
└── The-Dreamrift/
    ├── mythic/
    │   └── Chimaerus.lua
    ├── heroic/
    │   └── ...
    └── normal/
        └── ...
```

### manifest.json

Lists every encounter file the service should sync. Each entry maps a file to its encounter ID and difficulty:

```json
{
  "encounters": [
    {
      "encounterId": 3176,
      "encounterName": "Imperator Averzian",
      "difficultyIndex": 2,
      "fileName": "The-Voidspire/mythic/Imperator-Averzian.lua"
    }
  ]
}
```

- `encounterId` — WoW Journal encounter ID.
- `difficultyIndex` — `0` = normal, `1` = heroic, `2` = mythic.
- `fileName` — Path relative to `reminders/`.

## For Raid Leaders

To add or update reminders:

1. In WoW, open TimelineReminders (Liquid) and configure your reminders for a boss.
2. Export the encounter profile — this gives you a Lua snippet.
3. Save it as a `.lua` file under the correct path in `reminders/` (e.g., `reminders/The-Voidspire/mythic/Boss-Name.lua`).
4. Add or update the entry in `manifest.json` with the correct `encounterId`, `difficultyIndex`, and `fileName`.
5. Commit and push. The service will pick up the changes on the next poll cycle.

## License

See [LICENSE](LICENSE).
