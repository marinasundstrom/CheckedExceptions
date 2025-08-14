namespace Sundstrom.CheckedExceptions;

using System.Text.Json.Serialization;

public partial class AnalyzerSettings
{
    [JsonPropertyName("disableXmlDocInterop")]
    public bool DisableXmlDocInterop { get; set; } = false;

    [JsonIgnore]
    internal bool IsXmlInteropEnabled => !DisableXmlDocInterop;

    [JsonPropertyName("disableLinqSupport")]
    public bool DisableLinqSupport { get; set; } = false;

    [JsonIgnore]
    internal bool IsLinqSupportEnabled => !DisableLinqSupport;

    [JsonPropertyName("disableLinqImplicitlyDeclaredExceptions")]
    public bool DisableLinqImplicitlyDeclaredExceptions { get; set; } = false;

    [JsonIgnore]
    internal bool IsLinqImplicitlyDeclaredExceptionsEnabled => !DisableLinqImplicitlyDeclaredExceptions;

    [JsonPropertyName("disableControlFlowAnalysis")]
    public bool DisableControlFlowAnalysis { get; set; } = false;

    [JsonIgnore]
    internal bool IsControlFlowAnalysisEnabled => !DisableControlFlowAnalysis;

    [JsonPropertyName("enableLegacyRedundancyChecks")]
    public bool EnableLegacyRedundancyChecks { get; set; } = false;

    [JsonIgnore]
    internal bool IsLegacyRedundancyChecksEnabled => EnableLegacyRedundancyChecks;

    [JsonPropertyName("disableBaseExceptionDeclaredDiagnostic")]
    public bool DisableBaseExceptionDeclaredDiagnostic { get; set; } = false;

    [JsonIgnore]
    internal bool BaseExceptionDeclaredDiagnosticEnabled => !DisableBaseExceptionDeclaredDiagnostic;

    [JsonPropertyName("disableBaseExceptionThrownDiagnostic")]
    public bool DisableBaseExceptionThrownDiagnostic { get; set; } = false;

    [JsonIgnore]
    internal bool BaseExceptionThrownDiagnosticEnabled => !DisableBaseExceptionThrownDiagnostic;

    [JsonPropertyName("ignoredExceptions")]
    public IEnumerable<string> IgnoredExceptions { get; set; } = new List<string>();

    [JsonPropertyName("informationalExceptions")]
    public IDictionary<string, ExceptionMode> InformationalExceptions { get; set; } = new Dictionary<string, ExceptionMode>();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExceptionMode
{
    Throw = 1,
    Propagation = 2,
    Always = Throw | Propagation
}