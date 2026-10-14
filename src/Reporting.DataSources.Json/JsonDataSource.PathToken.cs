using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Reporting.DataSources.Json;

public sealed partial class JsonDataSource
{

    private readonly partial record struct PathToken(string Name, int Index, bool IsIndex)
    {
        public static PathToken Property(string name) => new(name, 0, false);
        public static PathToken AtIndex(int index) => new(string.Empty, index, true);
    }
}
