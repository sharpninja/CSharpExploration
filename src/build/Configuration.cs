// ReSharper disable InconsistentNaming

// ReSharper disable AnnotateNotNullTypeMember
// ReSharper disable MemberCanBePrivate.Global
namespace SingleFileCSharp;


[TypeConverter(typeof(TypeConverter<Configuration>))]
internal sealed class Configuration : Enumeration
{
    private Configuration()
    {
    }

    public static Configuration Instance
    {
        get;
    } = new();

    public static Configuration Debug
    {
        get
        {
            Configuration debug = Configuration.Instance;
            debug.Value = nameof(Configuration.Debug);

            return Configuration._debug ??= debug;
        }
    }

    public static implicit operator string(Configuration configuration)
        => configuration.Value;

    public static Configuration Release
    {
        get
        {
            Configuration release = Configuration.Instance;
            release.Value = nameof(Configuration.Release);

            return Configuration._release ??= release;
        }
    }

    private static Configuration? _debug;
    private static Configuration? _release;

    public override bool Equals(object obj)
        => GetHashCode().Equals(obj?.GetHashCode());
    public override int GetHashCode()
        => Value.GetHashCode(StringComparison.Ordinal);
    public override string ToString()
        => Value;
}
