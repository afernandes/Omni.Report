using System.Diagnostics;
using Xunit;

namespace Reporting.DataSources.Integration.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself when no Docker daemon is reachable.
/// </summary>
/// <remarks>
/// <para>These tests start real database engines in containers, so they cannot run everywhere: a
/// Windows CI runner has Docker but in Windows-container mode, and plenty of dev machines have Docker
/// installed but not running. Without this gate the suite would go red for reasons that have nothing
/// to do with the code — the same failure mode as a wall-clock assertion, and just as corrosive,
/// because a suite that is red by default stops being read.</para>
///
/// <para>xUnit 2 can only skip statically, so the probe runs once at discovery and the result is
/// cached for the whole assembly. Docker does not appear or disappear mid-run.</para>
///
/// <para>The probe is <c>docker version --format {{.Server.Os}}</c>: it needs the <em>daemon</em> to
/// answer, not merely the CLI to exist. A machine with the client installed and the engine stopped —
/// the common case — correctly reports unavailable. Only a Linux daemon counts, since the images are
/// Linux ones.</para>
/// </remarks>
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> Unavailable = new(Probe, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Creates the fact, skipping it when Docker cannot run Linux containers here.</summary>
    public DockerFactAttribute()
    {
        if (Unavailable.Value is { } reason)
        {
            Skip = reason;
        }
    }

    /// <summary>Returns null when Docker is usable, or the reason to skip.</summary>
    private static string? Probe()
    {
        // An explicit opt-out for someone who has Docker but does not want minutes of image pulls on
        // every local run. Set OMNIREPORT_SKIP_DB_TESTS=1.
        if (Environment.GetEnvironmentVariable("OMNIREPORT_SKIP_DB_TESTS") is { Length: > 0 })
        {
            return "OMNIREPORT_SKIP_DB_TESTS está definida.";
        }

        // In CI these run ONLY in the dedicated db-integration workflow, which sets OMNIREPORT_DB_TESTS.
        // Without this the tests would silently join whatever generic job happens to pick the project up
        // — and the Linux job of ci.yml derives its project list automatically, so it would start pulling
        // three database images on every PR. Gating here instead of in the workflow keeps that decision
        // in one place, independent of how any job chooses its projects.
        bool inCi = Environment.GetEnvironmentVariable("CI") is { Length: > 0 };
        bool optedIn = Environment.GetEnvironmentVariable("OMNIREPORT_DB_TESTS") is { Length: > 0 };
        if (inCi && !optedIn)
        {
            return "Em CI, os testes de banco rodam só no workflow db-integration (OMNIREPORT_DB_TESTS).";
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo("docker", "version --format {{.Server.Os}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null)
            {
                return "Docker não encontrado — testes de banco pulados.";
            }

            // A leitura tem de ser assíncrona ANTES do WaitForExit com timeout. Com
            // ReadToEnd() síncrono primeiro, um daemon travado bloquearia para sempre na leitura e o
            // timeout de 15s nunca seria alcançado — a descoberta de testes penduraria, que é pior que
            // qualquer falha. Assim o timeout é de fato o limite superior.
            var stdout = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(15_000))
            {
                p.Kill(entireProcessTree: true);
                return "O daemon Docker não respondeu em 15s — testes de banco pulados.";
            }
            var os = stdout.GetAwaiter().GetResult().Trim();
            if (p.ExitCode != 0)
            {
                return "O daemon Docker não está acessível — testes de banco pulados.";
            }
            return os.Equals("linux", StringComparison.OrdinalIgnoreCase)
                ? null
                : $"O Docker está em modo de contêiner '{os}'; as imagens usadas são Linux — testes pulados.";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Win32Exception = executável ausente no PATH.
            return "Docker não encontrado — testes de banco pulados.";
        }
    }
}
