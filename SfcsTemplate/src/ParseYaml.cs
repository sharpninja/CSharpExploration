// ReSharper disable EmptyNamespace

/// <Project Sdk="Microsoft.NET.Sdk">
/// 
///  <PropertyGroup>
///    <OutputType>Exe</OutputType>
///    <TargetFramework>net6.0</TargetFramework>
///    <RootNamespace>ParseYaml</RootNamespace>
///   </PropertyGroup>
/// 
///   <ItemGroup>
///    <PackageReference Include="YamlDotNet" Version="11.2.1" />
///    <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
///   </ItemGroup>
/// 
/// </Project>
namespace ParseYaml;

using System;
using System.IO;
using System.Xml.Linq;

using Newtonsoft.Json;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static class Program
{
  private const string YAML =
    @"Project:
  _Sdk: Microsoft.NET.Sdk

  PropertyGroup:
    - OutputType: Exe
      TargetFramework: net6.0
      RootNamespace: ParseYaml

  ItemGroup:
    PackageReference:
      - _Include: YamlDotNet
        _Version: 11.2.1
      - _Include: Newtonsoft.Json
        _Version: 13.0.1

";

  public static void Main(string[] args)
    => Console.WriteLine(YAML.YamlToXml());
      
  public static string YamlToXml(this string yaml)
  {
    IDeserializer deserializer = new DeserializerBuilder()
                                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                .Build();

    object project = deserializer
     .Deserialize(new StringReader(yaml));

    ISerializer serializer = new SerializerBuilder()
                            .JsonCompatible()
                            .Build();

    string json = serializer.Serialize(project);

    while (json.Contains("\"_"))
    {
      json = json.Replace("\"_",
                          "\"@");
    }

    XDocument xml = JsonConvert.DeserializeXNode(json);

    return xml.ToString();
  }
}