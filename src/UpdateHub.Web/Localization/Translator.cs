using System.Globalization;

namespace UpdateHub.Web.Localization;

/// <summary>
/// Resolves a UI string for the request's current culture, with English fallback
/// and a final fallback to the key itself so missing entries are visible but
/// never throw at render time.
/// </summary>
public class Translator
{
    public string this[string key] => Get(key);

    public string Get(string key)
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (UiStrings.Resources.TryGetValue(lang, out var dict) &&
            dict.TryGetValue(key, out var value))
            return value;

        if (UiStrings.Resources["en"].TryGetValue(key, out var en))
            return en;

        return key;
    }

    /// <summary>Two-letter code for the active culture, e.g. "cs".</summary>
    public string CurrentLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
}
