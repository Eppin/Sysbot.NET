namespace SysBot.Pokemon;

using System;
using PKHeX.Core;

public static class UpdateUtil
{
    /// <summary>
    /// Gets the latest version according to the GitHub API
    /// Credits to original source, PKHeX. Code is copied from https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Util/UpdateUtil.cs
    /// </summary>
    /// <returns>A version representing the latest available version, or null if the latest version could not be determined</returns>
    public static Version? GetLatestVersion()
    {
        const string apiEndpoint = "https://api.github.com/repos/Eppin/Sysbot.NET/releases/latest";
        var responseJson = NetUtil.GetStringFromURL(new Uri(apiEndpoint));
        if (responseJson is null)
            return null;

        // Parse it manually; no need to parse the entire json to object.
        const string tag = "tag_name";
        var index = responseJson.IndexOf(tag, StringComparison.Ordinal);
        if (index == -1)
            return null;

        var first = responseJson.IndexOf('"', index + tag.Length + 1) + 1;
        if (first == 0)
            return null;
        var second = responseJson.IndexOf('"', first);
        if (second == -1)
            return null;

        var tagString = responseJson.AsSpan()[first..second];
        return !Version.TryParse(tagString, out var latestVersion) ? null : latestVersion;
    }
}
