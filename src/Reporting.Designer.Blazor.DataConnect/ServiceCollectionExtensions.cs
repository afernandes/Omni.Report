using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Designer.Blazor.ViewModels;

namespace Reporting.Designer.Blazor.DataConnect;

/// <summary>
/// Fluent DI extensions for hosts that want the designer to talk to real databases at
/// design time (Test connection, Schema explorer, Get fields, Preview, SP signature).
/// </summary>
/// <remarks>
/// <para>Without these registrations the designer still works — it falls back to the
/// in-memory / JSON-sample mode. Registering them turns on the full live-DB experience.</para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IDesignerDataConnect"/> backed by the default
    /// <see cref="DesignerDataConnect"/> implementation (SQLite + PostgreSQL + SQL Server +
    /// MySQL), plus an <see cref="ISecretResolver"/> defaulting to environment variables.</summary>
    /// <remarks>
    /// <para>Override the secret resolver by chaining
    /// <c>services.AddSingleton&lt;ISecretResolver, MyVaultResolver&gt;()</c> <em>before</em>
    /// (the extension uses <c>TryAddSingleton</c> so caller-supplied registrations win).</para>
    ///
    /// <para>Typical wiring in <c>Program.cs</c>:</para>
    /// <code>
    /// builder.Services.AddOmniReportDesignerDataConnect();
    /// // …or with a custom secret resolver:
    /// builder.Services.AddSingleton&lt;ISecretResolver, AzureKeyVaultResolver&gt;();
    /// builder.Services.AddOmniReportDesignerDataConnect();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddOmniReportDesignerDataConnect(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISecretResolver, EnvironmentSecretResolver>();
        services.TryAddSingleton<IDesignerDataConnect, DesignerDataConnect>();
        return services;
    }
}
