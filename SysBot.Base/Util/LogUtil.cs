using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SysBot.Base;

public static class LogConfig
{
    public static int MaxArchiveFiles { get; set; } = 14; // 2 weeks
    public static bool LoggingEnabled { get; set; } = true;
    public static string LoggingFolder { get; set; } = "logs";
}

public static class LogUtil
{
    static LogUtil()
    {
        if (!LogConfig.LoggingEnabled)
            return;

        var logFolder = LogConfig.LoggingFolder;

        var config = new LoggingConfiguration();
        Directory.CreateDirectory(logFolder);
        var logfile = new FileTarget("logfile")
        {
            FileName = Path.Combine(logFolder, "SysBotLog.txt"),
            ConcurrentWrites = true,

            ArchiveEvery = FileArchivePeriod.Day,
            ArchiveNumbering = ArchiveNumberingMode.Date,
            ArchiveFileName = Path.Combine(logFolder, "SysBotLog.{#}.txt"),
            ArchiveDateFormat = "yyyy-MM-dd",
            ArchiveAboveSize = 104857600, // 100MB (never)
            MaxArchiveFiles = LogConfig.MaxArchiveFiles,
            Encoding = Encoding.Unicode,
            WriteBom = true,
        };
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
        LogManager.Configuration = config;
    }

    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    public static void LogText(string message) => Logger.Log(LogLevel.Info, message);

    // hook in here if you want to forward the message elsewhere???
    public static readonly List<(Action<string, string>, string type)> Forwarders = new();

    public static DateTime LastLogged { get; private set; } = DateTime.Now;

    public static void LogError(string message, string identity)
    {
        Logger.Log(LogLevel.Error, $"{identity} {message}");
        Log(message, identity);
    }

    public static void LogInfo(string message, string identity, bool logAlways = true)
    {
        Logger.Log(LogLevel.Info, $"{identity} {message}");
        Log(message, identity, logAlways);
    }

    private static void Log(string message, string identity, bool logAlways = true)
    {
        foreach (var (fwd, type) in Forwarders)
        {
            try
            {
                if (!"LogModule".Equals(type) || ("LogModule".Equals(type) && logAlways))
                    fwd(message, identity);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, $"Failed to forward log from {identity} - {message}");
                Logger.Log(LogLevel.Error, ex);
            }
        }

        LastLogged = DateTime.Now;
    }

    public static void LogSafe(Exception exception, string identity)
    {
        Logger.Log(LogLevel.Error, $"Exception from {identity}:");
        Logger.Log(LogLevel.Error, exception);

        var err = exception.InnerException;
        while (err is not null)
        {
            Logger.Log(LogLevel.Error, err);
            err = err.InnerException;
        }
    }
}
