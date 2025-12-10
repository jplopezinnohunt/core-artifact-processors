using Newtonsoft.Json;

namespace VendorMdm.Shared.Models;

public class ChangeRequestData
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString(); // Matches SQL ChangeRequest.Id

    [JsonProperty("requestId")]
    public string RequestId { get; set; } // Partition Key

    [JsonProperty("payload")]
    public object Payload { get; set; } = new object(); // Flexible JSON

    [JsonProperty("oldValue")]
    public object? OldValue { get; set; } // Snapshot of SAP data before change

    [JsonProperty("newValue")]
    public object? NewValue { get; set; } // The proposed change
}

public class DomainEvent
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("eventType")]
    public string EventType { get; set; } = string.Empty; // Partition Key

    [JsonProperty("entityId")]
    public string EntityId { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("data")]
    public object Data { get; set; } = new object();
}
