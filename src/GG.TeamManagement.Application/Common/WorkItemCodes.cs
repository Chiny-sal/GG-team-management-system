namespace GG.TeamManagement.Application.Common;

public static class WorkItemCodes
{
    public const string Prefix = "WORK-";

    public static string Format(int number) => $"{Prefix}{number}";

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var raw = input.Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);
        if (raw.StartsWith('/'))
        {
            raw = raw[1..];
            var at = raw.IndexOf('@');
            if (at >= 0) raw = raw[..at];
        }

        if (int.TryParse(raw, out var n) && n > 0)
            return Format(n);

        if (raw.StartsWith(Prefix, StringComparison.Ordinal))
            return raw;

        return raw;
    }
}

public static class BotConversationSteps
{
    public const string AwaitingWorkCode = "AwaitingWorkCode";
    public const string AwaitingStatus = "AwaitingStatus";
    public const string AwaitingMeetingTopic = "AwaitingMeetingTopic";
}

public static class BotCommands
{
    public static bool Is(string text, string command)
    {
        var value = text.Trim();
        if (value.StartsWith('/'))
            value = value[1..];
        var at = value.IndexOf('@');
        if (at >= 0)
            value = value[..at];
        return value.Equals(command, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCancel(string text)
    {
        var value = text.Trim();
        if (value.StartsWith('/'))
            value = value[1..];
        return value.Equals("cancel", StringComparison.OrdinalIgnoreCase);
    }
}
