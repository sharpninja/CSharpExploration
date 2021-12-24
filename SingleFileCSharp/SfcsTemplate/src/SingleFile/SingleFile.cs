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
