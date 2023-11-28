using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSV;

namespace SysBot.Pokemon;

public abstract class PokeRoutineExecutor9SV : PokeRoutineExecutor<PK9>
{
    protected PokeDataOffsetsSV Offsets { get; } = new();
    protected PokeRoutineExecutor9SV(PokeBotState cfg) : base(cfg)
    {
    }

    public override async Task<PK9> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

    public override async Task<PK9> ReadPokemon(ulong offset, int size, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
        return new PK9(data);
    }

    public override async Task<PK9> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return new PK9();
        return await ReadPokemon(offset, token).ConfigureAwait(false);
    }

    public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
    {
        var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
        return !result.SequenceEqual(original);
    }

    public async Task SetBoxPokemon(PK9 pkm, int box, int slot, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            var date = DateTime.Now;
            pkm.Trade(sav, date.Day, date.Month, date.Year);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();

        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        var (valid, b1s1) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return;

        const int boxSize = 30 * BoxFormatSlotSize;
        var boxStart = b1s1 + (ulong)(box * boxSize);
        var slotStart = boxStart + (ulong)(slot * BoxFormatSlotSize);

        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedPartyData, slotStart, token).ConfigureAwait(false);
    }

    public async Task<(PK9, byte[]?)> ReadRawBoxPokemon(int box, int slot, CancellationToken token)
    {
        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        var (valid, b1s1) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return (new PK9(), null);

        const int boxSize = 30 * BoxFormatSlotSize;
        var boxStart = b1s1 + (ulong)(box * boxSize);
        var slotStart = boxStart + (ulong)(slot * BoxFormatSlotSize);

        var copiedData = new byte[BoxFormatSlotSize];
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(slotStart, BoxFormatSlotSize, token).ConfigureAwait(false);

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PK9(data), copiedData);
    }

    public override async Task<PK9> ReadBoxPokemon(int box, int slot, CancellationToken token)
    {
        var (pk9, _) = await ReadRawBoxPokemon(box, slot, token).ConfigureAwait(false);
        return pk9;
    }

    public async Task<(PK9, byte[]?)> ReadRawPartyPokemon(int slot, CancellationToken token)
    {
        var jumps = Offsets.PartyStartPokemonPointer(slot).ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return (new PK9(), null);

        var copiedData = new byte[BoxFormatSlotSize];
        var data = new byte[BoxFormatSlotSize];

        var partyData = await SwitchConnection.ReadBytesAbsoluteAsync(party, PartyFormatSlotSize, token).ConfigureAwait(false);
        partyData.CopyTo(data, 0);

        var stats = await ReadRawPartyStats(slot, token).ConfigureAwait(false);
        stats.CopyTo(data, PartyFormatSlotSize);

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PK9(data), copiedData);
    }

    private async Task<byte[]> ReadRawPartyStats(int slot, CancellationToken token)
    {
        var jumps = Offsets.PartyStats.ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return Array.Empty<byte>();

        return await SwitchConnection.ReadBytesAbsoluteAsync(party + (uint)(slot * PartyStatsSize), PartyStatsSize, token).ConfigureAwait(false);
    }

    private async Task WritePartyStats(byte[] bytes, int slot, CancellationToken token)
    {
        var jumps = Offsets.PartyStats.ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return;

        await SwitchConnection.WriteBytesAbsoluteAsync(bytes, party + (uint)(slot * PartyStatsSize), token).ConfigureAwait(false);
    }

    public async Task SetPartyPokemon(PK9 pkm, int slot, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            var date = DateTime.Now;
            pkm.Trade(sav, date.Day, date.Month, date.Year);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();

        var jumps = Offsets.PartyStartPokemonPointer(slot).ToArray();
        var (valid, party) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return;

        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedBoxData, party, token).ConfigureAwait(false);
        await WritePartyStats(pkm.EncryptedPartyData[^PartyStatsSize..], slot, token).ConfigureAwait(false);
    }

    public async Task SetBoxPokemonAbsolute(ulong offset, PK9 pkm, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            pkm.Trade(sav);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();
        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedBoxData, offset, token).ConfigureAwait(false);
    }

    public async Task SetCurrentBox(byte box, CancellationToken token)
    {
        await SwitchConnection.PointerPoke(new[] { box }, Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
    }

    public async Task<byte> GetCurrentBox(CancellationToken token)
    {
        var data = await SwitchConnection.PointerPeek(1, Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
        return data[0];
    }

    public async Task<SAV9SV> IdentifyTrainer(CancellationToken token)
    {
        // Check if botbase is on the correct version or later.
        await VerifyBotbaseVersion(token).ConfigureAwait(false);

        // Check title so we can warn if mode is incorrect.
        string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
        if (title is not (ScarletID or VioletID))
            throw new Exception($"{title} is not a valid SV title. Is your mode correct?");

        // Verify the game version.
        var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
        if (!game_version.SequenceEqual(SVGameVersion))
            throw new Exception($"Game version is not supported. Expected version {SVGameVersion}, and current game version is {game_version}.");

        var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
        InitSaveData(sav);

        if (!IsValidTrainerData())
        {
            await CheckForRAMShiftingApps(token).ConfigureAwait(false);
            throw new Exception("Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) for more information.");
        }

        if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
            throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

        return sav;
    }

    public Task<SAV9SV> GetFakeTrainerSAV(CancellationToken token)
    {
        return GetFakeTrainerSAV(Offsets.MyStatusPointer, token);
    }

    public async Task<SAV9SV> GetFakeTrainerSAV(IEnumerable<long> jumps, CancellationToken token)
    {
        var sav = new SAV9SV();
        var info = sav.MyStatus;
        var read = await SwitchConnection.PointerPeek(info.Data.Length, jumps, token).ConfigureAwait(false);
        read.CopyTo(info.Data, 0);
        return sav;
    }

    public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
    {
        Log("Detaching on startup.");
        await DetachController(token).ConfigureAwait(false);
        if (settings.ScreenOff)
        {
            Log("Turning off screen.");
            await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
        }
    }

    public async Task CleanExit(CancellationToken token)
    {
        await SetScreen(ScreenState.On, token).ConfigureAwait(false);
        Log("Detaching controllers on routine exit.");
        await DetachController(token).ConfigureAwait(false);
    }

    protected virtual async Task EnterLinkCode(int code, PokeTradeHubConfig config, CancellationToken token)
    {
        // Default implementation to just press directional arrows. Can do via Hid keys, but users are slower than bots at even the default code entry.
        var keys = TradeUtil.GetPresses(code);
        foreach (var key in keys)
        {
            int delay = config.Timings.KeypressTime;
            await Click(key, delay, token).ConfigureAwait(false);
        }
        // Confirm Code outside of this method (allow synchronization)
    }

    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        Log("Restarting the game!!");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);
    }

    public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        // Close out of the game
        await Click(B, 0_500, token).ConfigureAwait(false);
        await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
        await Click(X, 1_000, token).ConfigureAwait(false);
        await Click(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
        Log("Closed out of the game!");
    }

    public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        // Open game.
        await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

        // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
        //  The user can optionally turn on the setting if they know of a breaking system update incoming.
        if (timing.AvoidSystemUpdate)
        {
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
        }

        await Click(A, 1_000 + timing.ExtraTimeCheckDLC, token).ConfigureAwait(false);
        // If they have DLC on the system and can't use it, requires an UP + A to start the game.
        // Should be harmless otherwise since they'll be in loading screen.
        await Click(DUP, 0_600, token).ConfigureAwait(false);
        await Click(A, 0_600, token).ConfigureAwait(false);

        Log("Restarting the game!");

        // Switch Logo and game load screen
        await Task.Delay(12_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

        for (int i = 0; i < 8; i++)
            await Click(A, 1_000, token).ConfigureAwait(false);

        var timer = 60_000;
        while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            timer -= 1_000;
            // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
            // Don't risk it if hub is set to avoid updates.
            if (timer <= 0 && !timing.AvoidSystemUpdate)
            {
                Log("Still not in the game, initiating rescue protocol!");
                while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                    await Click(A, 6_000, token).ConfigureAwait(false);
                break;
            }
        }

        await Task.Delay(5_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
        Log("Back in the overworld!");
    }

    public async Task<bool> IsConnectedOnline(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    public async Task<ulong> GetTradePartnerNID(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 8, token).ConfigureAwait(false);
        return BitConverter.ToUInt64(data, 0);
    }

    public async Task ClearTradePartnerNID(ulong offset, CancellationToken token)
    {
        var data = new byte[8];
        await SwitchConnection.WriteBytesAbsoluteAsync(data, offset, token).ConfigureAwait(false);
    }

    public async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 0x11;
    }

    // Only used to check if we made it off the title screen; the pointer isn't viable until a few seconds after clicking A.
    private async Task<bool> IsOnOverworldTitle(CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        if (!valid)
            return false;
        return await IsOnOverworld(offset, token).ConfigureAwait(false);
    }

    // Usually 0x9-0xA if fully loaded into Poké Portal.
    public async Task<bool> IsInPokePortal(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] >= 9;
    }

    // Usually 4-6 in a box.
    public async Task<bool> IsInBox(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] < 8;
    }

    public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
    {
        var data = await SwitchConnection.PointerPeek(1, Offsets.ConfigPointer, token).ConfigureAwait(false);
        return (TextSpeedOption)(data[0] & 3);
    }

    // Switches to box 1, then clears slot1 to prep fossil and egg bots.
    public async Task SetupBoxState(IDumper dumpSetting, CancellationToken token)
    {
        await SetCurrentBox(0, token).ConfigureAwait(false);

        var (existing, bytes) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);
        if (existing.Species != 0 && existing.ChecksumValid)
        {
            Log("Destination slot is occupied! Dumping the Pokémon found there...");
            DumpPokemon(dumpSetting.DumpFolder, "saved", existing, bytes!);
        }

        Log("Clearing destination slot to start the bot.");
        PK9 blank = new();
        await SetBoxPokemon(blank, 0, 0, token).ConfigureAwait(false);
    }
}
