
var mdFiles = Directory.GetFiles(@"C:\Users\kingd\ToMarkdown", "*.md");

Console.WriteLine(string.Join(Environment.NewLine, mdFiles));

Console.WriteLine("Done");
