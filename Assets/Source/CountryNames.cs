using System.Collections.Generic;

public static class CountryNames
{
    private static readonly Dictionary<string, string> FullNames = new Dictionary<string, string>
    {
        { "BMU", "Bermuda" },
        { "CAN", "Canada" },
        { "CUW", "Curacao" },
        { "CYM", "Cayman Islands" },
        { "GBR", "United Kingdom" },
        { "IRL", "Ireland" },
        { "ISR", "Israel" },
        { "LBR", "Liberia" },
        { "MHL", "Marshall Islands" },
        { "NLD", "Netherlands" },
        { "USA", "United States of America" },
        { "VGB", "British Virgin Islands" }
    };

    public static string GetFullName(string code)
    {
        return FullNames.TryGetValue(code, out string name) ? name : code;
    }
}