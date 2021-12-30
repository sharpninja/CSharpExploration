
var mdFiles = Directory.GetFiles(@"C:\GitHub\GPS\SingleFileCSharp\SfcsTemplate\src\", "*.md");

Console.WriteLine(string.Join(Environment.NewLine, mdFiles));

Console.WriteLine("Done");
