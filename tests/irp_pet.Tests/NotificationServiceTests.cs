using FluentAssertions;
using irp_pet.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace irp_pet.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task Send_WhenTelegramDisabled_ReturnsSkipped()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:Enabled"] = "false"
            })
            .Build();

        var api = new TelegramApiClient(
            new HttpClient(),
            config,
            NullLogger<TelegramApiClient>.Instance);
        var service = new TelegramNotificationService(
            api,
            config,
            NullLogger<TelegramNotificationService>.Instance);

        var result = await service.SendAsync(new NotificationMessage(
            "IncidentCreated",
            Guid.NewGuid(),
            "123",
            "orders-api",
            "Test",
            "High",
            "Open",
            "fp-1",
            "Egor",
            null,
            DateTime.UtcNow));

        result.Status.Should().Be(NotificationDeliveryStatus.Skipped);
    }

    [Fact]
    public void Format_IncludesRussianSeverityAndService()
    {
        var text = TelegramMessageFormatter.Format(new NotificationMessage(
            "IncidentCreated",
            Guid.Parse("8056d13c-c00f-4508-9442-f60b02505dfe"),
            "123",
            "orders-api",
            "Ошибка HTTP 500 на /health",
            "High",
            "Open",
            "orders-500",
            "Egor Makarov",
            null,
            DateTime.UtcNow,
            "Требуется ack."));

        text.Should().Contain("Новый инцидент");
        text.Should().Contain("orders-api");
        text.Should().Contain("Высокая");
        text.Should().Contain("Открыт");
        text.Should().Contain("Egor Makarov");
    }
}
