#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using ZeroDep;

namespace ZeoDepJsonTests;

public sealed class TypedDeserializationTests
{
    [TestCase("[0,1,-1,2147483647,-2147483648]")]
    [TestCase("[2147483648,-2147483649,9223372036854775807]")]
    [TestCase("[1.5,2.5,-1.5,1e2,1e-2]")]
    [TestCase("[null,true,false,\"42\",\"invalid\"]")]
    [TestCase("[]")]
    public void IntegerArraysMatchTheOriginalConversionPath(string text)
    {
        AssertEquivalent<int[]>(text);
        AssertEquivalent<List<int>>(text);
        AssertEquivalent<ICollection<int>>(text);
    }

    [TestCase("[1,2,3]")]
    [TestCase("[null,1,2]")]
    public void NullableAndOtherNumericCollectionsMatchTheOriginalPath(string text)
    {
        AssertEquivalent<int?[]>(text);
        AssertEquivalent<List<int?>>(text);
        AssertEquivalent<byte[]>(text);
        AssertEquivalent<long[]>(text);
        AssertEquivalent<decimal[]>(text);
        AssertEquivalent<double[]>(text);
        AssertEquivalent<float[]>(text);
    }

    [TestCase("[true,false,null,1,0,\"true\"]")]
    [TestCase("[]")]
    public void BooleanCollectionsMatchTheOriginalPath(string text)
    {
        AssertEquivalent<bool[]>(text);
        AssertEquivalent<bool?[]>(text);
    }

    [TestCase("[1.234567890123456789e0,1.25e2,-0,-0.0]")]
    [TestCase("[79228162514264337593543950335,79228162514264337593543950336.0]")]
    [TestCase("[0.00000000000000000000000000000000000000000000000000000000000000000000001]")]
    public void DecimalFastPathPreservesPrecisionAndOverflowConversions(string text)
    {
        AssertEquivalent<decimal[]>(text);
        var fallback = new JsonOptions { MapEntryCallback = arguments => { } };
        var expected = Json.Deserialize<decimal[]>(text, fallback);
        var actual = Json.Deserialize<decimal[]>(text);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.That(decimal.GetBits(actual[index]), Is.EqualTo(decimal.GetBits(expected[index])));
        }
    }

    [TestCase("01")]
    [TestCase("1.")]
    [TestCase("1e9999")]
    [TestCase("1true")]
    [TestCase("79228162514264337593543950336")]
    public void DecimalFastPathRejectsInvalidTokens(string text)
    {
        Assert.Throws<JsonException>(() => Json.Deserialize<decimal>(text));
    }

    [TestCase("01")]
    [TestCase("-01")]
    [TestCase("00.1")]
    [TestCase("00e1")]
    [TestCase("-")]
    [TestCase("1.")]
    [TestCase("1e+")]
    [TestCase("1e9999")]
    [TestCase("1true")]
    [TestCase("+1")]
    [TestCase(".1")]
    public void IntegerFastPathRejectsInvalidTokens(string text)
    {
        Assert.Throws<JsonException>(() => Json.Deserialize<int>(text));
        Assert.Throws<JsonException>(() => Json.Deserialize<int[]>("[" + text + "]"));
    }

    [TestCase("truefalse")]
    [TestCase("false0")]
    [TestCase("tru")]
    [TestCase("fals")]
    [TestCase("TRUE")]
    public void BooleanFastPathRejectsInvalidTokens(string text)
    {
        Assert.Throws<JsonException>(() => Json.Deserialize<bool>(text));
    }

    [TestCase("[1 2]")]
    [TestCase("[1,]")]
    [TestCase("[1,,2]")]
    [TestCase("[1")]
    [TestCase("[]true")]
    [TestCase("")]
    public void TypedArraysValidateTheWholeDocument(string text)
    {
        Assert.Throws<JsonException>(() => Json.Deserialize<int[]>(text));
    }

    [TestCase("{\"Value\":1,}")]
    [TestCase("{\"Value\":1 \"Name\":\"x\"}")]
    [TestCase("{\"unknown\":[1,]}")]
    [TestCase("{\"unknown\":{\"a\":1,}}")]
    [TestCase("{\"unknown\":[{\"a\":\"\\q\"}]}")]
    [TestCase("{\"unknown\":}")]
    [TestCase("{}[]")]
    public void TypedObjectsValidateKnownAndUnknownValues(string text)
    {
        Assert.Throws<JsonException>(() => Json.Deserialize<Model>(text));
    }

    [Test]
    public void ReadsNestedObjectsCollectionsAndUntypedMembers()
    {
        const string text = "{\"Value\":7,\"Name\":\"x\",\"Child\":{\"Value\":8},\"Values\":[1,2],\"Data\":{\"a\":[true,null]}}";
        AssertEquivalent<Model>(text);
        AssertEquivalent<Model[]>("[" + text + "," + text + "]");
        AssertEquivalent<int[][]>("[[1,2],[],[3]]");
        AssertEquivalent<object[]>("[1,\"x\",{\"a\":[2,null]}]");
    }

    [Test]
    public void PopulatesInitializedAndReadOnlyMembers()
    {
        const string text = "{\"Values\":[1,2],\"Items\":[3,4],\"Child\":{\"Value\":5}}";
        var options = new JsonOptions { SerializationOptions = JsonSerializationOptions.Default & ~JsonSerializationOptions.SkipGetOnly };
        AssertEquivalent<InitializedModel>(text, options);
        var value = Json.Deserialize<InitializedModel>(text, options);
        Assert.That(value.Values, Is.EqualTo([1, 2, 30]));
        Assert.That(value.Items, Is.EqualTo([99, 3, 4]));
        Assert.That(value.Child.Value, Is.EqualTo(5));
        Assert.That(value.SetterCalls, Is.Zero);
    }

    [Test]
    public void DictionaryKeysValuesAndDuplicateNamesMatchTheOriginalPath()
    {
        AssertEquivalent<Dictionary<int, Model>>("{\"1\":{\"Value\":2},\"1\":{\"Value\":3},\"4\":{\"Value\":5}}");
        AssertEquivalent<IReadOnlyDictionary<int, int[]>>("{\"1\":[2,3],\"4\":[5]}");
        AssertEquivalent<Model>("{\"Value\":1,\"value\":2}");
        AssertEquivalent<Model>("{\"Child\":{\"Name\":\"old\"},\"Child\":{\"Value\":2}}");
        var options = new JsonOptions { SerializationOptions = JsonSerializationOptions.Default & ~JsonSerializationOptions.SkipGetOnly };
        AssertEquivalent<InitializedModel>("{\"Values\":[1,2,3],\"Values\":[4]}", options);
        AssertEquivalent<InitializedModel>("{\"Items\":[1,2],\"Items\":[3]}", options);
        using var reader = new StringReader("[{\"Child\":{\"Name\":\"old\"},\"Child\":{\"Value\":2}}]");
        var values = Json.Deserialize<Model[]>(reader);
        Assert.That(values[0].Child!.Name, Is.Null);
        Assert.That(values[0].Child!.Value, Is.EqualTo(2));
    }

    [Test]
    public void PreservesEscapedCaseInsensitiveAndDuplicateMemberNames()
    {
        AssertEquivalent<Model>("{\"\\u0056alue\":7,\"NAME\":\"a\\n\\\"b\",\"unknown\":[{},[]]}");
        AssertEquivalent<Model>("{\"Child\":{\"Name\":\"first\"},\"child\":{\"Value\":2}}");
        AssertEquivalent<Model>("{\"Child\":{\"Name\":\"first\"},\"\\u0043hild\":{\"Value\":2}}");
    }

    [Test]
    public void PreservesSetAndNullableSetBehavior()
    {
        AssertEquivalent<HashSet<int>>("[1,1,2,null]");
        AssertEquivalent<HashSet<int?>>("[1,1,2]");
        Assert.Throws<JsonException>(() => Json.Deserialize<HashSet<int?>>("[null]"));
    }

    [Test]
    public void UnsupportedCollectionShapesRetainTheOriginalPath()
    {
        AssertEquivalent<EnumerableOnly>("[1,2]");
        AssertEquivalent<NonGenericModel>("{\"Items\":[1,2]}");
        Assert.That(Json.Deserialize<NonGenericModel>("{\"Items\":[1,2]}").Items, Is.EqualTo([99, 1, 2]));
    }

    [Test]
    public void PreservesScalarDatesEnumsAndBase64Conversion()
    {
        AssertEquivalent<string>("[1,2]");
        AssertEquivalent<string>("123");
        AssertEquivalent<decimal>("1.25");
        AssertEquivalent<decimal>("1e2");
        AssertEquivalent<DateTime>("new Date(0)");
        AssertEquivalent<DateTime?>("\"2026-10-11T12:30:00Z\"");
        AssertEquivalent<UriKind[]>("[0,1,\"Relative\"]");
        AssertEquivalent<string[]>("[\"2026-10-11T12:30:00Z\",\"text\",null]");
        var options = new JsonOptions { SerializationOptions = JsonSerializationOptions.ByteArrayAsBase64 };
        AssertEquivalent<byte[]>("\"AQIDBA==\"", options);
    }

    [Test]
    public void PreservesAliasesIgnoredMembersAndReflectionFields()
    {
        var options = new JsonOptions { SerializationOptions = JsonSerializationOptions.Default | JsonSerializationOptions.UseReflection };
        AssertEquivalent<AttributedModel>("{\"wire\":7,\"Ignored\":8,\"Field\":9}", options);
        var value = Json.Deserialize<AttributedModel>("{\"wire\":7,\"Ignored\":8,\"Field\":9}", options);
        Assert.That(value.Value, Is.EqualTo(7));
        Assert.That(value.Ignored, Is.Zero);
        Assert.That(value.Field, Is.EqualTo(9));
    }

    [Test]
    public void PreservesCallbacksAndTheirIntermediateData()
    {
        var creates = 0;
        var applies = 0;
        var options = new JsonOptions
        {
            CreateInstanceCallback = arguments =>
            {
                creates++;
                Assert.That(arguments.ObjectGraph["value"], Is.TypeOf<Dictionary<string, object>>());
            },
            ApplyEntryCallback = arguments =>
            {
                applies++;
                Assert.That(arguments.ObjectGraph["dictionary"], Is.TypeOf<Dictionary<string, object>>());
            }
        };

        var model = Json.Deserialize<Model>("{\"Value\":7}", options);
        Assert.That(model.Value, Is.EqualTo(7));
        Assert.That(creates, Is.EqualTo(1));
        Assert.That(applies, Is.EqualTo(1));
    }

    [Test]
    public void RetainsErrorCollectionAndReaderOverloads()
    {
        var options = new JsonOptions { ThrowExceptions = false };
        Json.Deserialize<Model>("{\"Value\":}", options);
        Assert.That(options.Exceptions, Is.Not.Empty);
        using var reader = new StringReader("[{\"Value\":7}]");
        Assert.That(Json.Deserialize<Model[]>(reader)[0].Value, Is.EqualTo(7));
    }

    [Test]
    public void HandlesConcurrentTypedReaderInitialization()
    {
        Parallel.For(0, 100, index =>
        {
            var values = Json.Deserialize<ConcurrentModel[]>("[{\"Values\":[1,2,3]}]");
            Assert.That(values[0].Values, Is.EqualTo([1, 2, 3]));
        });
    }

    private static void AssertEquivalent<T>(string text, JsonOptions? options = null)
    {
        options ??= new JsonOptions();
        var fallbackOptions = options.Clone();
        fallbackOptions.MapEntryCallback = arguments => { };
        var expected = Json.Deserialize<T>(text, fallbackOptions);
        var actual = Json.Deserialize<T>(text, options);
        Assert.That(Json.Serialize(actual, options), Is.EqualTo(Json.Serialize(expected, options)));
    }

    private sealed class Model
    {
        public int Value { get; set; }
        public string? Name { get; set; }
        public Model? Child { get; set; }
        public int[]? Values { get; set; }
        public object? Data { get; set; }
    }

    private sealed class InitializedModel
    {
        private readonly Model _child = new() { Value = 42 };
        public int[] Values { get; } = [10, 20, 30];
        public List<int> Items { get; } = [99];
        public int SetterCalls { get; private set; }
        public Model Child
        {
            get => _child;
            set => SetterCalls++;
        }
    }

    private sealed class AttributedModel
    {
        [Json("wire")]
        public int Value { get; set; }
        [Json(IgnoreWhenDeserializing = true)]
        public int Ignored { get; set; }
        public int Field = 0;
    }

    private sealed class ConcurrentModel
    {
        public int[] Values { get; set; } = [];
    }

    private sealed class EnumerableOnly(int value) : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator()
        {
            yield return value;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class NonGenericModel
    {
        public ArrayList Items { get; set; } = [99];
    }
}
