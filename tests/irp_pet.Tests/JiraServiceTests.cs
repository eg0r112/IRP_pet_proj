using FluentAssertions;
using irp_pet.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace irp_pet.Tests;

public class JiraServiceTests
{
    [Fact]
    public async Task CreateIssue_WhenJiraDisabled_ReturnsSkipped()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jira:Enabled"] = "false"
            })
            .Build();

        var service = new JiraService(new HttpClient(), config, NullLogger<JiraService>.Instance);

        var result = await service.CreateIncidentIssueAsync(new NotificationMessage(
            "IncidentCreated",
            Guid.NewGuid(),
            null,
            "orders-api",
            "Test",
            "High",
            "Open",
            "fp-1",
            null,
            null,
            DateTime.UtcNow));

        result.Status.Should().Be(JiraDeliveryStatus.Skipped);
    }
}
