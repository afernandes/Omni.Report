using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Reporting.Common;
using Reporting.Data;

namespace Reporting.Designer.Blazor.ViewModels;

/// <summary>Logical type of a designer-time field. Drives the icon/colour in the data tree
/// and the autocomplete shown by the expression editor.</summary>
public enum DesignerFieldType { Text, Number, Date, Bool, Money }
