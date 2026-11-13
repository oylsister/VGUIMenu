using Microsoft.Extensions.Logging;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using VGUIMenu.Rendering;
using VGUIMenu.Shared;

namespace VGUIMenu;

/// <summary>Per-client menu state. Port of SourceMod's <c>CBaseMenuPlayer</c>.</summary>
internal sealed class MenuPlayer
{
    public MenuPlayer(HudPresenter hud)
        => Hud = hud;

    public bool         InMenu;
    public bool         AutoIgnore;
    public IGameClient? Client;
    public double       MenuStartTime;
    public int          MenuHoldTime;

    public MenuState    States { get; } = new ();
    public HudPresenter Hud    { get; }

    public void Reset()
    {
        InMenu        = false;
        AutoIgnore    = false;
        Client        = null;
        MenuStartTime = 0;
        MenuHoldTime  = 0;
        States.Reset();
    }
}

/// <summary>
///     Menu display / selection logic. Port of SourceMod's <c>BaseMenuStyle</c> (core/MenuStyle_Base.cpp)
///     and <c>MenuManager::GetMenuSound</c>.
/// </summary>
internal sealed class MenuSystem : IVGUIMenuManager
{
    private const int MaxPlayers = 64;

    private readonly MenuPlayer[]     _players = new MenuPlayer[MaxPlayers];
    private readonly Func<MenuConfig> _config;
    private readonly Func<double>     _clock;
    private readonly ILogger          _logger;

    public MenuSystem(IPanoramaManager panorama, Func<MenuConfig> config, Func<double> clock, ILogger logger)
    {
        _config = config;
        _clock  = clock;
        _logger = logger;

        for (var i = 0; i < MaxPlayers; i++)
        {
            _players[i] = new MenuPlayer(new HudPresenter(panorama, config, logger));
        }
    }

    private MenuPlayer? GetPlayer(IGameClient client)
    {
        var slot = (int) client.Slot;

        return slot is >= 0 and < MaxPlayers ? _players[slot] : null;
    }

    private ControlLabels Labels
    {
        get
        {
            var l = _config().Labels;

            return new ControlLabels(l.Previous, l.ExitBack, l.Next, l.Exit);
        }
    }

#region IVGUIMenuManager

    public bool DisplayMenu(IGameClient client, Menu menu, int time = Menu.TimeForever)
        => DisplayMenuAtItem(client, menu, 0, time);

    public bool DisplayMenuAtItem(IGameClient client, Menu menu, int firstItem, int time = Menu.TimeForever)
    {
        MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.Start, client));

        var player = GetPlayer(client);

        if (player is null || !client.IsValid || client.IsFakeClient || !client.IsInGame || player.AutoIgnore)
        {
            FailDisplay(menu, client);

            return false;
        }

        // Block re-entrant displays from the cancel callbacks of the menu we replace.
        player.AutoIgnore = true;

        if (player.InMenu)
        {
            CancelPlayerMenu(player, MenuCancelReason.Interrupted, true, false);
        }

        var states = player.States;
        states.FirstItem = 0;
        states.LastItem  = Math.Max(0, firstItem);
        states.Menu      = menu;

        var panel = MenuRenderer.Render(client, states, ItemOrder.Ascending, Labels);

        if (panel is null)
        {
            player.AutoIgnore = false;
            player.InMenu     = false;
            states.Menu       = null;
            player.Hud.Hide(client);
            FailDisplay(menu, client);

            return false;
        }

        player.InMenu        = true;
        player.Client        = client;
        player.MenuStartTime = _clock();
        player.MenuHoldTime  = Math.Max(0, time);

        player.Hud.Show(client, panel);

        player.AutoIgnore = false;

        return true;
    }

    public void CancelMenu(Menu menu)
    {
        foreach (var player in _players)
        {
            if (player.InMenu && ReferenceEquals(player.States.Menu, menu))
            {
                CancelPlayerMenu(player, MenuCancelReason.Interrupted, false, true);
            }
        }
    }

    public bool CancelClientMenu(IGameClient client, bool autoIgnore = false)
    {
        if (GetPlayer(client) is not { InMenu: true } player)
        {
            return false;
        }

        CancelPlayerMenu(player, MenuCancelReason.Interrupted, autoIgnore, true);

        return true;
    }

    public bool IsInMenu(IGameClient client)
        => GetPlayer(client) is { InMenu: true };

    public Menu? GetClientMenu(IGameClient client)
        => GetPlayer(client) is { InMenu: true } player ? player.States.Menu : null;

#endregion

    private static void FailDisplay(Menu menu, IGameClient client)
    {
        MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.Cancel, client) { CancelReason = MenuCancelReason.NoDisplay });
        MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.End, client) { EndReason       = MenuEndReason.Cancelled });
    }

    /// <summary>Port of <c>BaseMenuStyle::_CancelClientMenu</c>.</summary>
    private void CancelPlayerMenu(MenuPlayer player, MenuCancelReason reason, bool autoIgnore, bool hide)
    {
        var oldIgnore = player.AutoIgnore;

        if (autoIgnore)
        {
            player.AutoIgnore = true;
        }

        var menu   = player.States.Menu;
        var client = player.Client;

        player.InMenu = false;

        if (menu is not null && client is not null)
        {
            var firstItemOnPage = player.States.ItemOnPage;

            MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.Cancel, client)
            {
                CancelReason    = reason,
                FirstItemOnPage = firstItemOnPage,
            });

            MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.End, client)
            {
                EndReason       = MenuEndReason.Cancelled,
                CancelReason    = reason,
                FirstItemOnPage = firstItemOnPage,
            });
        }

        if (autoIgnore)
        {
            player.AutoIgnore = oldIgnore;
        }

        if (hide && !player.InMenu && client is not null)
        {
            player.Hud.Hide(client);
        }
    }

    /// <summary>Port of <c>BaseMenuStyle::RedoClientMenu</c>.</summary>
    private bool RedoClientMenu(MenuPlayer player, IGameClient client, ItemOrder order)
    {
        player.AutoIgnore = true;

        var panel = MenuRenderer.Render(client, player.States, order, Labels);

        if (panel is not null)
        {
            player.Hud.Show(client, panel);
        }

        player.AutoIgnore = false;

        return panel is not null;
    }

    /// <summary>
    ///     Port of <c>BaseMenuStyle::ClientPressedKey</c>. Returns false when the client has no menu open
    ///     (so the chat message / command is left alone).
    /// </summary>
    public bool ClientPressedKey(IGameClient client, int key)
    {
        if (GetPlayer(client) is not { InMenu: true } player || player.States.Menu is not { } menu)
        {
            return false;
        }

        if (key is < 1 or > Menu.MaxPageItems)
        {
            return true;
        }

        var states = player.States;
        var slot   = states.Slots[key];

        // Radio menus only accept valid keys, so empty / disabled slots do nothing (the menu stays open).
        if (slot.Type == ItemSelection.None)
        {
            return true;
        }

        PlaySelectSound(client, menu, slot.Type);

        var cancel       = false;
        var cancelReason = MenuCancelReason.Exit;
        var endReason    = MenuEndReason.Selected;

        switch (slot.Type)
        {
            case ItemSelection.Back:
            case ItemSelection.Next:
                if (RedoClientMenu(player, client, slot.Type == ItemSelection.Back ? ItemOrder.Descending : ItemOrder.Ascending))
                {
                    return true;
                }

                cancel       = true;
                cancelReason = MenuCancelReason.NoDisplay;
                endReason    = MenuEndReason.Cancelled;

                break;
            case ItemSelection.Exit:
                cancel       = true;
                cancelReason = MenuCancelReason.Exit;
                endReason    = MenuEndReason.Exit;

                break;
            case ItemSelection.ExitBack:
                cancel       = true;
                cancelReason = MenuCancelReason.ExitBack;
                endReason    = MenuEndReason.ExitBack;

                break;
        }

        var itemOnPage = states.ItemOnPage;

        player.InMenu = false;

        if (cancel)
        {
            MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.Cancel, client)
            {
                CancelReason    = cancelReason,
                FirstItemOnPage = itemOnPage,
            });
        }
        else
        {
            var item = menu.GetItem(slot.Item);

            MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.Select, client)
            {
                Item            = slot.Item,
                MenuItem        = item,
                FirstItemOnPage = itemOnPage,
                Style           = item?.Style ?? ItemDraw.Default,
            });

            if (item is not null)
            {
                MenuCallbacks.InvokeItemSelect(item, client);
            }
        }

        MenuCallbacks.Invoke(menu, new MenuActionContext(MenuAction.End, client)
        {
            EndReason       = endReason,
            CancelReason    = cancel ? cancelReason : MenuCancelReason.None,
            Item            = cancel ? -1 : slot.Item,
            FirstItemOnPage = itemOnPage,
        });

        // A callback may have opened another menu; only hide when nothing replaced this one.
        if (!player.InMenu)
        {
            player.Hud.Hide(client);
        }

        return true;
    }

    private void PlaySelectSound(IGameClient client, Menu menu, ItemSelection type)
    {
        var sounds = _config().Sounds;

        if (!sounds.Enabled || menu.Flags.HasFlag(MenuFlags.NoSound))
        {
            return;
        }

        var sound = type switch
        {
            ItemSelection.Item or ItemSelection.Back or ItemSelection.Next => sounds.Select,
            ItemSelection.Exit                                               => sounds.Exit,
            ItemSelection.ExitBack                                           => sounds.ExitBack,
            _                                                                => null,
        };

        if (string.IsNullOrWhiteSpace(sound) || client.GetPlayerController() is not { } controller)
        {
            return;
        }

        try
        {
            controller.EmitSound(sound, null, new RecipientFilter(client));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to play menu sound {Sound}", sound);
        }
    }

    /// <summary>Timeouts and HUD recovery. Port of <c>BaseMenuStyle::ProcessWatchList</c>.</summary>
    public void Think()
    {
        var now = _clock();

        foreach (var player in _players)
        {
            if (!player.InMenu || player.Client is not { } client)
            {
                continue;
            }

            if (player.MenuHoldTime > 0 && now > player.MenuStartTime + player.MenuHoldTime)
            {
                CancelPlayerMenu(player, MenuCancelReason.Timeout, false, true);

                continue;
            }

            player.Hud.EnsureAlive(client);
        }
    }

    public void OnClientPutInServer(IGameClient client)
    {
        if (GetPlayer(client) is not { } player)
        {
            return;
        }

        player.Hud.Forget();
        player.Reset();
    }

    public void OnClientDisconnected(IGameClient client)
    {
        if (GetPlayer(client) is not { } player)
        {
            return;
        }

        if (player.InMenu)
        {
            CancelPlayerMenu(player, MenuCancelReason.Disconnected, true, false);
        }

        player.Hud.Kill();
        player.Reset();
    }

    /// <summary>Map is ending: close every menu and drop layout entities (the engine removes them).</summary>
    public void OnMapEnd()
    {
        foreach (var player in _players)
        {
            if (player.InMenu)
            {
                CancelPlayerMenu(player, MenuCancelReason.Interrupted, true, false);
            }

            player.Hud.Forget();
            player.Reset();
        }
    }

    /// <summary>Module unloading: close every menu and remove layout entities.</summary>
    public void Shutdown()
    {
        foreach (var player in _players)
        {
            if (player.InMenu)
            {
                CancelPlayerMenu(player, MenuCancelReason.Interrupted, true, false);
            }

            player.Hud.Kill();
            player.Reset();
        }
    }
}
