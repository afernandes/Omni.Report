using Reporting.Common;
using Reporting.Geometry;
using Reporting.Metadata;

namespace Reporting.Elements;

/// <summary>Source language of a <see cref="CodeElement"/> block — C# (default) or legacy VB.NET for SSRS interop.</summary>
public enum CodeLanguage
{
    /// <summary>C#. The default, and the only language the Roslyn evaluator compiles.</summary>
    CSharp,

    /// <summary>VB.NET, as SSRS reports declare it. Preserved on import for fidelity; not executed.</summary>
    VisualBasic,
}
