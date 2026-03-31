using System.Collections;
using System.Reflection;

internal static class ReflectionValueReader
{
    public static T? GetMemberValue<T>(object instance, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetMemberValue(instance, name);
            if (value is T typedValue)
            {
                return typedValue;
            }
        }

        return default;
    }

    public static object? GetMemberValue(object instance, string name)
    {
        try
        {
            var property = instance.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (property is not null)
            {
                return property.GetValue(instance);
            }

            var field = instance.GetType().GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            return field?.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

    public static IEnumerable<T> GetEnumerable<T>(object instance, params string[] names)
    {
        foreach (var name in names)
        {
            var enumerable = GetMemberValue<IEnumerable>(instance, name);
            if (enumerable is null)
            {
                continue;
            }

            foreach (var item in enumerable)
            {
                if (item is T typedValue)
                {
                    yield return typedValue;
                }
            }

            yield break;
        }
    }
}
