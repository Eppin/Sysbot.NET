namespace SysBot.Pokemon;

using PKHeX.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.IO;
using System.Linq;

public class PointerBot<TPKM> where TPKM : PKM, new()
{
    private PokeTradeHub<TPKM> Hub { get; set; }
    private PointerSettings Settings { get; set; }
    private PokeRoutineExecutor<TPKM> Executor { get; set; }

    public PointerBot(PokeTradeHub<TPKM> hub, PokeRoutineExecutor<TPKM> executor)
    {
        Hub = hub;
        Settings = hub.Config.Pointer;
        Executor = executor;
    }

    public async Task MainLoop(CancellationToken token)
    {
        var pointerDict = GetPointers();
        var workingDict = new Dictionary<PointerTestType, List<string>>();

        while (!token.IsCancellationRequested)
        {
            workingDict.Clear();

            foreach (var (type, pointers) in pointerDict)
            {
                Executor.Log($"Testing {type}");

                var workingPointers = new List<string>();
                foreach (var pointer in pointers)
                {
                    var parsedPointer = ParsePointer(pointer);

                    switch (type)
                    {
                        case PointerTestType.Box:
                        case PointerTestType.Party:
                            if (await TestPokemon(parsedPointer, token).ConfigureAwait(false))
                            {
                                Executor.Log($"Success {type} for {pointer}");
                                workingPointers.Add(pointer);
                            }
                            break;

                        case PointerTestType.MyStatus:
                            if (await TestMyStatus(parsedPointer, token).ConfigureAwait(false))
                            {
                                Executor.Log($"Success {type} for {pointer}");
                                workingPointers.Add(pointer);
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                workingDict.Add(type, workingPointers);
                Executor.Log($"Tested {type}, {workingPointers.Count}/{pointers.Count}");
            }

            pointerDict = workingDict;

            if (pointerDict.Count == 0)
            {
                Executor.Log("No valid pointers left...");
                return;
            }

            // Results
            await File.WriteAllLinesAsync(Settings.MyStatusPointerFile + "r", pointerDict[PointerTestType.MyStatus], token);
            await File.WriteAllLinesAsync(Settings.PartyPointerFile + "r", pointerDict[PointerTestType.Party], token);
            await File.WriteAllLinesAsync(Settings.BoxPointerFile + "r", pointerDict[PointerTestType.Box], token);

            //Executor.Log("Restarting game...");

            //switch (Executor)
            //{
            //    case PokeRoutineExecutor8SWSH swsh8:
            //        await swsh8.ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            //        break;

            //    case PokeRoutineExecutor9SV sv9:
            //        await sv9.ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            //        break;
            //}

            return;
        }
    }

    private async Task<bool> TestPokemon(IEnumerable<long> pointer, CancellationToken token)
    {
        var pk9 = await Executor.ReadPokemonPointer(pointer, 0x158, token).ConfigureAwait(false);
        return pk9.Valid && pk9 is { EncryptionConstant: > 0, Species: > 0 };
    }

    private async Task<bool> TestMyStatus(IEnumerable<long> pointer, CancellationToken token)
    {
        SaveFile? sav = Executor switch
        {
            PokeRoutineExecutor9SV sv9 => await sv9.GetFakeTrainerSAV(pointer, token).ConfigureAwait(false),
            _ => null
        };

        if (sav == null)
            return false; ;

        return (LanguageID)sav.Language is (> 0 and <= LanguageID.ChineseT) && sav.OT.Length > 0 && sav.Version > 0;
    }

    private IEnumerable<long> ParsePointer(string pointer)
    {
        var jumps = pointer
            .Replace("main", "")
            .Replace("[", "")
            .Replace("]", "")
            .Split(new[] { "+" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(j => (long)Util.GetHexValue64(j.Trim()))
            .ToArray();

        if (jumps.Length != 0)
            return jumps;

        Executor.Log("Invalid Pointer");
        return Array.Empty<long>();
    }

    private Dictionary<PointerTestType, List<string>> GetPointers()
    {
        var files = new Dictionary<PointerTestType, List<string>>
        {
            { PointerTestType.Party, File.ReadAllLines(Settings.PartyPointerFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() },
            { PointerTestType.Box, File.ReadAllLines(Settings.BoxPointerFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() },
            { PointerTestType.MyStatus, File.ReadAllLines(Settings.MyStatusPointerFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() }
        };

        return files;
    }
}
