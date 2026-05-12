using System.IO;
using System.Linq;
using Tamp.Scaffold;

namespace Tamp.Cli.Scaffold.Probes;

/// <summary>
/// Finds a single .NET solution at the repo root (preferring <c>.slnx</c>
/// per ADR 0006 — .NET 10 uses SLNX, not SLN). If exactly one is present, it
/// becomes <see cref="ScaffoldContext.Solution"/>. Zero or multiple → leaves
/// the slot empty (the CLI tells the user via stdout and the generated
/// <c>Build.cs</c> falls back to <c>[Solution]</c>'s auto-discovery).
/// </summary>
public sealed class DotnetSolutionProbe : IRepoProbe
{
    public void Probe(AbsolutePath repoRoot, ScaffoldContextBuilder ctx)
    {
        var root = repoRoot.Value;

        // .slnx first (modern). .sln second (back-compat for repos that haven't migrated).
        var slnx = Directory.GetFiles(root, "*.slnx", SearchOption.TopDirectoryOnly);
        var sln  = Directory.GetFiles(root, "*.sln",  SearchOption.TopDirectoryOnly);

        // Single .slnx wins outright. If zero .slnx but a single .sln, use that.
        // Anything else (multi-slnx, multi-sln, or mixed) — leave the slot null.
        if (slnx.Length == 1) { ctx.Solution = AbsolutePath.Create(slnx[0]); return; }
        if (slnx.Length == 0 && sln.Length == 1) { ctx.Solution = AbsolutePath.Create(sln[0]); return; }

        // Diagnostics for the user via ProbeData — the CLI surfaces these.
        if (slnx.Length > 1 || sln.Length > 1)
            ctx.Set("dotnet.solution.detection", $"multiple-solutions ({slnx.Length} .slnx, {sln.Length} .sln)");
        else
            ctx.Set("dotnet.solution.detection", "no-solution-found");
    }
}
