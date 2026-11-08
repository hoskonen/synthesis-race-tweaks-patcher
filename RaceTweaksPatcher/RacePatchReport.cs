using Mutagen.Bethesda.Skyrim;

namespace RaceTweaksPatcher;

internal enum RaceEntryStatus
{
    ChangeRequired,
    AlreadyMatching,
    Unchanged,
    Unresolved,
    Duplicate,
    Invalid
}

internal sealed record RaceEvaluation(
    RaceEntryStatus Status,
    IRaceGetter? Winner = null,
    FlagChange RequestedChange = FlagChange.Unchanged,
    Race.Flag ExistingFlags = default,
    Race.Flag ProposedFlags = default,
    string? Diagnostic = null);

internal sealed record RacePatchSummary(
    int ConfiguredEntries, int ChangesRequired, int AlreadyMatching, int Unchanged,
    int Unresolved, int DuplicateEntries, int InvalidEntries, int OverridesWritten)
{
    public int ResolvedRaces => ChangesRequired + AlreadyMatching + Unchanged;
}

internal sealed record RacePatchReport(IReadOnlyList<RaceEvaluation> Entries, RacePatchSummary Summary);
