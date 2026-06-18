using System.Globalization;

namespace Reporting.Expressions;

/// <summary>Formats a value as it would appear inside <c>{expr:format}</c> templates,
/// honoring the supplied <see cref="CultureInfo"/>.</summary>
public static class ValueFormatter
{
    public static string Format(object? value, string? format, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (value is null)
        {
            return string.Empty;
        }
        if (string.IsNullOrEmpty(format))
        {
            return value is IFormattable f
                ? f.ToString(null, culture)
                : Convert.ToString(value, culture) ?? string.Empty;
        }
        if (value is IFormattable formattable)
        {
            return formattable.ToString(format, culture);
        }
        // Fall back to string.Format via composite syntax (supports {0:format}).
        return string.Format(culture, "{0:" + format + "}", value);
    }
}
