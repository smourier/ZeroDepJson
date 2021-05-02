using System;
using NUnit.Framework;
using ZeroDep;

namespace ZeoDepJsonTests
{
    public class SerializationTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestSimpleTypes()
        {
            Assert.AreEqual("true", Json.Serialize(true));
            Assert.AreEqual("false", Json.Serialize(false));
            Assert.AreEqual("12345678", Json.Serialize(12345678));
            Assert.AreEqual("12345678901234567890", Json.Serialize(12345678901234567890));
            Assert.AreEqual("1234567890123456789.0123456789", Json.Serialize(1234567890123456789.01234567890m));
            Assert.AreEqual("12345678", Json.Serialize((uint)12345678));
            Assert.AreEqual("128", Json.Serialize((byte)128));
            Assert.AreEqual("-56", Json.Serialize((sbyte)-56));
            Assert.AreEqual("-56", Json.Serialize((short)-56));
            Assert.AreEqual("12345", Json.Serialize((ushort)12345));
            Assert.AreEqual("\"héllo world\"", Json.Serialize("héllo world"));
            var ts = new TimeSpan(12, 34, 56, 7, 8);
            Assert.AreEqual("11625670080000", Json.Serialize(ts));
            Assert.AreEqual("\"13:10:56:07.008\"", Json.Serialize(ts, new JsonOptions { SerializationOptions = JsonSerializationOptions.TimeSpanAsText }));
            var guid = Guid.NewGuid();
            Assert.AreEqual("\"" + guid.ToString() + "\"", Json.Serialize(guid));
            Assert.AreEqual("\"https://github.com/smourier/ZeroDepJson\"", Json.Serialize(new Uri("https://github.com/smourier/ZeroDepJson")));
            Assert.AreEqual("2", Json.Serialize(UriKind.Relative));
            Assert.AreEqual("\"Relative\"", Json.Serialize(UriKind.Relative, new JsonOptions { SerializationOptions = JsonSerializationOptions.EnumAsText }));
            Assert.AreEqual("\"x\"", Json.Serialize('x'));
            Assert.AreEqual("1234.5677", Json.Serialize(1234.5678f));
            Assert.AreEqual("1234.5678", Json.Serialize(1234.5678d));
        }
    }
}