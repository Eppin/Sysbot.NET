using PKHeX.Core;

namespace SysBot.Pokemon;

public class EncounterBotParadoxSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotEncounterSV(cfg, hub)
{
    protected override int StartBattleA => 6;
    protected override int StartBattleB => 8;
}
