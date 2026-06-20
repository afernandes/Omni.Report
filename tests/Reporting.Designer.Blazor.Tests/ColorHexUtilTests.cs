using FluentAssertions;
using Reporting.Designer.Blazor.Services;
using Reporting.Styling;
using Xunit;

namespace Reporting.Designer.Blazor.Tests;

/// <summary>
/// The shared colour helpers used by every colour editor (metadata section + list editor): an
/// <c>&lt;input type=color&gt;</c> only round-trips <c>#RRGGBB</c>, so an 8-digit <c>#AARRGGBB</c> is
/// stripped for display and its alpha re-attached on write — never silently forced opaque.
/// </summary>
public class ColorHexUtilTests
{
    [Fact]
    public void Strips_alpha_for_the_input_and_re_merges_it_on_write()
    {
        ColorHexUtil.ToRgbHexString("#80FF0000").Should().Be("#FF0000", "the input can't take 8 digits");
        ColorHexUtil.ToRgbHexString("#FF0000").Should().Be("#FF0000", "an opaque 6-digit hex is unchanged");

        ColorHexUtil.MergeHexAlpha("#80FF0000", "#00FF00").Should().Be("#8000FF00", "the original alpha prefix survives the pick");
        ColorHexUtil.MergeHexAlpha("#FF0000", "#00FF00").Should().Be("#00FF00", "an opaque source just takes the new RGB");
    }

    [Fact]
    public void Color_picker_helpers_strip_and_preserve_alpha()
    {
        var translucent = Color.FromArgb(128, 255, 0, 0);
        ColorHexUtil.ToRgbHex(translucent).Should().Be("#FF0000");

        var picked = ColorHexUtil.WithRgb(translucent, "#00FF00");
        picked.A.Should().Be(128, "the original alpha survives picking a new RGB");
        picked.G.Should().Be(255, "the new green channel is applied");
    }
}
