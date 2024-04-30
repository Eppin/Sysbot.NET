using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;

namespace SysBot.Pokemon;

public abstract class EncounterBotSWSH : PokeRoutineExecutor8SWSH, IEncounterBot
{
    protected readonly PokeTradeHub<PK8> Hub;
    private readonly IDumper DumpSetting;
    protected readonly EncounterSettingsSWSH Settings;
    protected readonly byte[] BattleMenuReady = [0, 0, 0, 255];
    public ICountSettings Counts => Settings;
    public readonly IReadOnlyList<string> UnwantedMarks;

    protected EncounterBotSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
    {
        Hub = hub;
        Settings = Hub.Config.EncounterSWSH;
        DumpSetting = Hub.Config.Folder;
        StopConditionSettings.ReadUnwantedMarks(Hub.Config.StopConditions, out UnwantedMarks);
    }

    // Cached offsets that stay the same per session.
    protected ulong OverworldOffset;

    protected int encounterCount;

    public override async Task MainLoop(CancellationToken token)
    {
        var settings = Hub.Config.EncounterSWSH;
        Log("Identifying trainer data of the host console.");
        var sav = await IdentifyTrainer(token).ConfigureAwait(false);
        await InitializeHardware(settings, token).ConfigureAwait(false);

        OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);

        try
        {
            Log($"Starting main {GetType().Name} loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);
            await EncounterLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"Ending {GetType().Name} loop.");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task HardStop()
    {
        await ResetStick(CancellationToken.None).ConfigureAwait(false);
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    protected abstract Task EncounterLoop(SAV8SWSH sav, CancellationToken token);

    // return true if breaking loop
    protected async Task<(bool Stop, bool Success)> HandleEncounter(PK8 pk, CancellationToken token, byte[]? raw = null, bool minimize = false)
    {
        encounterCount++;
        var print = Hub.Config.StopConditions.GetPrintName(pk);
        Log($"Encounter: {encounterCount}");

        if (!string.IsNullOrWhiteSpace(print))
            Log($"{print}{Environment.NewLine}", !minimize);

        var folder = IncrementAndGetDumpFolder(pk);

        if (pk.Valid)
        {
            switch (DumpSetting)
            {
                case { Dump: true, DumpShinyOnly: true } when pk.IsShiny:
                case { Dump: true, DumpShinyOnly: false }:
                    DumpPokemon(DumpSetting.DumpFolder, folder, pk);
                    break;
            }

            if (raw != null)
            {
                switch (DumpSetting)
                {
                    case { DumpRaw: true, DumpShinyOnly: true } when pk.IsShiny:
                    case { DumpRaw: true, DumpShinyOnly: false }:
                        DumpPokemon(DumpSetting.DumpFolder, folder, pk, raw);
                        break;
                }
            }
        }

        if (!StopConditionSettings.EncounterFound(pk, Hub.Config.StopConditions, UnwantedMarks))
        {
            if (folder.Equals("egg") && Hub.Config.StopConditions.ShinyTarget is TargetShinyType.AnyShiny or TargetShinyType.StarOnly or TargetShinyType.SquareOnly && pk.IsShiny)
                Hub.LogEmbed(pk, false);

            return (false, false);
        }

        if (Hub.Config.StopConditions.CaptureVideoClip)
        {
            await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
            await PressAndHold(CAPTURE, 2_000, 0, token).ConfigureAwait(false);
        }

        var mode = Settings.ContinueAfterMatch;
        var msg = $"Result found!\n{print}\n" + mode switch
        {
            ContinueAfterMatch.Continue             => "Continuing...",
            ContinueAfterMatch.PauseWaitAcknowledge => "Waiting for instructions to continue.",
            ContinueAfterMatch.StopExit             => "Stopping routine execution; restart the bot to search again.",
            _ => throw new ArgumentOutOfRangeException(nameof(ContinueAfterMatch), "Match result type was invalid.")
        };

        if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
            msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
        EchoUtil.Echo(msg);
        Hub.LogEmbed(pk, true);

        if (mode == ContinueAfterMatch.StopExit)
            return (true, true);
        if (mode == ContinueAfterMatch.Continue)
            return (false, true);

        IsWaiting = true;
        while (IsWaiting)
            await Task.Delay(1_000, token).ConfigureAwait(false);
        return (false, true);
    }

    private string IncrementAndGetDumpFolder(PKM pk)
    {
        try
        {
            var loggingFolder = string.IsNullOrWhiteSpace(Hub.Config.LoggingFolder)
                ? string.Empty
                : Hub.Config.LoggingFolder;

            var legendary = SpeciesCategory.IsLegendary(pk.Species) || SpeciesCategory.IsMythical(pk.Species) || SpeciesCategory.IsSubLegendary(pk.Species);
            if (legendary)
            {
                Settings.AddCompletedLegends();
                OutputExtensions<PK8>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_LegendSWSH.txt"));
                return "legends";
            }

            if (pk.IsEgg)
            {
                Settings.AddCompletedEggs();
                OutputExtensions<PK8>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EggSWSH.txt"));
                return "egg";
            }
            if (pk.Species is >= (int)Species.Dracozolt and <= (int)Species.Arctovish)
            {
                Settings.AddCompletedFossils();
                OutputExtensions<PK8>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_FosilSWSH.txt"));
                return "fossil";
            }

            Settings.AddCompletedEncounters();
            OutputExtensions<PK8>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EncounterSWSH.txt"));
            return "encounters";
        }
        catch (Exception e)
        {
            Log($"Couldn't update encounters:\n{e.Message}\n{e.StackTrace}");
            return "random";
        }
    }

    private bool IsWaiting;
    public void Acknowledge() => IsWaiting = false;

    protected async Task ResetStick(CancellationToken token)
    {
        // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
    }

    protected async Task FleeToOverworld(CancellationToken token)
    {
        // This routine will always escape a battle.
        await Click(DUP, 0_200, token).ConfigureAwait(false);
        await Click(A, 1_000, token).ConfigureAwait(false);

        while (await IsInBattle(token).ConfigureAwait(false))
        {
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(DUP, 0_200, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
        }
    }
}
