namespace SysBot.Pokemon;

using PKHeX.Core;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchStick;

public class PointerBotSV : PokeRoutineExecutor9SV
{
    private readonly PointerBot<PK9> _pointerBot;

    public PointerBotSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg)
    {
        _pointerBot = new PointerBot<PK9>(hub, this);
    }

    public override Task MainLoop(CancellationToken token)
    {
        return _pointerBot.MainLoop(token);
    }

    public override async Task HardStop()
    {
        await ResetStick(CancellationToken.None).ConfigureAwait(false);
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ResetStick(CancellationToken token)
    {
        // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
    }
}

