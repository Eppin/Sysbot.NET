namespace SysBot.Pokemon;

using System;
using PKHeX.Core;

public class FossilCountLZA
{
    private int Jaw;
    private int Sail;
    private int OldAmber;

    private void SetCount(int item, int count)
    {
        switch (item)
        {
            case 710: Jaw = count; break;
            case 711: Sail = count; break;
            case 103: OldAmber = count; break;
        }
    }

    public static FossilCountLZA GetFossilCounts(byte[] itemsBlock)
    {
        var pouch = GetTreasurePouch(itemsBlock);
        return ReadCounts(pouch);
    }

    private static FossilCountLZA ReadCounts(InventoryPouch pouch)
    {
        var counts = new FossilCountLZA();
        foreach (var item in pouch.Items)
            counts.SetCount(item.Index, item.Count);
        return counts;
    }

    private static InventoryPouch9a GetTreasurePouch(byte[] itemsBlock)
    {
        var pouch = new InventoryPouch9a(InventoryType.Items, ItemStorage9ZA.Instance, 999, 0);
        pouch.GetPouch(itemsBlock);
        return pouch;
    }

    public int PossibleRevives(FossilSpeciesLZA species)
    {
        if (species == FossilSpeciesLZA.Any) return Jaw + Sail + OldAmber;

        // Requirement: at least one of each fossil must be present to perform any revives.
        if (Jaw <= 0 || Sail <= 0 || OldAmber <= 0)
            return 0;

        return species switch
        {
            FossilSpeciesLZA.Tyrunt => Jaw,
            FossilSpeciesLZA.Amaura => Sail,
            FossilSpeciesLZA.Aerodactyl => OldAmber,
            _ => throw new ArgumentOutOfRangeException(nameof(species), species, "Fossil species was invalid."),
        };
    }
}
