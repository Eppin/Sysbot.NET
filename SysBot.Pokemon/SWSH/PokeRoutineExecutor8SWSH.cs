using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

/// <summary>
/// Executor for SW/SH games.
/// </summary>
public abstract class PokeRoutineExecutor8SWSH(PokeBotState cfg) : PokeRoutineExecutor<PK8>(cfg)
{
    protected PokeDataOffsetsSWSH Offsets { get; } = new();

    private static uint GetBoxSlotOffset(int box, int slot) => BoxStartOffset + (uint)(BoxFormatSlotSize * ((30 * box) + slot));

    public override async Task<PK8> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

    public override async Task<PK8> ReadPokemon(ulong offset, int size, CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync((uint)offset, size, token).ConfigureAwait(false);
        return new PK8(data);
    }

    public override async Task<PK8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return new PK8();
        return await ReadPokemon(offset, token).ConfigureAwait(false);
    }

    public async Task<(PK8, byte[]?)> ReadRawPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return (new PK8(), null);

        var copiedData = new byte[size];
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PK8(data), copiedData);
    }

    public async Task<PK8> ReadSurpriseTradePokemon(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(SurpriseTradePartnerPokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
        return new PK8(data);
    }

    public async Task SetBoxPokemon(PK8 pkm, int box, int slot, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            pkm.UpdateHandler(sav);
            pkm.RefreshChecksum();
        }
        var ofs = GetBoxSlotOffset(box, slot);
        pkm.ResetPartyStats();
        await Connection.WriteBytesAsync(pkm.EncryptedPartyData, ofs, token).ConfigureAwait(false);
    }

    public async Task<(PK8, byte[]?)> ReadRawBoxPokemon(int box, int slot, CancellationToken token)
    {
        var offset = GetBoxSlotOffset(box, slot);
        var copiedData = new byte[BoxFormatSlotSize];
        var data = await Connection.ReadBytesAsync(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PK8(data), copiedData);
    }

    public override async Task<PK8> ReadBoxPokemon(int box, int slot, CancellationToken token)
    {
        var (pkm, _) = await ReadRawBoxPokemon(box, slot, token).ConfigureAwait(false);
        return pkm;
    }

    public async Task SetCurrentBox(byte box, CancellationToken token)
    {
        await Connection.WriteBytesAsync([box], CurrentBoxOffset, token).ConfigureAwait(false);
    }

    public async Task<byte> GetCurrentBox(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentBoxOffset, 1, token).ConfigureAwait(false);
        return data[0];
    }

    public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
    {
        var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
        return !result.SequenceEqual(original);
    }

    public async Task<SAV8SWSH> IdentifyTrainer(CancellationToken token)
    {
        // Check if botbase is on the correct version or later.
        await VerifyBotbaseVersion(token).ConfigureAwait(false);

        // Check title so we can warn if mode is incorrect.
        string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
        if (title is not (SwordID or ShieldID))
            throw new Exception($"{title} is not a valid SWSH title. Is your mode correct?");

        // Verify the game version.
        var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
        if (!game_version.SequenceEqual(SWSHGameVersion))
            throw new Exception($"Game version is not supported. Expected version {SWSHGameVersion}, and current game version is {game_version}.");

        Log("Grabbing trainer data of host console...");
        var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
        InitSaveData(sav);

        if (!IsValidTrainerData())
        {
            await CheckForRAMShiftingApps(token).ConfigureAwait(false);
            throw new Exception("Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) for more information.");
        }

        var textSpeed = await GetTextSpeed(token).ConfigureAwait(false);

        if (GetType().Name == nameof(EncounterBotDenSWSH))
        {
            if (textSpeed > TextSpeedOption.Slow)
                throw new Exception($"Text speed should be set to SLOW for {nameof(PokeRoutineType.DenBot)}. Fix this for correct operation.");
        }
        else if (textSpeed < TextSpeedOption.Fast)
            throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

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

    /// <summary>
    /// Identifies the trainer information and loads the current runtime language.
    /// </summary>
    public async Task<SAV8SWSH> GetFakeTrainerSAV(CancellationToken token)
    {
        var sav = new SAV8SWSH();
        var info = sav.MyStatus;
        var read = await Connection.ReadBytesAsync(TrainerDataOffset, TrainerDataLength, token).ConfigureAwait(false);
        read.CopyTo(info.Data);
        return sav;
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

    public async Task EnsureConnectedToYComm(ulong overworldOffset, PokeTradeHubConfig config, CancellationToken token)
    {
        if (!await IsGameConnectedToYComm(token).ConfigureAwait(false))
        {
            Log("Reconnecting to Y-Comm...");
            await ReconnectToYComm(overworldOffset, config, token).ConfigureAwait(false);
        }
    }

    public async Task<bool> IsGameConnectedToYComm(CancellationToken token)
    {
        // Reads the Y-Comm Flag to check if the game is connected online
        var data = await Connection.ReadBytesAsync(IsConnectedOffset, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    public async Task ReconnectToYComm(ulong overworldOffset, PokeTradeHubConfig config, CancellationToken token)
    {
        // Press B in case a Error Message is Present
        await Click(B, 2000, token).ConfigureAwait(false);

        // Return to Overworld
        if (!await IsOnOverworld(overworldOffset, token).ConfigureAwait(false))
        {
            for (int i = 0; i < 5; i++)
            {
                await Click(B, 500, token).ConfigureAwait(false);
            }
        }

        await Click(Y, 1000, token).ConfigureAwait(false);

        // Press it twice for safety -- sometimes misses it the first time.
        await Click(PLUS, 2_000, token).ConfigureAwait(false);
        await Click(PLUS, 5_000 + config.Timings.ExtraTimeConnectOnline, token).ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
        {
            await Click(B, 500, token).ConfigureAwait(false);
        }
    }

    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        // Reopen the game if we get soft-banned
        Log("Potential soft ban detected, reopening game just in case!");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);

        // In case we are soft banned, reset the timestamp
        await UnSoftBan(token).ConfigureAwait(false);
    }

    public async Task UnSoftBan(CancellationToken token)
    {
        // Like previous generations, the game uses a Unix timestamp for 
        // how long we are soft banned and once the soft ban is lifted
        // the game sets the value back to 0 (1970/01/01 12:00 AM (UTC))
        Log("Soft ban detected, unbanning.");
        var data = BitConverter.GetBytes(0);
        await Connection.WriteBytesAsync(data, SoftBanUnixTimespanOffset, token).ConfigureAwait(false);
    }

    public async Task<bool> CheckIfSoftBanned(CancellationToken token)
    {
        // Check if the Unix Timestamp isn't zero, if so we are soft banned.
        var data = await Connection.ReadBytesAsync(SoftBanUnixTimespanOffset, 1, token).ConfigureAwait(false);
        return data[0] > 1;
    }

    public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token, bool skipHomeButton = false)
    {
        var timing = config.Timings;
        // Close out of the game

        if (!skipHomeButton)
        {
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
        }

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

        // Switch Logo lag, skip cutscene, game load screen
        await Task.Delay(10_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

        for (int i = 0; i < 4; i++)
            await Click(A, 1_000, token).ConfigureAwait(false);

        var timer = 60_000;
        while (!await IsOnOverworldTitle(token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
        {
            await Task.Delay(0_200, token).ConfigureAwait(false);
            timer -= 0_250;
            // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
            // Don't risk it if hub is set to avoid updates.
            if (timer <= 0 && !timing.AvoidSystemUpdate)
            {
                Log("Still not in the game, initiating rescue protocol!");
                while (!await IsOnOverworldTitle(token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
                    await Click(A, 6_000, token).ConfigureAwait(false);
                break;
            }
        }

        Log("Back in the overworld!");
    }

    public async Task SaveGame(CancellationToken token)
    {
        Log("Saving the game...");

        await Click(B, 0_500, token).ConfigureAwait(false);
        await Click(X, 2_000, token).ConfigureAwait(false);
        await Click(R, 0_250, token).ConfigureAwait(false);

        while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
            await Click(A, 0_500, token).ConfigureAwait(false);

        Log("Game saved!");
    }

    public async Task<bool> IsCorrectScreen(uint expectedScreen, CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
        return BitConverter.ToUInt32(data, 0) == expectedScreen;
    }

    public async Task<uint> GetCurrentScreen(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
        return BitConverter.ToUInt32(data, 0);
    }

    public async Task<bool> IsInBattle(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(Version == GameVersion.SH ? InBattleRaidOffsetSH : InBattleRaidOffsetSW, 1, token).ConfigureAwait(false);
        return data[0] == (Version == GameVersion.SH ? 0x40 : 0x41);
    }

    public async Task<bool> IsInBox(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
        var dataint = BitConverter.ToUInt32(data, 0);
        return dataint is CurrentScreen_Box1 or CurrentScreen_Box2;
    }

    // Only used to check if we made it off the title screen.
    private async Task<bool> IsOnOverworldTitle(CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
        if (!valid)
            return false;
        return await IsOnOverworld(offset, token).ConfigureAwait(false);
    }

    public async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    // Used to check if the battle menu has loaded, so we can attempt to flee.
    // This value starts at 1 and goes up each time a menu is opened.
    public async Task<bool> IsOnBattleMenu(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(BattleMenuOffset, 1, token).ConfigureAwait(false);
        return data[0] >= 1;
    }

    public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
    {
        var data = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
        return (TextSpeedOption)(data[0] & 3);
    }

    public async Task SetTextSpeed(TextSpeedOption speed, CancellationToken token)
    {
        var textSpeedByte = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
        var data = new[] { (byte)((textSpeedByte[0] & 0xFC) | (int)speed) };
        await Connection.WriteBytesAsync(data, TextSpeedOffset, token).ConfigureAwait(false);
    }

    // Switches to box 1, then clears slot1 to prep fossil and egg bots.
    public async Task SetupBoxState(IDumper DumpSetting, CancellationToken token)
    {
        await SetCurrentBox(0, token).ConfigureAwait(false);

        var (existing, bytes) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);
        if (existing.Species != 0 && existing.ChecksumValid)
        {
            Log("Destination slot is occupied! Dumping the Pokémon found there...");
            DumpPokemon(DumpSetting.DumpFolder, "saved", existing, bytes!);
        }

        Log("Clearing destination slot to start the bot.");
        PK8 blank = new();
        await SetBoxPokemon(blank, 0, 0, token).ConfigureAwait(false);
    }
}
