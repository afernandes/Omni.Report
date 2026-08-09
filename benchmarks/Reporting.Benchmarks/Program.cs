using BenchmarkDotNet.Running;

// Entry point for the benchmark harness. Run from the repo root:
//
//   dotnet run -c Release --project benchmarks/Reporting.Benchmarks -- --filter "*Pagination*"
//   dotnet run -c Release --project benchmarks/Reporting.Benchmarks -- --list flat
//
// No filter starts BenchmarkDotNet's interactive picker. Results land in
// BenchmarkDotNet.Artifacts/results (markdown + CSV), which is git-ignored.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

/// <summary>Marker for <see cref="BenchmarkSwitcher.FromAssembly"/>.</summary>
public partial class Program;
