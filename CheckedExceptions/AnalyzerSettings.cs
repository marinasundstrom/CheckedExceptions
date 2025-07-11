namespace Sundstrom.CheckedExceptions;

using System.Text.Json.Serialization;

public class AnalyzerSettings(IReadOnlyList<string> ignoredExceptions, IReadOnlyDictionary<string, ExceptionMode> informationalExceptions)
{
    public IReadOnlyList<string> IgnoredExceptions { get; } = ignoredExceptions;

    public IReadOnlyDictionary<string, ExceptionMode> InformationalExceptions { get; } = informationalExceptions;

    public static AnalyzerSettings CreateWithDefaults() => new(new List<string>(), new Dictionary<string, ExceptionMode>());
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExceptionMode
{
    Throw = 1,
    Propagation = 2,
    Always = Throw | Propagation
}