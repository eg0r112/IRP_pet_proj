using irp_pet.Models;

namespace irp_pet.DTOs;

public class IncidentListV2Response
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<IncidentListItemDto> Items { get; set; } = [];
    public string ApiVersion { get; set; } = "2.0";
}

public class ReceiveAlertV2Response : ReceiveAlertResponse
{
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public string ApiVersion { get; set; } = "2.0";
}
