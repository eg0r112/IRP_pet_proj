using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace irp_pet.Tests;

public sealed class TestcontainersIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("irp_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbit.StartAsync());
        await Task.Delay(TimeSpan.FromSeconds(5));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("RabbitMq:Enabled", "false");
            builder.UseSetting("RabbitMq:Username", "guest");
            builder.UseSetting("RabbitMq:Password", "guest");
            builder.UseSetting("OpenTelemetry:OtlpEndpoint", "");
            builder.UseSetting("Telegram:Enabled", "false");
        });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
        await _postgres.DisposeAsync().AsTask();
        await _redis.DisposeAsync().AsTask();
        await _rabbit.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task FullPipeline_Alert_TriggersNotificationAttemptInTimeline()
    {
        var fingerprint = $"tc-{Guid.NewGuid():N}";
        var alertReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alerts")
        {
            Content = JsonContent.Create(new
            {
                serviceKey = "orders-api",
                fingerprint,
                severity = "High",
                message = "Ошибка HTTP 500 при проверке health",
                source = "testcontainers"
            })
        };
        alertReq.Headers.Add("X-Api-Key", "dev-api-key-change-me");

        var alertRes = await _client!.SendAsync(alertReq);
        alertRes.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        var alertBody = await alertRes.Content.ReadFromJsonAsync<JsonElement>();
        var incidentId = alertBody.GetProperty("incidentId").GetGuid();

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "vedromakaron@gmail.com", password = "123456" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        JsonElement? detail = null;
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            var detailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/incidents/{incidentId}");
            detailReq.Headers.Add("Authorization", $"Bearer {token}");
            var detailRes = await _client.SendAsync(detailReq);
            detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await detailRes.Content.ReadAsStringAsync();
            if (json.Contains("NotificationSent", StringComparison.OrdinalIgnoreCase))
            {
                detail = await detailRes.Content.ReadFromJsonAsync<JsonElement>();
                break;
            }
        }

        detail.Should().NotBeNull("outbox worker should process IncidentCreated within 30s");
        var timeline = detail!.Value.GetProperty("timeline");
        timeline.EnumerateArray().Any(t =>
            t.GetProperty("eventType").GetString()?.Contains("Notification", StringComparison.OrdinalIgnoreCase) == true)
            .Should().BeTrue();
    }

    [Fact]
    public async Task RabbitPipeline_Alert_OutboxPublishedAndConsumerWritesTimeline()
    {
        var rabbitHost = $"{_rabbit.Hostname}:{_rabbit.GetMappedPublicPort(5672)}";
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("RabbitMq:Enabled", "true");
            builder.UseSetting("RabbitMq:Host", rabbitHost);
            builder.UseSetting("RabbitMq:Username", "guest");
            builder.UseSetting("RabbitMq:Password", "guest");
            builder.UseSetting("OpenTelemetry:OtlpEndpoint", "");
            builder.UseSetting("Telegram:Enabled", "false");
            builder.UseSetting("Jira:Enabled", "false");
        });

        using var client = factory.CreateClient();

        var fingerprint = $"rabbit-{Guid.NewGuid():N}";
        var alertReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alerts")
        {
            Content = JsonContent.Create(new
            {
                serviceKey = "orders-api",
                fingerprint,
                severity = "High",
                message = "Сообщение не доставлено в очередь RabbitMQ",
                source = "testcontainers"
            })
        };
        alertReq.Headers.Add("X-Api-Key", "dev-api-key-change-me");

        var alertRes = await client.SendAsync(alertReq);
        alertRes.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        var alertBody = await alertRes.Content.ReadFromJsonAsync<JsonElement>();
        var incidentId = alertBody.GetProperty("incidentId").GetGuid();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "vedromakaron@gmail.com", password = "123456" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        JsonElement? detail = null;
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            var detailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/incidents/{incidentId}");
            detailReq.Headers.Add("Authorization", $"Bearer {token}");
            var detailRes = await client.SendAsync(detailReq);
            detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await detailRes.Content.ReadAsStringAsync();
            if (json.Contains("NotificationSent", StringComparison.OrdinalIgnoreCase))
            {
                detail = await detailRes.Content.ReadFromJsonAsync<JsonElement>();
                break;
            }
        }

        detail.Should().NotBeNull("RabbitMQ consumer should process IncidentCreated within 40s");
        var timeline = detail!.Value.GetProperty("timeline");
        timeline.EnumerateArray().Any(t =>
            t.GetProperty("eventType").GetString()?.Contains("Notification", StringComparison.OrdinalIgnoreCase) == true)
            .Should().BeTrue();
    }

    [Fact]
    public async Task OnCall_Current_ReturnsSeededShift()
    {
        var login = await _client!.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "vedromakaron@gmail.com", password = "123456" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/oncall/current");
        req.Headers.Add("Authorization", $"Bearer {token}");
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.EnumerateArray().Any(x => x.GetProperty("email").GetString() == "vedromakaron@gmail.com")
            .Should().BeTrue();
    }
}
