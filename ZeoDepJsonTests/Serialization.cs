using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
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
            Assert.That("true", Is.EqualTo(Json.Serialize(true)));
            Assert.That("false", Is.EqualTo(Json.Serialize(false)));
            Assert.That("12345678", Is.EqualTo(Json.Serialize(12345678)));
            Assert.That("12345678901234567890", Is.EqualTo(Json.Serialize(12345678901234567890)));
            Assert.That("1234567890123456789.0123456789", Is.EqualTo(Json.Serialize(1234567890123456789.01234567890m)));
            Assert.That("12345678", Is.EqualTo(Json.Serialize((uint)12345678)));
            Assert.That("128", Is.EqualTo(Json.Serialize((byte)128)));
            Assert.That("-56", Is.EqualTo(Json.Serialize((sbyte)-56)));
            Assert.That("-56", Is.EqualTo(Json.Serialize((short)-56)));
            Assert.That("12345", Is.EqualTo(Json.Serialize((ushort)12345)));
            Assert.That("\"héllo world\"", Is.EqualTo(Json.Serialize("héllo world")));
            var ts = new TimeSpan(12, 34, 56, 7, 8);
            Assert.That("11625670080000", Is.EqualTo(Json.Serialize(ts)));
            Assert.That("\"13:10:56:07.008\"", Is.EqualTo(Json.Serialize(ts, new JsonOptions { SerializationOptions = JsonSerializationOptions.TimeSpanAsText })));
            var guid = Guid.NewGuid();
            Assert.That("\"" + guid.ToString() + "\"", Is.EqualTo(Json.Serialize(guid)));
            Assert.That("\"https://github.com/smourier/ZeroDepJson\"", Is.EqualTo(Json.Serialize(new Uri("https://github.com/smourier/ZeroDepJson"))));
            Assert.That("2", Is.EqualTo(Json.Serialize(UriKind.Relative)));
            Assert.That("\"Relative\"", Is.EqualTo(Json.Serialize(UriKind.Relative, new JsonOptions { SerializationOptions = JsonSerializationOptions.EnumAsText })));
            Assert.That("\"x\"", Is.EqualTo(Json.Serialize('x')));
            Assert.That("1234.5677", Is.EqualTo(Json.Serialize(1234.5678f)));
            Assert.That("1234.5678", Is.EqualTo(Json.Serialize(1234.5678d)));
        }

        [Test]
        public void TestList()
        {
            var list = new List<Customer>();
            for (var i = 0; i < 10; i++)
            {
                var customer = new Customer();
                customer.Index = i;
                list.Add(customer);

                var address1 = new Address();
                address1.ZipCode = 75000;
                address1.City = new City();
                address1.City.Name = "Paris";
                address1.City.Country = new Country();
                address1.City.Country.Name = "France";

                var address2 = new Address();
                address2.ZipCode = 10001;
                address2.City = new City();
                address2.City.Name = "New York";
                address2.City.Country = new Country();
                address2.City.Country.Name = "USA";

                customer.Addresses = new[] { address1, address2 };

                customer.GeoPoints = new List<GeoPoint> {
                    new GeoPoint { Latitude = 48.34266, Longitude = 7.30574 },
                    new GeoPoint { Latitude = 28.20042, Longitude = 15.99122 }
                };
            }

            var json = Json.Serialize(list);
            var list2 = Json.Deserialize<List<Customer>>(json);
            var json2 = Json.Serialize(list2);
            Assert.That(json, Is.EqualTo(json2));
        }

        [Test]
        public void TestDictionary()
        {
            var dic = new Dictionary<Guid, Customer>();
            for (var i = 0; i < 10; i++)
            {
                var customer = new Customer();
                customer.Index = i;
                customer.Name = "This is a name 这是一个名字" + Environment.TickCount;
                var address1 = new Address();
                address1.ZipCode = 75000;
                address1.City = new City();
                address1.City.Name = "Paris";
                address1.City.Country = new Country();
                address1.City.Country.Name = "France";

                var address2 = new Address();
                address2.ZipCode = 10001;
                address2.City = new City();
                address2.City.Name = "New York";
                address2.City.Country = new Country();
                address2.City.Country.Name = "USA";

                customer.Addresses = new[] { address1, address2 };

                customer.GeoPoints = new List<GeoPoint> {
                    new GeoPoint { Latitude = 48.34266, Longitude = 7.30574 },
                    new GeoPoint { Latitude = 28.20042, Longitude = 15.99122 }
                };

                dic[customer.Id] = customer;
            }

            var json1 = Json.Serialize(dic);
            var list2 = (Dictionary<string, object>)Json.Deserialize(json1);
            var json2 = Json.Serialize(list2);
            Assert.That(json1, Is.EqualTo(json2));

            var customers = list2.Values.Cast<Dictionary<string, object>>().ToList();
            var json3 = Json.Serialize(customers);
            var list3 = Json.Deserialize<List<Customer>>(json3);
            var json4 = Json.Serialize(list3);
            Assert.That(json3, Is.EqualTo(json4));
        }

        [Test]
        public void TestCyclic()
        {
            var person = new Person { Name = "foo" };
            var persons = new Person[] { person, person };
            try
            {
                var json = Json.Serialize(persons);
                Assert.Fail();
            }
            catch (JsonException ex)
            {
                Assert.That(ex.Code, Is.EqualTo(9));
            }
        }

        [Test]
        public void TestCyclicCustom()
        {
            var person = new Person { Name = "héllo" };
            var persons = new Person[] { person, person };
            var options = new CustomOptions();
            var json = Json.Serialize(persons, options);
            Assert.That(json, Is.EqualTo("[{\"Name\":\"héllo\"},{\"Name\":\"héllo\"}]"));
        }
    }

    class CustomOptions : JsonOptions
    {
        public CustomOptions()
        {
            ObjectGraph = new CustomObjectGraph();
        }

        private class CustomObjectGraph : IDictionary<object, object>, Json.IOptionsHolder
        {
            private readonly Dictionary<object, int> _hash = new Dictionary<object, int>();

            public JsonOptions Options { get; set; }

            public void Add(object key, object value)
            {
                _hash[key] = Options.SerializationLevel;
            }

            public bool ContainsKey(object key)
            {
                if (!_hash.TryGetValue(key, out var level))
                    return false;

                if (Options.SerializationLevel == level)
                    return false;

                return true;
            }

            public object this[object key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public ICollection<object> Keys => throw new NotImplementedException();
            public ICollection<object> Values => throw new NotImplementedException();
            public int Count => throw new NotImplementedException();
            public bool IsReadOnly => throw new NotImplementedException();
            public void Add(KeyValuePair<object, object> item) => throw new NotImplementedException();
            public void Clear() => throw new NotImplementedException();
            public bool Contains(KeyValuePair<object, object> item) => throw new NotImplementedException();
            public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex) => throw new NotImplementedException();
            public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => throw new NotImplementedException();
            public bool Remove(object key) => throw new NotImplementedException();
            public bool Remove(KeyValuePair<object, object> item) => throw new NotImplementedException();
            public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        }
    }

    class Person
    {
        public string Name { get; set; }
    }

    public class Customer
    {
        public Customer()
        {
            Id = Guid.NewGuid();

        }

        public Guid Id { get; }
        public int Index { get; set; }
        public string Name { get; set; }

        public Address[] Addresses { get; set; }
        public IReadOnlyList<GeoPoint> GeoPoints { get; set; }

        public override string ToString() => Name;
    }

    public class Address
    {
        public City City { get; set; }
        public int ZipCode { get; set; }

        public override string ToString() => ZipCode.ToString();
    }

    public class City
    {
        public string Name { get; set; }
        public Country Country { get; set; }

        public override string ToString() => Name;
    }

    public class Country
    {
        public string Name { get; set; }

        public override string ToString() => Name;
    }

    public class GeoPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public override string ToString() => Latitude.ToString(CultureInfo.InvariantCulture) + "," + Longitude.ToString(CultureInfo.InvariantCulture);
    }
}