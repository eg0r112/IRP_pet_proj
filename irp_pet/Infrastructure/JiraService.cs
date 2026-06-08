using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace irp_pet.Infrastructure;

public sealed class JiraService : IJiraService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<JiraService> _logger;

    public JiraService(HttpClient http, IConfiguration config, ILogger<JiraService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<JiraDeliveryResult> CreateIncidentIssueAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var enabled = _config.GetValue("Jira:Enabled", false);
        var baseUrl = _config["Jira:BaseUrl"]?.TrimEnd('/');
        var email = _config["Jira:Email"];
        var token = _config["Jira:ApiToken"];
        var projectKey = _config["Jira:ProjectKey"];
        var issueType = _config["Jira:IssueType"] ?? "Task";
        var issueTypeId = _config["Jira:IssueTypeId"];

        if (!enabled)
            return new JiraDeliveryResult(JiraDeliveryStatus.Skipped, Error: "Jira disabled");

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(projectKey))
            return new JiraDeliveryResult(JiraDeliveryStatus.Skipped, Error: "Jira not configured");

        var summary = Truncate($"[{message.Severity}] {message.ServiceKey}: {message.Title}", 250);
        var bodyText = BuildDescription(message);

        object issueTypeField = !string.IsNullOrWhiteSpace(issueTypeId)
            ? new { id = issueTypeId }
            : new { name = issueType };

        var payload = new
        {
            fields = new
            {
                project = new { key = projectKey },
                summary,
                issuetype = issueTypeField,
                description = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new[] { new { type = "text", text = bodyText } }
                        }
                    }
                }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/api/3/issue");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}")));
            request.Content = JsonContent.Create(payload);

            var response = await _http.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Jira API {StatusCode} для {IncidentId}: {Body}",
                    response.StatusCode, message.IncidentId, responseBody);
                return new JiraDeliveryResult(JiraDeliveryStatus.Failed, Error: $"HTTP {(int)response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var issueKey = doc.RootElement.GetProperty("key").GetString();

            _logger.LogInformation("Jira задача {IssueKey} создана для инцидента {IncidentId}", issueKey, message.IncidentId);
            return new JiraDeliveryResult(JiraDeliveryStatus.Sent, IssueKey: issueKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка Jira для инцидента {IncidentId}", message.IncidentId);
            return new JiraDeliveryResult(JiraDeliveryStatus.Failed, Error: ex.Message);
        }
    }

    private static string BuildDescription(NotificationMessage msg)
    {
        var lines = new List<string>
        {
            "Инцидент IRP",
            $"Сервис: {msg.ServiceKey}",
            $"Срочность: {msg.Severity}",
            $"Статус: {msg.Status}",
            $"Описание: {msg.Title}",
            $"Fingerprint: {msg.Fingerprint ?? "—"}",
            $"ID инцидента: {msg.IncidentId}",
            $"Дежурный: {msg.OnCallDisplayName ?? "—"}",
            $"Открыт: {msg.OpenedAtUtc:dd.MM.yyyy HH:mm} UTC"
        };

        if (!string.IsNullOrWhiteSpace(msg.Details))
            lines.Add($"Примечание: {msg.Details}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 3)] + "...";
}
