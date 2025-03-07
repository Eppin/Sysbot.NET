using System;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using PKHeX.Core.Searching;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon;

public class EncounterBotResetBS(PokeBotState cfg, PokeTradeHub<PB8> hub) : EncounterBotBS(cfg, hub)
{
    private ulong _playerPrefsProvider;
    private ulong _battleSetup;

    protected override async Task EncounterLoop(SAV8BS sav, CancellationToken token)
    {
        var pbOriginal = new PB8();

        while (!token.IsCancellationRequested)
        {
            PB8? pb8;

            await GetPlayerPrefsProvider(token);
            await GetBattleSetup(token);

            Log("Looking for a Pokémon...");
            do
            {
                await Click(A, 0_050, token).ConfigureAwait(false);
                pb8 = await GetEncounter(token).ConfigureAwait(false);
            } while (pb8 is null || SearchUtil.HashByDetails(pbOriginal) == SearchUtil.HashByDetails(pb8));

            var (stop, _) = await HandleEncounter(pb8, token).ConfigureAwait(false);
            if (stop)
                return;

            Log("No match, resetting the game...");
            await CloseGame(Hub.Config, token).ConfigureAwait(false);
            await StartGame(Hub.Config, token).ConfigureAwait(false);
        }
    }

    private async Task GetPlayerPrefsProvider(CancellationToken token)
    {
        const int size = sizeof(ulong);

        var tmp = BitConverter.ToUInt64(await SwitchConnection.ReadBytesMainAsync(Offsets.PlayerPrefsProviderInstance, size, token));

        var addresses = new ulong[] { 0x18, 0xc0, 0x28, 0xb8, 0 };

        var result = tmp;
        foreach (var addition in addresses)
            result = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(result + addition, size, token));

        _playerPrefsProvider = result;
    }

    private async Task GetBattleSetup(CancellationToken token)
    {
        const int size = sizeof(ulong);

        var battleSetup = await SwitchConnection.ReadBytesAbsoluteAsync(_playerPrefsProvider + 0x800, size, token).ConfigureAwait(false);
        _battleSetup = BitConverter.ToUInt64(battleSetup);
    }

    public async Task<PB8?> GetEncounter(CancellationToken token)
    {
        const int size = sizeof(ulong);

        var tmp = _battleSetup;

        var addresses = new ulong[] { 0x58, 0x28, 0x10, 0x20, 0x20, 0x18 };

        var result = tmp;
        foreach (var addition in addresses)
            result = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(result + addition, size, token));

        var pb8 = await ReadUntilPresent(result + 0x20, 0_050, 0_050, 0x148, token).ConfigureAwait(false);

        return pb8 is { Species: > 0, ChecksumValid: true } ? pb8 : null;
    }
}
