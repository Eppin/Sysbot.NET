﻿using PKHeX.Core;
using System;

namespace SysBot.Pokemon
{
    public sealed class BotFactory9SV : BotFactory<PK9>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK9> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                => new PokeTradeBotSV(Hub, cfg),

            PokeRoutineType.EggFetch => new EncounterBotEggSV(cfg, Hub),
            PokeRoutineType.EncounterRuinous => new EncounterBotRuinousSV(cfg, Hub),
            PokeRoutineType.EncounterGimmighoul => new EncounterBotGimmighoulSV(cfg, Hub),
            PokeRoutineType.RemoteControl => new RemoteControlBotSV(cfg),

            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };

        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                => true,

            PokeRoutineType.EggFetch => true,
            PokeRoutineType.EncounterRuinous => true,
            PokeRoutineType.EncounterGimmighoul => true,
            PokeRoutineType.RemoteControl => true,

            _ => false,
        };
    }
}
