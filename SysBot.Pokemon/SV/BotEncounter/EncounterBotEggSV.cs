namespace SysBot.Pokemon;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;

public class EncounterBotEggSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotSV(cfg, hub)
{
    private const int WaitBetweenCollecting = 1; // Seconds
    private static readonly PK9 Blank = new();

    private byte _box;
    private byte _slot;

    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        await SetupBoxState(DumpSetting, token).ConfigureAwait(false);

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
            var (pk, bytes) = await ReadRawBoxPokemon(_box, _slot, token).ConfigureAwait(false);

            if (previousPk?.EncryptionConstant != pk.EncryptionConstant && (Species)pk.Species != Species.None)
            {
                var (stop, success) = await HandleEncounter(pk, token, bytes, true).ConfigureAwait(false);

                if (success)
                {
                    Log($"You're egg has been claimed and placed in B{_box + 1}S{_slot + 1}. Be sure to save your game!");
                    _slot += 1;

                    if (_slot == 30)
                    {
                        _box++;
                        _slot = 0;

                        await SetCurrentBox(_box, token);
                        Log($"Change current position to B{_box + 1}S{_slot + 1}!");
                    }

                    if (!await IsUnlimited(token).ConfigureAwait(false))
                        return;
                }

                if (stop)
                    return;

                Log($"Clearing destination slot (B{_box + 1}S{_slot + 1}) for next Egg.", false);
                await SetBoxPokemon(Blank, _box, _slot, token).ConfigureAwait(false);

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

            var parents = Directory.GetFiles(Settings.UnlimitedParentsFolder, "*pk*");
            if (parents.Length == 0)
            {
                Log($"No valid parents found in [{Settings.UnlimitedParentsFolder}]");
                return false;
            }

            if (!await SetNextParent(parents, token))
                return false;
        }

        return true;
    }

    private async Task<bool> SetNextParent(IEnumerable<string> parents, CancellationToken token)
    {
        var parent = parents.FirstOrDefault();
        if (parent == null)
            return false;

        var fileInfo = new FileInfo(parent);
        var bytes = await File.ReadAllBytesAsync(parent, token);

        if (!FileUtil.TryGetPKM(bytes, out var pk, fileInfo.Extension))
        {
            Log($"Parent file [{parent}] isn't valid!");
            return false;
        }

        if (EntityConverter.ConvertToType(pk, typeof(PK9), out var result) is not PK9 pk9)
        {
            Log($"Parent {pk.FileName} isn't valid: {result}");
            return false;
        }

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

        return true;
    }
}
