# ZeroDepJson
A .NET JSON serializer/deserializer with zero dependencies.

There are already many JSON serializers available on the .NET platform. So why ZeroDepJson?

* It's only one .cs file [ZeroDepJson.cs](https://github.com/smourier/ZeroDepJson/blob/main/ZeroDepJsonCore/ZeroDepJson.cs) so it's super easy to integrate in any .NET project, .NET Framework, .NET core, .NET 5/6+, client, server, etc.
* It supports .NET framework 4.0 to .NET 6+ projects.
* Since it's one source file, you can change some parts of the code easily if you want to really customize it.
* As the name implies, it has ZERO dependency on anything except "some .NET with C#" platform.
* It's quite feature-full, more than some JSON parsers available today, it's not  "simple" nor "lightweight".


Just to make it clear, absolute performance is not a goal. Although its performance is, I hope, decent, if you're after the best performing JSON serializer / deserializer ever, then don't use this.
