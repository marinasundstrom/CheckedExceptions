namespace Sundstrom.CheckedExceptions;

using System.Text.Json.Serialization;

public partial class AnalyzerConfig
{
    [JsonPropertyName("ignoredExceptions")]
    public List<string> IgnoredExceptions { get; set; } = new List<string>();

    [JsonPropertyName("informationalExceptions")]
    public List<string> InformationalExceptions { get; set; } = new List<string>();
}