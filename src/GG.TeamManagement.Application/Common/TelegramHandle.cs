namespace GG.TeamManagement.Application.Common;

public static class TelegramHandle
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var handle = value.Trim();
        if (handle.StartsWith('@'))
            handle = handle[1..].Trim();

        return string.IsNullOrWhiteSpace(handle) ? null : handle.ToLowerInvariant();
    }

    public static bool LooksLikeNumericId(string? value) =>
        !string.IsNullOrEmpty(value) && value.All(char.IsDigit);
}
