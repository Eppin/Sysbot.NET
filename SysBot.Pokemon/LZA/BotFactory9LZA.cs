namespace SysBot.Pokemon.ZA;

using System;
using PKHeX.Core;

public sealed class BotFactory9LZA : BotFactory<PA9>
{
    public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PA9> hub, PokeBotState cfg) => cfg.NextRoutineType switch
    {
        PokeRoutineType.EncounterFloette => new EncounterBotFloetteLZA(cfg, hub),
        PokeRoutineType.EncounterOverworld => new EncounterBotOverworldScannerLZA(cfg, hub),
        PokeRoutineType.FossilBot => new EncounterBotFossilLZA(cfg, hub),

        PokeRoutineType.RemoteControl => new RemoteControlBotLZA(cfg),

        _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
    };

    public override bool SupportsRoutine(PokeRoutineType type) => type switch
    {
        PokeRoutineType.EncounterFloette => true,
        PokeRoutineType.EncounterOverworld => true,
        PokeRoutineType.FossilBot => true,

        PokeRoutineType.RemoteControl => true,

        _ => false,
    };
}
