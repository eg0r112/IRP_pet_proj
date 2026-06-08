using irp_pet.Models;

namespace irp_pet.Infrastructure;

/// <summary>Тексты сообщений Telegram-бота для дежурных.</summary>
public static class TelegramUiFormatter
{
    public static string FormatSeverity(Severity severity) => severity switch
    {
        Severity.Critical => "🔥🔥 <b>Критическая</b>",
        Severity.High => "🔥 <b>Высокая</b>",
        Severity.Medium => "⚠️ Средняя",
        Severity.Low => "ℹ️ Низкая",
        _ => severity.ToString()
    };

    public static string FormatSeverityShort(Severity severity) => severity switch
    {
        Severity.Critical => "🔥🔥 Крит.",
        Severity.High => "🔥 Высокая",
        Severity.Medium => "⚠️ Средняя",
        Severity.Low => "ℹ️ Низкая",
        _ => severity.ToString()
    };

    public static string FormatStatus(IncidentStatus status) => status switch
    {
        IncidentStatus.Open => "🔴 <b>Открыт</b>",
        IncidentStatus.Acknowledged => "🟡 <b>В работе</b>",
        IncidentStatus.Resolved => "🟢 <b>Закрыт</b>",
        _ => status.ToString()
    };

    public static string FormatStatusShort(IncidentStatus status) => status switch
    {
        IncidentStatus.Open => "🔴 Открыт",
        IncidentStatus.Acknowledged => "🟡 В работе",
        IncidentStatus.Resolved => "🟢 Закрыт",
        _ => status.ToString()
    };

    public static string FormatIncidentListMessage(IReadOnlyList<Incident> items)
    {
        var lines = new List<string>
        {
            $"<b>📋 Инциденты ({items.Count})</b>",
            "Выберите инцидент:",
            ""
        };

        for (var i = 0; i < items.Count; i++)
            lines.Add(FormatIncidentListLine(items[i], i + 1));

        return string.Join('\n', lines);
    }

    public static string FormatIncidentListLine(Incident incident, int index) =>
        $"""
        <b>#{index}</b> {FormatStatusShort(incident.Status)} · {FormatSeverityShort(incident.Severity)}
        serviceKey: <code>{Escape(incident.Service.Key)}</code>
        {FormatShortDescription(incident)}
        """;

    public static string FormatShortDescription(Incident incident) =>
        Escape(Truncate(StripLegacyTitlePrefix(incident), 120));

    private static string StripLegacyTitlePrefix(Incident incident)
    {
        var title = incident.Title.Trim();
        var legacyPrefix = $"[{incident.Service.Key}] ";
        return title.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)
            ? title[legacyPrefix.Length..].Trim()
            : title;
    }

    public static string FormatServiceKey(Incident incident) => incident.Service.Key;

    public static string FormatPickerButtonLabel(Incident incident, int index)
    {
        var label = $"{FormatStatusEmoji(incident.Status)} #{index} {FormatServiceKey(incident)} — {StripLegacyTitlePrefix(incident)}";
        return Truncate(label, 60);
    }

    public static string FormatIncidentDetail(Incident incident) =>
        $"""
        <b>Инцидент</b>
        {FormatStatus(incident.Status)}
        <b>Срочность:</b> {FormatSeverity(incident.Severity)}
        <b>serviceKey:</b> <code>{Escape(incident.Service.Key)}</code>
        <b>Описание:</b>
        {FormatShortDescription(incident)}
        <b>Открыт:</b> {incident.OpenedAtUtc:dd.MM.yyyy HH:mm} UTC
        <b>ID:</b> <code>{incident.Id}</code>
        """;

    private static string FormatStatusEmoji(IncidentStatus status) => status switch
    {
        IncidentStatus.Open => "🔴",
        IncidentStatus.Acknowledged => "🟡",
        _ => "⚪"
    };

    public static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
