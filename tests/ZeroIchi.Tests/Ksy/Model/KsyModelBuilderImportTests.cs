using ZeroIchi.Models.FileStructure.Ksy.Model;
using static ZeroIchi.Tests.Ksy.Model.KsyTestLoader;

namespace ZeroIchi.Tests.Ksy.Model;

public sealed class KsyModelBuilderImportTests
{
    private const string Riff = """
        meta:
          id: riff
          endian: le
        seq:
          - id: chunk
            type: chunk
        types:
          chunk:
            seq:
              - id: id
                type: u4
            enums:
              kinds:
                1: data
        """;

    [Fact]
    public void Load_AbsoluteImport_MakesTopLevelTypeAndNestedTypesReachable()
    {
        var source = new KsyMemoryImportSource()
            .Add("common/riff", Riff)
            .Add("media/wav", """
                meta:
                  id: wav
                  imports:
                    - /common/riff
                seq:
                  - id: whole
                    type: riff
                  - id: chunk
                    type: riff::chunk
                  - id: kind
                    type: u1
                    enum: riff::chunk::kinds
                """);

        var result = KsyModelBuilder.Load("media/wav", source);

        Assert.True(result.Format is not null, string.Join("\n", result.Diagnostics));
        var riff = Assert.Single(result.Format!.Imports);
        Assert.Equal("riff", riff.Id);
        Assert.Equal("common/riff", riff.SourcePath);
        Assert.Same(riff.Root, ((KsyUserType)result.Format.Root.Seq[0].DataType!).Type);
        Assert.Same(riff.Root.Types["chunk"], ((KsyUserType)result.Format.Root.Seq[1].DataType!).Type);
        Assert.Same(riff.Root.Types["chunk"].Enums["kinds"], result.Format.Root.Seq[2].Enum);
    }

    [Theory]
    [InlineData("media/wav", "riff", "media/riff")]
    [InlineData("media/wav", "../common/riff", "common/riff")]
    [InlineData("wav", "sub/riff", "sub/riff")]
    public void Load_RelativeImport_ResolvesAgainstImporterDirectory(string importer, string import, string expectedPath)
    {
        var source = new KsyMemoryImportSource()
            .Add(expectedPath, Riff)
            .Add(importer, $"meta:\n  id: wav\n  imports:\n    - {import}\nseq:\n  - id: r\n    type: riff\n");

        var result = KsyModelBuilder.Load(importer, source);

        Assert.Equal(expectedPath, Assert.Single(result.Format!.Imports).SourcePath);
    }

    [Fact]
    public void Load_TransitiveImportsAreVisible()
    {
        var source = new KsyMemoryImportSource()
            .Add("a", "meta:\n  id: a\n  imports: [b]\nseq: []\n")
            .Add("b", "meta:\n  id: b\n  imports: [c]\nseq: []\n")
            .Add("c", "meta:\n  id: c\nseq:\n  - id: v\n    type: u1\n")
            .Add("main", "meta:\n  id: main\n  imports: [a]\nseq:\n  - id: x\n    type: c\n");

        Assert.NotNull(KsyModelBuilder.Load("main", source).Format);
    }

    [Fact]
    public void Load_SameImportTwice_IsLoadedOnce()
    {
        var source = new KsyMemoryImportSource()
            .Add("common/riff", Riff)
            .Add("a", "meta:\n  id: a\n  imports: [/common/riff]\nseq: []\n")
            .Add("main", "meta:\n  id: main\n  imports: [a, /common/riff]\nseq: []\n");

        var format = KsyModelBuilder.Load("main", source).Format!;

        Assert.Same(format.Imports[0].Imports[0], format.Imports[1]);
    }

    [Fact]
    public void Load_MissingImport_IsError()
    {
        var source = new KsyMemoryImportSource().Add("main", "meta:\n  id: main\n  imports:\n    - /nope\n");

        var error = Assert.Single(KsyModelBuilder.Load("main", source).Diagnostics);

        Assert.Contains("Import '/nope' not found", error.Message);
        Assert.Equal("/meta/imports/0", error.Path);
        Assert.Equal((4, 7), (error.Line, error.Column));
    }

    [Fact]
    public void Load_ImportCycle_IsAllowed()
    {
        var source = new KsyMemoryImportSource()
            .Add("a", "meta:\n  id: a\n  imports: [b]\nseq:\n  - id: x\n    type: b\n")
            .Add("b", "meta:\n  id: b\n  imports: [a, b]\nseq:\n  - id: y\n    type: a\n    if: false\n");

        var result = KsyModelBuilder.Load("a", source);

        Assert.True(result.Format is not null, string.Join("\n", result.Diagnostics));
        var b = Assert.Single(result.Format!.Imports);
        Assert.Equal(new[] { "a", "b" }, b.Imports.Select(f => f.Id));
        Assert.Same(result.Format.Root, ((KsyUserType)b.Root.Seq[0].DataType!).Type);
    }

    [Fact]
    public void Load_ErrorsInImportedFile_FailTheImporter()
    {
        var source = new KsyMemoryImportSource()
            .Add("bad", "meta:\n  id: bad\nseq:\n  - id: v\n    type: nope\n")
            .Add("main", "meta:\n  id: main\n  imports: [bad]\n");

        var result = KsyModelBuilder.Load("main", source);

        Assert.Null(result.Format);
        Assert.Contains(result.Diagnostics, d => d.File == "bad" && d.Message.Contains("Type 'nope' not found"));
    }

    [Fact]
    public void Load_ImportedSpecDoesNotInheritImporterMeta()
    {
        var source = new KsyMemoryImportSource()
            .Add("lib", "meta:\n  id: lib\nseq:\n  - id: v\n    type: u4\n")
            .Add("main", "meta:\n  id: main\n  endian: le\n  imports: [lib]\n");

        var result = KsyModelBuilder.Load("main", source);

        Assert.Contains(result.Diagnostics, d => d.File == "lib" && d.Message.Contains("needs an endianness"));
    }

    [Fact]
    public void Load_NestedImports_AreError()
    {
        var error = SingleError("meta:\n  id: x\ntypes:\n  t:\n    meta:\n      imports: [a]\n");

        Assert.Contains("only supported in the top-level meta", error.Message);
    }

    [Fact]
    public void Load_MissingRootFile_IsError()
    {
        var result = KsyModelBuilder.Load("nope", new KsyMemoryImportSource());

        Assert.Contains("File 'nope' not found", Assert.Single(result.Diagnostics).Message);
    }
}
