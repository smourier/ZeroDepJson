#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ZeroDep;

namespace ZeoDepJsonTests;

public sealed class RegressionTests
{
    [TestCase("[1 2]")]
    [TestCase("[1,,2]")]
    [TestCase("[1,]")]
    [TestCase("[,1]")]
    [TestCase("{\"a\":1 \"b\":2}")]
    [TestCase("{\"a\":1,}")]
    [TestCase("{,\"a\":1}")]
    [TestCase("{\"a\":}")]
    [TestCase("{\"a\":")]
    [TestCase("[true false]")]
    [TestCase("[null null]")]
    [TestCase("true false")]
    [TestCase("{}[]")]
    [TestCase("01")]
    [TestCase("-01")]
    [TestCase("+1")]
    [TestCase(".1")]
    [TestCase("1.")]
    [TestCase("1e")]
    [TestCase("1e+")]
    [TestCase("1e9999")]
    [TestCase("TRUE")]
    [TestCase("NULL")]
    [TestCase("\"\\q\"")]
    [TestCase("\"raw\nline\"")]
    [TestCase("\"\\u12\"")]
    [TestCase("\"unterminated")]
    [TestCase("[1")]
    [TestCase("{\"a\":1")]
    [TestCase("\u00a01")]
    [TestCase("1\u00a0")]
    [TestCase("")]
    [TestCase(" ")]
    public void RejectsMalformedJson(string text)
    {
        Assert.Throws<JsonException>(() => Json.Deserialize(text));
    }

    [TestCase("[1,,2]")]
    [TestCase("{\"a\":}")]
    [TestCase("\"\\q\"")]
    [TestCase("[null null]")]
    public void RecordsMalformedJsonWhenExceptionsAreDisabled(string text)
    {
        var options = new JsonOptions { ThrowExceptions = false };
        Json.Deserialize(text, options: options);
        Assert.That(options.Exceptions, Is.Not.Empty);
    }

    [Test]
    public void ReadsNestedValuesAndEscapes()
    {
        var value = Json.Deserialize<Dictionary<string, object>>("{\"a\":[1,null,true,[2],{\"s\":\"\\u0041\\n\\\\\\\"\"}]}");
        var array = (object[])value["a"];
        Assert.That(array.Length, Is.EqualTo(5));
        Assert.That(array[1], Is.Null);
        Assert.That(array[2], Is.True);
        Assert.That(((object[])array[3])[0], Is.EqualTo(2));
        Assert.That(((Dictionary<string, object>)array[4])["s"], Is.EqualTo("A\n\\\""));
    }

    [TestCase("-0", 0d)]
    [TestCase("-1.25", -1.25d)]
    [TestCase("1e+2", 100d)]
    [TestCase("1E-2", 0.01d)]
    public void ReadsValidNumbers(string text, double expected)
    {
        Assert.That(Json.Deserialize<double>(text), Is.EqualTo(expected));
    }

    [Test]
    public void DeserializesTypedDictionaryKeysAndValues()
    {
        var values = Json.Deserialize<Dictionary<int, Model>>("{\"12\":{\"Value\":34}}");
        Assert.That(values[12].Value, Is.EqualTo(34));
        var identity = Guid.NewGuid();
        var source = new Dictionary<Guid, int> { [identity] = 7 };
        var roundTrip = Json.Deserialize<Dictionary<Guid, int>>(Json.Serialize(source));
        Assert.That(roundTrip[identity], Is.EqualTo(7));
    }

    [Test]
    public void DeserializesCollectionAndDictionaryInterfaces()
    {
        Assert.That(Json.Deserialize<ICollection<int>>("[1,2]"), Is.EqualTo([1, 2]));
        Assert.That(Json.Deserialize<IReadOnlyCollection<int>>("[1,2]"), Is.EqualTo([1, 2]));
        Assert.That(Json.Deserialize<IList<int>>("[1,2]"), Is.EqualTo([1, 2]));
        Assert.That(Json.Deserialize<IReadOnlyList<int>>("[1,2]"), Is.EqualTo([1, 2]));
        Assert.That(Json.Deserialize<IEnumerable<int>>("[1,2]"), Is.EqualTo([1, 2]));
        Assert.That(Json.Deserialize<IReadOnlyDictionary<int, int>>("{\"1\":2}")[1], Is.EqualTo(2));
        Assert.That(Json.Deserialize<IDictionary<int, int>>("{\"1\":2}")[1], Is.EqualTo(2));
        Assert.That(Json.Deserialize<IList>("[1,2]"), Is.EqualTo([1, 2]));
    }

    [Test]
    public void DeserializesIntoAnExistingTargetWithDefaultOptions()
    {
        var model = new Model();
        Json.DeserializeToTarget("{\"Value\":42}", model);
        Assert.That(model.Value, Is.EqualTo(42));
        Assert.That(Json.Deserialize<int>((string)null!), Is.Zero);
        Assert.That(Json.Deserialize<int?>((string)null!), Is.Null);
    }

    [Test]
    public void AppliesValuesToFixedSizeArrays()
    {
        var target = new int[2];
        Json.DeserializeToTarget("[1,2,3]", target);
        Assert.That(target, Is.EqualTo([1, 2]));
        var offsetTarget = Array.CreateInstance(typeof(int), [2], [5]);
        Json.Apply(new[] { 7, 8, 9 }, offsetTarget);
        Assert.That(offsetTarget.GetValue(5), Is.EqualTo(7));
        Assert.That(offsetTarget.GetValue(6), Is.EqualTo(8));
    }

    [Test]
    public void DetectsArrayCyclesAndRestoresSerializationLevel()
    {
        var array = new object[1];
        array[0] = array;
        var options = new JsonOptions();
        var exception = Assert.Throws<JsonException>(() => Json.Serialize(array, options));
        Assert.That(exception!.Code, Is.EqualTo(9));
        Assert.That(options.SerializationLevel, Is.Zero);
        options.SerializationOptions |= JsonSerializationOptions.ContinueOnCycle;
        Assert.That(Json.Serialize(array, options), Is.EqualTo("[null]"));
        Assert.That(options.SerializationLevel, Is.Zero);
    }

    [Test]
    public void PreservesEmptyEscapedStrings()
    {
        Assert.That(Json.EscapeString(string.Empty), Is.EqualTo(string.Empty));
        Assert.That(Json.Serialize(string.Empty), Is.EqualTo("\"\""));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(511)]
    [TestCase(512)]
    [TestCase(513)]
    [TestCase(4097)]
    public void StreamsBase64AcrossPartialReads(int length)
    {
        var bytes = Enumerable.Range(0, length).Select(index => (byte)index).ToArray();
        using var stream = new PartialReadStream(bytes);
        using var writer = new StringWriter();
        var options = new JsonOptions { StreamingBufferChunkSize = 512 };
        var count = Json.WriteBase64Stream(writer, stream, null, options);
        Assert.That(count, Is.EqualTo(length));
        Assert.That(writer.ToString(), Is.EqualTo("\"" + Convert.ToBase64String(bytes) + "\""));
        Assert.That(stream.CanRead, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Base64RespectsWriterEncoding(bool unicode)
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var encoding = unicode ? Encoding.Unicode : new UTF8Encoding(false);
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using (var writer = new StreamWriter(output, encoding, 1024, true))
        {
            writer.Write('[');
            Json.WriteBase64Stream(writer, input, null);
            writer.Write(']');
        }

        output.Position = 0;
        using var reader = new StreamReader(output, encoding);
        Assert.That(reader.ReadToEnd(), Is.EqualTo("[\"AQIDBAU=\"]"));
    }

    [Test]
    public void PreservesJavaScriptDateExtension()
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(Json.Deserialize("new Date(0)"), Is.EqualTo(epoch));
        Assert.That(Json.Deserialize<object[]>("[new Date(0),null]")[0], Is.EqualTo(epoch));
    }

    [Test]
    public void InvalidQuotedDateReturnsFalse()
    {
        Assert.That(Json.TryParseDateTime("\"\\q\"", out _), Is.False);
    }

    [TestCase("/Date(9223372036854775807)/")]
    [TestCase("/Date(253402300800000)/")]
    [TestCase("/Date(-62135596800001)/")]
    public void DateParsingReturnsFalseForOutOfRangeMilliseconds(string text)
    {
        Assert.That(Json.TryParseDateTime(text, out _), Is.False);
    }

    [TestCase("2026-10-11T12:30:00+0200", 10)]
    [TestCase("2026-10-11T12:30:00-0200", 14)]
    public void ConvertsCompactDateOffsetsToUtc(string text, int hour)
    {
        Assert.That(Json.TryParseDateTime(text, out var value), Is.True);
        Assert.That(value, Is.EqualTo(new DateTime(2026, 10, 11, hour, 30, 0, DateTimeKind.Utc)));
        Assert.That(value.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void ContinuesAfterGetterErrorsWhenRequested()
    {
        var options = new JsonOptions { SerializationOptions = JsonSerializationOptions.ContinueOnValueError };
        Assert.That(Json.Serialize(new ThrowingModel(), options), Is.EqualTo("{\"Value\":7}"));
        Assert.That(options.SerializationLevel, Is.Zero);
    }

    [Test]
    public void ReflectionHonorsSkipGetOnlyAndIgnoresIndexers()
    {
        var options = new JsonOptions
        {
            SerializationOptions = JsonSerializationOptions.UseReflection | JsonSerializationOptions.SkipGetOnly
        };

        Assert.That(Json.Serialize(new ReflectionModel(), options), Is.EqualTo("{\"Value\":7}"));
        Assert.That(Json.Deserialize<ReflectionModel>("{\"Value\":9,\"Item\":4}", options).Value, Is.EqualTo(9));
    }

    [Test]
    public void IdentifiesElementsOfCollectionInterfaces()
    {
        Assert.That(Json.GetItemType(typeof(IEnumerable<int>)), Is.EqualTo(typeof(int)));
        Assert.That(Json.GetItemType(typeof(IDictionary<string, int>)), Is.EqualTo(typeof(int)));
        Assert.That(Json.GetItemType(typeof(int[])), Is.EqualTo(typeof(int)));
    }

    private sealed class ThrowingModel
    {
        public int Error => throw new InvalidOperationException();
        public int Value => 7;
    }

    private sealed class ReflectionModel
    {
        public int ReadOnlyValue => 3;
        public int Value { get; set; } = 7;
        public int this[int index] => index;
    }

    private sealed class Model
    {
        public int Value { get; set; }
    }

    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, Math.Min(count, 2));
        }
    }
}
