using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace SysBot.Pokemon;

public class EncounterSettingsSWSH : IBotStateSettings, ICountSettings
{
    private const string Counts = nameof(Counts);
    private const string Encounter = nameof(Encounter);
    private const string Settings = nameof(Settings);
    public override string ToString() => "Encounter Bot SWSH Settings";

    [Category(Encounter), Description("The method used by the Line and Reset bots to encounter Pokémon.")]
    public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FossilSettings Fossil { get; set; } = new();

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public MaxLairSettings MaxLair { get; set; } = new();

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DenSettings Den { get; set; } = new();

    [Category(Encounter), Description("When enabled, the bot will continue after finding a suitable match.")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Encounter), Description("When enabled, the 100% catch cheat will be enabled. Only applicable for the Calyrex routine")]
    public bool EnableCatchCheat { get; set; }

    [Category(Encounter), Description("Set egg mode to unlimited")]
    public bool UnlimitedMode { get; set; }

    [Category(Encounter), Description("When mode is unlimited, this folder will be used for parents.")]
    public string UnlimitedParentsFolder { get; set; } = string.Empty;

    [Category(Encounter), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
    public bool ScreenOff { get; set; }

    private int _completedAdventure;
    private int _completedWild;
    private int _completedLegend;
    private int _completedEggs;
    private int _completedFossils;

    [Category(Counts), Description("Max Lair Adventures")]
    public int CompletedAdventures
    {
        get => _completedAdventure;
        set => _completedAdventure = value;
    }

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

    [Category(Counts), Description("Fossil Pokémon Revived")]
    public int CompletedFossils
    {
        get => _completedFossils;
        set => _completedFossils = value;
    }

    [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
    public bool EmitCountsOnStatusCheck { get; set; }

    public int AddCompletedAdventures() => Interlocked.Increment(ref _completedAdventure);
    public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);
    public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);
    public int AddCompletedEggs() => Interlocked.Increment(ref _completedEggs);
    public int AddCompletedFossils() => Interlocked.Increment(ref _completedFossils);

    public void CreateDefaults(string path)
    {
        var unlimited = Path.Combine(path, "unlimited");
        Directory.CreateDirectory(unlimited);
        UnlimitedParentsFolder = unlimited;
    }

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedAdventures != 0)
            yield return $"Max Lair Adventures: {CompletedAdventures}";
        if (CompletedEncounters != 0)
            yield return $"Wild Encounters: {CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"Legendary Encounters: {CompletedLegends}";
        if (CompletedEggs != 0)
            yield return $"Eggs Received: {CompletedEggs}";
        if (CompletedFossils != 0)
            yield return $"Completed Fossils: {CompletedFossils}";
    }
}
