using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// Pokémon Legends: Z-A RAM offsets
/// </summary>
public class PokeDataOffsetsZA
{
    public const string ZAGameVersion = "1.0.1";
    public const string LegendsZAID = "0100F43008C44000";

    public IReadOnlyList<long> BoxStartPokemonPointer { get; } = [0x5F0B250, 0xB0, 0x978, 0x0]; // Thanks Anubis
    public IReadOnlyList<long> MyStatusPointer { get; } = [0x5F0B250, 0xA0, 0x40]; // Thanks Anubis
    public IReadOnlyList<long> KOverworldPointer { get; } = [0x5F0B1B0, 0x30, 0x08, 0x8A0];

    public const int OverworldBlockKey = 0x5E8E1711;

    public const int BoxFormatSlotSize = 0x158;
}
