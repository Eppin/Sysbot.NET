using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// Pokémon Scarlet/Violet RAM offsets
/// </summary>
public class PokeDataOffsetsSV
{
    public const string SVGameVersion = "2.0.1";
    public const string ScarletID = "0100A3D008C5C000";
    public const string VioletID  = "01008F6008C5E000";
    public IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x4622A30, 0x198, 0x30, 0x10, 0x9D0, 0x0 };
    public IReadOnlyList<long> MyStatusPointer { get; } = new long[] { 0x4622A30, 0xE0, 0x30, 0xB8, 0x300, 0x10, 0x18 };
    public IReadOnlyList<long> ConfigPointer { get; } = new long[] { 0x46213A8, 0x10, 0x40 };
    public IReadOnlyList<long> CurrentBoxPointer { get; } = new long[] { 0x46447D0, 0xF0, 0x50, 0x548 };
    public IReadOnlyList<long> OverworldPointer { get; } = new long[] { 0x4644870, 0x348, 0x10, 0xD8, 0x28 };
    public IReadOnlyList<long> PartyStats { get; } = new long[] { 0x46447D8, 0x08, 0x30, 0x50, 0x0 };

    public IReadOnlyList<long> PartyStartPokemonPointer(int slot = 0) => new long[] { 0x46447D8, 0x8, 0x30 + (slot * 0x8), 0x30, 0x0 };

    public const int BoxFormatSlotSize = 0x158;
    public const int PartyFormatSlotSize = 0x148;
    public const int PartyStatsSize = 0x10;
    public const ulong LibAppletWeID = 0x010000000000100a; // One of the process IDs for the news.
}