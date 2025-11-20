using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// Pokémon Legends: Z-A RAM offsets
/// </summary>
public class PokeDataOffsetsLZA
{
    public const string ZAGameVersion = "1.0.2";
    public const string LegendsZAID = "0100F43008C44000";

    public IReadOnlyList<long> BoxStartPokemonPointer { get; } = [0x5F0C250, 0xB0, 0x978, 0x0];
    public IReadOnlyList<long> MyStatusPointer { get; } = [0x5F0C250, 0x80, 0x100];
    public IReadOnlyList<long> KItemPointer { get; } = [0x5F0C1B0, 0x30, 0x08, 0x400];
    public IReadOnlyList<long> KOverworldPointer { get; } = [0x5F0C1B0, 0x30, 0x08, 0x8A0];
    public IReadOnlyList<long> KStoredShinyEntityPointer { get; } = [0x5F0C1B0, 0x30, 0x08, 0x1380];

    public const uint KItemKey = 0x21C9BD44;
    public const uint KOverworldKey = 0x5E8E1711;
    public const uint KStoredShinyEntityKey = 0xF3A8569D;

    public const int FormatSlotSize = 0x158; // Party format size
    public const int BoxSlotSize = 0x198; // Size between box entries
}
