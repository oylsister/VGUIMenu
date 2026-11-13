using Sharp.Shared.Objects;

namespace VGUIMenu.Shared;

/// <summary>
///     Shared interface of the VGUIMenu module.
///     <code>
///     var vgui = modules.GetRequiredSharpModuleInterface&lt;IVGUIMenuManager&gt;(IVGUIMenuManager.Identity).Instance;
///     </code>
/// </summary>
public interface IVGUIMenuManager
{
    const string Identity = nameof(IVGUIMenuManager);

    /// <summary>Shows the first page of <paramref name="menu" /> to the client. Any open menu is cancelled with <see cref="MenuCancelReason.Interrupted" />.</summary>
    /// <param name="time">Seconds before the menu times out, or <see cref="Menu.TimeForever" />.</param>
    bool DisplayMenu(IGameClient client, Menu menu, int time = Menu.TimeForever);

    /// <summary>Shows the page that starts at <paramref name="firstItem" /> (use <see cref="MenuActionContext.FirstItemOnPage" /> to reopen the same page).</summary>
    bool DisplayMenuAtItem(IGameClient client, Menu menu, int firstItem, int time = Menu.TimeForever);

    /// <summary>Closes <paramref name="menu" /> for every client viewing it (<see cref="MenuCancelReason.Interrupted" />).</summary>
    void CancelMenu(Menu menu);

    /// <summary>Closes the client's open menu. With <paramref name="autoIgnore" />, menus opened from the cancel callbacks are blocked.</summary>
    bool CancelClientMenu(IGameClient client, bool autoIgnore = false);

    bool IsInMenu(IGameClient client);

    /// <summary>The menu the client is currently viewing, or null.</summary>
    Menu? GetClientMenu(IGameClient client);
}
