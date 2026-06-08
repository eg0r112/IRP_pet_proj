using System.Text;

namespace irp_pet.Infrastructure;

public static class TelegramMessageFormatter
{
    public static string Format(NotificationMessage msg)
    {
        var sb = new StringBuilder();
        var (header, emoji) = GetHeader(msg.EventType);

        sb.AppendLine($"{emoji} <b>{header}</b>");
        sb.AppendLine();
        sb.AppendLine($"<b>Сервис:</b> {Escape(msg.ServiceKey)}");
        sb.AppendLine($"<b>Срочность:</b> {FormatSeverity(msg.Severity)}");
        sb.AppendLine($"<b>Статус:</b> {FormatStatus(msg.Status)}");
        sb.AppendLine();
        sb.AppendLine($"<b>Описание:</b>");
        sb.AppendLine(Escape(msg.Title));

        if (!string.IsNullOrWhiteSpace(msg.Fingerprint))
            sb.AppendLine($"<b>Fingerprint:</b> <code>{Escape(msg.Fingerprint)}</code>");

        sb.AppendLine($"<b>ID инцидента:</b> <code>{msg.IncidentId}</code>");

        if (!string.IsNullOrWhiteSpace(msg.OnCallDisplayName))
            sb.AppendLine($"<b>Дежурный:</b> {Escape(msg.OnCallDisplayName)}");

        if (!string.IsNullOrWhiteSpace(msg.ActorDisplayName))
            sb.AppendLine($"<b>Действие выполнил:</b> {Escape(msg.ActorDisplayName)}");

        if (msg.OpenedAtUtc.HasValue)
            sb.AppendLine($"<b>Открыт:</b> {msg.OpenedAtUtc.Value:dd.MM.yyyy HH:mm} UTC");

        if (!string.IsNullOrWhiteSpace(msg.Details))
        {
            sb.AppendLine();
            sb.AppendLine($"<i>{Escape(msg.Details)}</i>");
        }

        return sb.ToString().TrimEnd();
    }

    private static (string Header, string Emoji) GetHeader(string eventType) => eventType switch
    {
        "IncidentCreated" => ("Новый инцидент", "🔴"),
        "IncidentEscalated" => ("Эскалация — нет подтверждения", "🚨"),
        "IncidentAcknowledged" => ("Инцидент принят в работу", "✅"),
        "IncidentResolved" => ("Инцидент закрыт", "🟢"),
        _ => ("Уведомление IRP", "ℹ️")
    };

    private static string FormatSeverity(string severity) => severity switch
    {
        "Critical" => "🔥🔥 <b>Критическая</b>",
        "High" => "🔥 <b>Высокая</b>",
        "Medium" => "⚠️ Средняя",
        "Low" => "ℹ️ Низкая",
        _ => Escape(severity)
    };

    private static string FormatStatus(string status) => status switch
    {
        "Open" => "🔴 Открыт",
        "Acknowledged" => "🟡 Принят",
        "Resolved" => "🟢 Закрыт",
        _ => Escape(status)
    };

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
