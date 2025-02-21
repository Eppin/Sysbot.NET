namespace SysBot.Pokemon;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Base;
using PKHeX.Core;
using static Base.SwitchButton;
using static Base.SwitchStick;

public abstract class EncounterBotBS : PokeRoutineExecutor8BS, IEncounterBot
{
    protected readonly PokeTradeHub<PB8> Hub;
    protected readonly EncounterSettingsBS Settings;
    protected readonly IDumper DumpSetting;

    public ICountSettings Counts => Settings;

    protected EncounterBotBS(PokeBotState cfg, PokeTradeHub<PB8> hub) : base(cfg)
    {
        Hub = hub;
        Settings = Hub.Config.EncounterBS;
        DumpSetting = Hub.Config.Folder;
    }

    protected int EncounterCount;

    public override async Task MainLoop(CancellationToken token)
    {
        var settings = Hub.Config.EncounterBS;
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

    protected abstract Task EncounterLoop(SAV8BS sav, CancellationToken token);

    // Return true if breaking loop
    protected async Task<(bool Stop, bool Success)> HandleEncounter(PB8 pk, CancellationToken token, byte[]? raw = null, bool minimize = false, bool skipDump = false)
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

        if (!StopConditionSettings.EncounterFound(pk, Hub.Config.StopConditions, null))
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

    private string IncrementAndGetDumpFolder(PB8 pk)
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
                OutputExtensions<PB8>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_LegendBS.txt"));
                return "legends";
            }

            if (pk.IsEgg)
            {
                Settings.AddCompletedEggs();
                OutputExtensions<PB8>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EggBS.txt"));
                return "egg";
            }

            Settings.AddCompletedEncounters();
            OutputExtensions<PB8>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EncounterBS.txt"));
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
}
