using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

public class EncounterBotEggSWSH : EncounterBotSWSH
{
    private readonly IDumper DumpSetting;

    private byte Box = 0;
    private byte Slot;

    public EncounterBotEggSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg, hub)
    {
        DumpSetting = Hub.Config.Folder;
    }

    private static readonly PK8 Blank = new();

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        if (!await IsUnlimited(token).ConfigureAwait(false))
            return;

        await SetupBoxState(DumpSetting, token).ConfigureAwait(false);
        await EnableAlwaysEgg(sav.Version, token).ConfigureAwait(false);

        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            while (!token.IsCancellationRequested && !await IsEggReady(token) && sw.Elapsed.TotalSeconds < 10)
            {
                await Task.Delay(50, token).ConfigureAwait(false);

                // Walk diagonally left.
                await SetStick(LEFT, -19000, 19000, 0_250, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

                // Walk diagonally right, slightly longer to ensure we stay at the Daycare lady.
                await SetStick(LEFT, 19000, 19000, 0_300, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset
            }
            sw.Stop();

            if (sw.Elapsed.TotalSeconds >= 10)
            {
                Log($"Tried {sw.Elapsed}, still no egg.");
                await Click(B, 500, token).ConfigureAwait(false);
                continue;
            }

            Log($"Egg available after {sw.Elapsed}! Clearing destination slot.");
            await SetBoxPokemon(Blank, Box, Slot, token).ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
                await Click(A, 0_200, token).ConfigureAwait(false);

            // Safe to mash B from here until we get out of all menus.
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(B, 0_200, token).ConfigureAwait(false);

            Log($"Egg received in B{Box + 1}S{Slot + 1}. Checking details.");
            var pk = await ReadBoxPokemon(Box, Slot, token).ConfigureAwait(false);
            if (pk.Species == 0)
            {
                Log($"No egg found in B{Box + 1}S{Slot + 1}. Ensure that the party is full. Restarting loop.");
                continue;
            }

            var (stop, success) = await HandleEncounter(pk, token).ConfigureAwait(false);

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
        }
    }

    public async Task<bool> IsEggReady(CancellationToken token)
    {
        // Read a single byte of the Daycare metadata to check the IsEggReady flag.
        var data = await Connection.ReadBytesAsync(DayCare_Route5_Egg_Is_Ready, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    private async Task EnableAlwaysEgg(GameVersion game, CancellationToken token)
    {
        Log("Enable 'nurse always have an egg' cheat", false);
        // Source: https://gbatemp.net/threads/pokemon-sword-and-shield-cheats-hacks-pkhex.551986/post-9845202

        // Original cheat:
        /*
         * [(v1.3.2) Nursery Staff Always Have an Egg (on)] - Sword
         * 04000000 01401594 D503201F
         * 04000000 014016E4 D503201F
         *
         * [(v1.3.2) Nursery Staff Always Have an Egg (on)] - Shield
         * 04000000 014015C4 D503201F
         * 04000000 01401714 D503201F
         */

        switch (game)
        {
            case GameVersion.SW:
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x01401594, token);
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x014016E4, token);
                break;

            case GameVersion.SH:
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x014015C4, token);
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0xD503201F), 0x01401714, token);
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

        if (EntityConverter.ConvertToType(pk, typeof(PK8), out var result) is not PK8 pk8)
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
        await SetDayCare(pk8, !dittoFirstSlot, token);

        (slot1, slot2) = await GetDayCare(token);
        Log($"Set parent: {pk8.FileName}, slot 1 is {slot1?.Species}, valid: {slot1?.Valid} and slot 2 is {slot2?.Species}, valid: {slot2?.Valid}");

        var info = new FileInfo(parent);
        File.Move(info.FullName, Path.Combine(DumpSetting.DumpFolder, "saved", info.Name));

        return true;
    }

    private async Task<(PK8? Slot1, PK8? Slot2)> GetDayCare(CancellationToken token)
    {
        PK8? slot1 = null;
        PK8? slot2 = null;

        var dayCareBytes = await SwitchConnection.ReadBytesAsync(DayCare_Start, DayCareSize, token);

        if (dayCareBytes[0] == 1)
        {
            var slot1Bytes = dayCareBytes.Skip(1).Take(0x148).ToArray();
            slot1 = new PK8(slot1Bytes);
        }

        if (dayCareBytes[0x149] == 1)
        {
            var slot1Bytes = dayCareBytes.Skip(0x149 + 1).Take(0x148).ToArray();
            slot2 = new PK8(slot1Bytes);
        }

        return (slot1, slot2);
    }

    private async Task SetDayCare(PKM pk8, bool firstSlot, CancellationToken token)
    {
        var dayCareBytes = await SwitchConnection.ReadBytesAsync(DayCare_Start, DayCareSize, token);

        var newBytes = new List<byte>();
        if (firstSlot)
        {
            newBytes.Add(1);
            newBytes.AddRange(pk8.EncryptedBoxData);

            newBytes.AddRange(dayCareBytes.Skip(0x149).Take(0x148));
        }
        else
        {
            newBytes.AddRange(dayCareBytes.Take(0x149));

            newBytes.Add(1);
            newBytes.AddRange(pk8.EncryptedBoxData);
        }

        await SwitchConnection.WriteBytesAsync([.. newBytes], DayCare_Start, token);
    }
}
