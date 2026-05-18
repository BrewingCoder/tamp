using Xunit;

namespace Tamp.Sarif.Tests;

public class SarifFileIoTests : IDisposable
{
    private readonly AbsolutePath _scratch;

    public SarifFileIoTests()
    {
        _scratch = AbsolutePath.CreateTempDirectory("tamp-sarif-tests");
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Write_Then_Read_Roundtrips()
    {
        Bogus.Randomizer.Seed = new Random(2026);
        var original = SarifFakers.Log().Generate();
        var path = AbsolutePath.Create(Path.Combine(_scratch, "sample.sarif"));

        SarifWriter.WriteToFile(original, path);
        var loaded = SarifReader.LoadFromFile(path);

        JsonAssert.Equivalent(SarifWriter.Serialize(original), SarifWriter.Serialize(loaded));
    }

    [Fact]
    public async Task WriteAsync_Then_ReadAsync_Roundtrips()
    {
        Bogus.Randomizer.Seed = new Random(2027);
        var original = SarifFakers.Log().Generate();
        var path = AbsolutePath.Create(Path.Combine(_scratch, "sample-async.sarif"));

        await SarifWriter.WriteToFileAsync(original, path);
        var loaded = await SarifReader.LoadFromFileAsync(path);

        JsonAssert.Equivalent(SarifWriter.Serialize(original), SarifWriter.Serialize(loaded));
    }

    [Fact]
    public void WriteToFile_Overwrites_Existing()
    {
        var path = AbsolutePath.Create(Path.Combine(_scratch, "overwrite.sarif"));
        File.WriteAllText(path, "stale content that is definitely not SARIF");

        var fresh = new SarifLog { Runs = [new SarifRun { Tool = new SarifTool { Driver = new SarifToolComponent { Name = "fresh" } } }] };
        SarifWriter.WriteToFile(fresh, path);

        var loaded = SarifReader.LoadFromFile(path);
        Assert.Equal("fresh", loaded.Runs[0].Tool.Driver.Name);
    }

    [Fact]
    public void LoadFromFile_Throws_For_Missing_File()
    {
        var path = AbsolutePath.Create(Path.Combine(_scratch, "does-not-exist.sarif"));
        Assert.Throws<FileNotFoundException>(() => SarifReader.LoadFromFile(path));
    }

    [Fact]
    public void File_Helpers_Throw_On_Null_Path()
    {
        Assert.Throws<ArgumentNullException>(() => SarifReader.LoadFromFile(null!));
        Assert.Throws<ArgumentNullException>(() => SarifWriter.WriteToFile(new SarifLog(), null!));
    }
}
