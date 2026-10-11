#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using ZeroDep;

namespace ZeoDepJsonTests;

public sealed class DeserializationPerformanceTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10000)]
    public void ArrayBuffersPreserveValuesAcrossCapacityBoundaries(int count)
    {
        var expected = Enumerable.Range(0, count).ToArray();
        var text = Json.Serialize(expected);
        Assert.That(Json.Deserialize<int[]>(text), Is.EqualTo(expected));
        var referenceValues = expected.Select(value => value.ToString()).ToArray();
        Assert.That(Json.Deserialize<string[]>(Json.Serialize(referenceValues)), Is.EqualTo(referenceValues));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(8)]
    [TestCase(9)]
    public void ArrayBuffersReuseInitializedArraysAndPreserveTheirTrailingValues(int count)
    {
        var values = Enumerable.Range(0, count).ToArray();
        var text = "{\"Values\":" + Json.Serialize(values) + "}";
        var fallback = new JsonOptions { MapEntryCallback = arguments => { } };
        var expected = Json.Deserialize<ArrayModel>(text, fallback);
        var actual = Json.Deserialize<ArrayModel>(text);
        Assert.That(actual.Values, Is.EqualTo(expected.Values));
        Assert.That(actual.ReusesOriginal, Is.EqualTo(count <= ArrayModel.Capacity));
    }

    [Test]
    public void ArrayBuffersPreserveNullElements()
    {
        Assert.That(Json.Deserialize<string?[]>("[null,\"a\",null,\"b\",null]"),
            Is.EqualTo([null, "a", null, "b", null]));
        Assert.That(Json.Deserialize<int?[]>("[null,1,null,2,null]"),
            Is.EqualTo(new int?[] { null, 1, null, 2, null }));
    }

    [Test]
    public void ReaderCachesHandleRecursiveCollectionTypes()
    {
        var dictionary = Json.Deserialize<RecursiveDictionary>("{\"a\":{\"b\":{}}}");
        Assert.That(dictionary["a"]["b"], Is.Empty);
        var list = Json.Deserialize<RecursiveList>("[[[]]]");
        Assert.That(list[0][0], Is.Empty);
    }

    [Test]
    public void WhitespaceScanningPreservesValidation()
    {
        Assert.That(Json.Deserialize<int[]>(" \t\r\n[ \t1\r\n, 2 \t] \r\n"), Is.EqualTo([1, 2]));
        Assert.Throws<JsonException>(() => Json.Deserialize<int[]>("[1,2]\u00a0"));
        Assert.Throws<JsonException>(() => Json.Deserialize<int[]>("[1,\u00a02]"));
    }

    [TestCase("en-US")]
    [TestCase("fr-FR")]
    [TestCase("tr-TR")]
    public void DateDetectionHandlesOrdinaryStringsAndSupportedFormats(string cultureName)
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(cultureName);
            Assert.That(Json.TryParseDateTime(nameof(DeserializationPerformanceTests), out _), Is.False);
            var expected = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(Json.Deserialize<object>("\"new Date(0)\""), Is.EqualTo(expected));
            Assert.That(Json.Deserialize<object>("\"/Date(0)/\""), Is.EqualTo(expected));
            Assert.That(Json.Deserialize<object>("\"1970-01-01T00:00:00Z\""), Is.EqualTo(expected));
            Assert.That(Json.Deserialize<string>("\"ordinary text\""), Is.EqualTo("ordinary text"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Test]
    public void DefaultDescriptorsPreserveReadOnlyAttributes()
    {
        var options = new JsonOptions
        {
            SerializationOptions = JsonSerializationOptions.Default & ~JsonSerializationOptions.SkipGetOnly
        };

        var value = Json.Deserialize<ReadOnlyModel>("{\"Value\":9}", options);
        Assert.That(value.Value, Is.EqualTo(7));
    }

    [Test]
    public void DefaultDescriptorsPreserveValueChangedNotifications()
    {
        var value = Json.Deserialize<NotificationModel>("{\"Value\":9}");
        try
        {
            Assert.That(value.Value, Is.EqualTo(9));
            Assert.That(value.Notifications, Is.EqualTo(1));
        }
        finally
        {
            value.Detach();
        }
    }

    [Test]
    public void DefaultDescriptorsPreserveSetterExceptions()
    {
        Assert.Throws<InvalidOperationException>(() => Json.Deserialize<ThrowingModel>("{\"Value\":9}"));
    }

    [Test]
    public void CustomDescriptorsKeepTheirReadOnlyGuard()
    {
        var options = new JsonOptions
        {
            SerializationOptions = JsonSerializationOptions.Default & ~JsonSerializationOptions.SkipGetOnly
        };

        var value = Json.Deserialize<CustomModel>("{\"Value\":9}", options);
        Assert.That(value.Value, Is.EqualTo(7));
    }

    private sealed class ArrayModel
    {
        public const int Capacity = 8;
        private readonly int[] _original = Enumerable.Repeat(-1, Capacity).ToArray();

        public ArrayModel()
        {
            Values = _original;
        }

        public int[] Values { get; set; }
        public bool ReusesOriginal => ReferenceEquals(Values, _original);
    }

    private sealed class RecursiveDictionary : Dictionary<string, RecursiveDictionary>
    {
    }

    private sealed class RecursiveList : List<RecursiveList>
    {
    }

    private sealed class ReadOnlyModel
    {
        [ReadOnly(true)]
        public int Value { get; set; } = 7;
    }

    private sealed class NotificationModel
    {
        public NotificationModel()
        {
            TypeDescriptor.GetProperties(GetType())[nameof(Value)]!.AddValueChanged(this, OnValueChanged);
        }

        public int Value { get; set; }
        public int Notifications { get; private set; }

        public void Detach() => TypeDescriptor.GetProperties(GetType())[nameof(Value)]!.RemoveValueChanged(this, OnValueChanged);

        private void OnValueChanged(object? sender, EventArgs arguments)
        {
            Notifications++;
        }
    }

    private sealed class ThrowingModel
    {
        public int Value
        {
            get => 0;
            set => throw new InvalidOperationException();
        }
    }

    [TypeDescriptionProvider(typeof(CustomProvider))]
    private sealed class CustomModel
    {
        public int Value { get; set; } = 7;
    }

    private sealed class CustomProvider() : TypeDescriptionProvider(TypeDescriptor.GetProvider(typeof(object)))
    {
        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance) =>
            new CustomDescriptor(base.GetTypeDescriptor(objectType, instance)!);
    }

    private sealed class CustomDescriptor(ICustomTypeDescriptor parent) : CustomTypeDescriptor(parent)
    {
        public override PropertyDescriptorCollection GetProperties() =>
            new([new ReadOnlyDescriptor(base.GetProperties()[nameof(CustomModel.Value)]!)], true);
    }

    private sealed class ReadOnlyDescriptor(PropertyDescriptor descriptor) : PropertyDescriptor(descriptor)
    {
        public override Type ComponentType => descriptor.ComponentType;
        public override Type PropertyType => descriptor.PropertyType;
        public override bool IsReadOnly => true;
        public override bool CanResetValue(object component) => false;
        public override object? GetValue(object? component) => descriptor.GetValue(component);
        public override void ResetValue(object component) => throw new InvalidOperationException();
        public override void SetValue(object? component, object? value) => throw new InvalidOperationException();
        public override bool ShouldSerializeValue(object component) => true;
    }
}
