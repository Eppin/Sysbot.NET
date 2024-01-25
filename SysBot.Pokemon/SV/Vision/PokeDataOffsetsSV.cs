using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// Pokémon Scarlet/Violet RAM offsets
/// </summary>
public class PokeDataOffsetsSV
{
    public const string SVGameVersion = "3.0.0";
    public const string ScarletID = "0100A3D008C5C000";
    public const string VioletID  = "01008F6008C5E000";

    public IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x4763C80, 0x8, 0x230, 0x9D0, 0x0 };
    public IReadOnlyList<long> MyStatusPointer { get; } = new long[] { 0x4763C80, 0x8, 0x200, 0x10, 0x40 };
    public IReadOnlyList<long> ConfigPointer { get; } = new long[] { 0x47350D8, 0xD8, 0x8, 0xB8, 0xD0, 0x40 };
    public IReadOnlyList<long> CurrentBoxPointer { get; } = new long[] { 0x47350D8, 0xD8, 0x8, 0xB8, 0x28, 0x570 };
    public IReadOnlyList<long> OverworldPointer { get; } = new long[] { 0x473ADE0, 0x160, 0xE8, 0x28 };
    public IReadOnlyList<long> BlockKeyPointer { get; } = new long[] { 0x47350D8, 0xD8, 0x0, 0x0, 0x30, 0x0 };

    public IReadOnlyList<long> PartyStats { get; } = new long[] { 0x4763C98, 0x08, 0x30, 0x50, 0x0 };
    public static IReadOnlyList<long> PartyStartPokemonPointer(int slot = 0) => new long[] { 0x4763C98, 0x8, 0x30 + (slot * 0x8), 0x30, 0x0 };

    public const int BoxFormatSlotSize = 0x158;
    public const int PartyFormatSlotSize = 0x148;
    public const int PartyStatsSize = 0x10;

    public const int OverworldBlockKey = 0x173304D8;
}
