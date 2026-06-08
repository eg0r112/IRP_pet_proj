using FluentAssertions;
using irp_pet.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace irp_pet.Tests;

public class TelegramNotificationPolicyTests
{
    [Fact]
    public void AckAndResolve_DisabledByDefault()
    {
        var policy = new TelegramNotificationPolicy(new ConfigurationBuilder().Build());

        policy.IsEnabledFor("IncidentCreated").Should().BeTrue();
        policy.IsEnabledFor("IncidentEscalated").Should().BeTrue();
        policy.IsEnabledFor("IncidentAcknowledged").Should().BeFalse();
        policy.IsEnabledFor("IncidentResolved").Should().BeFalse();
    }

    [Fact]
    public void CanDisableEscalatedViaConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telegram:NotifyOn:Escalated"] = "false"
            })
            .Build();

        var policy = new TelegramNotificationPolicy(config);
        policy.IsEnabledFor("IncidentEscalated").Should().BeFalse();
    }
}
