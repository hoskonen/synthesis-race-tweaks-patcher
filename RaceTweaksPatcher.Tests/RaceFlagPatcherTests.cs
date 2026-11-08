using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace RaceTweaksPatcher.Tests;

[TestClass]
public sealed class RaceFlagPatcherTests
{
    private const Race.Flag Knockdowns = Race.Flag.NoKnockdowns;
    private const Race.Flag Unrelated = Race.Flag.Playable;

    [TestMethod]
    [DataRow(Unrelated, FlagChange.Enable, Unrelated | Knockdowns)]
    [DataRow(Unrelated | Knockdowns, FlagChange.Disable, Unrelated)]
    [DataRow(Unrelated, FlagChange.Unchanged, Unrelated)]
    [DataRow(Unrelated | Knockdowns, FlagChange.Unchanged, Unrelated | Knockdowns)]
    [DataRow(Unrelated | Knockdowns, FlagChange.Enable, Unrelated | Knockdowns)]
    [DataRow(Unrelated, FlagChange.Disable, Unrelated)]
    public void ChangesOnlyTheRequestedBit(Race.Flag input, FlagChange change, Race.Flag expected)
    {
        Assert.AreEqual(expected, RaceFlagPatcher.ApplyNoKnockdowns(input, change));
    }

    [TestMethod]
    public void DryRunAndPatchEvaluateTheSameWinningRecords()
    {
        var source = MakeMod("Skyrim.esm");
        var giant = source.Races.AddNew("GiantRace");
        giant.Flags = Knockdowns;
        var second = source.Races.AddNew("SecondRace");
        second.Flags = Unrelated | Knockdowns;
        var matching = source.Races.AddNew("AlreadyEnabledRace");
        matching.Flags = Knockdowns;
        var unchanged = source.Races.AddNew("UnchangedRace");
        var other = source.Races.AddNew("UnconfiguredRace");

        var laterMod = MakeMod("Later.esp");
        var winner = laterMod.Races.GetOrAddAsOverride(giant);
        winner.EditorID = "WinningGiantRace";
        winner.Flags = Unrelated;

        using var cache = new ISkyrimModGetter[] { source, laterMod }.ToImmutableLinkCache();
        var patch = MakeMod("Patch.esp");
        var dryPatch = MakeMod("DryPatch.esp");
        var dryLogs = new List<string>();
        var patchLogs = new List<string>();
        var settings = new PatcherSettings
        {
            DryRun = true,
            RaceTweaks =
            [
                new() { Race = giant.FormKey.ToString(), NoKnockdowns = (FlagChange)99 },
                new() { Race = giant.FormKey.ToString(), NoKnockdowns = FlagChange.Enable },
                new() { Race = giant.FormKey.ToString(), NoKnockdowns = FlagChange.Disable },
                new() { Race = second.FormKey.ToString(), NoKnockdowns = FlagChange.Disable },
                new() { Race = matching.FormKey.ToString(), NoKnockdowns = FlagChange.Enable },
                new() { Race = unchanged.FormKey.ToString() },
                new() { Race = "123456:Missing.esp", NoKnockdowns = FlagChange.Enable },
                new() { Race = "invalid" }
            ]
        };
        var dryReport = RaceFlagPatcher.Run(settings, cache, dryPatch, dryLogs.Add);
        settings.DryRun = false;
        var patchReport = RaceFlagPatcher.Run(settings, cache, patch, patchLogs.Add);

        Assert.AreEqual(0, dryPatch.Races.Count);
        CollectionAssert.AreEqual(dryReport.Entries.ToArray(), patchReport.Entries.ToArray());
        RaceEntryStatus[] expectedStatuses =
        [
            RaceEntryStatus.Invalid, RaceEntryStatus.ChangeRequired, RaceEntryStatus.Duplicate,
            RaceEntryStatus.ChangeRequired, RaceEntryStatus.AlreadyMatching, RaceEntryStatus.Unchanged,
            RaceEntryStatus.Unresolved, RaceEntryStatus.Invalid
        ];
        CollectionAssert.AreEqual(expectedStatuses, dryReport.Entries.Select(entry => entry.Status).ToArray());
        var giantProposal = dryReport.Entries[1];
        Assert.AreSame(winner, giantProposal.Winner);
        Assert.AreEqual(FlagChange.Enable, giantProposal.RequestedChange);
        Assert.AreEqual(Unrelated, giantProposal.ExistingFlags);
        Assert.AreEqual(Unrelated | Knockdowns, giantProposal.ProposedFlags);
        Assert.AreEqual(new RacePatchSummary(8, 2, 1, 1, 1, 1, 2, 0), dryReport.Summary);
        Assert.AreEqual(4, dryReport.Summary.ResolvedRaces);
        Assert.AreEqual(dryReport.Summary with { OverridesWritten = 2 }, patchReport.Summary);

        Assert.AreEqual(2, patch.Races.Count);
        Assert.AreEqual("WinningGiantRace", patch.Races[giant.FormKey].EditorID);
        Assert.AreEqual(Unrelated | Knockdowns, patch.Races[giant.FormKey].Flags);
        Assert.AreEqual(Unrelated, patch.Races[second.FormKey].Flags);
        Assert.AreEqual(Unrelated, winner.Flags);
        Assert.IsFalse(patch.Races.ContainsKey(matching.FormKey));
        Assert.IsFalse(patch.Races.ContainsKey(unchanged.FormKey));
        Assert.IsFalse(patch.Races.ContainsKey(other.FormKey));
        StringAssert.Contains(dryLogs[0], "Mode: DRY RUN");
        StringAssert.Contains(patchLogs[0], "Mode: PATCH");
        Assert.IsTrue(dryLogs.Exists(line => line.Contains("Disabled -> Enabled") && line.Contains("WOULD MODIFY")));
        Assert.IsTrue(patchLogs.Exists(line => line.Contains("Disabled -> Enabled") && line.Contains("Status: MODIFIED")));
        StringAssert.Contains(dryLogs[^1], "Overrides written: 0");
        StringAssert.Contains(patchLogs[^1], "Overrides written: 2");
    }

    [TestMethod]
    public void DryRunLeavesAnExistingOverrideUntouched()
    {
        var source = MakeMod("Skyrim.esm");
        var race = source.Races.AddNew("GiantRace");
        using var cache = source.ToImmutableLinkCache();
        var patch = MakeMod("Patch.esp");
        var existing = patch.Races.GetOrAddAsOverride(race);
        existing.Flags = Unrelated;

        var report = RaceFlagPatcher.Run(new PatcherSettings
        {
            DryRun = true,
            RaceTweaks = [new() { Race = race.FormKey.ToString(), NoKnockdowns = FlagChange.Enable }]
        }, cache, patch, _ => { });

        Assert.AreEqual(1, report.Summary.ChangesRequired);
        Assert.AreEqual(0, report.Summary.OverridesWritten);
        Assert.AreEqual(1, patch.Races.Count);
        Assert.AreSame(existing, patch.Races[race.FormKey]);
        Assert.AreEqual(Unrelated, existing.Flags);
    }

    private static SkyrimMod MakeMod(string name) => new(ModKey.FromFileName(name), SkyrimRelease.SkyrimSE);
}
