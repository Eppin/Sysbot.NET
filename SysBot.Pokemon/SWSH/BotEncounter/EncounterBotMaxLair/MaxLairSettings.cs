namespace SysBot.Pokemon;

using System.ComponentModel;

public class MaxLairSettings
{
    private const string MaxLair = nameof(MaxLair);
    public override string ToString() => "Max Lair Bot Settings";

    [Category(MaxLair), Description("(Injects) species of legendary Pokémon to hunt for.")]
    public MaxLairSpecies Species { get; set; } = MaxLairSpecies.None;

    [Category(MaxLair), Description("Inject 1HKO cheat to rush the enemies. It is unlikely to be able to complete an adventure without this cheat enabled.")]
    public bool InstantKill { get; set; } = true;
}
