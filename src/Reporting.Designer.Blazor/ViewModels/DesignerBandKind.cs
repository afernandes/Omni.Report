using System.Collections.ObjectModel;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Elements;
using Reporting.Geometry;

namespace Reporting.Designer.Blazor.ViewModels;

public enum DesignerBandKind
{
    ReportHeader,
    PageHeader,
    GroupHeader,
    Detail,
    /// <summary>Sub-detail (DevExpress DetailReportBand / FastReport sub-band). Visually nested
    /// under the Detail, fires once per child row of <see cref="BandViewModel.DataMember"/>.</summary>
    SubDetail,
    GroupFooter,
    PageFooter,
    ReportFooter,
}
