// ReSharper disable EmptyNamespace

namespace SingleFileCSharp;

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
