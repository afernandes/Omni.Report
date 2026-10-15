namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>The pt-BR name of a <see cref="DesignerFieldType"/> for the UI. The enum names are identifiers, and a
/// prompt that shows "DATE" under a Portuguese label reads as an untranslated leftover.</summary>
internal static class DesignerFieldTypeLabel
{
    internal static string Of(DesignerFieldType type) => type switch
    {
        DesignerFieldType.Text => "Texto",
        DesignerFieldType.Number => "Número",
        DesignerFieldType.Date => "Data",
        DesignerFieldType.Bool => "Sim/Não",
        DesignerFieldType.Money => "Moeda",
        _ => type.ToString(),
    };
}
