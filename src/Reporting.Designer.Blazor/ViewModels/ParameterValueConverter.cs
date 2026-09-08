using System.Globalization;
namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Converts canonical editor values without guessing thousands separators.</summary>
public static class ParameterValueConverter
{
    public static string? Format(object? value) => value switch
    {
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString()
    };
    public static object? Parse(string? raw, DesignerFieldType type)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return type switch
        {
            DesignerFieldType.Number => double.Parse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture),
            DesignerFieldType.Money => decimal.Parse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture),
            DesignerFieldType.Date => DateTime.ParseExact(raw, ["yyyy-MM-dd", "dd/MM/yyyy", "O"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DesignerFieldType.Bool => bool.Parse(raw),
            _ => raw
        };
    }
}
