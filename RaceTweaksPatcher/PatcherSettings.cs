namespace RaceTweaksPatcher;

public sealed class PatcherSettings
{
    public bool DryRun = false;

    public List<RaceTweak?>? RaceTweaks =
    [
        new() { Race = "0131F9:Skyrim.esm", NoKnockdowns = FlagChange.Enable }
    ];
}

public sealed class RaceTweak
{
    public string Race = "";
    public FlagChange NoKnockdowns;
}

public enum FlagChange
{
    Unchanged,
    Enable,
    Disable
}
