// ReSharper disable EmptyNamespace

namespace ParseYaml;

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
    var deserializer = new DeserializerBuilder()
      .WithNamingConvention(CamelCaseNamingConvention.Instance)
      .Build();

    var project = deserializer
      .Deserialize(new StringReader(yaml));

    var serializer = new SerializerBuilder()
      .JsonCompatible()
      .Build();
    var json = serializer.Serialize(project);
        
    while (json.Contains("\"_"))
    {
      json = json.Replace("\"_", "\"@");
    }

    var xml = JsonConvert.DeserializeXNode(json);

    return xml.ToString();
  }
}