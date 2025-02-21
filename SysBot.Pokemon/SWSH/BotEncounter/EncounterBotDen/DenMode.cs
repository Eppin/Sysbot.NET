using System.ComponentModel;

namespace SysBot.Pokemon;

public class DenMode
{
    private const string Mode = nameof(Mode);
    public override string ToString() => "Den Bot Settings";

    [Category(Mode), Description("Throw a Wishing Piece in the den?")]
    public bool ThrowWishingPiece { get; set; } = true;
}
