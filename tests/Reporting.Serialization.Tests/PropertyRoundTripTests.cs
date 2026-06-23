using System.Xml;
using System.Xml.Schema;
using FluentAssertions;
using Reporting;
using Reporting.Bands;
using Reporting.Common;
using Reporting.Data;
using Reporting.Elements;
using Reporting.Geometry;
using Reporting.Paper;
using Reporting.Parameters;
using Reporting.Serialization;
using Reporting.Styling;
using Xunit;

namespace Reporting.Serialization.Tests;

/// <summary>
/// Property-based round-trip: over many pseudo-randomly generated <see cref="ReportDefinition"/>s, the native
/// serializers must satisfy the spec contract <c>Load(Save(def)).Equals(def)</c> (full structural equality), and
/// every export must be valid against the official RDL 2016 XSD. Fixtures cover specific shapes; this blankets
/// the combinatorial surface so a dropped/mis-wired field regresses loudly on at least one seed.
/// </summary>
public class PropertyRoundTripTests
{
    public static TheoryData<int> Seeds
    {
        get { var d = new TheoryData<int>(); for (var s = 1; s <= 60; s++) { d.Add(s); } return d; }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Repx_round_trips_any_report(int seed)
    {
        var def = Gen.Report(seed);
        var s = new RepxSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def, "seed {0}", seed);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RepJson_round_trips_any_report(int seed)
    {
        var def = Gen.Report(seed);
        var s = new RepJsonSerializer();
        s.LoadFromBytes(s.SaveToBytes(def)).Should().Be(def, "seed {0}", seed);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Any_report_exports_to_xsd_valid_rdl(int seed)
    {
        var def = Gen.Report(seed);
        RdlSchema.ValidationErrors(new RdlExporter().SaveToBytes(def)).Should().BeEmpty("seed {0}", seed);
    }

    // ── Vendored RDL 2016 XSD (embedded in this test assembly, see RdlXsdValidationTests) ──
    private static class RdlSchema
    {
        private static readonly XmlSchemaSet Set = Load();

        private static XmlSchemaSet Load()
        {
            var asm = typeof(RdlSchema).Assembly;
            var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("ReportDefinition.xsd"));
            using var stream = asm.GetManifestResourceStream(name)!;
            var set = new XmlSchemaSet();
            set.Add(null, XmlReader.Create(stream));
            set.Compile();
            return set;
        }

        public static IReadOnlyList<string> ValidationErrors(byte[] rdl)
        {
            var errors = new List<string>();
            var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
            settings.Schemas.Add(Set);
            settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity} (line {e.Exception?.LineNumber}): {e.Message}");
            using var ms = new MemoryStream(rdl);
            using var reader = XmlReader.Create(ms, settings);
            while (reader.Read()) { }
            return errors;
        }
    }

    // ── Seeded generator over the round-trip-safe model surface ──
    private static class Gen
    {
        private static readonly string[] Words = { "Total", "Cliente", "Produto", "Mes", "Valor", "Qtd", "Nome", "Id" };
        private static readonly string[] Formats = { "C2", "N0", "P1", "dd/MM/yyyy", "#,##0.00", "" };

        public static ReportDefinition Report(int seed)
        {
            var r = new Random(seed);
            var paper = Pick(r, PaperSize.A4, PaperSize.Letter, PaperSize.A5);
            var orientation = r.Next(2) == 0 ? Orientation.Portrait : Orientation.Landscape;
            var page = new PageSetup(paper, orientation,
                new Thickness(Mm(r, 5, 20), Mm(r, 5, 20), Mm(r, 5, 20), Mm(r, 5, 20)),
                Columns: r.Next(3) == 0 ? 2 : 1, ColumnSpacing: Unit.FromMm(r.Next(3) + 2));

            var detail = new DetailBand(Unit.FromMm(r.Next(4) + 4), Elements(r, r.Next(4)),
                CanGrow: r.NextBool(), CanShrink: r.NextBool());

            var def = new ReportDefinition(Word(r) + seed, page, detail)
            {
                Parameters = Many(r, r.Next(4), i => Parameter(r, i)),
                DataSources = Many(r, 1, _ => DataSource(r)),
                Variables = Many(r, r.Next(3), i => new ReportVariable("v" + i, "Sum(Fields." + Word(r) + ")",
                    Pick(r, VariableScope.Report, VariableScope.Row, VariableScope.Group))),
                ReportHeader = r.NextBool() ? Band(r, BandKind.ReportHeader) : null,
                PageHeader = r.NextBool() ? Band(r, BandKind.PageHeader) : null,
                PageFooter = r.NextBool() ? Band(r, BandKind.PageFooter) : null,
                ReportFooter = r.NextBool() ? Band(r, BandKind.ReportFooter) : null,
                Metadata = new EquatableDictionary<string, string>(
                    new Dictionary<string, string> { ["Author"] = Word(r), ["Build"] = seed.ToString() }),
            };
            return def;
        }

        private static ReportBand Band(Random r, BandKind kind)
            => new(kind, Unit.FromMm(r.Next(8) + 4), Elements(r, r.Next(3) + 1))
            {
                VisibleExpression = r.Next(4) == 0 ? "Page.Number > 0" : null,
            };

        private static EquatableArray<ReportElement> Elements(Random r, int count)
        {
            var list = new List<ReportElement>();
            for (var i = 0; i < count; i++) { list.Add(Element(r, i)); }
            return new EquatableArray<ReportElement>(list.ToArray());
        }

        private static ReportElement Element(Random r, int i)
        {
            var b = new Rectangle(Mm(r, 0, 100), Mm(r, 0, 100), Mm(r, 10, 80), Mm(r, 0, 20));
            switch (r.Next(8))
            {
                case 0:
                    return new TextBoxElement { Bounds = b, Expression = "{Fields." + Word(r) + "}", Style = RandStyle(r),
                        Bookmark = r.Next(3) == 0 ? "bm" + i : null, Visible = r.NextBool() };
                case 1:
                    return new LabelElement { Bounds = b, Text = Word(r), Style = RandStyle(r) };
                case 2:
                    return new LineElement { Bounds = b with { Height = Unit.Zero },
                        Direction = LineDirection.Horizontal,
                        Pen = new BorderSide(Pick(r, BorderLineStyle.Solid, BorderLineStyle.Dashed, BorderLineStyle.Dotted), Pt(r), Col(r)) };
                case 3:
                    return new RectangleElement { Bounds = b, FillColor = Col(r), CornerRadius = Unit.FromMm(r.Next(4)),
                        Style = Style.Default with { Border = Border.Uniform(BorderLineStyle.Solid, Pt(r), Col(r)) } };
                case 4:
                    return new EllipseElement { Bounds = b, FillColor = Col(r) };
                case 5:
                    return new BarcodeElement { Bounds = b, Expression = "{Fields." + Word(r) + "}",
                        Symbology = Pick(r, BarcodeSymbology.Ean13, BarcodeSymbology.Code128, BarcodeSymbology.QrCode),
                        ShowText = r.NextBool(), QrEcc = Pick(r, QrEccLevel.Low, QrEccLevel.Medium, QrEccLevel.High) };
                case 6:
                    return new ChartElement { Bounds = b, Kind = Pick(r, ChartKind.Bar, ChartKind.Line, ChartKind.Pie, ChartKind.Area),
                        Title = r.NextBool() ? Word(r) : null, ShowLegend = r.NextBool(),
                        Series = EquatableArray.Create(new ChartSeries("S", "Fields." + Word(r), "Fields." + Word(r), Col(r))) };
                default:
                    return new SubreportElement { Bounds = b, ReportId = "Sub" + i,
                        DataExpression = r.Next(3) == 0 ? "Fields." + Word(r) : null,
                        ParameterBindings = new EquatableDictionary<string, string>(
                            new Dictionary<string, string> { ["p"] = "Fields." + Word(r) }) };
            }
        }

        private static Style RandStyle(Random r)
            => new(
                Font: new Font(Pick(r, "Arial", "Calibri", "Times New Roman"), r.Next(6) + 8,
                    Pick(r, FontStyle.Regular, FontStyle.Bold, FontStyle.Italic)),
                ForeColor: Col(r),
                BackColor: r.Next(3) == 0 ? Col(r) : null,
                HorizontalAlignment: Pick(r, HorizontalAlignment.Left, HorizontalAlignment.Center, HorizontalAlignment.Right),
                Format: Pick(r, Formats) is { Length: > 0 } f ? f : null);

        private static ReportParameter Parameter(Random r, int i)
        {
            var type = Pick(r, typeof(string), typeof(int), typeof(decimal), typeof(DateTime), typeof(bool));
            object? def = r.Next(3) == 0 ? null : type switch
            {
                var t when t == typeof(string) => Word(r),
                var t when t == typeof(int) => r.Next(1000),
                var t when t == typeof(decimal) => (decimal)r.Next(1000),
                var t when t == typeof(DateTime) => new DateTime(2026, 1 + r.Next(12), 1 + r.Next(28)),
                _ => r.NextBool(),
            };
            return new ReportParameter("p" + i, type, r.NextBool() ? Word(r) : null, def,
                Required: r.NextBool(), Nullable: r.NextBool(), AllowBlank: r.NextBool(), Hidden: r.NextBool())
            {
                DefaultValueExpression = def is null && r.Next(3) == 0 ? "Today()" : null,
            };
        }

        private static DataSourceDefinition DataSource(Random r)
            => new("DS" + r.Next(100),
                Fields: Many(r, r.Next(3) + 1, _ => new DataField(Word(r) + r.Next(100),
                    Pick(r, typeof(string), typeof(decimal), typeof(int)), DisplayName: r.NextBool() ? Word(r) : null)));

        // helpers
        private static EquatableArray<T> Many<T>(Random r, int n, Func<int, T> make)
        {
            var a = new T[n];
            for (var i = 0; i < n; i++) { a[i] = make(i); }
            return new EquatableArray<T>(a);
        }
        private static T Pick<T>(Random r, params T[] xs) => xs[r.Next(xs.Length)];
        private static string Word(Random r) => Words[r.Next(Words.Length)];
        private static Unit Mm(Random r, int lo, int hi) => Unit.FromMm(r.Next(lo, hi + 1));
        private static Unit Pt(Random r) => Unit.FromPoint(r.Next(1, 4) * 0.5);
        private static Color Col(Random r) => Color.FromRgb((byte)r.Next(256), (byte)r.Next(256), (byte)r.Next(256));
    }
}

file static class RandomExtensions
{
    public static bool NextBool(this Random r) => r.Next(2) == 0;
}
