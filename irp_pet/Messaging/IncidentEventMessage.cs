namespace irp_pet.Messaging;

public record IncidentEventMessage(string EventType, string PayloadJson);
