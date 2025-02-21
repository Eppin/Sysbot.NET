using System.ComponentModel;

namespace SysBot.Pokemon;

public class DenSettings
{
    private const string Den = nameof(Den);
    public override string ToString() => "Den Bot Settings";

    [Category(Den)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public DenMode Mode { get; set; } = new();

    [Category(Den), Description("Location of the Den to be used.")]
    public DenLocation Location { get; set; } = DenLocation.Galar;

    [Category(Den), Description("Should the Den beam be purple?")]
    public bool PurpleBeam { get; set; }

    [Category(Den), Description("Guaranteed IVs.")]
    public uint GuaranteedIVs { get; set; } = 4;

    [Category(Den), Description("Maximum advances to search for a result")]
    public long Advances { get; set; } = 10_000;

    [Category(Den), Description("Additional delay between skips in milliseconds.")]
    public int SkipDelay { get; set; } = 360;
}
