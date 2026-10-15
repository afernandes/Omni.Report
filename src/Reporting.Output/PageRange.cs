using System.Globalization;

namespace Reporting.Output;

/// <summary>A contiguous range of pages to export, 1-based and inclusive — "páginas 3 a 7" is
/// <c>new PageRange(3, 7)</c>. Same convention as <c>PrintOptions.PageRange</c>.</summary>
public readonly record struct PageRange
{
    /// <summary>Creates the range <paramref name="from"/>–<paramref name="to"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="from"/> is below 1, or <paramref name="to"/>
    /// is below <paramref name="from"/>.</exception>
    public PageRange(int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(from, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(to, from);
        From = from;
        To = to;
    }

    /// <summary>First page, 1-based.</summary>
    public int From { get; }

    /// <summary>Last page, 1-based and inclusive.</summary>
    public int To { get; }

    /// <summary>Number of pages in the range.</summary>
    public int Count => To - From + 1;

    /// <summary>The range made of one page.</summary>
    public static PageRange Single(int page) => new(page, page);

    /// <summary>Parses <c>"5"</c> or <c>"3-7"</c> (spaces allowed around the numbers), the form a query string or
    /// a print dialog carries.</summary>
    /// <exception cref="FormatException">The text is not a page or a range of pages.</exception>
    public static PageRange Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var range)
            ? range
            : throw new FormatException($"'{text}' não é uma página nem uma faixa de páginas. Use \"5\" ou \"3-7\".");
    }

    /// <summary>Parses <c>"5"</c> or <c>"3-7"</c>; <see langword="false"/> for anything else, including an
    /// inverted or zero-based range.</summary>
    public static bool TryParse(string? text, out PageRange range)
    {
        range = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var parts = text.Split('-');
        if (parts.Length is not (1 or 2)
            || !int.TryParse(parts[0], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture, out int from))
        {
            return false;
        }
        int to = from;
        if (parts.Length == 2 && !int.TryParse(parts[1], NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture, out to))
        {
            return false;
        }
        if (from < 1 || to < from)
        {
            return false;
        }
        range = new PageRange(from, to);
        return true;
    }

    /// <summary>Throws when the range ends after the document does — exporting pages that do not exist is a caller
    /// error, not an empty file.</summary>
    internal void EnsureWithin(int pageCount)
    {
        if (From < 1)
        {
            // default(PageRange): a struct cannot forbid it, so it is rejected where it would be used.
            throw new ArgumentOutOfRangeException(nameof(PageRange),
                "A faixa não foi inicializada; crie-a com new PageRange(de, até) ou PageRange.Parse(\"3-7\").");
        }
        if (To > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(PageRange),
                $"O relatório tem {pageCount} página(s); a faixa pede {From}–{To}.");
        }
    }

    /// <inheritdoc />
    public override string ToString() => From == To
        ? From.ToString(CultureInfo.InvariantCulture)
        : string.Create(CultureInfo.InvariantCulture, $"{From}-{To}");
}
