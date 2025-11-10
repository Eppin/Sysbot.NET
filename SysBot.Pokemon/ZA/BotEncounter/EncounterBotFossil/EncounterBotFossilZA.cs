namespace SysBot.Pokemon;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;
using static PokeDataOffsetsZA;

public class EncounterBotFossilZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotZA(cfg, hub)
{
    private new readonly FossilSettingsZA Settings = hub.Config.EncounterZA.Fossil;

    private bool _itemKeyInitialized;
    private byte _box;
    private byte _slot;

    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        Log("Make sure the first box is selected and there's enough free space!");

        Log("Checking item counts...");
        var pouchData = await GetPouchData(token).ConfigureAwait(false);
        var counts = FossilCountZA.GetFossilCounts(pouchData);

        var reviveCount = counts.PossibleRevives(Settings.Species);
        if (reviveCount == 0)
        {
            Log("Insufficient fossil pieces. Please obtain at least one of each required fossil piece first.");
            return;
        }

        Log($"Enough fossil pieces are available to revive {reviveCount} {(Settings.Species is FossilSpeciesZA.Any ? "fossils" : Settings.Species)}.");

        PA9? prev = null;
        while (!token.IsCancellationRequested)
        {
            if (EncounterCount != 0 && EncounterCount % reviveCount == 0)
            {
                Log("Fossil pieces have been depleted. Resetting the game.");
                _box = _slot = 0;
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }

            await ReviveFossil(token).ConfigureAwait(false);
            Log("Fossil revived. Checking details...");

            var (pa9, raw) = await ReadRawBoxPokemon(_box, _slot, token).ConfigureAwait(false);
            if (pa9.Species == 0 || !pa9.ChecksumValid || pa9.EncryptionConstant == prev?.EncryptionConstant)
            {
                Log($"No fossil found in Box {_box + 1}, slot {_slot + 1}. Ensure that the party is full. Restarting loop.");
                continue;
            }

            if (new[] { (int)Species.Aerodactyl, (int)Species.Tyrunt, (int)Species.Amaura }.Contains(pa9.Species) == false)
            {
                Log($"Fossil revival appears to have failed, found {(Species)pa9.Species}.");
                return;
            }

            var (stop, success) = await HandleEncounter(pa9, token, raw).ConfigureAwait(false);

            if (success) Log($"You're fossil has been claimed and placed in B{_box + 1}S{_slot + 1}. Be sure to save your game!");

            _slot += 1;
            if (_slot == 30)
            {
                _box++;
                _slot = 0;
            }

            if (stop)
                return;

            prev = pa9;
        }
    }

    private async Task<byte[]> GetPouchData(CancellationToken token)
    {
        _itemKeyInitialized = false;
        var bytes = await ReadEncryptedBlock(Offsets.KItemPointer, KItemKey, !_itemKeyInitialized, token).ConfigureAwait(false);
        _itemKeyInitialized = true;

        return bytes;
    }

    private async Task ReviveFossil(CancellationToken token)
    {
        Log("Starting fossil revival routine...");

        if (Settings.Species == FossilSpeciesZA.Any)
        {
            // Just mash the buttons through the menus if any fossil is acceptable.
            for (var i = 0; i < 14; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);

            await Task.Delay(3_000, token).ConfigureAwait(false);

            for (var i = 0; i < 16; i++)
                await Click(B, 0_500, token).ConfigureAwait(false);

            return;
        }

        for (var i = 0; i < 4; i++)
            await Click(A, 1_100, token).ConfigureAwait(false);

        switch (Settings.Species)
        {
            // Selecting second fossil.
            case FossilSpeciesZA.Amaura:
                await Click(DDOWN, 0_300, token).ConfigureAwait(false);
                break;

            // Selecting third fossil.
            case FossilSpeciesZA.Aerodactyl:
                {
                    for (var i = 0; i < 2; i++) await Click(DDOWN, 0_300, token).ConfigureAwait(false);
                    break;
                }
        }

        // A spam through accepting the fossil and agreeing to revive.
        for (var i = 0; i < 6; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        await Task.Delay(3_000, token).ConfigureAwait(false);

        for (var i = 0; i < 16; i++)
            await Click(B, 0_500, token).ConfigureAwait(false);
    }
}
