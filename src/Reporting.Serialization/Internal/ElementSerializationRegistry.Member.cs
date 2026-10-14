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

    /// <summary>One element-specific property, with its wire names (PascalCase for repx, camelCase for
    /// repjson), its value on a default instance (for sparse omission), and whether it is required.</summary>
    internal sealed partial class Member
    {
        public required PropertyInfo Prop { get; init; }
        public required string XmlName { get; init; }
        public required string JsonName { get; init; }
        public required object? Default { get; init; }
        public required bool Required { get; init; }
    }
}
