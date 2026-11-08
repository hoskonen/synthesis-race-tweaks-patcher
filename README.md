# Race Tweaks Patcher

A Synthesis patcher for Skyrim Special Edition and Anniversary Edition. By default, it enables **No Knockdowns** for `GiantRace` (`0131F9:Skyrim.esm`), preventing Unrelenting Force from knocking giants down. The patcher starts from the winning race record, preserves unrelated fields and flags, and writes an override only when a requested flag needs to change.

## Install in Synthesis

Requires Synthesis 0.36.6 and the .NET 10 SDK.

1. Download [RaceTweaksPatcher.synth](RaceTweaksPatcher.synth).
2. Open Synthesis, select the Skyrim SE profile and patcher group, then double-click the downloaded file.
3. Review the patcher settings, run the group, and load its output after the input plugins.

## Configure races

In **Settings → Race Tweaks**, enter the race's plugin-local FormID and defining plugin, such as `0131F9:Skyrim.esm`. Set **No Knockdowns** to **Enable**, **Disable**, or **Unchanged**. An empty list disables all tweaks. The first valid entry for a race takes precedence.

## Preview changes with dry-run

Enable **Dry Run** in Settings to preview changes in the log. Dry-run applies zero overrides. Synthesis still exports an ESP, carrying forward any records from earlier patchers in the group. Use a separate output filename to keep an existing gameplay patch.

See the [development notes](DEVELOPMENT.md) for build, test, and CLI instructions.
