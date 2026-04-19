using System.Collections.Generic;

namespace Jellyfin.Plugin.LanguageFlags.Services;

internal static class LanguageMapping
{
    private static readonly Dictionary<string, string> Iso639ToCountry = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = "DE", ["ger"] = "DE", ["deu"] = "DE",
        ["en"] = "GB", ["eng"] = "GB",
        ["fr"] = "FR", ["fre"] = "FR", ["fra"] = "FR",
        ["es"] = "ES", ["spa"] = "ES",
        ["it"] = "IT", ["ita"] = "IT",
        ["nl"] = "NL", ["dut"] = "NL", ["nld"] = "NL",
        ["pt"] = "PT", ["por"] = "PT",
        ["pl"] = "PL", ["pol"] = "PL",
        ["tr"] = "TR", ["tur"] = "TR",
        ["cs"] = "CZ", ["cze"] = "CZ", ["ces"] = "CZ",
        ["ja"] = "JP", ["jpn"] = "JP",
        ["ko"] = "KR", ["kor"] = "KR",
        ["ru"] = "RU", ["rus"] = "RU",
        ["uk"] = "UA", ["ukr"] = "UA",
        ["sv"] = "SE", ["swe"] = "SE",
        ["no"] = "NO", ["nor"] = "NO",
        ["da"] = "DK", ["dan"] = "DK",
        ["fi"] = "FI", ["fin"] = "FI",
        ["zh"] = "CN", ["zho"] = "CN", ["chi"] = "CN",
        ["ar"] = "SA", ["ara"] = "SA",
        ["hi"] = "IN", ["hin"] = "IN"
    };

    public static string ToCountryCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "UN";
        }

        language = language.Trim();
        return Iso639ToCountry.TryGetValue(language, out var country) ? country : language.Length >= 2 ? language[..2].ToUpperInvariant() : "UN";
    }
}
