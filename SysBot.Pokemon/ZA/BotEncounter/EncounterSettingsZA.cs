using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon;

public class EncounterSettingsZA : IBotStateSettings, ICountSettings
{
    private const string Counts = nameof(Counts);
    private const string Encounter = nameof(Encounter);
    private const string Settings = nameof(Settings);
    public override string ToString() => "Encounter Bot ZA Settings";

    [Category(Encounter), Description("When enabled, the bot will continue after finding a suitable match.")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Encounter), Description("When enabled, the bot will only stop when encounter has a Scale of XXXS or XXXL.")]
    public bool MinMaxScaleOnly { get; set; } = false;

    [Category(Encounter), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
    public bool ScreenOff { get; set; }

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public OverworldEncounter Overworld { get; set; } = new();

    [Category(Encounter)]
    public class OverworldEncounter
    {
        public override string ToString() => "Overworld Bot Settings";

        [Category(Encounter), DisplayName("Which mode is used to find the target in the overworld.")]
        public OverworldModeZA Mode { get; set; }

        [Category(Encounter), DisplayName("Stop when maximum (10) shinies are stored")]
        public bool StopOnMaxShiniesStored { get; set; } = true;

        [Category(Encounter), DisplayName("Check overworld after amount of bench sitting (only applicable when searching for shinies), use '0' to disable")]
        public int OverworldSpawnCheck { get; set; } = 1;
    }

    private int _completedWild;
    private int _completedLegend;

    [Category(Counts), Description("Encountered Wild Pokémon")]
    public int CompletedEncounters
    {
        get => _completedWild;
        set => _completedWild = value;
    }

    [Category(Counts), Description("Encountered Legendary Pokémon")]
    public int CompletedLegends
    {
        get => _completedLegend;
        set => _completedLegend = value;
    }

    [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
    public bool EmitCountsOnStatusCheck { get; set; }

    public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);
    public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);
    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedEncounters != 0)
            yield return $"Wild Encounters: {CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"Legendary Encounters: {CompletedLegends}";
    }

    public enum OverworldModeZA
    {
        BenchSit,
        WildZoneEntrance
    }
}
