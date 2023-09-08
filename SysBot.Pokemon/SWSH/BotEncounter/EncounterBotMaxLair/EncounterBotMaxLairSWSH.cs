namespace SysBot.Pokemon;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;
using static PokeDataOffsetsSWSH;

public class EncounterBotMaxLairSWSH : EncounterBotSWSH
{
    public EncounterBotMaxLairSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg, hub)
    {
    }

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {

        while (!token.IsCancellationRequested)
        {
            var seed = BitConverter.ToUInt64(await Connection.ReadBytesAsync(AdventureSeedOffset, 8, token).ConfigureAwait(false), 0);
            Log($"Current Lair seed: {seed:X16}");

            if (await ResetForStats(token))
                return;
        }
    }

    private async Task<bool> ResetForStats(CancellationToken token)
    {
        await SetTarget(token);
        await ClearMaxLairPenalty(token).ConfigureAwait(false);
        await StartAdventure(token).ConfigureAwait(false);
        await RestoreStatsSeed(token).ConfigureAwait(false);

        if (await IsMatchMaxLairLegendary(token))
        {
            Log("Match...");
            return true;
        }

        Log("No match, resetting the game...");
        await CloseGame(Hub.Config, token).ConfigureAwait(false);
        await StartGame(Hub.Config, token).ConfigureAwait(false);
        await Task.Delay(0_500, token).ConfigureAwait(false);

        return false;
    }

    private async Task SetTarget(CancellationToken token)
    {
        var target = Settings.MaxLair.Species;
        if (target != MaxLairSpecies.None)
        {
            for (var i = 0; i < 4; i++)
            {
                var note = i switch
                {
                    0 => LairSpeciesNote1,
                    1 => LairSpeciesNote2,
                    2 => LairSpeciesNote3,
                    _ => LairSpeciesNote4
                };

                // First note shifts due to yet unknown reasons, just clear possible slots, check which note to use on startup and after catching a legendary.
                var bytes = BitConverter.GetBytes((ushort)target);
                await Connection.WriteBytesAsync(bytes, note, token);
            }

            Log($"Lair Notes set to {target}!");
        }
    }

    private async Task ClearMaxLairPenalty(CancellationToken token)
    {
        var data = BitConverter.GetBytes(0);
        await Connection.WriteBytesAsync(data, MaxLairPenaltyWarnOffset, token).ConfigureAwait(false);
        await Connection.WriteBytesAsync(data, MaxLairPenaltyCountOffset, token).ConfigureAwait(false);
    }

    private async Task StartAdventure(CancellationToken token)
    {
        // Should start in front of Scientist with no penalty.
        // Timings are optimized for an English game with Text speed of Fast. Adjust if this doesn't work for you.
        Log("Starting a new Dynamax Adventure.");
        await Click(A, 0_700, token).ConfigureAwait(false);
        await Click(A, 0_400, token).ConfigureAwait(false);
        await Click(A, 1_300, token).ConfigureAwait(false);
        await Click(A, 0_700, token).ConfigureAwait(false);
        await Click(A, 0_400, token).ConfigureAwait(false);
        await Click(A, 0_700, token).ConfigureAwait(false);
        await Click(A, 0_700, token).ConfigureAwait(false);
        await Click(A, 0_700, token).ConfigureAwait(false);

        Log("Accepting and saving...");
        for (var i = 0; i < 5; i++)
            await Click(A, 1_000, token).ConfigureAwait(false);

        await Task.Delay(1_000, token).ConfigureAwait(false);

        // Lobby should load here. Click down to "Don't Invite Others".
        await Click(DDOWN, 0_300, token).ConfigureAwait(false);

        Log("Entering the rental lobby.");
        await Click(A, 1_000, token).ConfigureAwait(false);
    }

    private async Task RestoreStatsSeed(CancellationToken token)
    {
        if (Settings.MaxLair.RememberSeed && Settings.MaxLair.Seed0 + Settings.MaxLair.Seed1 > 0)
        {
            Log("Restore saved stats seed.");

            var valid = false;
            ulong ofs = 0;

            while (!valid)
                (valid, ofs) = await ValidatePointerAll(MaxLairPokemonRNGPointer, token).ConfigureAwait(false);

            var s0 = BitConverter.GetBytes(Settings.MaxLair.Seed0);
            var s1 = BitConverter.GetBytes(Settings.MaxLair.Seed1);
            var s = s0.Concat(s1).ToArray();

            await SwitchConnection.WriteBytesAbsoluteAsync(s, ofs, token).ConfigureAwait(false);
        }
    }

    private async Task<bool> IsMatchMaxLairLegendary(CancellationToken token)
    {
        var valid = false;
        ulong ofs = 0;

        while (!valid)
            (valid, ofs) = await ValidatePointerAll(MaxLairPokemonRNGPointer, token).ConfigureAwait(false);

        var (s0, s1) = await GetMaxLairRNGState(ofs, token).ConfigureAwait(false);

        var (pk8, msg) = GetMaxLairLegendary(s0, s1, token);

        var stop = await HandleEncounter(pk8, token);
        if (stop)
        {
            Log($"Wanted: {msg}");

            if (Settings.MaxLair.RememberSeed)
            {
                Settings.MaxLair.Seed0 = s0;
                Settings.MaxLair.Seed1 = s1;
            }
        }
        else
            Log($"Unwanted: {msg}");

        return stop;
    }

    public async Task<(ulong s0, ulong s1)> GetMaxLairRNGState(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 16, token).ConfigureAwait(false);
        var s0 = BitConverter.ToUInt64(data, 0);
        var s1 = BitConverter.ToUInt64(data, 8);

        Log($"Lair Pokémon RNG state: {s0:X16}, {s1:X16}");

        return (s0, s1);
    }

    private static (PK8 pk, string? print) GetMaxLairLegendary(ulong s0, ulong s1, CancellationToken token)
    {
        // This should be the RNG state after generating 3 rental Pokémon.
        // Advance it 3 more times for replacement rentals, then 10 times for the Pokémon on the field.
        var rng_upper = new Xoroshiro128Plus(s0, s1);
        for (var i = 0; i < 13; i++)
            rng_upper.Next();

        // Generate the seed for the legendary Pokémon.
        var init = rng_upper.Next();
        var rng = new Xoroshiro128Plus(init);

        rng.NextInt(); // EC
        rng.NextInt(); // TID
        rng.NextInt(); // PID

        // Max Lair always has 4 fixed IVs.
        Span<int> ivs = stackalloc int[6] { -1, -1, -1, -1, -1, -1 };
        for (var i = 0; i < 4; i++)
        {
            int slot;
            do
            {
                slot = (int)rng.NextInt(6);
            } while (ivs[slot] != -1);

            ivs[slot] = 31;
        }
        for (var i = 0; i < 6; i++)
        {
            if (ivs[i] != -1)
                continue;

            var iv = (int)rng.NextInt(32);
            ivs[i] = iv;
        }

        // Skip Ability -- all fixed.
        // Skip Gender -- all fixed.

        var nature = (int)rng.NextInt(25);

        var pk = new PK8
        {
            Nature = nature,
            IV_HP = ivs[0],
            IV_ATK = ivs[1],
            IV_DEF = ivs[2],
            IV_SPA = ivs[3],
            IV_SPD = ivs[4],
            IV_SPE = ivs[5]
        };

        pk.SetIsShiny(true);

        var msg = $"IVs: {ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[3]}/{ivs[4]}/{ivs[5]}, Nature: {GameInfo.GetStrings(1).Natures[nature]}";

        return (pk, msg);
    }
}