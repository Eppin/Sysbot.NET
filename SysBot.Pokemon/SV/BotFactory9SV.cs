using PKHeX.Core;
using System;

namespace SysBot.Pokemon
{
    public sealed class BotFactory9SV : BotFactory<PK9>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK9> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.EggFetch => new EncounterBotEggSV(cfg, Hub),
            PokeRoutineType.EncounterRuinous => new EncounterBotRuinousSV(cfg, Hub),
            PokeRoutineType.EncounterGimmighoul => new EncounterBotGimmighoulSV(cfg, Hub),
            PokeRoutineType.EncounterLoyal => new EncounterBotLoyalSV(cfg, Hub),
            PokeRoutineType.RemoteControl => new RemoteControlBotSV(cfg),
            PokeRoutineType.Pointer => new PointerBotSV(cfg, Hub),

            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };

        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.EggFetch => true,
            PokeRoutineType.EncounterRuinous => true,
            PokeRoutineType.EncounterGimmighoul => true,
            PokeRoutineType.EncounterLoyal => true,
            PokeRoutineType.RemoteControl => true,
            PokeRoutineType.Pointer => true,

            _ => false,
        };
    }
}
