using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon;

public class EncounterSettingsSV : IBotStateSettings, ICountSettings
{
    private const string Counts = nameof(Counts);
    private const string Encounter = nameof(Encounter);
    private const string Settings = nameof(Settings);
    public override string ToString() => "Encounter Bot SV Settings";

    [Category(Encounter), Description("When enabled, the bot will continue after finding a suitable match.")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Encounter), Description("When enabled, the bot will only stop when encounter has a Scale of XXXS or XXXL.")]
    public bool MinMaxScaleOnly { get; set; } = false;

    [Category(Encounter), Description("When enabled, the bot will look for 3 Segment Dunsparce or Family of Three Maus.")]
    public bool OneInOneHundredOnly { get; set; } = true;

    [Category(Encounter), Description("When enabled, the 100% catch cheat will be enabled.")]
    public bool EnableCatchCheat { get; set; }

    [Category(Encounter), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
    public bool ScreenOff { get; set; }

    [Category(Encounter), Description("Set egg mode to unlimited")]
    public bool UnlimitedMode { get; set; }

    [Category(Encounter), Description("When mode is unlimited, this folder will be used for parents.")]
    public string UnlimitedParentsFolder { get; set; } = string.Empty;

    [Category(Encounter), Description("When mode is Scanner, keep saving the game to let the bot scan the overworld.")]
    public OverworldMode Overworld { get; set; }

    [Category(Encounter), Description("Stop condition for Mass Outbreak only.")]
    public List<MassOutbreakSearchCondition> MassOutbreakSearchConditions { get; set; } = new();

    [Category(Encounter)]
    public class MassOutbreakSearchCondition
    {
        public override string ToString() => $"{(!IsEnabled ? $"{StopOnSpecies}, condition is disabled" : $"{StopOnSpecies}-{Form}")}";

        [Category(Encounter), DisplayName("1. Enabled")]
        public bool IsEnabled { get; set; } = true;

        [Category(Encounter), DisplayName("2. Species")]
        public Species StopOnSpecies { get; set; }

        [Category(Encounter), DisplayName("3. Form, if applicable")]
        public int Form { get; set; }
    }

    private int _completedWild;
    private int _completedLegend;
    private int _completedEggs;

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

    [Category(Counts), Description("Eggs Retrieved")]
    public int CompletedEggs
    {
        get => _completedEggs;
        set => _completedEggs = value;
    }

    [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
    public bool EmitCountsOnStatusCheck { get; set; }

    public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);
    public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);
    public int AddCompletedEggs() => Interlocked.Increment(ref _completedEggs);
    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedEncounters != 0)
            yield return $"Wild Encounters: {CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"Legendary Encounters: {CompletedLegends}";
        if (CompletedEggs != 0)
            yield return $"Eggs Received: {CompletedEggs}";
    }

    public void CreateDefaults(string path)
    {
        var unlimited = Path.Combine(path, "unlimited");
        Directory.CreateDirectory(unlimited);
        UnlimitedParentsFolder = unlimited;
    }

    public enum OverworldMode
    {
        Scanner,
        ResearchStation,
        Outbreak,
        Picknick
    }
}
