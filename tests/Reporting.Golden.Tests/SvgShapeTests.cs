using FluentAssertions;
using Xunit;

namespace Reporting.Golden.Tests;

/// <summary>
/// The SVG golden absorbs float-serialisation noise so that only real geometry changes show up in a
/// diff. These tests pin that behaviour, because when it fails it fails <em>silently useful</em>: the
/// golden goes red on an upgrade that changed nothing, and the temptation is to accept the new file.
/// </summary>
public class SvgShapeTests
{
    private static string PathData(string svg) =>
        SvgShape.Summarize($"<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"{svg}\"/></svg>");

    [Fact]
    public void Rounds_numbers_packed_against_a_path_command()
    {
        // Path data has no separator between a number and the next command letter, so "187.12801L368.496"
        // arrives as ONE space-delimited token. An earlier version of the normaliser only peeled a leading
        // letter, so this token failed to parse and passed through raw — which is precisely how the
        // SkiaSharp 4 upgrade produced a golden diff of 187.128 vs 187.12801: the same coordinate, one
        // extra digit, on the one token the normaliser could not read.
        PathData("M28.368 187.12801L368.496 187.128")
            .Should().Be(PathData("M28.368 187.128L368.496 187.128"));
    }

    [Fact]
    public void Rounds_every_number_in_an_attribute_not_just_the_first()
    {
        PathData("M42.552 99.216003L425.23199 99.216")
            .Should().Be(PathData("M42.552 99.216L425.232 99.216"));
    }

    [Fact]
    public void Keeps_a_real_move_visible()
    {
        // The tolerance is three decimals of a point — about 0.0009 mm. A move anyone could see must
        // still show, otherwise the normaliser would be hiding regressions instead of noise.
        PathData("M28.368 187.128L368.496 187.128")
            .Should().NotBe(PathData("M28.368 187.5L368.496 187.128"));
    }

    [Fact]
    public void Leaves_non_numeric_content_alone()
    {
        var svg = SvgShape.Summarize(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect fill=\"url(#gradient_0)\" stroke=\"black\"/></svg>");

        // A naive number-scrubber would mangle the "0" inside the gradient id.
        svg.Should().Contain("url(#gradient_0)").And.Contain("black");
    }
}
