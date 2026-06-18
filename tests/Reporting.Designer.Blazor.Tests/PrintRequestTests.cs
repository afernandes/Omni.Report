using FluentAssertions;
using Reporting.Designer.Blazor.Services;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>Unit tests for PrintRequest's range parsing — the most-error-prone bit of the
/// print pipeline. Invalid tokens must degrade gracefully (skip silently); valid tokens
/// must clamp to [1, totalPages] so a user typing "1000" doesn't crash anything.</summary>
public class PrintRequestTests
{
    [Theory]
    [InlineData(PrintPageRangeMode.All,     null,           10, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    [InlineData(PrintPageRangeMode.Range,   "1",            10, new[] { 1 })]
    [InlineData(PrintPageRangeMode.Range,   "1,3,5",        10, new[] { 1, 3, 5 })]
    [InlineData(PrintPageRangeMode.Range,   "1-3",          10, new[] { 1, 2, 3 })]
    [InlineData(PrintPageRangeMode.Range,   "1-3, 7-9",     10, new[] { 1, 2, 3, 7, 8, 9 })]
    [InlineData(PrintPageRangeMode.Range,   "5-",           10, new[] { 5, 6, 7, 8, 9, 10 })] // open right
    [InlineData(PrintPageRangeMode.Range,   "-3",           10, new[] { 1, 2, 3 })]            // open left
    [InlineData(PrintPageRangeMode.Range,   "3-1",          10, new[] { 1, 2, 3 })]            // swap order
    [InlineData(PrintPageRangeMode.Range,   "0,11,200",     10, new int[0])]                   // out of range
    [InlineData(PrintPageRangeMode.Range,   "abc, !$%",     10, new int[0])]                   // garbage
    [InlineData(PrintPageRangeMode.Range,   "",             10, new int[0])]                   // empty
    [InlineData(PrintPageRangeMode.Range,   null,           10, new int[0])]                   // null
    public void EnumeratePageNumbers_handles_diverse_inputs(
        PrintPageRangeMode mode, string? rangeText, int totalPages, int[] expected)
    {
        var request = new PrintRequest(RangeMode: mode, RangeText: rangeText);
        var actual = request.EnumeratePageNumbers(totalPages).ToArray();
        actual.Should().Equal(expected);
    }

    [Fact]
    public void Default_is_sensible()
    {
        // The default request prints everything, one copy, color, via the browser dialog.
        // If a user clicks "Imprimir" without touching anything else, that's what they get.
        var r = PrintRequest.Default;
        r.RangeMode.Should().Be(PrintPageRangeMode.All);
        r.Copies.Should().Be(1);
        r.Collate.Should().BeTrue();
        r.ColorMode.Should().Be(PrintColorMode.Color);
        r.OutputMode.Should().Be(PrintOutputMode.BrowserDialog);
        r.PaperSize.Should().BeNull("keep the document's own paper by default");
        r.Orientation.Should().BeNull("keep the document's own orientation by default");
    }

    [Fact]
    public void Range_text_with_extra_whitespace_is_tolerated()
    {
        // Real-world input often has spaces around commas. We trim per-token before parsing.
        var r = new PrintRequest(RangeMode: PrintPageRangeMode.Range, RangeText: "  1 ,  2-4 , 6 ");
        r.EnumeratePageNumbers(10).Should().Equal(1, 2, 3, 4, 6);
    }

    [Fact]
    public void Out_of_bounds_range_clamps_to_document()
    {
        // "5-100" on a 7-page doc → pages 5..7. We clamp the right side and skip nothing
        // on the left when the bound is in range.
        var r = new PrintRequest(RangeMode: PrintPageRangeMode.Range, RangeText: "5-100");
        r.EnumeratePageNumbers(7).Should().Equal(5, 6, 7);
    }
}
