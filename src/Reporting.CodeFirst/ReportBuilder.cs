using System.Linq.Expressions;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.DataSources;
using Reporting.DataSources.Enumerable;
using Reporting.Parameters;
using Reporting.Styling;

namespace Reporting.CodeFirst;

/// <summary>Entry point of the fluent code-first API. Chains description of a report and
/// produces a <see cref="Report"/> bound to an internal data source registry.</summary>
public static class ReportBuilder
{
    /// <summary>Starts a new fluent report definition with the given report name and returns the
    /// builder root to chain page setup, data sources, and bands.</summary>
    public static ReportBuilderRoot Create(string name) => new(name);
}
