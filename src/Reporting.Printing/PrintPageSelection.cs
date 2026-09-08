namespace Reporting.Printing;

/// <summary>Validates print requests and enumerates zero-based source page indices in job order.</summary>
public static class PrintPageSelection
{
    /// <summary>Returns the requested pages, repeating them for software copies when requested.</summary>
    public static IEnumerable<int> Enumerate(int pageCount, PrintOptions options, bool includeCopies = true)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        if (options.Copies is < 1 or > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(options), "Copies must be between 1 and 32767.");
        var (from, to) = options.PageRange ?? (1, pageCount);
        if (options.PageRange is not null && (from < 1 || to < from || to > pageCount))
            throw new ArgumentOutOfRangeException(nameof(options), "PageRange must be within the document.");
        int copies = includeCopies ? options.Copies : 1;
        return Indices();

        IEnumerable<int> Indices()
        {
            if (options.Collate)
            {
                for (int copy = 0; copy < copies; copy++)
                    for (int page = from - 1; page < to; page++) yield return page;
            }
            else
            {
                for (int page = from - 1; page < to; page++)
                    for (int copy = 0; copy < copies; copy++) yield return page;
            }
        }
    }
}
