/// Project:
///   _Sdk: Microsoft.NET.Sdk
///   PropertyGroup:
///   - TargetFramework: net6.0
///     OutputType: Exe
///     ImplicitUsings: True
///     Nullable: Enable
///     DefaultNamespace: MarkdownSplitter

var mdFiles = Directory.GetFiles(@"C:\Users\kingd\ToMarkdown", "*.md");

Console.WriteLine(string.Join(Environment.NewLine, mdFiles));

Console.WriteLine("Done");
