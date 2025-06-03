namespace SysBot.Pokemon;

using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static PokeDataOffsetsSWSH;

// Thanks to the following two source, who made this routine possible:
// Stats calculation and resetting @ https://github.com/Lusamine/SysBot.NET/blob/moarencounterbots/SysBot.Pokemon/SWSH/BotEncounter/EncounterBotMaxLairStatReset.cs
// Max Lair battle + catching @ https://github.com/Manu098vm/Sys-EncounterBot.NET/blob/master/SysBot.Pokemon/SWSH/BotEncounter/EncounterBotLair.cs#L166
public class EncounterBotMaxLairSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : EncounterBotSWSH(cfg, hub)
{
    private const uint StandardDamage = 0x7900E808;
    private const uint AlteredDamage = 0x7900E81F;

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var seed = BitConverter.ToUInt64(await Connection.ReadBytesAsync(AdventureSeedOffset, 8, token).ConfigureAwait(false), 0);
            Log($"Current Lair seed: {seed:X16}", false);

            if (!await ResetForStats(token).ConfigureAwait(false))
                continue;

            if (await WalkthroughDen(token).ConfigureAwait(false))
                return;
        }
    }

    private async Task<bool> WalkthroughDen(CancellationToken token)
    {
        if (Settings.MaxLair.InstantKill)
        {
            Log("Enable OHKO-cheat");
            var damageTemporalState = await SwitchConnection.ReadBytesMainAsync(DamageOutputOffset, 4, token).ConfigureAwait(false);

            if (BitConverter.GetBytes(StandardDamage).SequenceEqual(damageTemporalState))
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(AlteredDamage), DamageOutputOffset, token).ConfigureAwait(false);
        }

        Log("Den loop started");

        var stopwatch = Stopwatch.StartNew();
        var raidCount = 1;
        var inBattle = false;
        var lost = false;

        while (!(await IsInLairEndList(token).ConfigureAwait(false) || lost || token.IsCancellationRequested))
        {
            await Click(A, 0_200, token).ConfigureAwait(false);
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                lost = true;
                Log("Lost at first raid. Starting again.");
            }
            else if (!await IsInBattle(token).ConfigureAwait(false) && inBattle)
            {
                inBattle = false;
            }
            else if (await IsInBattle(token).ConfigureAwait(false) && !inBattle)
            {
                var pk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);

                Log(pk != null
                    ? $"Raid Battle {raidCount}: ({pk.Species}) {pk.Nickname}"
                    : $"Raid Battle {raidCount}.{Environment.NewLine}RAM probably shifted. It is suggested to reboot the game or console.");

                inBattle = true;
                raidCount++;
                stopwatch.Restart();
            }
            else if (await IsInBattle(token).ConfigureAwait(false) && inBattle)
            {
                if (stopwatch.ElapsedMilliseconds <= 120_000)
                    continue;

                Log("Stuck in a battle, trying to change move.");

                for (var j = 0; j < 50; j++)
                    await Click(B, 0_100, token).ConfigureAwait(false);

                await Click(A, 0_500, token).ConfigureAwait(false);
                await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                stopwatch.Restart();
            }
        }

        if (Settings.MaxLair.InstantKill)
        {
            Log("Disable OHKO-cheat");
            var demageTemporalState = await SwitchConnection.ReadBytesMainAsync(DamageOutputOffset, 4, token).ConfigureAwait(false);

            if (BitConverter.GetBytes(AlteredDamage).SequenceEqual(demageTemporalState))
                await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(StandardDamage), DamageOutputOffset, token).ConfigureAwait(false);
        }

        if (lost)
            return false;

        // Check for shinies, check all the StopConditions for the Legendary
        var (selection, legendaryDefeated, stopConditionsMatch) = await IsAdventureHuntFound(token).ConfigureAwait(false);

        Settings.AddCompletedAdventures();

        if (raidCount < 5)
            Log($"Lost at battle n.{raidCount - 1}, adventure n. {Settings.CompletedAdventures}");
        else if (!legendaryDefeated)
            Log($"Lost at battle n.4, adventure n.{Settings.CompletedAdventures}");
        else
            Log($"Adventure n.{Settings.CompletedAdventures} completed");

        if (selection > 0)
        {
            var (pk, _) = await ReadLairResult(selection - 1, token).ConfigureAwait(false);

            await Task.Delay(1_500, token).ConfigureAwait(false);

            for (var i = 1; i < selection; i++)
                await Click(DDOWN, 1_000, token).ConfigureAwait(false);

            await Click(A, 0_900, token).ConfigureAwait(false);
            await Click(DDOWN, 0_800, token).ConfigureAwait(false);
            await Click(A, 2_300, token).ConfigureAwait(false);

            if (Hub.Config.StopConditions.CaptureVideoClip)
                await PressAndHold(CAPTURE, 2_000, 10_000, token).ConfigureAwait(false);

            if (pk != null && stopConditionsMatch)
            {
                Log($"Found match in result n.{selection}: {(Species)pk.Species}");
                return true;
            }

            if (pk != null)
                Log($"Found shiny in result n.{selection}: {(Species)pk.Species}");

            await Task.Delay(1_500, token).ConfigureAwait(false);
            await Click(B, 1_500, token).ConfigureAwait(false);
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(A, 1_000, token).ConfigureAwait(false);
        }
        else
        {
            Log("No result found, starting again.");
            await Task.Delay(1_500, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(A, 1_000, token).ConfigureAwait(false);
            Log("Back to overworld. Restarting the routine...");
        }

        return false;
    }

    private async Task<(int, bool, bool)> IsAdventureHuntFound(CancellationToken token)
    {
        var selection = 0;
        var legendaryDefeated = false;
        var stopConditionsMatch = false;
        var i = 0;

        while (i < 4)
        {
            var (pkm, bytes) = await ReadLairResult(i, token).ConfigureAwait(false);
            if (pkm != null)
            {
                if (i == 3)
                    legendaryDefeated = true;

                var (stop, _) = await HandleEncounter(pkm, token, bytes).ConfigureAwait(false);
                if (stop)
                {
                    selection = i + 1;
                    stopConditionsMatch = true;
                }
            }

            i++;
        }

        return (selection, legendaryDefeated, stopConditionsMatch);
    }

    private async Task<(PK8?, byte[]?)> ReadLairResult(int slot, CancellationToken token)
    {
        var pointer = new long[] { 0x28F4060, 0x1B0, 0x68, 0x58 + 0x08 * slot, 0x58, 0x0 };
        var (pkm, bytes) = await ReadUntilPresentPointer(pointer, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);

        if (pkm is not null && pkm.Species == 0)
            return (null, null);

        return (pkm, bytes);
    }

    // Hack to read 'absolute' bytes
    public new async Task<(PK8?, byte[]?)> ReadUntilPresentPointer(IReadOnlyList<long> jumps, int waitms, int waitInterval, int size, CancellationToken token)
    {
        var msWaited = 0;
        while (msWaited < waitms)
        {
            var (pk, bytes) = await ReadRawPokemonPointer(jumps, size, token).ConfigureAwait(false);
            if (pk.Species != 0 && pk.ChecksumValid)
                return (pk, bytes);
            await Task.Delay(waitInterval, token).ConfigureAwait(false);
            msWaited += waitInterval;
        }
        return (null, null);
    }

    private async Task<bool> IsInLairEndList(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(LairRewardsOffset, 1, token).ConfigureAwait(false))[0] != 0;


    private async Task<bool> ResetForStats(CancellationToken token)
    {
        await SetTarget(token);
        await ClearMaxLairPenalty(token).ConfigureAwait(false);
        await StartAdventure(token).ConfigureAwait(false);

        if (await IsMatchMaxLairLegendary(token))
            return true;

        Log("No match, resetting the game");
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
        await Click(DDOWN, 1_000, token).ConfigureAwait(false);

        Log("Entering the rental lobby.");
        await Click(A, 1_000, token).ConfigureAwait(false);
    }

    private async Task<bool> IsMatchMaxLairLegendary(CancellationToken token)
    {
        var valid = false;
        ulong ofs = 0;

        while (!valid)
            (valid, ofs) = await ValidatePointerAll(MaxLairPokemonRNGPointer, token).ConfigureAwait(false);

        var (s0, s1) = await GetMaxLairRNGState(ofs, token).ConfigureAwait(false);

        var (pk8, msg) = GetMaxLairLegendary(s0, s1, token);

        var stop = StopConditionSettings.EncounterFound(pk8, Hub.Config.StopConditions, UnwantedMarks);

        Log($"Legendary: {msg}");

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

    private (PK8 pk, string? print) GetMaxLairLegendary(ulong s0, ulong s1, CancellationToken token)
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
        Span<int> ivs = [-1, -1, -1, -1, -1, -1];
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
            Nature = (Nature)nature,
            IV_HP = ivs[0],
            IV_ATK = ivs[1],
            IV_DEF = ivs[2],
            IV_SPA = ivs[3],
            IV_SPD = ivs[4],
            IV_SPE = ivs[5]
        };

        pk.SetIsShiny(true);
        pk.Species = (ushort)Settings.MaxLair.Species;

        var msg = $"IVs: {ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[3]}/{ivs[4]}/{ivs[5]}, Nature: {GameInfo.GetStrings("en").Natures[nature]}";

        return (pk, msg);
    }
}
