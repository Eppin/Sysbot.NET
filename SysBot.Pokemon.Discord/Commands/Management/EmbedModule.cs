namespace SysBot.Pokemon.Discord;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Base;
using global::Discord;
using global::Discord.Commands;
using global::Discord.WebSocket;
using PKHeX.Core;

public class EmbedModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static readonly Dictionary<ulong, LogAction> Channels = new();

    public static void RestoreEmbeds(DiscordSocketClient discord, DiscordSettings settings)
    {
        foreach (var ch in settings.EmbedResultChannels)
        {
            if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                AddEmbedChannel(c, ch.ID);
        }

        LogUtil.LogInfo("Added Embed results to Discord channel(s) on Bot startup.", "Discord");
    }

    [Command("embedHere")]
    [Summary("Makes the bot log embed results to the channel.")]
    [RequireSudo]
    public async Task AddEmbedAsync()
    {
        var c = Context.Channel;
        var cid = c.Id;
        if (Channels.TryGetValue(cid, out _))
        {
            await ReplyAsync("Already logging embeds here.").ConfigureAwait(false);
            return;
        }

        AddEmbedChannel(c, cid);

        // Add to discord global loggers (saves on program close)
        SysCordSettings.Settings.EmbedResultChannels.AddIfNew(new[] { GetReference(Context.Channel) });
        await ReplyAsync("Added Start Notification output to this channel!").ConfigureAwait(false);
    }

    [Command("embedInfo")]
    [Summary("Dumps the logging embed settings.")]
    [RequireSudo]
    public async Task DumpEmbedInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    [Command("embedClear")]
    [Summary("Clears the logging embed settings in that specific channel.")]
    [RequireSudo]
    public async Task ClearEmbedsAsync()
    {
        var id = Context.Channel.Id;
        if (!Channels.TryGetValue(id, out var log))
        {
            await ReplyAsync("Not embedding in this channel.").ConfigureAwait(false);
            return;
        }
        SysCord<T>.Runner.Hub.EmbedForwarders.Remove(log.Action);
        Channels.Remove(Context.Channel.Id);
        SysCordSettings.Settings.EmbedResultChannels.RemoveAll(z => z.ID == id);
        await ReplyAsync($"Logging cleared from channel: {Context.Channel.Name}").ConfigureAwait(false);
    }

    [Command("embedClearAll")]
    [Summary("Clears all the logging embed settings.")]
    [RequireSudo]
    public async Task ClearEmbedsAllAsync()
    {
        foreach (var l in Channels)
        {
            var entry = l.Value;
            await ReplyAsync($"Logging cleared from {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
            SysCord<T>.Runner.Hub.EmbedForwarders.Remove(entry.Action);
        }

        SysCord<T>.Runner.Hub.EmbedForwarders.RemoveAll(y => Channels.Select(x => x.Value.Action).Contains(y));
        Channels.Clear();
        SysCordSettings.Settings.EmbedResultChannels.Clear();
        await ReplyAsync("Logging embed cleared from all channels!").ConfigureAwait(false);
    }

    private class LogAction(ulong id, Action<T?, bool> messenger, string channel)
        : ChannelAction<T?, bool>(id, messenger, channel);

    private static void AddEmbedChannel(ISocketMessageChannel c, ulong cid)
    {
        var l = Logger;
        SysCord<T>.Runner.Hub.EmbedForwarders.Add(l);

        var entry = new LogAction(cid, l, c.Name);
        Channels.Add(cid, entry);
        return;

        void Logger(T? pkm, bool success)
        {
            try
            {
                var (text, embed) = GetMessage(pkm, success);
                c.SendMessageAsync(text, embed: embed);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, "(unknown)");
            }
        }

        static (string, Embed?) GetMessage(T? pkm, bool success) => Embed(pkm, success);
    }

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-HH:mm:ss}",
    };

    private static (string Text, Embed? Embed) Embed(T? pkm, bool success)
    {
        return pkm switch
        {
            null => ("(no valid data to embed)", null),
            PK8 pk8 => EmbedPk(pk8, success),
            PK9 pk9 => EmbedPk(pk9, success),
            _ => ("(unsupported embed)", null)
        };
    }

    private static (string Text, Embed? Embed) EmbedPk(PKM pkm, bool success)
    {
        if (pkm is not T pk)
            throw new Exception();

        var url = OutputExtensions<T>.PokeImage(pk, false, false);

        var is1of100 = (Species)pk.Species is Species.Dunsparce or Species.Tandemaus;
        var spec = string.Empty;

        if (is1of100)
        {
            if (pk.EncryptionConstant % 100 == 0)
            {
                spec = (Species)pk.Species is Species.Dunsparce
                    ? "\n3 Segment"
                    : "\nFamily of 3";
            }

            if (pk.EncryptionConstant % 100 != 0)
            {
                spec = (Species)pk.Species is Species.Dunsparce
                    ? "\n2 Segment"
                    : "\nFamily of 4";
            }
        }

        var gender = pk.Gender switch
        {
            0 => " - (M)",
            1 => " - (F)",
            _ => ""
        };

        var shiny = pk.ShinyXor == 0
            ? "■ - "
            : pk.ShinyXor <= 16
                ? "★ - "
                : "";

        var description = $"{shiny}{SpeciesName.GetSpeciesNameGeneration(pk.Species, 2, 8)}{OutputExtensions<T>.FormOutput(pk.Species, pk.Form, out _)}{gender}{spec}\n{(Nature)pk.Nature}, {(Ability)pk.Ability}\nIVs: {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}";

        var markUrl = success
            ? "https://i.imgur.com/T8vEiIk.jpg"
            : "https://i.imgur.com/t2M8qF4.png";

        var author = new EmbedAuthorBuilder { IconUrl = markUrl, Name = success ? "Match found!" : "Unwanted match..." };
        var embed = new EmbedBuilder
        {
            Color = success ? Color.Teal : Color.Red,
            ThumbnailUrl = url
        }.WithAuthor(author).WithDescription(description);

        var ping = SysCord<T>.Runner.Hub.Config.StopConditions.MatchFoundEchoMention;

        return (success ? ping : "", embed.Build());
    }
}
