namespace LibraryTemplate.Tests;

public record TestObject(int IntProperty, TestObject? ParentProperty)
{
    public TestObject() : this(0, null)
    {
    }

    public static implicit operator string(TestObject testObject) => testObject.ToString()!;
    //public static explicit operator string(TestObject testObject) => testObject.ToString();
}
