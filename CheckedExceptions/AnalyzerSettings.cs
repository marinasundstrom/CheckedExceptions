namespace Sundstrom.CheckedExceptions;

using System.Text.Json.Serialization;

public record AnalyzerSettings
{
    public IReadOnlyList<string> IgnoredExceptions { get; }

    public IReadOnlyDictionary<string, ExceptionMode> InformationalExceptions { get; }

    public AnalyzerSettings(IReadOnlyList<string> ignoredExceptions, IReadOnlyDictionary<string, ExceptionMode> informationalExceptions)
    {
        IgnoredExceptions = ignoredExceptions;
        InformationalExceptions = informationalExceptions;
    }

    public static AnalyzerSettings CreateWithDefaults() => new(new List<string>(), new Dictionary<string, ExceptionMode>());
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExceptionMode
{
    Throw = 1,
    Propagation = 2,
    Always = Throw | Propagation
}