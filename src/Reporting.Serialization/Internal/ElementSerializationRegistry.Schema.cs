using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Styling;

namespace Reporting.Serialization.Internal;

internal static partial class ElementSerializationRegistry
{

    private sealed record Schema(string Tag, IReadOnlyList<Member> Members);
}
