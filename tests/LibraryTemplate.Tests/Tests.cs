namespace LibraryTemplate.Tests;

public class LibraryTemplateTests
{
    public ITestOutputHelper Output
    {
        get;
    }

    public LibraryTemplateTests(ITestOutputHelper output)
    {
        Output = output;
        Output.WriteLine($"{MethodBase.GetCurrentMethod()?.Name ?? "<<unknown>>"} has completed successfully");
    }

    [Fact]
    public void UseCase1Test()
    {
        var testObject = new TestObject
        {
            IntProperty = 200,
            ParentProperty = new TestObject
            {
                IntProperty = 100
            }
        };

        var result = testObject.AsType<TestObject,string>();

        result.Should().NotBeNullOrEmpty();

        Output.WriteLine($"{MethodBase.GetCurrentMethod()?.Name ?? "<<unknown>>"} has completed successfully");
    }
}