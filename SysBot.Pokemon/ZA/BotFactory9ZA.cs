using System;
using PKHeX.Core;

namespace SysBot.Pokemon.ZA;

public sealed class BotFactory9ZA : BotFactory<PA9>
{
    public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PA9> hub, PokeBotState cfg) => cfg.NextRoutineType switch
    {
        //PokeRoutineType.FlexTrade or PokeRoutineType.Idle
        //    or PokeRoutineType.LinkTrade
        //    or PokeRoutineType.Clone
        //    or PokeRoutineType.Dump
        //    => new PokeTradeBotLA(Hub, cfg),

        PokeRoutineType.RemoteControl => new RemoteControlBotZA(cfg),

        _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
    };

    public override bool SupportsRoutine(PokeRoutineType type) => type switch
    {
        PokeRoutineType.RemoteControl => true,

        _ => false,
    };
}
