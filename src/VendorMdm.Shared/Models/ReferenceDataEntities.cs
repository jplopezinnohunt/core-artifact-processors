using Newtonsoft.Json;

namespace VendorMdm.Shared.Models;

public class ReferenceDataItem
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty; // e.g., "COUNTRY_US"

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty; // Partition Key: "Country", "Currency", "VendorType"

    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty; // "US", "USD"

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("sapCode")]
    public string? SapCode { get; set; } // Mapping to SAP, e.g. "US" -> "US"
    
    [JsonProperty("isActive")]
    public bool IsActive { get; set; } = true;
}

public class ValidationRule
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("entityType")]
    public string EntityType { get; set; } = string.Empty; // Partition Key: "VendorApplication"

    [JsonProperty("fieldName")]
    public string FieldName { get; set; } = string.Empty; // "TaxId"

    [JsonProperty("ruleType")]
    public string RuleType { get; set; } = "Regex"; // "Regex", "Required", "Range"

    [JsonProperty("ruleValue")]
    public string RuleValue { get; set; } = string.Empty; // The regex pattern or value

    [JsonProperty("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;
}
