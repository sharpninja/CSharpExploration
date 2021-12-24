/// <Project Sdk="Microsoft.NET.Sdk">
///  <PropertyGroup>
///    <OutputType>Exe</OutputType>
///    <TargetFramework>net6.0</TargetFramework>
///    <RootNamespace>SingleFileCSharp</RootNamespace>
///   </PropertyGroup>
///   <ItemGroup>
///     <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
///   </ItemGroup>
/// </Project>
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
