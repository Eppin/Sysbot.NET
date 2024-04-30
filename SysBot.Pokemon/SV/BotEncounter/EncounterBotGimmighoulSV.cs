namespace SysBot.Pokemon;

using PKHeX.Core;

public class EncounterBotGimmighoulSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotEncounterSV(cfg, hub)
{
    protected override int StartBattleA => 2;
    protected override int StartBattleB => 5;
}

