using System.Text.Json;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{

    private const string SettingsFileName = "CheckedExceptions.settings.json";

    private static AnalyzerSettings GetAnalyzerSettings(AnalyzerOptions analyzerOptions)
    {
        if (!configs.TryGetValue(analyzerOptions, out var config))
        {
            foreach (var additionalFile in analyzerOptions.AdditionalFiles)
            {
                if (Path.GetFileName(additionalFile.Path).Equals(SettingsFileName, StringComparison.OrdinalIgnoreCase))
                {
                    var text = additionalFile.GetText();
                    if (text is not null)
                    {
                        var json = text.ToString();
                        config = JsonSerializer.Deserialize<AnalyzerSettings>(json);
                        break;
                    }
                }
            }

            config ??= new AnalyzerSettings(); // Return default options if config file is not found

            configs.TryAdd(analyzerOptions, config);
        }

        return config ?? new AnalyzerSettings();
    }
}