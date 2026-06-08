using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using irp_pet.DTOs;

namespace irp_pet.Tests;

public class ApiV2Tests : IClassFixture<IrpWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public ApiV2Tests(IrpWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task IncidentsV2_ReturnsPagedList()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "vedromakaron@gmail.com", password = "123456" });
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await _client.GetAsync("/api/v2/incidents?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IncidentListV2Response>(JsonOptions);
        body!.ApiVersion.Should().Be("2.0");
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(10);
        body.Items.Should().NotBeNull();
    }
}
