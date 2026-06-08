using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using FluentAssertions;
using irp_pet.DTOs;

namespace irp_pet.Tests;

public class IrpApiTests : IClassFixture<IrpWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private readonly Faker _faker = new();

    public IrpApiTests(IrpWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "x@x.com", password = "wrong12" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "vedromakaron@gmail.com", password = "123456" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.Role.Should().Be("admin");
    }

    [Fact]
    public async Task AlertFlow_CreatesIncident_Ack_Resolve()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "vedromakaron@gmail.com", password = "123456" });
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var fingerprint = $"test-{_faker.Random.AlphaNumeric(8)}";
        var alertRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alerts")
        {
            Content = JsonContent.Create(new
            {
                serviceKey = "orders-api",
                fingerprint,
                severity = "High",
                message = "Сбой интеграционного теста: сервис недоступен",
                source = "xunit"
            })
        };
        alertRequest.Headers.Add("X-Api-Key", "dev-api-key-change-me");
        var alertResponse = await _client.SendAsync(alertRequest);
        alertResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        var alertBody = await alertResponse.Content.ReadFromJsonAsync<ReceiveAlertResponse>(JsonOptions);
        alertBody!.IsNewIncident.Should().BeTrue();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var listResponse = await _client.GetAsync("/api/v1/incidents");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailResponse = await _client.GetAsync($"/api/v1/incidents/{alertBody.IncidentId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailJson = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var rowVersion = detailJson.GetProperty("rowVersion").GetInt64();

        var ackResponse = await _client.PostAsJsonAsync($"/api/v1/incidents/{alertBody.IncidentId}/ack",
            new { rowVersion });
        ackResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailAfterAckJson = await (await _client.GetAsync($"/api/v1/incidents/{alertBody.IncidentId}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var resolveResponse = await _client.PostAsJsonAsync($"/api/v1/incidents/{alertBody.IncidentId}/resolve",
            new { rowVersion = detailAfterAckJson.GetProperty("rowVersion").GetInt64() });
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Alert_WithSameIdempotencyKey_ReturnsSameAlert()
    {
        var idemKey = $"idem-{_faker.Random.Guid()}";
        var fingerprint = $"idem-fp-{_faker.Random.AlphaNumeric(6)}";
        async Task<HttpResponseMessage> SendAlert() {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alerts")
            {
                Content = JsonContent.Create(new
                {
                    serviceKey = "orders-api",
                    fingerprint,
                    severity = "Medium",
                    message = "Повторный алерт: проверка идемпотентности",
                    source = "xunit",
                    idempotencyKey = idemKey
                })
            };
            req.Headers.Add("X-Api-Key", "dev-api-key-change-me");
            return await _client.SendAsync(req);
        }

        var first = await SendAlert();
        var second = await SendAlert();
        first.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var a = await first.Content.ReadFromJsonAsync<ReceiveAlertResponse>(JsonOptions);
        var b = await second.Content.ReadFromJsonAsync<ReceiveAlertResponse>(JsonOptions);
        a!.AlertId.Should().Be(b!.AlertId);
        a.IncidentId.Should().Be(b.IncidentId);
    }

    [Fact]
    public async Task Ack_WithStaleRowVersion_Returns409()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "vedromakaron@gmail.com", password = "123456" });
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var fingerprint = $"409-{_faker.Random.AlphaNumeric(8)}";
        var alertRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/alerts")
        {
            Content = JsonContent.Create(new
            {
                serviceKey = "orders-api",
                fingerprint,
                severity = "High",
                message = "Конфликт версий при одновременном изменении",
                source = "xunit"
            })
        };
        alertRequest.Headers.Add("X-Api-Key", "dev-api-key-change-me");
        var alertResponse = await _client.SendAsync(alertRequest);
        alertResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        var alertBody = await alertResponse.Content.ReadFromJsonAsync<ReceiveAlertResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var detailJson = await (await _client.GetAsync($"/api/v1/incidents/{alertBody!.IncidentId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var rowVersion = detailJson.GetProperty("rowVersion").GetInt64();

        var results = await Task.WhenAll(
            _client.PostAsJsonAsync($"/api/v1/incidents/{alertBody.IncidentId}/ack", new { rowVersion }),
            _client.PostAsJsonAsync($"/api/v1/incidents/{alertBody.IncidentId}/ack", new { rowVersion }));

        var statuses = results.Select(r => r.StatusCode).ToArray();
        statuses.Should().Contain(HttpStatusCode.OK);
        statuses.Should().Contain(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rbac_OnCallCannotAccessAdminEndpoint_Returns403()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "oncall@irp.local", password = "123456" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!.AccessToken;

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/ping");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
