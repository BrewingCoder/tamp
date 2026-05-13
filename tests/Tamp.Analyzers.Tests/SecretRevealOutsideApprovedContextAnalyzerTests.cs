using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Tamp.Analyzers;
using Xunit;

namespace Tamp.Analyzers.Tests;

public sealed class SecretRevealOutsideApprovedContextAnalyzerTests
{
    // Minimal stub mirroring the 1.6.0 Tamp.Secret surface.
    private const string StubSource = """
        namespace Tamp
        {
            public sealed class Secret
            {
                public Secret(string name, string value) { Name = name; }
                public string Name { get; }
                public string Reveal() => "";   // public as of 1.6.0
                public override string ToString() => $"<Secret:{Name}>";
            }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(string userSource)
    {
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create(
            "test-asm",
            new[] { CSharpSyntaxTree.ParseText(StubSource), CSharpSyntaxTree.ParseText(userSource) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new SecretRevealOutsideApprovedContextAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    // ─── Approved contexts — should NOT fire ─────────────────────────────

    [Fact]
    public async Task Settings_Class_Suffix_Is_Approved()
    {
        var source = """
            using Tamp;
            namespace MyWrapper
            {
                public class MyVerbSettings {
                    void Build(Secret s) {
                        var raw = s.Reveal();
                    }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task SettingsBase_Class_Suffix_Is_Approved()
    {
        var source = """
            using Tamp;
            namespace MyWrapper
            {
                public abstract class MySettingsBase {
                    protected string Hand(Secret s) => s.Reveal();
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Tamp_Core_Namespace_Is_Approved()
    {
        var source = """
            using Tamp;
            namespace Tamp.Core
            {
                public class Runner {
                    void Build(Secret s) { var raw = s.Reveal(); }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Tamp_Cli_Namespace_Is_Approved()
    {
        var source = """
            using Tamp;
            namespace Tamp.Cli
            {
                public class Foo {
                    void Build(Secret s) { var raw = s.Reveal(); }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Tamp_NetCli_V10_Namespace_Is_Approved()
    {
        var source = """
            using Tamp;
            namespace Tamp.NetCli.V10
            {
                public class Foo {
                    void Build(Secret s) { var raw = s.Reveal(); }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Test_Code_With_Tests_Suffix_Is_Approved()
    {
        var source = """
            using Tamp;
            namespace MyWrapper.UnitTests
            {
                public class MyVerbTests {
                    void M() {
                        var s = new Secret("n", "v");
                        var raw = s.Reveal();
                    }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Tests_Namespace_Segment_Is_Approved()
    {
        var source = """
            using Tamp;
            namespace MyWrapper.Tests.Internal
            {
                public class SomeHelper {
                    void M() {
                        var s = new Secret("n", "v");
                        var raw = s.Reveal();
                    }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    // ─── Non-approved contexts — SHOULD fire ─────────────────────────────

    [Fact]
    public async Task Build_Class_In_Build_Script_Fires()
    {
        var source = """
            using Tamp;
            class Build {
                Secret Token = new Secret("token", "v");
                void M() {
                    var leaked = Token.Reveal();   // build-script author shouldn't need this
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Single(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Random_Logger_Helper_Fires()
    {
        // The exact accidental-leak shape the analyzer targets.
        var source = """
            using Tamp;
            namespace MyApp
            {
                public class TelemetryLogger {
                    public void LogSecret(Secret s) {
                        // BAD: cleartext into a log sink
                        System.Console.WriteLine("token: " + s.Reveal());
                    }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Single(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Class_Named_Settings_But_In_Wrong_Place_Still_Approved_By_Heuristic()
    {
        // Permissive-by-design: a class anywhere named *Settings is approved.
        // This is a documented heuristic limit — adopters can opt-in to a different
        // rule shape via TAM-197's WrapperSettingsBase if they want stricter gating.
        var source = """
            using Tamp;
            namespace SomeApp.Logging
            {
                public class WeirdLoggingSettings {
                    public string Hand(Secret s) => s.Reveal();
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Method_Name_Reveal_Outside_Secret_Type_Does_Not_Fire()
    {
        // Defensive: another type with a Reveal() method shouldn't trip the analyzer.
        var source = """
            namespace MyApp
            {
                public class Magician {
                    public string Reveal() => "abracadabra";
                }
                public class Build {
                    void M() {
                        var leaked = new Magician().Reveal();
                    }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Settings_Suffix_Outside_Tamp_Namespace_Still_Approved()
    {
        // Heuristic is by class-name shape, not namespace; third-party satellites
        // (anywhere in their own namespace) work without any opt-in dance.
        var source = """
            using Tamp;
            namespace Third.Party.Co
            {
                public class FancyServiceSettings {
                    public string Build(Secret s) => s.Reveal();
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Empty(diags.Where(d => d.Id == "TAMP004"));
    }

    [Fact]
    public async Task Multiple_Reveal_Calls_All_Flagged()
    {
        var source = """
            using Tamp;
            namespace MyApp
            {
                public class Looser {
                    public string A(Secret s1, Secret s2) {
                        return s1.Reveal() + "/" + s2.Reveal();
                    }
                }
            }
            """;
        var diags = await RunAsync(source);
        Assert.Equal(2, diags.Count(d => d.Id == "TAMP004"));
    }
}
