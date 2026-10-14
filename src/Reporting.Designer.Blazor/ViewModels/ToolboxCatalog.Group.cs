using System.Reflection;

namespace Reporting.Designer.Blazor.ViewModels;

public static partial class ToolboxCatalog
{

    public sealed record Group(string Name, IReadOnlyList<Item> Items);
}
