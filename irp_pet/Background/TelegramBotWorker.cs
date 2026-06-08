using irp_pet.Infrastructure;

namespace irp_pet.Background;

public sealed class TelegramBotWorker : BackgroundService
{
    private readonly TelegramApiClient _api;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotWorker> _logger;

    public TelegramBotWorker(
        TelegramApiClient api,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotWorker> logger)
    {
        _api = api;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_api.PollingEnabled)
        {
            _logger.LogInformation("Telegram bot polling disabled");
            return;
        }

        try
        {
            // drop_pending_updates=true — сбрасывает очередь без long-poll getUpdates
            await _api.DeleteWebhookAsync(stoppingToken);
            await _api.SetupBotMenuAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram bot setup failed, polling will retry");
        }

        var offset = 0L;
        _logger.LogInformation("Telegram bot polling started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _api.GetUpdatesAsync(offset, stoppingToken);
                if (updates is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                foreach (var update in updates)
                {
                    offset = update.UpdateId + 1;
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var handler = scope.ServiceProvider.GetRequiredService<TelegramBotHandler>();
                        await handler.HandleUpdateAsync(update, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Telegram update {UpdateId} failed", update.UpdateId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram polling error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
