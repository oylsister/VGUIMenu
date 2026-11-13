using System.Reflection;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace VGUIMenu.Tests;

/// <summary>
///     Minimal interface fakes built on <see cref="DispatchProxy" />. Unhandled members return default values,
///     so only what MenuSystem / HudPresenter touch needs an implementation.
/// </summary>
public class FakeProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, (bool Handled, object? Result)> Handler { get; set; } = (_, _) => (false, null);

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        // Interfaces like IGameClient may declare IEquatable<T>; use reference identity so fakes work as dictionary keys.
        switch (method!.Name)
        {
            case "Equals" when args is [var other]:
                return ReferenceEquals(this, other);
            case "GetHashCode" when args is null or []:
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        }

        var (handled, result) = Handler(method, args);

        if (handled)
        {
            return result;
        }

        var type = method!.ReturnType;

        return type == typeof(void) || !type.IsValueType ? null : Activator.CreateInstance(type);
    }

    public static T Create<T>(Func<MethodInfo, object?[]?, (bool, object?)> handler) where T : class
    {
        var proxy = Create<T, FakeProxy>();
        ((FakeProxy) (object) proxy).Handler = handler;

        return proxy;
    }
}

internal sealed class FakeClient
{
    public FakeClient(int slot, string name = "player", bool bot = false, bool hasController = true)
    {
        Slot  = slot;
        Name  = name;
        IsBot = bot;

        // Controllers use entity index slot + 1, like the engine.
        Controller = hasController
            ? FakeProxy.Create<IPlayerController>((method, _) => method.Name switch
            {
                "get_Index" => (true, MakeValue(method.ReturnType, slot + 1)),
                _           => (false, null),
            })
            : null;

        Client = FakeProxy.Create<IGameClient>((method, _) => method.Name switch
        {
            "get_Slot"            => (true, MakeValue(method.ReturnType, Slot)),
            "GetPlayerController" => (true, Controller),
            "get_IsValid"      => (true, true),
            "get_IsInGame"     => (true, true),
            "get_IsFakeClient" => (true, IsBot),
            "get_Name"         => (true, Name),
            _                  => (false, null),
        });
    }

    public int         Slot   { get; }
    public string      Name   { get; }
    public bool        IsBot  { get; }
    public IGameClient        Client     { get; }
    public IPlayerController? Controller { get; }

    /// <summary>Builds a PlayerSlot / EntityIndex (or plain int) from an int via its conversion operator or constructor.</summary>
    public static object MakeValue(Type type, int value)
    {
        if (type == typeof(int))
        {
            return value;
        }

        foreach (var op in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (op.Name is "op_Implicit" or "op_Explicit" && op.ReturnType == type && op.GetParameters() is [var p])
            {
                return op.Invoke(null, [Convert.ChangeType(value, p.ParameterType)])!;
            }
        }

        foreach (var ctor in type.GetConstructors())
        {
            if (ctor.GetParameters() is [var p])
            {
                return ctor.Invoke([Convert.ChangeType(value, p.ParameterType)]);
            }
        }

        throw new InvalidOperationException($"Cannot build {type} from int");
    }
}

/// <summary>Records what a layout received, per player.</summary>
internal sealed class FakeLayout
{
    public FakeLayout(int index)
    {
        Index = index;

        Layout = FakeProxy.Create<ICustomHudLayout>((method, args) =>
        {
            switch (method.Name)
            {
                case "get_Index":
                    return (true, FakeClient.MakeValue(method.ReturnType, Index));
                case "get_Observable":
                    return (true, Observable);
                case "set_Observable":
                    Observable = (bool) args![0]!;

                    return (true, null);
                case "IsValid":
                    return (true, Valid);
                case "Kill":
                    Valid  = false;
                    Killed = true;

                    return (true, null);
                case "SetDialogVariableStringForPlayer":
                    Vars[(IGameClient) args![0]!, (string) args[2]!] = (string) args[3]!;

                    return (true, null);
                case "SetClassOverrideForPlayer":
                    Classes[(IGameClient) args![0]!, (string) args[1]!, (string) args[2]!] = (HudPanelClassStatus) args[3]!;

                    return (true, null);
            }

            return (false, null);
        });
    }

    public int              Index      { get; }
    public ICustomHudLayout Layout     { get; }
    public bool             Observable { get; private set; } = true;
    public bool             Valid      { get; set; } = true;
    public bool             Killed { get; private set; }

    public Dictionary2<IGameClient, string, string>                       Vars    { get; } = new ();
    public Dictionary3<IGameClient, string, string, HudPanelClassStatus> Classes { get; } = new ();

    public bool IsHidden(IGameClient client)
        => !Classes.TryGet(client, "vguimenu", "Hidden", out var status) || status == HudPanelClassStatus.ForceEnable;
}

internal sealed class FakePanorama
{
    public FakePanorama()
        => Manager = FakeProxy.Create<IPanoramaManager>((method, _) =>
        {
            if (method.Name != "CreateLayout")
            {
                return (false, null);
            }

            var layout = new FakeLayout(1000 + Layouts.Count);
            Layouts.Add(layout);

            return (true, layout.Layout);
        });

    public IPanoramaManager Manager { get; }
    public List<FakeLayout> Layouts { get; } = [];
}

internal sealed class Dictionary2<TA, TB, TV> where TA : notnull where TB : notnull
{
    private readonly Dictionary<(TA, TB), TV> _map = new ();

    public TV this[TA a, TB b]
    {
        get => _map[(a, b)];
        set => _map[(a, b)] = value;
    }

    public IEnumerable<TA> Keys1 => _map.Keys.Select(k => k.Item1).Distinct();
}

internal sealed class Dictionary3<TA, TB, TC, TV> where TA : notnull where TB : notnull where TC : notnull
{
    private readonly Dictionary<(TA, TB, TC), TV> _map = new ();

    public TV this[TA a, TB b, TC c]
    {
        set => _map[(a, b, c)] = value;
    }

    public bool TryGet(TA a, TB b, TC c, out TV value)
        => _map.TryGetValue((a, b, c), out value!);

    public IEnumerable<TA> Keys1 => _map.Keys.Select(k => k.Item1).Distinct();
}

/// <summary>Records transmit hooks: which entities are hooked (and their default) and per-controller overrides.</summary>
internal sealed class FakeTransmit
{
    public FakeTransmit()
        => Manager = FakeProxy.Create<ITransmitManager>((method, args) =>
        {
            switch (method.Name)
            {
                case "AddEntityHooks":
                    Hooked[ToInt(((IBaseEntity) args![0]!).Index)] = (bool) args[1]!;

                    return (true, true);
                case "RemoveEntityHooks":
                    return (true, Hooked.Remove(ToInt(((IBaseEntity) args![0]!).Index)));
                case "SetEntityState":
                    States[(ToInt(args![0]!), ToInt(args[1]!))] = (bool) args[2]!;

                    return (true, true);
            }

            return (false, null);
        });

    public ITransmitManager Manager { get; }

    /// <summary>Entity index → defaultTransmit.</summary>
    public Dictionary<int, bool> Hooked { get; } = new ();

    /// <summary>(entity, controller) → transmit.</summary>
    public Dictionary<(int Entity, int Controller), bool> States { get; } = new ();

    private static int ToInt(object index)
        => Convert.ToInt32(index.GetType().GetProperties().FirstOrDefault(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(uint))
                               ?.GetValue(index)
                           ?? index.GetHashCode());
}
