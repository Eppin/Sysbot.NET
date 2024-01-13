namespace SysBot.Pokemon;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;

public class EncounterBotEggSV : EncounterBotSV
{
    private readonly IDumper _dumpSetting;

    private const int WaitBetweenCollecting = 1; // Seconds
    private static readonly PK9 Blank = new();

    private byte Box;
    private byte Slot;

    public EncounterBotEggSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg, hub)
    {
        _dumpSetting = Hub.Config.Folder;
    }

    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        await SetupBoxState(_dumpSetting, token).ConfigureAwait(false);

        if (!await IsUnlimited(token).ConfigureAwait(false))
            return;

        // Start with initial presses
        await Click(A, 250, token).ConfigureAwait(false);
        await Click(A, 250, token).ConfigureAwait(false);

        PK9? previousPk = null;
        var eggsPerBatch = 0;

        while (!token.IsCancellationRequested)
        {
            // Mash A button
            await Click(A, 150, token).ConfigureAwait(false);

            if (eggsPerBatch >= 10)
            {
                eggsPerBatch = 0;
                for (var i = 0; i < 20; i++)
                {
                    await Click(B, 150, token).ConfigureAwait(false);
                }

                Log($"Waiting [{WaitBetweenCollecting}] second(s) for picnic basket to fill");
                for (var i = 0; i < 10; i++)
                    await Click(B, 100, token).ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromSeconds(WaitBetweenCollecting), token).ConfigureAwait(false);
            }

            // Validate result
            var (pk, bytes) = await ReadRawBoxPokemon(Box, Slot, token).ConfigureAwait(false);

            if (previousPk?.EncryptionConstant != pk.EncryptionConstant && (Species)pk.Species != Species.None)
            {
                var (stop, success) = await HandleEncounter(pk, token, bytes, true).ConfigureAwait(false);

                if (success)
                {
                    Log($"You're egg has been claimed and placed in B{Box + 1}S{Slot + 1}. Be sure to save your game!");
                    Slot += 1;

                    if (Slot == 30)
                    {
                        Box++;
                        Slot = 0;

                        await SetCurrentBox(Box, token);
                        Log($"Change current position to B{Box + 1}S{Slot + 1}!");
                    }

                    if (!await IsUnlimited(token).ConfigureAwait(false))
                        return;
                }

                if (stop)
                    return;

                Log($"Clearing destination slot (B{Box + 1}S{Slot + 1}) for next Egg.", false);
                await SetBoxPokemon(Blank, Box, Slot, token).ConfigureAwait(false);

                previousPk = pk;
                eggsPerBatch++;
            }
        }
    }

    private async Task<bool> IsUnlimited(CancellationToken token)
    {
        if (Settings.UnlimitedMode)
        {
            if (!Directory.Exists(Settings.UnlimitedParentsFolder))
            {
                Log($"Directory for unlimited doesn't exist: [{Settings.UnlimitedParentsFolder}]");
                return false;
            }

            var parents = Directory.GetFiles(Settings.UnlimitedParentsFolder, "*.pk9");
            if (parents.Length == 0)
            {
                Log($"No valid parents found in [{Settings.UnlimitedParentsFolder}]");
                return false;
            }

            await SetNextParent(parents, token);
        }

        return true;
    }

    private async Task SetNextParent(IEnumerable<string> parents, CancellationToken token)
    {
        var parent = parents.FirstOrDefault();
        if (parent == null)
            return;

        var bytes = await File.ReadAllBytesAsync(parent, token);
        var pk9 = new PK9(bytes);

        var retryCount = 0;
        PK9? party1;

        do
        {
            Log($"{(retryCount == 0 ? "Set" : "Retry")} next parent: {pk9.FileName}");
            await SetPartyPokemon(pk9, 1, token).ConfigureAwait(false);

            await Task.Delay(0_100, token).ConfigureAwait(false);

            (party1, _) = await ReadRawPartyPokemon(1, token).ConfigureAwait(false);
            Log($"Verify ({retryCount + 1}) parent: {pk9.FileName}, species: {(Species)party1.Species}, valid: {party1.Valid}, EC: {party1.EncryptionConstant:X8}");

            retryCount++;
        } while (retryCount < 10 && (!party1.Valid || party1.EncryptionConstant == 0 || (Species)party1.Species == Species.None || (Species)party1.Species >= Species.MAX_COUNT));

        var info = new FileInfo(parent);
        File.Move(info.FullName, Path.Combine(DumpSetting.DumpFolder, "saved", info.Name));
    }
}
