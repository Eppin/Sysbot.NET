﻿namespace SysBot.Pokemon;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;

public abstract class EncounterBotSV : PokeRoutineExecutor9SV, IEncounterBot
{
    protected readonly PokeTradeHub<PK9> Hub;
    private readonly IDumper _dumpSetting;
    private readonly EncounterSettingsSV _settings;
    public ICountSettings Counts => _settings;
    public readonly IReadOnlyList<string> UnwantedMarks;

    protected EncounterBotSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg)
    {
        Hub = hub;
        _settings = Hub.Config.EncounterSV;
        _dumpSetting = Hub.Config.Folder;
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
    protected async Task<(bool Stop, bool Success)> HandleEncounter(PK9 pk, CancellationToken token, byte[]? raw = null, bool minimize = false)
    {
        EncounterCount++;
        var print = Hub.Config.StopConditions.GetPrintName(pk);
        Log($"Encounter: {EncounterCount}");

        if (!string.IsNullOrWhiteSpace(print))
            Log($"{print}{Environment.NewLine}", !minimize);

        var folder = IncrementAndGetDumpFolder(pk);

        if (pk.Valid)
        {
            switch (_dumpSetting)
            {
                case { Dump: true, DumpShinyOnly: true } when pk.IsShiny:
                case { Dump: true, DumpShinyOnly: false }:
                    DumpPokemon(_dumpSetting.DumpFolder, folder, pk);
                    break;
            }

            if (raw != null)
            {
                switch (_dumpSetting)
                {
                    case { DumpRaw: true, DumpShinyOnly: true } when pk.IsShiny:
                    case { DumpRaw: true, DumpShinyOnly: false }:
                        DumpPokemon(_dumpSetting.DumpFolder, folder, pk, raw);
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

        if (_settings.MinMaxScaleOnly && pk.Scale is > 0 and < 255)
        {
            Hub.LogEmbed(pk, false);
            return (false, false);
        }

        if (_settings.OneInOneHundredOnly)
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

        var mode = _settings.ContinueAfterMatch;
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
            var legendary = SpeciesCategory.IsLegendary(pk.Species) || SpeciesCategory.IsMythical(pk.Species) || SpeciesCategory.IsSubLegendary(pk.Species);
            if (legendary)
            {
                _settings.AddCompletedLegends();
                OutputExtensions<PK9>.EncounterLogs(pk, "EncounterLogPretty_LegendSV.txt");
                OutputExtensions<PK9>.EncounterScaleLogs(pk, "EncounterLogScale_LegendSV.txt");
                return "legends";
            }
            else if (pk.IsEgg)
            {
                _settings.AddCompletedEggs();
                OutputExtensions<PK9>.EncounterLogs(pk, "EncounterLogPretty_EggSV.txt");
                OutputExtensions<PK9>.EncounterScaleLogs(pk, "EncounterLogScale_EggSV.txt");
                return "egg";
            }

            OutputExtensions<PK9>.EncounterLogs(pk, "EncounterLogPretty_EncounterSV.txt");
            OutputExtensions<PK9>.EncounterScaleLogs(pk, "EncounterLogScale_EncounterSV.txt");
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
         * [100% Fast capture on(v1.3.2)]
         * 04000000 01857FE8 52800028
         * 04000000 01857FF4 14000020
         * 04000000 0185804C 52800028
         * 04000000 01858084 52800028
         */

        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x01857FE8, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x14000020), 0x01857FF4, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x0185804C, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x01858084, token);
    }
}