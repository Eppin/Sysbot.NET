using PKHeX.Core;
using PKHeX.Core.Searching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;

namespace SysBot.Pokemon;

public class EncounterBotEggBS(PokeBotState cfg, PokeTradeHub<PB8> hub) : EncounterBotBS(cfg, hub)
{
    private const byte Box = 0;
    private const byte Slot = 0;
    private byte _result;

    private static readonly PB8 Blank = new();

    protected override async Task EncounterLoop(SAV8BS sav, CancellationToken token)
    {
        _result = 0; // Always reset the result

        // Hatch a party full of Eggs
        if (Settings.EncounteringType == EncounterMode.EggHatch)
        {
            await MassEggHatch(token);
            return;
        }

        // Fetch lots of Eggs?
        if (!await IsUnlimited(token).ConfigureAwait(false))
            return;

        await EnableAlwaysEgg(sav.Version, token).ConfigureAwait(false);
        var pkOriginal = await ReadBoxPokemon(Box, Slot, token).ConfigureAwait(false);

        while (!token.IsCancellationRequested)
        {
            await SetBoxPokemon(Blank, token).ConfigureAwait(false);

            PB8? pk;
            byte[]? bytes;

            Log("Looking for a Pokémon Egg...");
            do
            {
                await Click(A, 0_050, token).ConfigureAwait(false);
                (pk, bytes) = await ReadUntilPresent(0_050, 0_050, token).ConfigureAwait(false);
            } while (pk is null || SearchUtil.HashByDetails(pkOriginal) == SearchUtil.HashByDetails(pk));

            Log($"Egg received in B{Box + 1}S{Slot + 1}. Checking details.");

            if (pk.Species == 0)
            {
                Log($"No egg found in B{Box + 1}S{Slot + 1}. Ensure that the party is full. Restarting loop.");
                continue;
            }

            var (stop, success) = await HandleEncounter(pk, token, bytes, true).ConfigureAwait(false);

            if (success)
            {
                Log($"You're egg has been claimed and placed in B{Box + 1}S{Slot + 1} ({_result}). Be sure to save your game!");
                _result += 1;

                if (!await IsUnlimited(token).ConfigureAwait(false))
                    return;
            }

            if (stop)
                return;
        }
    }

    private async Task<(PB8?, byte[]?)> ReadUntilPresent(int msWait, int waitInterval, CancellationToken token)
    {
        var msWaited = 0;
        while (msWaited < msWait)
        {
            var (pk, bytes) = await ReadRawBoxPokemon(Box, Slot, token).ConfigureAwait(false);

            if (pk.Species != 0 && pk.ChecksumValid)
                return (pk, bytes);

            await Task.Delay(waitInterval, token).ConfigureAwait(false);
            msWaited += waitInterval;
        }
        return (null, null);
    }

    private async Task EnableAlwaysEgg(GameVersion game, CancellationToken token)
    {
        Log("Enable 'nurse always have an egg' cheat", false);
        // Source: https://gbatemp.net/threads/pokemon-brilliant-diamond-shining-pearl-cheat-database.602559/

        // Original cheat:
        /*
         * [Nursery Instant Egg (Hold L)] - Brilliant Diamond
         * 04000000 021EF9B8 5400132B
         * 04000000 021EFB9C 5400040D
         * 80000040
         * 04000000 021EF9B8 D503201F
         * 04000000 021EFB9C D503201F
         * 20000000
         *
         * [Nursery Instant Egg (Hold L)] - Shining Pearl
         * 04000000 025F2578 5400132B
         * 04000000 025F275C 5400040D
         * 80000040
         * 04000000 025F2578 D503201F
         * 04000000 025F275C D503201F
         * 20000000
         */

        switch (game)
        {
            case GameVersion.BD:
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x021EF9B8, token);
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x021EFB9C, token);
                break;

            case GameVersion.SP:
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x025F2578, token);
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x025F275C, token);
                break;

            default:
                Log($"Unsupported game {game} detected");
                break;
        }
    }

    private async Task<bool> IsUnlimited(CancellationToken token)
    {
        if (Settings.UnlimitedMode)
        {
            Log("Unlimited mode will not work properly! B1S1 will always be overwritten, keep that in mind!");

            if (!Directory.Exists(Settings.UnlimitedParentsFolder))
            {
                Log($"Directory for unlimited doesn't exist: [{Settings.UnlimitedParentsFolder}]");
                return false;
            }

            var parents = Directory.GetFiles(Settings.UnlimitedParentsFolder, "*p*");
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

        if (EntityConverter.ConvertToType(pk, typeof(PB8), out var result) is not PB8 PB8)
        {
            Log($"Parent {pk.FileName} isn't valid: {result}");
            return false;
        }

        var (slot1, slot2) = await GetDayCare(token);
        if (slot1?.Species != (int)Species.Ditto && slot2?.Species != (int)Species.Ditto)
        {
            Log($"There is not {Species.Ditto} in Day Care");
            return false;
        }

        var dittoFirstSlot = slot1?.Species == (int)Species.Ditto;
        await SetDayCare(PB8, !dittoFirstSlot, token);

        (slot1, slot2) = await GetDayCare(token);
        Log($"Set parent: {PB8.FileName}, slot 1 is {slot1?.Species}, valid: {slot1?.Valid} and slot 2 is {slot2?.Species}, valid: {slot2?.Valid}");

        var info = new FileInfo(parent);
        File.Move(info.FullName, Path.Combine(DumpSetting.DumpFolder, "saved", info.Name));

        return true;
    }

    private async Task<(PB8? Slot1, PB8? Slot2)> GetDayCare(CancellationToken token)
    {
        PB8? slot1 = null;
        PB8? slot2 = null;

        var dayCareParent1 = await ReadPokemonPointer(Offsets.DayCareParent1PokemonPointer, BasePokeDataOffsetsBS.BoxFormatSlotSize, token);
        var dayCareParent2 = await ReadPokemonPointer(Offsets.DayCareParent2PokemonPointer, BasePokeDataOffsetsBS.BoxFormatSlotSize, token);

        if (dayCareParent1 is { Valid: true, EncryptionConstant: > 0 }) slot1 = dayCareParent1;
        if (dayCareParent2 is { Valid: true, EncryptionConstant: > 0 }) slot2 = dayCareParent2;

        return (slot1, slot2);
    }

    private async Task SetDayCare(PB8 pb8, bool firstSlot, CancellationToken token)
    {
        var pointer = firstSlot ? Offsets.DayCareParent1PokemonPointer : Offsets.DayCareParent2PokemonPointer;
        await SetPokemonPointer(pointer, pb8, token);
    }

    private async Task MassEggHatch(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await SetStick(LEFT, 0, -30000, 8_000, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

            await Click(B, 0, token);

            await SetStick(LEFT, 0, 30000, 8_000, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

            await Click(B, 0, token);
        }
    }
}
