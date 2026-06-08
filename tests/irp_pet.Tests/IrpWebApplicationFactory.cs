using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace irp_pet.Tests;

public class IrpWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("RabbitMq:Enabled", "false");
        // RabbitMQ выкл — OutboxDispatcher обрабатывает события локально (fallback)
        builder.UseSetting("ConnectionStrings:Redis", "");
        builder.UseSetting("OpenTelemetry:OtlpEndpoint", "");
        builder.UseSetting("Telegram:Enabled", "false");
        builder.UseSetting("Telegram:BotPollingEnabled", "false");
        // Отдельная БД для тестов — не засоряем dev pet_proj_irp
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Host=localhost;Port=5432;Database=pet_proj_irp_test;Username=postgres;Password=postgres");
    }
}
