namespace SysBot.Pokemon;

using PKHeX.Core;

public class EncounterBotLoyalSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotEncounterSV(cfg, hub)
{
    protected override int StartBattleA => 8;
    protected override int StartBattleB => 12;
}

