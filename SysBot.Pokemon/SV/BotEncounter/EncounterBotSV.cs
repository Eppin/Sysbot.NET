namespace SysBot.Pokemon;

using Base;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static Base.SwitchStick;

public abstract class EncounterBotSV : PokeRoutineExecutor9SV, IEncounterBot
{
    protected readonly PokeTradeHub<PK9> Hub;
    protected readonly EncounterSettingsSV Settings;
    protected readonly IDumper DumpSetting;

    public ICountSettings Counts => Settings;
    public readonly IReadOnlyList<string> UnwantedMarks;

    protected EncounterBotSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg)
    {
        Hub = hub;
        Settings = Hub.Config.EncounterSV;
        DumpSetting = Hub.Config.Folder;
        StopConditionSettings.ReadUnwantedMarks(Hub.Config.StopConditions, out UnwantedMarks);
    }

    protected int EncounterCount;

    public override async Task MainLoop(CancellationToken token)
    {
        var settings = Hub.Config.EncounterSV;
        Log("Identifying trainer data of the host console.");
        var sav = await IdentifyTrainer(token).ConfigureAwait(false);
        await InitializeHardware(settings, token).ConfigureAwait(false);

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

    protected abstract Task EncounterLoop(SAV9SV sav, CancellationToken token);

    // Return true if breaking loop
    protected async Task<(bool Stop, bool Success)> HandleEncounter(PK9 pk, CancellationToken token, byte[]? raw = null, bool minimize = false, bool skipDump = false)
    {
        EncounterCount++;
        var print = Hub.Config.StopConditions.GetPrintName(pk);
        Log($"Encounter: {EncounterCount}");

        if (!string.IsNullOrWhiteSpace(print))
            Log($"{print}{Environment.NewLine}", !minimize);

        var folder = IncrementAndGetDumpFolder(pk);

        if (!skipDump && pk.Valid)
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

        if (Settings.MinMaxScaleOnly && pk.Scale is > 0 and < 255)
        {
            Hub.LogEmbed(pk, false);
            return (false, false);
        }

        if (Settings.OneInOneHundredOnly)
        {
            if ((Species)pk.Species is Species.Dunsparce or Species.Tandemaus && pk.EncryptionConstant % 100 != 0)
            {
                Hub.LogEmbed(pk, false);
                return (false, false);
            }
        }

        if (Hub.Config.StopConditions.CaptureVideoClip)
        {
            await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
            await PressAndHold(CAPTURE, 2_000, 0, token).ConfigureAwait(false);
        }

        var mode = Settings.ContinueAfterMatch;
        var msg = $"Result found!\n{print}\n" + mode switch
        {
            ContinueAfterMatch.Continue => "Continuing...",
            ContinueAfterMatch.PauseWaitAcknowledge => "Waiting for instructions to continue.",
            ContinueAfterMatch.StopExit => "Stopping routine execution; restart the bot to search again.",
            _ => throw new ArgumentOutOfRangeException("Match result type was invalid.", nameof(ContinueAfterMatch))
        };

        if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
            msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
        EchoUtil.Echo(msg);
        Hub.LogEmbed(pk, true);

        if (mode == ContinueAfterMatch.StopExit)
            return (true, true);
        if (mode == ContinueAfterMatch.Continue)
            return (false, true);

        _isWaiting = true;
        while (_isWaiting)
            await Task.Delay(1_000, token).ConfigureAwait(false);

        return (false, true);
    }

    private string IncrementAndGetDumpFolder(PK9 pk)
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
                OutputExtensions<PK9>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_LegendSV.txt"));
                OutputExtensions<PK9>.EncounterScaleLogs(pk, Path.Combine(loggingFolder, "EncounterLogScale_LegendSV.txt"));
                return "legends";
            }

            if (pk.IsEgg)
            {
                Settings.AddCompletedEggs();
                OutputExtensions<PK9>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EggSV.txt"));
                OutputExtensions<PK9>.EncounterScaleLogs(pk, Path.Combine(loggingFolder, "EncounterLogScale_EggSV.txt"));
                return "egg";
            }

            Settings.AddCompletedEncounters();
            OutputExtensions<PK9>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EncounterSV.txt"));
            OutputExtensions<PK9>.EncounterScaleLogs(pk, Path.Combine(loggingFolder, "EncounterLogScale_EncounterSV.txt"));
            return "encounters";
        }
        catch (Exception e)
        {
            Log($"Couldn't update encounters:\n{e.Message}\n{e.StackTrace}");
            return "random";
        }
    }

    private bool _isWaiting;
    public void Acknowledge() => _isWaiting = false;

    protected async Task ResetStick(CancellationToken token)
    {
        // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
    }

    protected async Task EnableAlwaysCatch(CancellationToken token)
    {
        if (!Hub.Config.EncounterSV.EnableCatchCheat)
            return;

        Log("Enable critical capture cheat", false);
        // Source: https://gbatemp.net/threads/pokemon-scarlet-violet-cheat-database.621563/

        // Original cheat:
        /*
         * [100% Fast capture on(v3.0.0)]
         * 04000000 0197E428 52800028
         * 04000000 0197E434 14000020
         * 04000000 0197E48C 52800028
         * 04000000 0197E4C4 52800028
         */

        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x0197E428, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x14000020), 0x0197E434, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x0197E48C, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x0197E4C4, token);
    }
}
