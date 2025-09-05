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

    [JsonPropertyName("disableLinqEnumerableBoundaryWarnings")]
    public bool DisableLinqEnumerableBoundaryWarnings { get; set; } = false;

    [JsonIgnore]
    internal bool IsLinqEnumerableBoundaryWarningsEnabled => !DisableLinqEnumerableBoundaryWarnings;

    [JsonPropertyName("disableLinqQueryableSupport")]
    public bool DisableLinqQueryableSupport { get; set; } = false;

    [JsonIgnore]
    internal bool IsLinqQueryableSupportEnabled => !DisableLinqQueryableSupport;

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

    [JsonPropertyName("treatThrowsExceptionAsCatchRest")]
    public bool TreatThrowsExceptionAsCatchRest { get; set; } = false;

    [JsonIgnore]
    internal bool TreatThrowsExceptionAsCatchRestEnabled => TreatThrowsExceptionAsCatchRest;

    [JsonPropertyName("defaultExceptionClassification")]
    public ExceptionClassification DefaultExceptionClassification { get; set; } = ExceptionClassification.NonStrict;

    [JsonPropertyName("exceptions")]
    public IDictionary<string, ExceptionClassification> Exceptions { get; set; } = new Dictionary<string, ExceptionClassification>();

    [JsonPropertyName("ignoredExceptions")]
    [Obsolete("Use 'exceptions' instead.")]
    public IList<string>? IgnoredExceptions
    {
        get => null;
        set
        {
            if (value is null)
            {
                return;
            }

            foreach (var exception in value)
            {
                Exceptions[exception] = ExceptionClassification.Ignored;
            }
        }
    }

    [JsonPropertyName("informationalExceptions")]
    [Obsolete("Use 'exceptions' instead.")]
    public IDictionary<string, string>? InformationalExceptions
    {
        get => null;
        set
        {
            if (value is null)
            {
                return;
            }

            foreach (var exception in value.Keys)
            {
                Exceptions[exception] = ExceptionClassification.Informational;
            }
        }
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExceptionClassification
{
    Ignored,
    Informational,
    NonStrict,
    Strict
}