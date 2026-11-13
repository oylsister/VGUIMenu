using Sharp.Shared.Objects;
using VGUIMenu.Rendering;
using VGUIMenu.Shared;
using Xunit;

namespace VGUIMenu.Tests;

public class MenuRendererTests
{
    // The renderer only passes the client through to callbacks, so tests can use null.
    private static readonly IGameClient NoClient = null!;

    private static Menu MakeMenu(int count, Action<Menu>? configure = null)
    {
        var menu = new Menu();
        menu.SetTitle("Title");
        configure?.Invoke(menu);

        for (var i = 0; i < count; i++)
        {
            menu.AddItem(i.ToString(), $"Item {i}");
        }

        return menu;
    }

    private static (MenuPanel Panel, MenuState State) Render(Menu menu, int firstItem = 0)
    {
        var state = new MenuState { Menu = menu, LastItem = firstItem };
        var panel = MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English);
        Assert.NotNull(panel);

        return (panel, state);
    }

    private static string[] Texts(MenuPanel panel)
        => panel.Lines.Select(l => l.Kind == PanelLineKind.Spacer ? "" : l.Text).ToArray();

    [Fact]
    public void SixItems_ShowItemsGapAndExitOnNine()
    {
        var (panel, state) = Render(MakeMenu(6));

        Assert.Equal(["1. Item 0", "2. Item 1", "3. Item 2", "4. Item 3", "5. Item 4", "6. Item 5", "", "9. Exit"], Texts(panel));
        Assert.Equal(ItemSelection.Item, state.Slots[1].Type);
        Assert.Equal(5, state.Slots[6].Item);
        Assert.Equal(ItemSelection.None, state.Slots[7].Type);
        Assert.Equal(ItemSelection.None, state.Slots[8].Type);
        Assert.Equal(ItemSelection.Exit, state.Slots[9].Type);
        Assert.Equal("Title", panel.Title);
    }

    [Fact]
    public void FewItems_ExitStillOnNine()
    {
        var (panel, state) = Render(MakeMenu(2));

        Assert.Equal(["1. Item 0", "2. Item 1", "", "9. Exit"], Texts(panel));
        Assert.Equal(ItemSelection.Exit, state.Slots[9].Type);
        Assert.Equal(ItemSelection.None, state.Slots[3].Type);
    }

    [Fact]
    public void ThirteenItems_PagesForwardAndBack()
    {
        var menu = MakeMenu(13);

        // Page 1: no Back, slot 7 is an empty line.
        var state = new MenuState { Menu = menu };
        var page1 = MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English)!;
        Assert.Equal(["1. Item 0", "2. Item 1", "3. Item 2", "4. Item 3", "5. Item 4", "6. Item 5", "", "", "8. Next", "9. Exit"], Texts(page1));
        Assert.Equal(ItemSelection.None, state.Slots[7].Type);
        Assert.Equal(ItemSelection.Next, state.Slots[8].Type);

        // Page 2: Back + Next.
        var page2 = MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English)!;
        Assert.Equal("1. Item 6", page2.Lines[0].Text);
        Assert.Equal(6, state.ItemOnPage);
        Assert.Equal(ItemSelection.Back, state.Slots[7].Type);
        Assert.Equal(ItemSelection.Next, state.Slots[8].Type);

        // Page 3: one item, padded so Back / Exit stay on 7 / 9.
        var page3 = MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English)!;
        Assert.Equal(["1. Item 12", "", "", "", "", "", "", "7. Back", "", "9. Exit"], Texts(page3));
        Assert.Equal(ItemSelection.Back, state.Slots[7].Type);
        Assert.Equal(ItemSelection.None, state.Slots[8].Type);

        // Back to page 2.
        var back = MenuRenderer.Render(NoClient, state, ItemOrder.Descending, ControlLabels.English)!;
        Assert.Equal("1. Item 6", back.Lines[0].Text);
        Assert.Equal("6. Item 11", back.Lines[5].Text);
        Assert.Equal(6, state.ItemOnPage);
        Assert.Equal(11, state.Slots[6].Item);

        // Back to page 1.
        var first = MenuRenderer.Render(NoClient, state, ItemOrder.Descending, ControlLabels.English)!;
        Assert.Equal("1. Item 0", first.Lines[0].Text);
        Assert.Equal(0, state.ItemOnPage);
    }

    [Fact]
    public void ExitBackButton_ShowsBackOnFirstPage()
    {
        var (panel, state) = Render(MakeMenu(3, m => m.ExitBackButton = true));

        Assert.Contains("7. Back", Texts(panel));
        Assert.Equal(ItemSelection.ExitBack, state.Slots[7].Type);
        Assert.Equal(ItemSelection.Exit, state.Slots[9].Type);
    }

    [Fact]
    public void DisabledAndSpacer_ArePlacedButNotSelectable()
    {
        var menu = new Menu();
        menu.AddItem("a", "Normal");
        menu.AddItem("b", "Disabled", ItemDraw.Disabled);
        menu.AddSpacer();
        menu.AddItem("c", "After");

        var (panel, state) = Render(menu);

        Assert.Equal(PanelLineKind.Muted, panel.Lines[1].Kind);
        Assert.Equal("2. Disabled", panel.Lines[1].Text);
        Assert.Equal(PanelLineKind.Spacer, panel.Lines[2].Kind);
        Assert.Equal("4. After", panel.Lines[3].Text);
        Assert.Equal(ItemSelection.Item, state.Slots[1].Type);
        Assert.Equal(ItemSelection.None, state.Slots[2].Type);
        Assert.Equal(ItemSelection.None, state.Slots[3].Type);
        Assert.Equal(ItemSelection.Item, state.Slots[4].Type);
        Assert.Equal(3, state.Slots[4].Item);
    }

    [Fact]
    public void NoPagination_EightItemsPlusExit()
    {
        var menu = new Menu { Pagination = Menu.NoPagination, ExitButton = true };

        for (var i = 0; i < 9; i++)
        {
            menu.AddItem(i.ToString(), $"Item {i}");
        }

        var (panel, state) = Render(menu);

        Assert.Equal("8. Item 7", panel.Lines[7].Text);
        Assert.Equal("9. Exit", panel.Lines[^1].Text);
        Assert.Equal(ItemSelection.Exit, state.Slots[9].Type);
    }

    [Fact]
    public void NoPaginationWithoutExit_NineItems()
    {
        var menu = new Menu { Pagination = Menu.NoPagination, ExitButton = false };

        for (var i = 0; i < 9; i++)
        {
            menu.AddItem(i.ToString(), $"Item {i}");
        }

        var (panel, state) = Render(menu);

        Assert.Equal(9, panel.Lines.Count);
        Assert.Equal("9. Item 8", panel.Lines[8].Text);
        Assert.Equal(ItemSelection.Item, state.Slots[9].Type);
    }

    [Fact]
    public void DisplayAtSecondItem_StillOffersBack()
    {
        var (panel, state) = Render(MakeMenu(7), firstItem: 1);

        Assert.Equal("1. Item 1", panel.Lines[0].Text);
        Assert.Equal(ItemSelection.Back, state.Slots[7].Type);
        Assert.Equal(0, state.FirstItem);
    }

    [Fact]
    public void OneItem_ItemAndExit()
    {
        var (panel, state) = Render(MakeMenu(1));

        Assert.Equal(["1. Item 0", "", "9. Exit"], Texts(panel));
        Assert.Equal(ItemSelection.Item, state.Slots[1].Type);
        Assert.Equal(ItemSelection.Exit, state.Slots[9].Type);

        for (var key = 2; key <= 8; key++)
        {
            Assert.Equal(ItemSelection.None, state.Slots[key].Type);
        }
    }

    [Fact]
    public void SevenItems_TwoPages()
    {
        var menu  = MakeMenu(7);
        var state = new MenuState { Menu = menu };

        var page1 = MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English)!;
        Assert.Equal("8. Next", Texts(page1)[^2]);
        Assert.Equal(ItemSelection.Next, state.Slots[8].Type);

        var page2 = MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English)!;
        Assert.Equal(["1. Item 6", "", "", "", "", "", "", "7. Back", "", "9. Exit"], Texts(page2));
        Assert.Equal(6, state.Slots[1].Item);
        Assert.Equal(ItemSelection.None, state.Slots[8].Type);
    }

    [Fact]
    public void DisplayAtSecondItem_NextThenBack_ReturnsToSamePage()
    {
        var menu  = MakeMenu(13);
        var state = new MenuState { Menu = menu, LastItem = 1 };

        MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English);
        Assert.Equal(1, state.ItemOnPage);

        MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English);
        Assert.Equal(7, state.ItemOnPage);

        var back = MenuRenderer.Render(NoClient, state, ItemOrder.Descending, ControlLabels.English)!;
        Assert.Equal(1, state.ItemOnPage);
        Assert.Equal("1. Item 1", back.Lines[0].Text);
        Assert.Equal(ItemSelection.Back, state.Slots[7].Type);
        Assert.Equal(ItemSelection.Next, state.Slots[8].Type);
    }

    [Fact]
    public void EmptyMenu_RendersNothing()
    {
        var state = new MenuState { Menu = new Menu() };

        Assert.Null(MenuRenderer.Render(NoClient, state, ItemOrder.Ascending, ControlLabels.English));
    }

    [Fact]
    public void Handler_CanChangeStyleTextAndTitle()
    {
        var menu = new Menu((_, ctx) =>
                            {
                                switch (ctx.Action)
                                {
                                    case MenuAction.DrawItem when ctx.Item == 0:
                                        ctx.Style = ItemDraw.Disabled;

                                        break;
                                    case MenuAction.DisplayItem when ctx.Item == 1:
                                        ctx.DisplayText = "Changed";

                                        break;
                                    case MenuAction.Display:
                                        ctx.DisplayText = "New title";

                                        break;
                                }
                            },
                            MenuAction.All);

        menu.AddItem("a", "First");
        menu.AddItem("b", "Second");

        var (panel, state) = Render(menu);

        Assert.Equal(PanelLineKind.Muted, panel.Lines[0].Kind);
        Assert.Equal(ItemSelection.None, state.Slots[1].Type);
        Assert.Equal("2. Changed", panel.Lines[1].Text);
        Assert.Equal("New title", panel.Title);
    }
}
