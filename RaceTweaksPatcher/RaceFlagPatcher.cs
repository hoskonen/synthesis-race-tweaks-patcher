using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace RaceTweaksPatcher;

internal static class RaceFlagPatcher
{
    private const string Prefix = "[Race Tweaks]";

    internal static RacePatchReport Run(
        PatcherSettings settings, ILinkCache linkCache, ISkyrimMod patch, Action<string> log)
    {
        log($"{Prefix} Mode: {(settings.DryRun ? "DRY RUN" : "PATCH")}");
        if (settings.RaceTweaks is null)
            log($"{Prefix} RaceTweaks is missing. Use an empty list to configure no races.");

        var entries = Evaluate(settings.RaceTweaks ?? [], linkCache).ToArray();
        int written = 0;
        foreach (var entry in entries)
        {
            if (entry.Diagnostic is { } diagnostic)
                log($"{Prefix} {diagnostic}");

            if (entry.Status != RaceEntryStatus.ChangeRequired || entry.Winner is not { } race)
                continue;

            if (!settings.DryRun)
            {
                patch.Races.GetOrAddAsOverride(race).Flags = entry.ProposedFlags;
                written++;
            }

            log($"{Prefix} {race.EditorID ?? "(no EditorID)"} ({race.FormKey})\n" +
                $"  No Knockdowns: {FlagValue(entry.ExistingFlags)} -> {FlagValue(entry.ProposedFlags)}\n" +
                $"  Status: {(settings.DryRun ? "WOULD MODIFY" : "MODIFIED")}");
        }

        int Count(RaceEntryStatus status) => entries.Count(entry => entry.Status == status);
        var summary = new RacePatchSummary(entries.Length, Count(RaceEntryStatus.ChangeRequired),
            Count(RaceEntryStatus.AlreadyMatching), Count(RaceEntryStatus.Unchanged),
            Count(RaceEntryStatus.Unresolved), Count(RaceEntryStatus.Duplicate),
            Count(RaceEntryStatus.Invalid), written);
        log($"{Prefix} Summary\n" +
            $"  Configured entries: {summary.ConfiguredEntries}\n" +
            $"  Resolved races: {summary.ResolvedRaces}\n" +
            $"  {(settings.DryRun ? "Would modify" : "Modified")}: {summary.ChangesRequired}\n" +
            $"  Already matching: {summary.AlreadyMatching}\n" +
            $"  Unchanged (no change requested): {summary.Unchanged}\n" +
            $"  Unresolved: {summary.Unresolved}\n" +
            $"  Duplicate entries: {summary.DuplicateEntries}\n" +
            $"  Invalid entries: {summary.InvalidEntries}\n" +
            $"  Overrides written: {summary.OverridesWritten}");
        return new(entries, summary);
    }

    private static IEnumerable<RaceEvaluation> Evaluate(IReadOnlyList<RaceTweak?> tweaks, ILinkCache linkCache)
    {
        var seen = new HashSet<FormKey>();
        for (int i = 0; i < tweaks.Count; i++)
        {
            var tweak = tweaks[i];
            if (tweak is null || !FormKey.TryFactory(tweak.Race.AsSpan().Trim(), out var key) || key.IsNull)
            {
                yield return new(RaceEntryStatus.Invalid, Diagnostic:
                    $"Race entry {i + 1} has an invalid FormKey '{tweak?.Race}'. Use six hexadecimal digits and the source plugin, for example 0131F9:Skyrim.esm.");
                continue;
            }

            if (!Enum.IsDefined(tweak.NoKnockdowns))
            {
                yield return new(RaceEntryStatus.Invalid, Diagnostic:
                    $"Race entry {i + 1} ({key}) has an invalid No Knockdowns setting '{tweak.NoKnockdowns}'.");
                continue;
            }

            if (!seen.Add(key))
            {
                yield return new(RaceEntryStatus.Duplicate, Diagnostic:
                    $"Race entry {i + 1} duplicates {key}. Using the first valid entry.");
                continue;
            }

            if (!linkCache.TryResolve<IRaceGetter>(key, out var race) || race.IsDeleted)
            {
                yield return new(RaceEntryStatus.Unresolved, Diagnostic:
                    $"No active winning RACE record found for {key}. Check the FormKey and enabled plugins.");
                continue;
            }

            var existingFlags = race.Flags;
            var proposedFlags = ApplyNoKnockdowns(existingFlags, tweak.NoKnockdowns);
            var status = tweak.NoKnockdowns switch
            {
                FlagChange.Unchanged => RaceEntryStatus.Unchanged,
                _ when existingFlags == proposedFlags => RaceEntryStatus.AlreadyMatching,
                _ => RaceEntryStatus.ChangeRequired
            };
            yield return new(status, race, tweak.NoKnockdowns, existingFlags, proposedFlags);
        }
    }

    private static string FlagValue(Race.Flag flags) =>
        flags.HasFlag(Race.Flag.NoKnockdowns) ? "Enabled" : "Disabled";

    internal static Race.Flag ApplyNoKnockdowns(Race.Flag flags, FlagChange change) => change switch
    {
        FlagChange.Enable => flags | Race.Flag.NoKnockdowns,
        FlagChange.Disable => flags & ~Race.Flag.NoKnockdowns,
        _ => flags
    };
}
