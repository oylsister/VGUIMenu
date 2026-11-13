using Sharp.Shared.Objects;
using Microsoft.Extensions.Logging.Abstractions;
using VGUIMenu.Shared;
using Xunit;

namespace VGUIMenu.Tests;

/// <summary>Display / key press / cancel flow (port of SourceMod's BaseMenuStyle).</summary>
public class MenuSystemTests
{
    private readonly FakePanorama _panorama = new ();
    private readonly FakeTransmit _transmit = new ();
    private readonly MenuConfig   _config   = new () { Sounds = { Enabled = false } };
    private          double       _now;
    private readonly MenuSystem   _system;

    private readonly FakeClient _alice = new (0, "alice");
    private readonly FakeClient _bob   = new (1, "bob");

    public MenuSystemTests()
        => _system = new MenuSystem(_panorama.Manager, _transmit.Manager, () => _config, () => _now, NullLogger.Instance);

    private sealed class Recorder
    {
        public List<string> Events { get; } = [];

        public void Handle(Menu menu, MenuActionContext ctx)
            => Events.Add(ctx.Action switch
            {
                MenuAction.Select => $"Select:{ctx.Item}",
                MenuAction.Cancel => $"Cancel:{ctx.CancelReason}",
                MenuAction.End    => $"End:{ctx.EndReason}",
                _                 => ctx.Action.ToString(),
            });
    }

    private static Menu MakeMenu(Recorder rec, int items)
    {
        var menu = new Menu(rec.Handle);
        menu.SetTitle("Test");

        for (var i = 0; i < items; i++)
        {
            menu.AddItem(i.ToString(), $"Item {i}");
        }

        return menu;
    }

    [Fact]
    public void Select_FiresSelectThenEnd_AndHides()
    {
        var rec  = new Recorder();
        var menu = MakeMenu(rec, 3);

        Assert.True(_system.DisplayMenu(_alice.Client, menu));
        Assert.True(_system.IsInMenu(_alice.Client));
        Assert.False(_panorama.Layouts[0].IsHidden(_alice.Client));

        Assert.True(_system.ClientPressedKey(_alice.Client, 2));

        Assert.Equal(["Select:1", "End:Selected"], rec.Events);
        Assert.False(_system.IsInMenu(_alice.Client));
        Assert.True(_panorama.Layouts[0].IsHidden(_alice.Client));
    }

    [Fact]
    public void ItemOnSelect_IsInvoked()
    {
        var hit  = 0;
        var menu = new Menu();
        menu.AddItem("Go", _ => hit++);

        _system.DisplayMenu(_alice.Client, menu);
        _system.ClientPressedKey(_alice.Client, 1);

        Assert.Equal(1, hit);
    }

    [Fact]
    public void Exit_FiresCancelExitThenEndExit()
    {
        var rec = new Recorder();
        _system.DisplayMenu(_alice.Client, MakeMenu(rec, 3));

        _system.ClientPressedKey(_alice.Client, 9);

        Assert.Equal(["Cancel:Exit", "End:Exit"], rec.Events);
        Assert.False(_system.IsInMenu(_alice.Client));
    }

    [Fact]
    public void EmptyOrDisabledSlot_KeepsMenuOpen()
    {
        var rec  = new Recorder();
        var menu = MakeMenu(rec, 2);
        menu.AddItem("d", "Disabled", ItemDraw.Disabled);

        _system.DisplayMenu(_alice.Client, menu);

        Assert.True(_system.ClientPressedKey(_alice.Client, 3)); // disabled
        Assert.True(_system.ClientPressedKey(_alice.Client, 5)); // empty
        Assert.Empty(rec.Events);
        Assert.True(_system.IsInMenu(_alice.Client));
    }

    [Fact]
    public void NotInMenu_KeyIsNotConsumed()
        => Assert.False(_system.ClientPressedKey(_alice.Client, 1));

    [Fact]
    public void NextAndBack_StayOpenAndChangePage()
    {
        var rec = new Recorder();
        _system.DisplayMenu(_alice.Client, MakeMenu(rec, 13));
        var layout = _panorama.Layouts[0];

        Assert.True(_system.ClientPressedKey(_alice.Client, 8));
        Assert.Equal("1. Item 6", layout.Vars[_alice.Client, "line1"]);

        Assert.True(_system.ClientPressedKey(_alice.Client, 7));
        Assert.Equal("1. Item 0", layout.Vars[_alice.Client, "line1"]);

        Assert.Empty(rec.Events);
        Assert.True(_system.IsInMenu(_alice.Client));
    }

    [Fact]
    public void ExitBack_FiresCancelExitBack()
    {
        var rec  = new Recorder();
        var menu = MakeMenu(rec, 3);
        menu.ExitBackButton = true;

        _system.DisplayMenu(_alice.Client, menu);
        _system.ClientPressedKey(_alice.Client, 7);

        Assert.Equal(["Cancel:ExitBack", "End:ExitBack"], rec.Events);
    }

    [Fact]
    public void NewMenu_InterruptsOldOne()
    {
        var first  = new Recorder();
        var second = new Recorder();
        var menu2  = MakeMenu(second, 2);

        _system.DisplayMenu(_alice.Client, MakeMenu(first, 2));
        _system.DisplayMenu(_alice.Client, menu2);

        Assert.Equal(["Cancel:Interrupted", "End:Cancelled"], first.Events);
        Assert.Same(menu2, _system.GetClientMenu(_alice.Client));
        Assert.False(_panorama.Layouts[0].IsHidden(_alice.Client));
    }

    [Fact]
    public void DisplayFromInterruptCallback_IsBlocked()
    {
        var other    = new Recorder();
        var otherMenu = MakeMenu(other, 1);

        var first = new Menu((_, ctx) =>
        {
            if (ctx.Action == MenuAction.Cancel)
            {
                Assert.False(_system.DisplayMenu(ctx.Client, otherMenu));
            }
        });

        first.AddItem("a", "A");

        var replacement = MakeMenu(new Recorder(), 1);

        _system.DisplayMenu(_alice.Client, first);
        _system.DisplayMenu(_alice.Client, replacement);

        Assert.Equal(["Cancel:NoDisplay", "End:Cancelled"], other.Events);
        Assert.Same(replacement, _system.GetClientMenu(_alice.Client));
    }

    [Fact]
    public void SelectHandler_CanOpenAnotherMenu()
    {
        var next  = MakeMenu(new Recorder(), 1);
        var first = new Menu();
        first.AddItem("Open", c => _system.DisplayMenu(c, next));

        _system.DisplayMenu(_alice.Client, first);
        _system.ClientPressedKey(_alice.Client, 1);

        Assert.Same(next, _system.GetClientMenu(_alice.Client));
        Assert.False(_panorama.Layouts[0].IsHidden(_alice.Client));
    }

    [Fact]
    public void Timeout_CancelsAfterHoldTime()
    {
        var rec = new Recorder();
        _system.DisplayMenu(_alice.Client, MakeMenu(rec, 1), 5);

        _now = 4.9;
        _system.Think();
        Assert.True(_system.IsInMenu(_alice.Client));

        _now = 5.1;
        _system.Think();
        Assert.Equal(["Cancel:Timeout", "End:Cancelled"], rec.Events);
        Assert.True(_panorama.Layouts[0].IsHidden(_alice.Client));
    }

    [Fact]
    public void Disconnect_CancelsAndKillsLayout()
    {
        var rec = new Recorder();
        _system.DisplayMenu(_alice.Client, MakeMenu(rec, 1));

        _system.OnClientDisconnected(_alice.Client);

        Assert.Equal(["Cancel:Disconnected", "End:Cancelled"], rec.Events);
        Assert.True(_panorama.Layouts[0].Killed);
        Assert.False(_system.IsInMenu(_alice.Client));
    }

    [Fact]
    public void CancelMenu_ClosesForEveryViewer()
    {
        var rec  = new Recorder();
        var menu = MakeMenu(rec, 1);
        _system.DisplayMenu(_alice.Client, menu);
        _system.DisplayMenu(_bob.Client, menu);

        _system.CancelMenu(menu);

        Assert.Equal(4, rec.Events.Count);
        Assert.False(_system.IsInMenu(_alice.Client));
        Assert.False(_system.IsInMenu(_bob.Client));
    }

    [Fact]
    public void Bots_AreRejected()
    {
        var rec = new Recorder();
        var bot = new FakeClient(5, "bot", bot: true);

        Assert.False(_system.DisplayMenu(bot.Client, MakeMenu(rec, 1)));
        Assert.Equal(["Cancel:NoDisplay", "End:Cancelled"], rec.Events);
        Assert.Empty(_panorama.Layouts);
    }

    [Fact]
    public void TwoPlayers_DoNotSeeEachOthersMenu()
    {
        var menuA = new Menu();
        menuA.AddItem("a", "Alice item");
        var menuB = new Menu();
        menuB.AddItem("b", "Bob item");

        _system.DisplayMenu(_alice.Client, menuA);
        _system.DisplayMenu(_bob.Client, menuB);

        Assert.Equal(2, _panorama.Layouts.Count);

        foreach (var layout in _panorama.Layouts)
        {
            // Each layout only ever received per-player data for one client.
            Assert.Single(layout.Vars.Keys1);
            Assert.Single(layout.Classes.Keys1);
        }

        Assert.Equal("1. Alice item", _panorama.Layouts[0].Vars[_alice.Client, "line1"]);
        Assert.Equal("1. Bob item", _panorama.Layouts[1].Vars[_bob.Client, "line1"]);
    }

    [Fact]
    public void Layout_IsOnlyTransmittedToItsOwner()
    {
        _system.DisplayMenu(_alice.Client, MakeMenu(new Recorder(), 1));
        _system.DisplayMenu(_bob.Client, MakeMenu(new Recorder(), 1));

        var aliceLayout = _panorama.Layouts[0];
        var bobLayout   = _panorama.Layouts[1];

        // Hooked with "don't transmit" as the default, so nobody (incl. players joining later) gets it ...
        Assert.False(_transmit.Hooked[aliceLayout.Index]);
        Assert.False(_transmit.Hooked[bobLayout.Index]);

        // ... except the owner's controller.
        Assert.Equal([((aliceLayout.Index, _alice.Slot + 1), true)],
                     _transmit.States.Where(kv => kv.Key.Entity == aliceLayout.Index).Select(kv => (kv.Key, kv.Value)));
        Assert.Equal([((bobLayout.Index, _bob.Slot + 1), true)],
                     _transmit.States.Where(kv => kv.Key.Entity == bobLayout.Index).Select(kv => (kv.Key, kv.Value)));

        // Spectators of the owner don't see it either.
        Assert.False(aliceLayout.Observable);
        Assert.False(bobLayout.Observable);
    }

    [Fact]
    public void Disconnect_RemovesTransmitHook()
    {
        _system.DisplayMenu(_alice.Client, MakeMenu(new Recorder(), 1));
        _system.OnClientDisconnected(_alice.Client);

        Assert.Empty(_transmit.Hooked);
    }

    [Fact]
    public void NoController_NoLayoutIsCreated()
    {
        var ghost = new FakeClient(7, "ghost", hasController: false);

        _system.DisplayMenu(ghost.Client, MakeMenu(new Recorder(), 1));

        Assert.Empty(_panorama.Layouts);
    }

    private sealed class FakeExternalMenus : IExternalMenuSource
    {
        public Dictionary<IGameClient, ulong> Open      { get; } = new (ReferenceEqualityComparer.Instance);
        public bool                           CanClose  { get; set; } = true;
        public int                            Closed    { get; private set; }

        public ulong? GetOpenMenu(IGameClient client)
            => Open.TryGetValue(client, out var id) ? id : null;

        public void Close(IGameClient client)
        {
            Closed++;

            if (CanClose)
            {
                Open.Remove(client);
            }
        }
    }

    [Fact]
    public void ExternalMenuOpenedLater_ClosesVGUIMenu()
    {
        var external = new FakeExternalMenus();
        _system.ExternalMenus = external;

        var rec = new Recorder();
        _system.DisplayMenu(_alice.Client, MakeMenu(rec, 1));
        _system.Think();
        Assert.True(_system.IsInMenu(_alice.Client));

        external.Open[_alice.Client] = 42; // stock MenuManager shows a menu
        _system.Think();

        Assert.Equal(["Cancel:Interrupted", "End:Cancelled"], rec.Events);
        Assert.False(_system.IsInMenu(_alice.Client));
        Assert.True(_panorama.Layouts[0].IsHidden(_alice.Client));
    }

    [Fact]
    public void ExternalMenuForOtherPlayer_DoesNotAffectUs()
    {
        var external = new FakeExternalMenus();
        _system.ExternalMenus = external;

        _system.DisplayMenu(_alice.Client, MakeMenu(new Recorder(), 1));
        external.Open[_bob.Client] = 7;
        _system.Think();

        Assert.True(_system.IsInMenu(_alice.Client));
    }

    [Fact]
    public void VGUIMenuOpenedLater_ClosesExternalMenu()
    {
        var external = new FakeExternalMenus();
        external.Open[_alice.Client] = 42;
        _system.ExternalMenus = external;

        _system.DisplayMenu(_alice.Client, MakeMenu(new Recorder(), 1));
        _system.Think();

        Assert.Equal(1, external.Closed);
        Assert.Empty(external.Open);
        Assert.True(_system.IsInMenu(_alice.Client));
    }

    [Fact]
    public void ExternalMenuThatWasAlreadyOpen_DoesNotCloseVGUIMenu()
    {
        // If the other menu can't be closed, only a *new* one (different session) makes us step aside.
        var external = new FakeExternalMenus { CanClose = false };
        external.Open[_alice.Client] = 42;
        _system.ExternalMenus = external;

        _system.DisplayMenu(_alice.Client, MakeMenu(new Recorder(), 1));
        _system.Think();
        Assert.True(_system.IsInMenu(_alice.Client));

        external.Open[_alice.Client] = 43;
        _system.Think();
        Assert.False(_system.IsInMenu(_alice.Client));
    }

    [Fact]
    public void LostLayout_IsRecreatedOnThink()
    {
        _system.DisplayMenu(_alice.Client, MakeMenu(new Recorder(), 1));
        _panorama.Layouts[0].Valid = false; // round restart / cleanup removed the entity

        _system.Think();

        Assert.Equal(2, _panorama.Layouts.Count);
        Assert.Equal("1. Item 0", _panorama.Layouts[1].Vars[_alice.Client, "line1"]);
        Assert.True(_transmit.States[(_panorama.Layouts[1].Index, _alice.Slot + 1)]);
        Assert.False(_panorama.Layouts[1].IsHidden(_alice.Client));
    }
}
