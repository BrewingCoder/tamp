using Xunit;

namespace Tamp.Sbom.Tests;

public class SbomFileIoTests : IDisposable
{
    private readonly AbsolutePath _scratch;

    public SbomFileIoTests()
    {
        _scratch = AbsolutePath.CreateTempDirectory("tamp-sbom-tests");
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Write_Then_Read_Roundtrips()
    {
        Bogus.Randomizer.Seed = new Random(404);
        var original = SbomFakers.Bom().Generate();
        var path = AbsolutePath.Create(Path.Combine(_scratch, "bom.cdx.json"));

        SbomWriter.WriteToFile(original, path);
        var loaded = SbomReader.LoadFromFile(path);

        JsonAssert.Equivalent(SbomWriter.Serialize(original), SbomWriter.Serialize(loaded));
    }

    [Fact]
    public async Task WriteAsync_Then_ReadAsync_Roundtrips()
    {
        Bogus.Randomizer.Seed = new Random(405);
        var original = SbomFakers.Bom().Generate();
        var path = AbsolutePath.Create(Path.Combine(_scratch, "bom-async.cdx.json"));

        await SbomWriter.WriteToFileAsync(original, path);
        var loaded = await SbomReader.LoadFromFileAsync(path);

        JsonAssert.Equivalent(SbomWriter.Serialize(original), SbomWriter.Serialize(loaded));
    }

    [Fact]
    public void LoadFromFile_Throws_For_Missing_File()
    {
        var path = AbsolutePath.Create(Path.Combine(_scratch, "missing.cdx.json"));
        Assert.Throws<FileNotFoundException>(() => SbomReader.LoadFromFile(path));
    }

    [Fact]
    public void File_Helpers_Throw_On_Null_Path()
    {
        Assert.Throws<ArgumentNullException>(() => SbomReader.LoadFromFile(null!));
        Assert.Throws<ArgumentNullException>(() => SbomWriter.WriteToFile(new CycloneDxBom(), null!));
    }
}
