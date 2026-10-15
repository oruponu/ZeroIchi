using System.Diagnostics.CodeAnalysis;
using System.Text;
using ZeroIchi.Models.Buffers;
using ZeroIchi.Models.FileStructure;
using ZeroIchi.Models.FileStructure.Ksy.Expressions;
using ZeroIchi.Models.FileStructure.Ksy.Model;
using ZeroIchi.Models.FileStructure.Ksy.Runtime;
using ZeroIchi.Models.FileStructure.Ksy.Yaml;

namespace ZeroIchi.Tests.KaitaiConformance;

public sealed class KaitaiConformanceTests
{
    static KaitaiConformanceTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "KaitaiConformance");

    private const string CustomProcess = "Custom process routines are not supported (spec decision 1)";
    private const string OpaqueType = "Opaque types are external compiled classes; only registered native types are accepted";

    private static readonly Dictionary<string, string> Excluded = new()
    {
        ["debug_array_user_current_excluded"] = "Depends on the compiler's debug mode (arrays visible while being read)",
        ["debug_array_user_eof_exception"] = "Depends on the compiler's debug mode (partial arrays kept after an error)",
        ["expr_sizeof_value_0"] = "_sizeof of a field (not of a type) is not supported",
        ["expr_sizeof_value_sized"] = "_sizeof of a sized field is not supported (the type's static size is used)",
        ["imports_abs"] = "Imports /common/vlq_base128_le from the format gallery, which is not part of the suite",
        ["nested_types_import"] = "Kaitai sets _parent and _root to null across .ksy files; spec §3.4 keeps the creating object and the main root",
        ["opaque_external_type"] = OpaqueType,
        ["type_ternary_opaque"] = OpaqueType,
        ["params_pass_array_int"] = "Casting to an array type (.as<u2[]>) is not in the expression language",
        ["process_custom"] = CustomProcess,
        ["process_custom_no_args"] = CustomProcess,
        ["process_repeat_usertype_dynarg_custom"] = CustomProcess,
        ["process_term_struct"] = CustomProcess,
        ["str_encodings_escaping_enc"] = "Unknown encoding names are load errors (spec §3.6)",
        ["switch_manual_int_size_eos"] = "Relies on the compiler's lenient lookup of type paths",
    };

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "spec", "ks"), "*.kst").Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (!Excluded.ContainsKey(id)) data.Add(id);
        }
        return data;
    }

    [Fact]
    public void Exclusions_ReferToExistingTests()
    {
        foreach (var id in Excluded.Keys)
            Assert.True(File.Exists(Path.Combine(Root, "spec", "ks", id + ".kst")), id);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Kst_Holds(string id)
    {
        var kst = (YamlMapping)YamlLoader.Load(File.ReadAllText(Path.Combine(Root, "spec", "ks", id + ".kst"))).Root!;
        var load = KsyModelBuilder.Load(id, new SuiteImportSource());
        Assert.True(load.Format is not null, string.Join("\n", load.Diagnostics));

        var data = File.ReadAllBytes(Path.Combine(Root, "src", Text(kst.Get("data")!)));
        var run = new KsyRun(load.Format, new ArrayByteBuffer(data), KsyParseOptions.Default);
        var tree = run.Run();
        run.Thaw();

        if (kst.Get("exception") is { } exception)
        {
            AssertFailure(exception, run, tree);
            return;
        }

        Assert.True(run.Parsed, run.Failure?.Message);
        if (kst.Get("asserts") is not YamlSequence asserts) return;
        foreach (var item in asserts.Items)
            AssertHolds((YamlMapping)item, run.RootObject!);
    }

    private static void AssertFailure(YamlNode exception, KsyRun run, FileStructureNode tree)
    {
        var type = exception is YamlMapping map ? Text(map.Get("type")!) : Text(exception);
        var failure = run.Failure?.Message ?? "";
        switch (type)
        {
            case "EndOfStreamError":
                Assert.Contains("end of stream", failure, StringComparison.OrdinalIgnoreCase);
                break;
            case "UndecidedEndiannessError":
                Assert.Contains("No case matches", failure, StringComparison.Ordinal);
                break;
            default:
                Assert.StartsWith("Validation", type, StringComparison.Ordinal);
                Assert.Contains(Errors(tree), e => e.StartsWith("Validation failed", StringComparison.Ordinal)
                    || e.StartsWith("Contents mismatch", StringComparison.Ordinal));
                break;
        }
    }

    private static void AssertHolds(YamlMapping assert, KsObject root)
    {
        var actualText = Text(assert.Get("actual")!);
        if (assert.Get("exception") is not null)
        {
            var error = Record.Exception(() => Evaluate(actualText, root));
            Assert.True(error is KsExpressionException or KsyParseException, $"{actualText}: expected an error");
            return;
        }

        var actual = Evaluate(actualText, root);
        var expectedText = Text(assert.Get("expected")!);
        if (expectedText == "null")
        {
            Assert.True(actual.Kind == KsValueKind.Null, $"{actualText}: expected null, got {Show(actual)}");
            return;
        }

        var expected = Evaluate(expectedText, root);
        Assert.True(Same(actual, expected), $"{actualText}: expected {Show(expected)}, got {Show(actual)}");
    }

    private static KsValue Evaluate(string text, KsObject root) =>
        KsEvaluator.Evaluate(KsExpressionParser.Parse(text), root);

    private static string Text(YamlNode node) =>
        node is YamlScalar { Value.Kind: not YamlScalarKind.String } scalar ? scalar.Value.ToExpressionSource() : ((YamlScalar)node).Text;

    private static IEnumerable<string> Errors(FileStructureNode node)
    {
        if (node.Error is not null) yield return node.Error;
        foreach (var child in node.Children)
        {
            foreach (var error in Errors(child)) yield return error;
        }
    }

    private static bool Same(KsValue actual, KsValue expected)
    {
        switch (actual.Kind, expected.Kind)
        {
            case (KsValueKind.Array, KsValueKind.Array):
                return actual.AsArray.Count == expected.AsArray.Count
                    && actual.AsArray.Zip(expected.AsArray).All(p => Same(p.First, p.Second));
            case (KsValueKind.Array, KsValueKind.Bytes):
                return Same(actual, KsValue.FromArray(expected.AsBytes.ToArray().Select(b => KsValue.FromInt(b)).ToList()));
            case (KsValueKind.Enum, KsValueKind.Int):
                return actual.EnumValue == expected.AsInt;
            case (KsValueKind.Float, KsValueKind.Int or KsValueKind.Float) or (KsValueKind.Int, KsValueKind.Float):
            {
                var x = actual.Kind == KsValueKind.Int ? (double)actual.AsInt : actual.AsFloat;
                var y = expected.Kind == KsValueKind.Int ? (double)expected.AsInt : expected.AsFloat;
                return Math.Abs(x - y) <= 1e-6 * Math.Max(1, Math.Abs(y));
            }
        }

        try
        {
            return KsEvaluator.ValuesEqual(new KsName("actual", 0, 0), actual, expected);
        }
        catch (KsExpressionException)
        {
            return false;
        }
    }

    private static string Show(KsValue value) => value.Kind switch
    {
        KsValueKind.Bytes => "[" + Convert.ToHexString(value.AsBytes.ToArray()) + "]",
        KsValueKind.Array => "[" + string.Join(", ", value.AsArray.Select(Show)) + "]",
        _ => $"{KsValue.KindName(value.Kind)} {value}",
    };

    private sealed class SuiteImportSource : IKsyImportSource
    {
        public bool TryRead(string path, [NotNullWhen(true)] out string? text)
        {
            foreach (var directory in new[] { "formats", Path.Combine("formats", "ks_path") })
            {
                var file = Path.Combine(Root, directory, path + ".ksy");
                if (File.Exists(file))
                {
                    text = File.ReadAllText(file);
                    return true;
                }
            }
            text = null;
            return false;
        }
    }
}
