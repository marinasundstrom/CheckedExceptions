using System.Text.RegularExpressions;

namespace Sundstrom.CheckedExceptions;

public static class HeuristicRules
{
    private static readonly Regex GetterRegex = new(@"\b(gets?|retriev(es|ing|ed)|returns?|provides?)\b",
     RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SetterRegex = new(@"\b(sets?|assigns?|specif(y|ies)|updat(es|ing)|allow(s|ed) setting)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsForGetter(string text) =>
        !string.IsNullOrWhiteSpace(text) && GetterRegex.IsMatch(text);

    public static bool IsForSetter(string text) =>
        !string.IsNullOrWhiteSpace(text) && SetterRegex.IsMatch(text);
    public static bool IsForBoth(string text) =>
        IsForGetter(text) && IsForSetter(text);
}