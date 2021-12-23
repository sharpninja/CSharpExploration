using System.Reflection;

namespace LibraryTemplate;

public static class LibraryTemplateExtensions
{
    public static TReturn? AsType<TSource, TReturn>(
        this TSource source
    )
    {
        if(source?.Equals(default) ?? true) {
            return default;
        }

        if(source is TReturn r)
        {
            return r;
        }
        else
        {
            try
            {
                var mis = typeof(TSource)
                            .GetMethods()?
                            .FirstOrDefault(static mi =>
                                mi.IsSpecialName &&
                                mi.ReturnType == typeof(TReturn));

                if (mis is not null)
                {
                    return (TReturn?)mis.Invoke(null, new object[] { source });
                }
            }
            catch
            {
                // Ignore
            }
        }

        return default;
    }
}