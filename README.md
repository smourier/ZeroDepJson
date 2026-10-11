# ZeroDepJson

A .NET JSON serializer and deserializer with zero dependencies, implemented in a single C# source file.

## Why ZeroDepJson?

ZeroDepJson is useful when you want flexible JSON handling that lives directly in your own codebase.
It combines practical performance with easy integration, extensive customization and direct control over the implementation.

* **Simple integration.** Copy [ZeroDepJson.cs](ZeroDepJsonCore/ZeroDepJson.cs) into your project.
  No serializer package or external dependency is required. **You just need C# and a compatible .NET runtime.**
* **Source you own.** Inspect, debug and adapt serialization behavior in the same codebase as your application.
  You can apply a focused change without maintaining a separate library build or waiting for a package release.
* **Flexible data handling.** Read typed models or untyped dictionaries, arrays and scalar values.
  Populate existing objects when creating a replacement instance does not suit your application.
* **Customization at several levels.** Use options and attributes for common changes, callbacks for specific operations,
  or derive from `JsonOptions` to customize the member definitions used for serialization and deserialization.
* **A choice of member access.** Use property descriptors when component-model metadata matters,
  or select `UseReflection` for direct CLR member access and better performance on ordinary models.
* **Competitive performance on useful workloads.** The measured integer array path is close to System.Text.Json and faster than Newtonsoft.Json.
  With `UseReflection`, nested object deserialization also beats Newtonsoft.Json on the benchmark below.

### Versatility in practice

The library provides several ways to adapt JSON handling to the surrounding application:

* `CreateInstanceCallback`, `MapEntryCallback` and `ApplyEntryCallback` let you customize object creation and incoming member handling.
* Writing callbacks can adjust values and named members, or handle an operation themselves.
* `DeserializeToTarget` and `Apply` can populate existing objects, dictionaries and collections.
* `TryGetValueByPath<T>` retrieves and converts nested dictionary values without requiring a dedicated model for every document shape.
* Options control date and enum formats, null and default value omission, ignored members, fields and base64 representations.
* `TextReader` and `TextWriter` overloads integrate with existing I/O code. `WriteBase64Stream` writes binary streams as base64.
* Error collection is available through `ThrowExceptions` and `Exceptions`, alongside options for getter errors and object cycles.

These capabilities are available in the same source file. You can use the built-in behavior, extend its public hooks,
or adapt the implementation itself to a specialized format or integration.

The solution targets .NET Framework 4.8 and .NET 10.
The current source uses `Array.Empty<T>()`, so integrations with older .NET Framework projects require version 4.6 or later.

On .NET 8 and later, `FormatterConverter` and `ISerializable` support are excluded.
See Microsoft's [SYSLIB0050 guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0050).

## Basic usage

```csharp
using ZeroDep;

var json = Json.Serialize(new[] { 1, 2, 3 });
var values = Json.Deserialize<int[]>(json);
```

Passing a target type enables typed deserialization for supported shapes.
Calling `Json.Deserialize(json)` without a target type returns an untyped representation.

## Typed deserialization

Supported typed objects, generic dictionaries and collections are populated directly while parsing.
This avoids building an intermediate untyped graph of dictionaries and object arrays before converting it to the requested type.
Unknown members are validated and skipped without constructing their complete object graph.

The implementation also reduces temporary allocations and repeated work:

* Integer parsing avoids temporary numeric strings on the common path.
* Known property names can reuse cached schema names.
* Arrays hold their first four elements inline before allocating a growth buffer. Final arrays retain their requested element type.
* Collection and dictionary readers cache their element readers.
* Date prefix checks use ordinal comparisons, and whitespace scanning avoids a predicate delegate call.
* Built-in property descriptors retain their setter behavior while avoiding a duplicate external read-only check.

Compatibility paths preserve existing behavior for custom options, deserialization callbacks, error collection and unsupported shapes.
Duplicate complex properties replay through the original parser to preserve replacement semantics.
`DeserializeToTarget` continues to use the original population path.

## Performance

The following measurements were taken on Windows x64 with .NET 10.0.12 in Release mode.
They describe these payloads on one machine. Workload shape, options and runtime can change the results.

Typed deserialization time per complete operation:

| Implementation | 10,000 integers | 1,000 nested objects | Nested object allocation |
| :--- | ---: | ---: | ---: |
| ZeroDepJson typed baseline | 0.143 ms | 1.693 ms | 672,072 bytes |
| ZeroDepJson optimized default | 0.138 ms | 0.940 ms | 600,018 bytes |
| ZeroDepJson optimized with `UseReflection` | 0.136 ms | 0.627 ms | 375,826 bytes |
| Newtonsoft.Json 13.0.4 | 0.273 ms | 0.781 ms | 497,872 bytes |
| System.Text.Json | 0.130 ms | 0.432 ms | 376,304 bytes |
| System.Text.Json with source generation | 0.129 ms | 0.428 ms | 376,304 bytes |

Performance depends on the workload and the selected member access strategy:

* Integer array deserialization is roughly twice as fast as Newtonsoft.Json and close to System.Text.Json on this payload.
* With `UseReflection`, nested object deserialization takes about 20 percent less time than Newtonsoft.Json on this payload.
  It also allocates about 25 percent fewer bytes.
* The default nested object path supports property descriptor behavior and takes longer than Newtonsoft.Json on this payload.
* System.Text.Json remains faster for nested objects in both of its measured configurations.

The optimized default path takes 44.5 percent less time than the typed baseline for nested objects, with 10.7 percent fewer allocated bytes.

The nested payload contains 1,000 objects with integer, string, decimal and Boolean properties.
Each object also contains an address with two properties and a three-element integer array.

Measurements use three independent processes. Each implementation warms for at least one second and 100 operations.
Each process measures five rotating batches of 50 operations. Results are medians of the three process medians.
Allocations use `GC.GetAllocatedBytesForCurrentThread()`. Tracing is disabled during timing.
Serialization output and deserialized contents are validated before measurement.
Newtonsoft.Json and System.Text.Json reuse their configured settings or options. Metadata caches are warm.

These changes optimize deserialization. They do not establish a serialization speed improvement.
ZeroDepJson offers a balance of throughput, integration simplicity and control over serialization behavior.
Choose it when owning and adapting the implementation, or using its flexible data handling, is valuable to your application.
Measure your own payloads when throughput or allocation is a deciding factor.

### Using reflection accessors

The existing `UseReflection` option selects reflection metadata and member accessors instead of property descriptors:

```csharp
var options = new JsonOptions
{
    SerializationOptions = JsonSerializationOptions.Default | JsonSerializationOptions.UseReflection
};

var value = Json.Deserialize<YourModel>(json, options);
```

Replace `YourModel` with your application's target type.
Combining the flag with `Default` retains the other default serialization options.

This option reduces descriptor dispatch overhead for ordinary CLR models, as shown in the table.
It can change behavior for custom property descriptors and descriptor notifications.
Choose it when reflection metadata and accessor behavior match your application.

## Validation

* All 155 tests pass on .NET 10 in Debug and Release configurations.
* The solution builds for .NET 10 and .NET Framework 4.8 with zero warnings and zero errors.
* Additional .NET Framework 4.8 runtime checks cover arrays, dates, read-only properties and descriptor notifications.
