using System.Reflection;

namespace YAMJCS;

internal static class ReflectionUtil
{
    public static object? FindOwningClient(Character character)
    {
        object? server = GetStaticPropertyOrField(typeof(GameMain), "Server");
        if (server is null) { return null; }

        object? connectedClients = GetPropertyOrField(server, "ConnectedClients");
        if (connectedClients is not IEnumerable clients) { return null; }

        foreach (object? client in clients)
        {
            if (client is null) { continue; }

            object? clientCharacter = GetPropertyOrField(client, "Character");
            if (ReferenceEquals(clientCharacter, character))
            {
                return client;
            }

            object? characterInfo = GetPropertyOrField(client, "CharacterInfo");
            object? infoCharacter = characterInfo is null ? null : GetPropertyOrField(characterInfo, "Character");
            if (ReferenceEquals(infoCharacter, character))
            {
                return client;
            }
        }

        return null;
    }

    public static void TrySetClientCharacter(object? client, Character newCharacter)
    {
        if (client is null) { return; }

        foreach (MethodInfo method in client.GetType()
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(m => m.Name == "SetClientCharacter"))
        {
            ParameterInfo[] ps = method.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(typeof(Character)))
            {
                method.Invoke(client, new object?[] { newCharacter });
                return;
            }
        }

        object? server = GetStaticPropertyOrField(typeof(GameMain), "Server");
        if (server is null) { return; }

        foreach (MethodInfo method in server.GetType()
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(m => m.Name == "SetClientCharacter"))
        {
            ParameterInfo[] ps = method.GetParameters();
            if (ps.Length == 2 &&
                ps[0].ParameterType.IsInstanceOfType(client) &&
                ps[1].ParameterType.IsAssignableFrom(typeof(Character)))
            {
                method.Invoke(server, new object?[] { client, newCharacter });
                return;
            }
        }
    }

    public static bool TryPutItemAnywhere(Character character, Item item)
    {
        if (character.Inventory is null) { return false; }

        foreach (MethodInfo method in character.GetType()
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(m => m.Name == "TryPutItemInAnySlot"))
        {
            bool? result = TryInvokeBoolMethod(method, character, item);
            if (result.HasValue) { return result.Value; }
        }

        int index = character.Inventory.FindAllowedSlot(item, ignoreCondition: true);
        if (index < 0) { return false; }

        return TryInvokeBestPutItem(character.Inventory, item, index, character);
    }

    public static bool TryInvokeBestPutItem(object inventory, Item item, int index, Character user)
    {
        foreach (MethodInfo method in inventory.GetType()
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(m => m.Name == "TryPutItem"))
        {
            bool? result = TryInvokeTryPutItemOverload(method, inventory, item, index, user);
            if (result.HasValue) { return result.Value; }
        }

        return false;
    }

    private static bool? TryInvokeTryPutItemOverload(MethodInfo method, object instance, Item item, int index, Character user)
    {
        ParameterInfo[] ps = method.GetParameters();
        object?[] args = new object?[ps.Length];

        for (int i = 0; i < ps.Length; i++)
        {
            Type pt = ps[i].ParameterType;

            if (pt == typeof(Item))
            {
                args[i] = item;
            }
            else if (pt == typeof(int))
            {
                args[i] = index;
            }
            else if (pt == typeof(bool))
            {
                args[i] = true;
            }
            else if (pt.IsAssignableFrom(typeof(Character)))
            {
                args[i] = user;
            }
            else if (!pt.IsValueType)
            {
                args[i] = null;
            }
            else
            {
                return null;
            }
        }

        object? value = method.Invoke(instance, args);
        return value is bool b ? b : null;
    }

    private static bool? TryInvokeBoolMethod(MethodInfo method, object instance, Item item)
    {
        ParameterInfo[] ps = method.GetParameters();
        object?[] args = new object?[ps.Length];

        for (int i = 0; i < ps.Length; i++)
        {
            Type pt = ps[i].ParameterType;

            if (pt == typeof(Item))
            {
                args[i] = item;
            }
            else if (pt == typeof(bool))
            {
                args[i] = true;
            }
            else if (!pt.IsValueType)
            {
                args[i] = null;
            }
            else
            {
                return null;
            }
        }

        object? value = method.Invoke(instance, args);
        return value is bool b ? b : null;
    }

    private static object? GetStaticPropertyOrField(Type type, string name)
    {
        PropertyInfo? prop = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop is not null) { return prop.GetValue(null); }

        FieldInfo? field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (field is not null) { return field.GetValue(null); }

        return null;
    }

    private static object? GetPropertyOrField(object obj, string name)
    {
        Type type = obj.GetType();

        PropertyInfo? prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop is not null) { return prop.GetValue(obj); }

        FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field is not null) { return field.GetValue(obj); }

        return null;
    }
}