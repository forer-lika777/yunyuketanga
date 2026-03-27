namespace yunyuketanga.Models;

using System.Text.Json.Serialization;

public class AjaxRequest
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("methodname")]
    public string MethodName { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public RequestArgs Args { get; set; } = new();
}

public class RequestArgs
{
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("classification")]
    public string Classification { get; set; } = "all";

    [JsonPropertyName("sort")]
    public string Sort { get; set; } = "fullname";

    [JsonPropertyName("customfieldname")]
    public string CustomFieldName { get; set; } = string.Empty;

    [JsonPropertyName("customfieldvalue")]
    public string CustomFieldValue { get; set; } = string.Empty;

    [JsonPropertyName("requiredfields")]
    public List<string> RequiredFields { get; set; } = new();
}
