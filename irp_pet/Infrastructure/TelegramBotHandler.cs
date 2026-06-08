using Microsoft.EntityFrameworkCore;
using irp_pet.Data;
using irp_pet.Models;
using irp_pet.Services;

namespace irp_pet.Infrastructure;

public sealed class TelegramBotHandler
{
    private readonly TelegramApiClient _api;
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramBotHandler(TelegramApiClient api, IServiceScopeFactory scopeFactory) =>
        (_api, _scopeFactory) = (api, scopeFactory);

    public async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken ct)
    {
        if (update.CallbackQuery is not null)
        {
            await HandleCallbackAsync(update.CallbackQuery, ct);
            return;
        }

        var chatId = update.Message?.Chat?.Id;
        var text = update.Message?.Text?.Trim();
        if (chatId is null || string.IsNullOrWhiteSpace(text))
            return;

        var chatIdStr = chatId.Value.ToString();

        if (text.StartsWith('/'))
        {
            var command = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
                .Split('@')[0].ToLowerInvariant();
            await HandleCommandAsync(command, chatIdStr, ct);
            return;
        }

        switch (text)
        {
            case TelegramApiClient.MenuIncidents:
                await SendIncidentListAsync(chatIdStr, ct);
                break;
            case TelegramApiClient.MenuProfile:
                await SendMeAsync(chatIdStr, ct);
                break;
            case TelegramApiClient.MenuHelp:
                await SendHelpAsync(chatIdStr, ct);
                break;
        }
    }

    private Task HandleCommandAsync(string command, string chatId, CancellationToken ct) =>
        command switch
        {
            "/start" => SendWelcomeAsync(chatId, ct),
            "/help" => SendHelpAsync(chatId, ct),
            "/incidents" or "/list" => SendIncidentListAsync(chatId, ct),
            "/me" => SendMeAsync(chatId, ct),
            _ => _api.SendMessageAsync(chatId,
                "Неизвестная команда. Используйте кнопку <b>📋 Инциденты</b> или /help",
                TelegramApiClient.MainMenuKeyboard(), ct)
        };

    private async Task HandleCallbackAsync(TelegramCallbackQuery query, CancellationToken ct)
    {
        var chatId = query.Message?.Chat?.Id.ToString();
        var messageId = query.Message?.MessageId ?? 0;
        if (chatId is null || string.IsNullOrWhiteSpace(query.Data))
            return;

        if (query.Message?.Date is long unix && unix > 0)
        {
            var messageAge = DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            if (messageAge > TimeSpan.FromHours(2))
            {
                await _api.AnswerCallbackQueryAsync(query.Id,
                    "Сообщение устарело. Нажмите 📋 Инциденты внизу экрана.", ct);
                return;
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var incidents = scope.ServiceProvider.GetRequiredService<IncidentService>();

        var user = await FindAuthorizedUserAsync(db, chatId, ct);
        if (user is null)
        {
            await _api.AnswerCallbackQueryAsync(query.Id, "Нет доступа. Chat ID не привязан к дежурному.", ct);
            return;
        }

        if (query.Data == "list")
        {
            await _api.AnswerCallbackQueryAsync(query.Id, null, ct);
            if (messageId > 0)
                await _api.DeleteMessageAsync(chatId, messageId, ct);
            await SendIncidentListAsync(chatId, ct);
            return;
        }

        var parts = query.Data.Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var incidentId))
        {
            await _api.AnswerCallbackQueryAsync(query.Id, "Некорректная команда", ct);
            return;
        }

        if (parts[0] == "view")
        {
            await _api.AnswerCallbackQueryAsync(query.Id, null, ct);
            if (messageId > 0)
                await _api.DeleteMessageAsync(chatId, messageId, ct);
            await SendIncidentDetailAsync(chatId, incidentId, ct);
            return;
        }

        var incident = await db.Incidents
            .Include(i => i.Service)
            .FirstOrDefaultAsync(i => i.Id == incidentId, ct);

        if (incident is null)
        {
            await _api.AnswerCallbackQueryAsync(query.Id, "Инцидент не найден", ct);
            return;
        }

        if (parts[0] == "ack")
        {
            var result = await incidents.AcknowledgeAsync(incidentId, user.Id, incident.RowVersion, ct);
            var toast = result.Status switch
            {
                DTOs.IncidentActionStatus.Success => "✅ Принят в работу",
                DTOs.IncidentActionStatus.InvalidStatus => "Уже не открыт",
                DTOs.IncidentActionStatus.ConcurrencyConflict => "Обновите карточку",
                _ => "Не удалось принять"
            };
            await _api.AnswerCallbackQueryAsync(query.Id, toast, ct);

            if (result.Status == DTOs.IncidentActionStatus.Success && messageId > 0)
            {
                await _api.DeleteMessageAsync(chatId, messageId, ct);
                await SendIncidentDetailAsync(chatId, incidentId, ct);
            }
            return;
        }

        if (parts[0] == "resolve")
        {
            incident = await db.Incidents.Include(i => i.Service).FirstOrDefaultAsync(i => i.Id == incidentId, ct);
            if (incident is null)
            {
                await _api.AnswerCallbackQueryAsync(query.Id, "Инцидент не найден", ct);
                return;
            }

            var result = await incidents.ResolveAsync(incidentId, user.Id, incident.RowVersion, ct);
            var toast = result.Status switch
            {
                DTOs.IncidentActionStatus.Success => "🟢 Инцидент закрыт",
                DTOs.IncidentActionStatus.InvalidStatus => "Уже закрыт",
                DTOs.IncidentActionStatus.ConcurrencyConflict => "Обновите карточку",
                _ => "Не удалось закрыть"
            };
            await _api.AnswerCallbackQueryAsync(query.Id, toast, ct);

            if (result.Status == DTOs.IncidentActionStatus.Success && messageId > 0)
            {
                await _api.DeleteMessageAsync(chatId, messageId, ct);
                await SendIncidentListAsync(chatId, ct);
            }
        }
    }

    private async Task SendWelcomeAsync(string chatId, CancellationToken ct)
    {
        const string text = """
            <b>👋 IRP — панель дежурного</b>

            1. Нажмите <b>📋 Инциденты</b>
            2. Выберите инцидент из списка
            3. Примите в работу или закройте

            Статус и срочность видны на каждом шаге.
            """;
        await _api.SendMessageAsync(chatId, text, TelegramApiClient.MainMenuKeyboard(), ct);
    }

    private async Task SendHelpAsync(string chatId, CancellationToken ct)
    {
        const string text = """
            <b>📖 Справка IRP</b>

            <b>Как работать с инцидентами:</b>
            1. <b>📋 Инциденты</b> — список
            2. Нажмите на инцидент — откроется карточка
            3. <b>✅ Принять</b> / <b>🟢 Закрыть</b> или <b>◀️ Назад</b>

            <b>Кнопки внизу:</b> Инциденты · Профиль · Справка
            <b>Команды:</b> /incidents /me /help
            """;
        await _api.SendMessageAsync(chatId, text, TelegramApiClient.MainMenuKeyboard(), ct);
    }

    private async Task SendMeAsync(string chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var onCall = scope.ServiceProvider.GetRequiredService<OnCallService>();

        var user = await FindAuthorizedUserAsync(db, chatId, ct);
        if (user is null)
        {
            await _api.SendMessageAsync(chatId,
                "❌ Chat ID не привязан. Попросите админа указать ваш Telegram chat id в профиле.",
                TelegramApiClient.MainMenuKeyboard(), ct);
            return;
        }

        var shifts = await onCall.GetCurrentAllAsync(ct);
        var myShift = shifts.FirstOrDefault(s => s.UserId == user.Id);
        var shiftLine = myShift is not null
            ? $"✅ Вы на смене до {myShift.ShiftEndsAtUtc:dd.MM.yyyy HH:mm} UTC"
            : shifts.Count > 0
                ? $"Сейчас дежурят: {string.Join(", ", shifts.Select(s => s.DisplayName))}"
                : "⚠️ Активных смен нет";

        await _api.SendMessageAsync(chatId,
            $"""
            <b>👤 {TelegramUiFormatter.Escape(user.DisplayName)}</b>
            Email: {user.Email}
            Роль: <b>{user.Role}</b>
            {shiftLine}
            """,
            TelegramApiClient.MainMenuKeyboard(), ct);
    }

    private async Task<List<Incident>> LoadActiveIncidentsAsync(AppDbContext db, CancellationToken ct) =>
        await db.Incidents
            .Include(i => i.Service)
            .Where(i => i.Status == IncidentStatus.Open || i.Status == IncidentStatus.Acknowledged)
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.OpenedAtUtc)
            .Take(5)
            .ToListAsync(ct);

    private async Task SendIncidentListAsync(string chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await FindAuthorizedUserAsync(db, chatId, ct) is null)
        {
            await _api.SendMessageAsync(chatId,
                "❌ Chat ID не привязан. Попросите админа указать ваш Telegram chat id.",
                TelegramApiClient.MainMenuKeyboard(), ct);
            return;
        }

        var items = await LoadActiveIncidentsAsync(db, ct);

        if (items.Count == 0)
        {
            await _api.SendMessageAsync(chatId,
                "✅ Нет активных инцидентов — всё спокойно.",
                TelegramApiClient.MainMenuKeyboard(), ct);
            return;
        }

        await _api.SendMessageAsync(chatId,
            TelegramUiFormatter.FormatIncidentListMessage(items),
            TelegramApiClient.IncidentPickerKeyboard(items), ct);
    }

    private async Task SendIncidentDetailAsync(string chatId, Guid incidentId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var incident = await db.Incidents
            .Include(i => i.Service)
            .FirstOrDefaultAsync(i => i.Id == incidentId, ct);

        if (incident is null || incident.Status == IncidentStatus.Resolved)
        {
            await _api.SendMessageAsync(chatId,
                "Инцидент не найден или уже закрыт.",
                TelegramApiClient.MainMenuKeyboard(), ct);
            await SendIncidentListAsync(chatId, ct);
            return;
        }

        await _api.SendMessageAsync(chatId,
            TelegramUiFormatter.FormatIncidentDetail(incident),
            TelegramApiClient.IncidentDetailKeyboard(incident.Id, incident.Status), ct);
    }

    private static async Task<User?> FindAuthorizedUserAsync(AppDbContext db, string chatId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u =>
            u.IsActive
            && u.TelegramChatId == chatId
            && (u.Role == Roles.Admin || u.Role == Roles.OnCall), ct);
}
