namespace SysBot.Pokemon;

using System.ComponentModel;

public class MaxLairSettings
{
    private const string MaxLair = nameof(MaxLair);
    private const string Counts = nameof(Counts);
    public override string ToString() => "Max Lair Bot Settings";

    [Category(MaxLair), Description("(Injects) species of legendary Pokémon to hunt for.")]
    public MaxLairSpecies Species { get; set; } = MaxLairSpecies.None;

    [Category(MaxLair), Description("Save seed when stats found.")]
    public bool RememberSeed { get; set; }

    [Category(MaxLair), Description("Saved S0.")]
    public ulong Seed0 { get; set; }

    [Category(MaxLair), Description("Saved S1.")]
    public ulong Seed1 { get; set; }
}