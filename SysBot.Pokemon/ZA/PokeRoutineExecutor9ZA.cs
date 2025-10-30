using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.FlatBuffers;
using PKHeX.Core;
using SysBot.Base;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsZA;

namespace SysBot.Pokemon.ZA;

public class PokeRoutineExecutor9ZA(PokeBotState cfg) : PokeRoutineExecutor<PA9>(cfg)
{
    protected PokeDataOffsetsZA Offsets { get; } = new();

    public override Task MainLoop(CancellationToken token)
    {
        throw new System.NotImplementedException();
    }

    public override Task HardStop()
    {
        throw new System.NotImplementedException();
    }

    public override Task<PA9> ReadPokemon(ulong offset, CancellationToken token)
    {
        throw new System.NotImplementedException();
    }

    public override Task<PA9> ReadPokemon(ulong offset, int size, CancellationToken token)
    {
        throw new System.NotImplementedException();
    }

    public override Task<PA9> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        throw new System.NotImplementedException();
    }

    public override Task<PA9> ReadBoxPokemon(int box, int slot, CancellationToken token)
    {
        throw new System.NotImplementedException();
    }

    public async Task<SAV9ZA> IdentifyTrainer(CancellationToken token)
    {
        // Check if botbase is on the correct version or later.
        await VerifyBotbaseVersion(token).ConfigureAwait(false);

        // Check title so we can warn if mode is incorrect.
        string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
        if (title != LegendsZAID)
            throw new Exception($"{title} is not a valid Pokémon Legends: ZA title. Is your mode correct?");

        // Verify the game version.
        var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
        if (!game_version.SequenceEqual(ZAGameVersion))
            throw new Exception($"Game version is not supported. Expected version {ZAGameVersion}, and current game version is {game_version}.");

        var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
        InitSaveData(sav);

        if (!IsValidTrainerData())
        {
            await CheckForRAMShiftingApps(token).ConfigureAwait(false);
            throw new Exception("Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) for more information.");
        }

        return sav;
    }

    public async Task<SAV9ZA> GetFakeTrainerSAV(CancellationToken token)
    {
        var sav = new SAV9ZA();
        var info = sav.MyStatus;
        var read = await SwitchConnection.PointerPeek(info.Data.Length, Offsets.MyStatusPointer, token).ConfigureAwait(false);
        read.CopyTo(info.Data);
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

    private readonly Dictionary<uint, ulong> _cacheBlockAddresses = new();
    public async Task<byte[]> ReadEncryptedBlock(IEnumerable<long> pointer, uint blockKey, bool init, CancellationToken token)
    {
        var exists = _cacheBlockAddresses.TryGetValue(blockKey, out var cachedAddress);
        if (init || !exists)
        {
            var address = await SwitchConnection.PointerAll(pointer, token);
            address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            cachedAddress = address;

            if (exists)
            {
                _cacheBlockAddresses[blockKey] = cachedAddress;
                Log($"Refreshed address for {blockKey:X8} found at {cachedAddress:X8}");
            }
            else
            {
                _cacheBlockAddresses.Add(blockKey, cachedAddress);
                Log($"Initial address for {blockKey:X8} found at {cachedAddress:X8}");
            }
        }

        return await ReadEncryptedBlock(cachedAddress, blockKey, token);
    }
}
