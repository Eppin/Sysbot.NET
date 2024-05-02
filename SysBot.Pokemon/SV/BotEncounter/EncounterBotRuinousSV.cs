namespace SysBot.Pokemon;

using PKHeX.Core;

public class EncounterBotRuinousSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotEncounterSV(cfg, hub)
{
    protected override int StartBattleA => 18;
    protected override int StartBattleB => 18;
}
