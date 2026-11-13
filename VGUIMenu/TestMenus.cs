using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using VGUIMenu.Shared;

namespace VGUIMenu;

/// <summary>
///     Preview menus for in-game testing: <c>!vguimenu_test [leader|market|styles|wrap]</c>.
///     Disable with <c>EnableTestCommand: false</c> in vguimenu.jsonc.
/// </summary>
internal sealed class TestMenus
{
    public const string Command = "vguimenu_test";

    private readonly IVGUIMenuManager _manager;
    private readonly Menu             _root;
    private readonly Menu             _leader;
    private readonly Menu             _market;
    private readonly Menu             _styles;
    private readonly Menu             _wrap;

    public TestMenus(IVGUIMenuManager manager)
    {
        _manager = manager;

        // Same as the "[ZLeader] Leader Menu" screenshot: 6 items + Exit on 9.
        _leader = new Menu(OnSelectPrint);
        _leader.SetTitle("[ZLeader] Leader Menu");

        foreach (var name in new[] { "Defend Here", "Follow Me", "Toggle Beacon", "Place Marker", "Remove Marker", "Resign from Leader" })
        {
            _leader.AddItem(name, name);
        }

        // Same as the "ZMarket" screenshot: multi-line title and enough items for several pages.
        _market = new Menu(OnSelectPrint) { ExitBackButton = true };
        _market.SetTitle("ZMarket\nMy Current Setup:\nSelect weapon slot to clear.\nNote: Rebuy refers to these weapons.");

        foreach (var name in new[]
                 {
                     "Primary: p90", "Secondary: deagle", "Melee: Knife", "Projectile: HEGrenade", "Fire Grenade: IncGrenade",
                     "Decoy Grenade: Decoy", "Armor: Kevlar + Helmet", "Defuser: None", "Zeus: Taser", "Smoke: SmokeGrenade",
                     "Flash: Flashbang", "Healthshot: x1", "Tactical: TAGrenade",
                 })
        {
            _market.AddItem(name, name);
        }

        // Disabled / spacer / per-client text.
        _styles = new Menu(OnSelectPrint);
        _styles.SetTitle(client => $"Item styles ({client.Name})");
        _styles.AddItem("a", "Normal item");
        _styles.AddItem("b", "Disabled item", ItemDraw.Disabled);
        _styles.AddSpacer();
        _styles.AddItem("c", "Item after a spacer");
        _styles.AddItem(client => $"Hello {client.Name}", client => Print(client, "Per-item callback fired"));

        // Wrapping at 32 characters.
        _wrap = new Menu(OnSelectPrint);
        _wrap.SetTitle("A long title that is definitely longer than thirty-two characters");
        _wrap.AddItem("1", "This option text is much longer than thirty-two characters and wraps");
        _wrap.AddItem("2", "Supercalifragilisticexpialidocious-and-even-longer-word");
        _wrap.AddItem("3", "Short");

        _root = new Menu(OnSelectPrint);
        _root.SetTitle("VGUIMenu test");
        _root.AddItem("Leader menu (6 items)", client => Open(client, _leader));
        _root.AddItem("ZMarket (13 items, pages)", client => Open(client, _market));
        _root.AddItem("Item styles", client => Open(client, _styles));
        _root.AddItem("Text wrapping", client => Open(client, _wrap));
        _root.AddItem("Timeout (5 s)", client => _manager.DisplayMenu(client, _leader, 5));
    }

    private void Open(IGameClient client, Menu menu)
        => _manager.DisplayMenu(client, menu);

    private void OnSelectPrint(Menu menu, MenuActionContext context)
    {
        switch (context.Action)
        {
            case MenuAction.Select when context.MenuItem is { OnSelect: null } item:
                Print(context.Client, $"Selected #{context.Item}: {item.GetDisplay(context.Client)}");

                break;

            // "Back" on the first page of a menu with ExitBackButton returns to the test root.
            case MenuAction.Cancel when context.CancelReason == MenuCancelReason.ExitBack:
                _manager.DisplayMenu(context.Client, _root);

                break;
        }
    }

    private static void Print(IGameClient client, string message)
        => client.GetPlayerController()?.Print(HudPrintChannel.Chat, $" [VGUIMenu] {message}");

    public ECommandAction OnCommand(IGameClient client, StringCommand command)
    {
        var menu = command.ArgCount > 0 ? command.GetArg(1).ToLowerInvariant() : string.Empty;

        _manager.DisplayMenu(client,
                             menu switch
                             {
                                 "leader" => _leader,
                                 "market" => _market,
                                 "styles" => _styles,
                                 "wrap"   => _wrap,
                                 _        => _root,
                             });

        return ECommandAction.Stopped;
    }
}
