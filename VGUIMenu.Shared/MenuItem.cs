using Sharp.Shared.Objects;

namespace VGUIMenu.Shared;

/// <summary>
///     A single menu entry.
/// </summary>
public sealed class MenuItem
{
    public MenuItem(string info, string display, ItemDraw style = ItemDraw.Default, Action<IGameClient>? onSelect = null)
    {
        Info     = info;
        Display  = display;
        Style    = style;
        OnSelect = onSelect;
    }

    public MenuItem(string info, Func<IGameClient, string> displayFactory, ItemDraw style = ItemDraw.Default, Action<IGameClient>? onSelect = null)
        : this(info, string.Empty, style, onSelect)
        => DisplayFactory = displayFactory;

    /// <summary>Hidden value identifying the item (SourceMod's "info" string).</summary>
    public string Info { get; set; }

    /// <summary>Static display text. Ignored when <see cref="DisplayFactory" /> is set.</summary>
    public string Display { get; set; }

    /// <summary>Optional per-client display text (e.g. localized).</summary>
    public Func<IGameClient, string>? DisplayFactory { get; set; }

    public ItemDraw Style { get; set; }

    /// <summary>Optional per-item callback, invoked after the menu handler's <see cref="MenuAction.Select" />.</summary>
    public Action<IGameClient>? OnSelect { get; set; }

    /// <summary>Free slot for consumer data.</summary>
    public object? Tag { get; set; }

    public string GetDisplay(IGameClient client)
        => DisplayFactory is { } factory ? factory(client) : Display;
}
