namespace SysBot.Pokemon.ZA;

using System;
using PKHeX.Core;

public sealed class BotFactory9ZA : BotFactory<PA9>
{
    public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PA9> hub, PokeBotState cfg) => cfg.NextRoutineType switch
    {
        PokeRoutineType.EncounterOverworld => new EncounterBotOverworldScannerZA(cfg, hub),

        PokeRoutineType.RemoteControl => new RemoteControlBotZA(cfg),

        _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
    };

    public override bool SupportsRoutine(PokeRoutineType type) => type switch
    {
        PokeRoutineType.EncounterOverworld => true,

        PokeRoutineType.RemoteControl => true,

        _ => false,
    };
}
