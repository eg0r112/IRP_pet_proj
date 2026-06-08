using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using irp_pet.Models;

namespace irp_pet.Infrastructure;

public sealed class TelegramApiClient
{
    public const string MenuIncidents = "📋 Инциденты";
    public const string MenuProfile = "👤 Мой профиль";
    public const string MenuHelp = "❓ Справка";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramApiClient> _logger;

    public TelegramApiClient(HttpClient http, IConfiguration config, ILogger<TelegramApiClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public bool IsConfigured =>
        _config.GetValue("Telegram:Enabled", false)
        && !string.IsNullOrWhiteSpace(_config["Telegram:BotToken"]);

    public bool PollingEnabled =>
        IsConfigured && _config.GetValue("Telegram:BotPollingEnabled", true);

    private string? Token => _config["Telegram:BotToken"];

    public async Task<bool> SendMessageAsync(
        string chatId,
        string text,
        object? replyMarkup = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return false;

        var url = $"https://api.telegram.org/bot{Token}/sendMessage";
        var response = await _http.PostAsJsonAsync(url, new
        {
            chat_id = chatId,
            text,
            parse_mode = "HTML",
            reply_markup = replyMarkup
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Telegram sendMessage {StatusCode}: {Body}", response.StatusCode, body);
            return false;
        }

        return true;
    }

    public async Task<bool> DeleteMessageAsync(string chatId, long messageId, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return false;

        var url = $"https://api.telegram.org/bot{Token}/deleteMessage";
        var response = await _http.PostAsJsonAsync(url, new
        {
            chat_id = chatId,
            message_id = messageId
        }, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return;

        var url = $"https://api.telegram.org/bot{Token}/answerCallbackQuery";
        await _http.PostAsJsonAsync(url, new
        {
            callback_query_id = callbackQueryId,
            text,
            show_alert = text is { Length: > 0 }
        }, ct);
    }

    public async Task DeleteWebhookAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return;

        var url = $"https://api.telegram.org/bot{Token}/deleteWebhook";
        await _http.PostAsJsonAsync(url, new { drop_pending_updates = true }, ct);
    }

    public async Task SetupBotMenuAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return;

        var url = $"https://api.telegram.org/bot{Token}/setMyCommands";
        await _http.PostAsJsonAsync(url, new
        {
            commands = new[]
            {
                new { command = "incidents", description = "Список активных инцидентов" },
                new { command = "me", description = "Мой профиль и смена" },
                new { command = "help", description = "Справка по боту" }
            }
        }, ct);
    }

    public async Task<TelegramUpdate[]?> GetUpdatesAsync(long offset, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return null;

        try
        {
            var url = $"https://api.telegram.org/bot{Token}/getUpdates?timeout=25&offset={offset}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<TelegramGetUpdatesResponse>(JsonOptions, ct);
            return payload?.Ok == true ? payload.Result : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            // Long-poll прерван (shutdown или timeout) — не валим background service.
            return null;
        }
    }

    public static object MainMenuKeyboard() =>
        new
        {
            keyboard = new[]
            {
                new[] { new { text = MenuIncidents } },
                new[] { new { text = MenuProfile }, new { text = MenuHelp } }
            },
            resize_keyboard = true,
            is_persistent = true
        };

    /// <summary>Кнопки выбора инцидента из списка.</summary>
    public static object IncidentPickerKeyboard(IReadOnlyList<Incident> items)
    {
        var rows = new List<object[]>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var id = item.Id.ToString("N");
            var label = TelegramUiFormatter.FormatPickerButtonLabel(item, i + 1);
            rows.Add([new { text = label, callback_data = $"view:{id}" }]);
        }

        rows.Add([new { text = "🔄 Обновить", callback_data = "list" }]);
        return new { inline_keyboard = rows.ToArray() };
    }

    /// <summary>Кнопки на экране деталей инцидента.</summary>
    public static object IncidentDetailKeyboard(Guid incidentId, IncidentStatus status)
    {
        var id = incidentId.ToString("N");
        var actionRow = new List<object> { new { text = "◀️ Назад", callback_data = "list" } };

        if (status == IncidentStatus.Open)
            actionRow.Add(new { text = "✅ Принять", callback_data = $"ack:{id}" });

        if (status != IncidentStatus.Resolved)
            actionRow.Add(new { text = "🟢 Закрыть", callback_data = $"resolve:{id}" });

        return new { inline_keyboard = new[] { actionRow.ToArray() } };
    }

    /// <summary>Кнопки на push-уведомлении — сразу открыть карточку.</summary>
    public static object IncidentNotificationKeyboard(Guid incidentId) =>
        new
        {
            inline_keyboard = new[]
            {
                new[] { new { text = "📋 Открыть инцидент", callback_data = $"view:{incidentId:N}" } }
            }
        };

    private sealed class TelegramGetUpdatesResponse
    {
        public bool Ok { get; set; }
        public TelegramUpdate[]? Result { get; set; }
    }
}

public sealed class TelegramUpdate
{
    public long UpdateId { get; set; }
    public TelegramMessage? Message { get; set; }
    public TelegramCallbackQuery? CallbackQuery { get; set; }
}

public sealed class TelegramMessage
{
    public long MessageId { get; set; }
    public TelegramChat? Chat { get; set; }
    public string? Text { get; set; }
    public long Date { get; set; }
}

public sealed class TelegramChat
{
    public long Id { get; set; }
}

public sealed class TelegramCallbackQuery
{
    public string Id { get; set; } = string.Empty;
    public TelegramUser? From { get; set; }
    public TelegramMessage? Message { get; set; }
    public string? Data { get; set; }
}

public sealed class TelegramUser
{
    public long Id { get; set; }
}
