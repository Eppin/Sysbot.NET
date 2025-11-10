using System.ComponentModel;

namespace SysBot.Pokemon;

public class FossilSettingsZA
{
    private const string Fossil = nameof(Fossil);
    public override string ToString() => "Fossil Bot Settings";

    [Category(Fossil), Description("Species of fossil Pokémon to hunt for.")]
    public FossilSpeciesZA Species { get; set; } = FossilSpeciesZA.Any;
}
