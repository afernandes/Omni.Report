using System.Runtime.CompilerServices;
using DiffEngine;
using VerifyTests;

namespace Reporting.Golden.Tests;

/// <summary>
/// Verify configuration for the golden-file suite. Runs once per test assembly.
/// </summary>
internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Goldens live next to the tests in Goldens/ instead of scattering *.verified.* across the
        // project root — the folder is the review surface, so keep it browsable.
        Verifier.UseSourceFileRelativeDirectory("Goldens");

        // No diff tool. On a dev machine a failing golden would otherwise pop a GUI; in CI the launch
        // just fails. The *.received.* file next to the *.verified.* one is the whole report we need.
        DiffRunner.Disabled = true;

        // We only ever verify strings we build ourselves (display list, SVG), so the object serializer
        // is never involved and needs no scrubbers. Verify's default scrubbers would otherwise rewrite
        // things like "Guid_1" inside our text — turn them off so a golden is literally what we emit.
        VerifierSettings.DontScrubGuids();
        VerifierSettings.DontScrubDateTimes();
    }
}
