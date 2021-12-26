// ReSharper disable EmptyNamespace

/// Project:
///  PropertyGroup:
///    OutputType: Exe
///    TargetFramework: net6.0
///    RootNamespace: SingleFileCSharp
///  ItemGroup:
///    PackageReference:
///      _Include: Newtonsoft.Json
///      _Version: 13.0.1
///  _Sdk: Microsoft.NET.Sdk
namespace SingleFileCSharp;

using System;

using Newtonsoft.Json;

public static class Program
{
    public static void Main(string[] args)
    {
        string message = JsonConvert.SerializeObject(
            new[]
            {
                "Hello, World",
            }
        );

        Console.WriteLine(message);
    }
}
