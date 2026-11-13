using Sharp.Shared.Objects;

namespace VGUIMenu.Shared;

/// <summary>
///     Callback for menu events. Only actions included in <see cref="Menu.Actions" /> are delivered,
///     except <see cref="MenuAction.Select" />, <see cref="MenuAction.Cancel" /> and <see cref="MenuAction.End" />,
///     which are always delivered.
/// </summary>
public delegate void MenuHandler(Menu menu, MenuActionContext context);

/// <summary>
///     Data for a single <see cref="MenuHandler" /> call.
/// </summary>
public sealed class MenuActionContext
{
    public MenuActionContext(MenuAction action, IGameClient client)
    {
        Action = action;
        Client = client;
    }

    public MenuAction Action { get; }

    public IGameClient Client { get; }

    /// <summary>Item index for <see cref="MenuAction.Select" />, <see cref="MenuAction.DrawItem" /> and <see cref="MenuAction.DisplayItem" />; otherwise -1.</summary>
    public int Item { get; init; } = -1;

    /// <summary>The item for <see cref="MenuAction.Select" />, <see cref="MenuAction.DrawItem" /> and <see cref="MenuAction.DisplayItem" />.</summary>
    public MenuItem? MenuItem { get; init; }

    /// <summary>Index of the first item on the page the client was looking at (for re-displaying the same page).</summary>
    public int FirstItemOnPage { get; init; }

    public MenuCancelReason CancelReason { get; init; }

    public MenuEndReason EndReason { get; init; }

    /// <summary><see cref="MenuAction.DrawItem" />: the style to draw with. Change it to override.</summary>
    public ItemDraw Style { get; set; }

    /// <summary>
    ///     <see cref="MenuAction.DisplayItem" />: set to override the item text.<br />
    ///     <see cref="MenuAction.Display" />: set to override the title.
    /// </summary>
    public string? DisplayText { get; set; }
}
