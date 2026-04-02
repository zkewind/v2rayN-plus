using NLog;
using NLog.Config;
using NLog.Targets;
using System.Text;
using System.Text.RegularExpressions;

namespace ServiceLib.Common;

public class Logging
{
    private const string CompactLayout = "${pad:inner=[${uppercase:${substring:${level}:0:1}}]:width=4}${pad:inner=${logger}:width=20}${message}";
    private const string FullLayout = "${longdate}-${level:uppercase=true} ${message}";
    private static readonly Regex TimestampPrefixRegex = new(
        @"^(?:[+-]\d{2}:?\d{2}\s+)?(?:\d{4}[-/]\d{2}[-/]\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:\s*[+-]\d{2}:?\d{2})?|time=\d{4}-\d{2}-\d{2}T\S+)\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex LevelPrefixRegex = new(
        @"^(?:\[(?<levelBracket>trace|debug|info|warning|warn|error|fatal)\]|(?<levelWord>trace|debug|debu|info|warn|warning|error|erro|fatal|fata)(?:\[[^\]]+\])?|level=(?<levelKv>trace|debug|info|warning|warn|error|fatal))\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Logger _logger1 = LogManager.GetLogger("Log1");
    private static readonly Logger _logger2 = LogManager.GetLogger("Log2");

    // Use compact layout by default (no date, single-letter level in brackets)
    public static bool UseCompactLayout { get; set; } = true;

    public static void Setup()
    {
        LoggingConfiguration config = new();
        FileTarget fileTarget = new();
        config.AddTarget("file", fileTarget);
        if (UseCompactLayout)
        {
            // Compact: level as single uppercase letter in brackets, then logger padded, then message
            // Example: [D] MyLogger           Message text
            fileTarget.Layout = CompactLayout;
        }
        else
        {
            // Full layout: date - LEVEL Message
            fileTarget.Layout = FullLayout;
        }
        fileTarget.FileName = Utils.GetLogPath("${shortdate}.txt");
        config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));
        LogManager.Configuration = config;
    }

    public static void SetCompactLayout(bool compact)
    {
        UseCompactLayout = compact;
        Setup();
    }

    public static string FormatMessageForDisplay(string? message)
    {
        if (!UseCompactLayout || string.IsNullOrEmpty(message))
        {
            return message ?? string.Empty;
        }

        var lines = message.ReplaceLineEndings("\n").Split('\n');
        var builder = new StringBuilder(message.Length + 16);
        for (var i = 0; i < lines.Length; i++)
        {
            builder.Append(FormatSingleLineForDisplay(lines[i]));
            if (i < lines.Length - 1)
            {
                builder.Append(Environment.NewLine);
            }
        }

        return builder.ToString();
    }

    public static void LoggingEnabled(bool enable)
    {
        if (!enable)
        {
            LogManager.SuspendLogging();
        }
    }

    public static void SaveLog(string strContent)
    {
        if (!LogManager.IsLoggingEnabled())
        {
            return;
        }

        _logger1.Info(strContent);
    }

    public static void SaveLog(string strTitle, Exception ex)
    {
        if (!LogManager.IsLoggingEnabled())
        {
            return;
        }

        _logger2.Debug($"{strTitle},{ex.Message}");
        _logger2.Debug(ex.StackTrace);
        if (ex?.InnerException != null)
        {
            _logger2.Error(ex.InnerException);
        }
    }

    private static string FormatSingleLineForDisplay(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var text = line.Trim();
        if (Regex.IsMatch(text, @"^\[[A-Z]\]\s", RegexOptions.CultureInvariant))
        {
            return text;
        }

        text = TimestampPrefixRegex.Replace(text, string.Empty);

        var match = LevelPrefixRegex.Match(text);
        if (!match.Success)
        {
            return text;
        }

        var rawLevel = match.Groups["levelBracket"].Success
            ? match.Groups["levelBracket"].Value
            : match.Groups["levelWord"].Success
                ? match.Groups["levelWord"].Value
                : match.Groups["levelKv"].Value;
        var level = rawLevel.ToLowerInvariant() switch
        {
            "trace" => "T",
            "debug" or "debu" => "D",
            "info" => "I",
            "warn" or "warning" => "W",
            "error" or "erro" => "E",
            "fatal" or "fata" => "F",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(level))
        {
            return text;
        }

        var content = text[match.Length..].TrimStart();
        return $"[{level}] {content}";
    }
}
