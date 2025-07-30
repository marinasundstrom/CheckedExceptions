namespace Sundstrom.CheckedExceptions;

public static class HeuristicRules
{
    public static bool IsForGetter(string name) => name is not null &&
            (name.Contains(" get ") ||
            name.Contains(" gets ") ||
            name.Contains(" getting ") ||
            name.Contains(" retrieved "));

    public static bool IsForSetter(string name) => name is not null && (
            name.Contains(" set ") ||
            name.Contains(" sets ") ||
            name.Contains(" setting "));
}