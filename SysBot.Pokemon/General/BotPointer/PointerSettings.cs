using System.ComponentModel;
using System.IO;

namespace SysBot.Pokemon;

public class PointerSettings
{
    private const string Pointer = nameof(Pointer);
    public override string ToString() => "Pointer SV Settings";

    [Category(Pointer), Description($"Source file: where pointers for '{nameof(PointerTestType.Box)}' are read from.")]
    public string BoxPointerFile { get; set; } = string.Empty;

    [Category(Pointer), Description($"Source file: where pointers for '{nameof(PointerTestType.Party)}' are read from.")]
    public string PartyPointerFile { get; set; } = string.Empty;

    [Category(Pointer), Description($"Source file: where pointers for '{nameof(PointerTestType.MyStatus)}' are read from.")]
    public string MyStatusPointerFile { get; set; } = string.Empty;

    public void CreateDefaults(string path)
    {
        var pointer = Path.Combine(path, "pointer");
        Directory.CreateDirectory(pointer);

        var box = Path.Combine(pointer, "box.txt");
        File.WriteAllText(box, string.Empty);
        BoxPointerFile = box;

        var party = Path.Combine(pointer, "party.txt");
        File.WriteAllText(party, string.Empty);
        PartyPointerFile = party;

        var myStatus = Path.Combine(pointer, "myStatus.txt");
        File.WriteAllText(myStatus, string.Empty);
        MyStatusPointerFile = myStatus;
    }
}

public enum PointerTestType
{
    Box,
    Party,
    MyStatus
}
