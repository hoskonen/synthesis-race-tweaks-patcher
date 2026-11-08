# Race Tweaks Patcher

Race Tweaks Patcher applies targeted changes to race records in Skyrim Special Edition and Anniversary Edition. By default it enables **No Knockdowns** for the vanilla `GiantRace` (`0131F9:Skyrim.esm`), preventing Unrelenting Force from knocking giants down. The default has one target.

For each configured FormKey, the patcher reads the winning race record from the active load order. It copies that record into the output only when the requested flag differs and changes only that bit. Load the Synthesis output after the input plugins whose changes it forwards, and rerun Synthesis when their order or contents change.

## Requirements

- Skyrim Special Edition or Anniversary Edition.
- Synthesis 0.36.6 and the .NET 10 SDK. The patcher references Mutagen.Bethesda.Skyrim 0.54.4 and Mutagen.Bethesda.Synthesis 0.36.6.
- SSEEdit for manual verification.

## Install in Synthesis

1. Download [RaceTweaksPatcher.synth](RaceTweaksPatcher.synth) as a file from the repository.
2. Open Synthesis, select your Skyrim SE profile and the group you want to use, then double-click the downloaded `.synth` file. It points to `hoskonen/synthesis-race-tweaks-patcher` and selects `RaceTweaksPatcher/RaceTweaksPatcher.csproj`.
3. If you prefer to add it manually, add a **Git Repository** patcher, paste `https://github.com/hoskonen/synthesis-race-tweaks-patcher` into its **Input** tab, and select the same project. Use the `v0.1.0` tag in Versioning to reproduce this version.
4. Once Synthesis has built the patcher, open **Settings** to review the defaults. Run the group with your intended load order and enable the resulting plugin after its input mods. The group determines the output filename.

[Synthesis .synth installation guide](https://mutagen-modding.github.io/Synthesis/Synth-File/)

## Configure races

Under **Race Tweaks** in the patcher settings, add one entry per race. Enter the FormKey as six hexadecimal digits, a colon, and the plugin that originally defines the record. For example, use `0131F9:Skyrim.esm` for `GiantRace` even if a later plugin overrides it. For a mod-added race, use the record's plugin-local ID and original plugin filename.

Set **No Knockdowns** to **Enable**, **Disable**, or **Unchanged**. New entries default to **Unchanged**. An empty list disables all tweaks. If several valid entries name the same FormKey, the first takes precedence. Synthesis logs changed flags, invalid settings, missing targets, duplicates, and final counts.

This example enables the flag for giants and disables it for Dwarven Centurions. The second entry is optional and is outside the default configuration.

```json
{
  "DryRun": false,
  "RaceTweaks": [
    { "Race": "0131F9:Skyrim.esm", "NoKnockdowns": "Enable" },
    { "Race": "0131F1:Skyrim.esm", "NoKnockdowns": "Disable" }
  ]
}
```

## Preview changes with dry-run

In the patcher's Synthesis **Settings** tab, enable **Dry Run**, save, and run the patcher in a group by itself. Both modes read the same winning races and calculate the same flag changes. Dry-run writes zero overrides and skips plugin export. Turn **Dry Run** off to generate the patch. It is off by default.

```text
[Race Tweaks] Mode: DRY RUN
[Race Tweaks] GiantRace (0131F9:Skyrim.esm)
  No Knockdowns: Disabled -> Enabled
  Status: WOULD MODIFY
[Race Tweaks] Summary
  Configured entries: 1
  Resolved races: 1
  Would modify: 1
  Already matching: 0
  Unchanged (no change requested): 0
  Unresolved: 0
  Duplicate entries: 0
  Invalid entries: 0
  Overrides written: 0
```

In **PATCH** mode the same change appears as **MODIFIED**, and **Overrides written** counts the race records this patcher updated. **Already matching** counts races whose winning flags already have the requested value. **Unchanged** counts resolved races with no change requested. These two categories appear only in the summary. Missing or deleted winners, duplicate entries, and invalid entries also get individual diagnostics.

The dry-run setting applies only to Race Tweaks Patcher. Synthesis 0.36.6 may leave the patcher row looking active because it expects an output ESP to mark the row complete. Check the log for the summary and `Finished patch.`, or run the patcher directly for a CLI exit code. [Synthesis output finalizer](https://github.com/Mutagen-Modding/Synthesis/blob/0.36.6/Synthesis.Bethesda.Execution/Running/Runner/FinalizePatcherRun.cs)

## Build and develop locally

From the solution directory, run:

```powershell
dotnet build -c Release
dotnet test -c Release
```

For a local checkout, add a **Local Solution** patcher in Synthesis using `Race Tweaks Patcher.sln` and select **RaceTweaksPatcher**. See the [development notes](DEVELOPMENT.md) for the architecture and CLI verification.

## Verify in xEdit and in game

This check requires a Skyrim SE or AE installation and SSEEdit.

1. For Ancestries, run Synthesis and SSEEdit with the Ancestries MO2 profile so they see its virtual Data directory and load order. Disable the previously tested manual override and any prior generated output for this check. Keep the race mods you want to forward enabled.
2. Run the patcher with **Dry Run** off. If the input winner has **No Knockdowns** disabled, the patcher log should show `GiantRace` changing from **Disabled** to **Enabled**. If the winner already enables the flag, expect no override.
3. Load the same plugins and the new Synthesis output in SSEEdit. Find `GiantRace` by FormID `000131F9` and compare the output column with the preceding winning column. Under the race's data flags, confirm **No Knockdowns** is enabled. Compare the remaining flags and fields against that winning column to confirm they match.
4. If the group contains only this patcher, check that the output's Race group contains only `GiantRace` when a change was needed. For a shared output, isolate this patcher in its own group to verify its race changes independently.
5. Enable the generated output and test Unrelenting Force against a giant that uses `GiantRace`. Confirm the shout leaves the giant standing.
