using Sharp.Shared.Objects;
using VGUIMenu.Shared;

namespace VGUIMenu.Rendering;

/// <summary>Text for the control slots (7 / 8 / 9).</summary>
internal readonly record struct ControlLabels(string Previous, string ExitBack, string Next, string Exit)
{
    public static readonly ControlLabels English = new ("Back", "Back", "Next", "Exit");
}

/// <summary>
///     Builds one page of a menu. Port of SourceMod's <c>MenuManager::RenderMenu</c>
///     (core/MenuManager.cpp) for the radio style with 9 keys.
/// </summary>
internal static class MenuRenderer
{
    private readonly record struct DrawEntry(int Position, ItemDraw Style, MenuItem Item);

    private static bool IsSlotItem(ItemDraw style)
    {
        if (!MenuPanel.CanDrawItem(style))
        {
            return false;
        }

        if ((style & ItemDraw.Ignore) == ItemDraw.Ignore)
        {
            return false;
        }

        return !style.HasFlag(ItemDraw.RawLine);
    }

    /// <summary>Fires <see cref="MenuAction.DrawItem" /> so the handler can change the style.</summary>
    private static ItemDraw GetDrawStyle(Menu menu, IGameClient client, int position, MenuItem item)
    {
        if (menu.Handler is null || !menu.Actions.HasFlag(MenuAction.DrawItem))
        {
            return item.Style;
        }

        var context = new MenuActionContext(MenuAction.DrawItem, client)
        {
            Item     = position,
            MenuItem = item,
            Style    = item.Style,
        };

        MenuCallbacks.Invoke(menu, context);

        return context.Style;
    }

    /// <summary>Fires <see cref="MenuAction.DisplayItem" /> so the handler can change the text.</summary>
    private static string GetDisplayText(Menu menu, IGameClient client, int position, MenuItem item, ItemDraw style)
    {
        var text = MenuCallbacks.BuildDisplay(item, client);

        if (menu.Handler is null || !menu.Actions.HasFlag(MenuAction.DisplayItem))
        {
            return text;
        }

        var context = new MenuActionContext(MenuAction.DisplayItem, client)
        {
            Item        = position,
            MenuItem    = item,
            Style       = style,
            DisplayText = null,
        };

        MenuCallbacks.Invoke(menu, context);

        return context.DisplayText ?? text;
    }

    /// <summary>
    ///     Renders the page and updates <paramref name="md" /> (paging positions and key slots).
    ///     Returns null when nothing can be drawn.
    /// </summary>
    public static MenuPanel? Render(IGameClient client, MenuState md, ItemOrder order, ControlLabels labels)
    {
        var menu = md.Menu;

        if (menu is null)
        {
            return null;
        }

        var pgn        = menu.Pagination;
        var maxItems   = Menu.MaxPageItems;
        var exitButton = menu.ExitButton;

        if (pgn != Menu.NoPagination)
        {
            maxItems = pgn;
        }
        else if (exitButton)
        {
            maxItems--;
        }

        if (maxItems < 2)
        {
            return null;
        }

        var totalItems = menu.ItemCount;
        var startItem  = 0;

        if (pgn != Menu.NoPagination)
        {
            if (order == ItemOrder.Ascending)
            {
                startItem = md.LastItem;

                // Out of bounds: start from the end, going backwards.
                if (startItem >= totalItems)
                {
                    startItem = totalItems - 1;
                    order     = ItemOrder.Descending;
                }
            }
            else
            {
                startItem = md.FirstItem;

                // Not enough room going backwards: start from the beginning.
                if (startItem <= maxItems)
                {
                    startItem = 0;
                    order     = ItemOrder.Ascending;
                }
            }
        }

        var drawItems  = new DrawEntry[Menu.MaxPageItems + 1];
        var foundExtra = false;
        var extraItem  = 0;
        var foundItems = 0;
        var i          = startItem;

        // Collect items until: no more items, one past the page size, or the menu is full (no pagination).
        while (totalItems > 0 && i >= 0)
        {
            if (menu.GetItem(i) is { } item)
            {
                var style = GetDrawStyle(menu, client, i, item);

                if (IsSlotItem(style))
                {
                    if (foundItems >= maxItems)
                    {
                        foundExtra = true;
                        extraItem  = i;

                        break;
                    }

                    drawItems[foundItems++] = new DrawEntry(i, style, item);
                }
            }

            if (pgn == Menu.NoPagination && foundItems >= maxItems)
            {
                break;
            }

            if (order == ItemOrder.Descending)
            {
                if (i == 0)
                {
                    break;
                }

                i--;
            }
            else
            {
                if (i >= totalItems - 1)
                {
                    break;
                }

                i++;
            }
        }

        if (foundItems == 0)
        {
            return null;
        }

        var displayPrev = false;
        var displayNext = false;

        if (pgn != Menu.NoPagination)
        {
            if (foundExtra)
            {
                if (order == ItemOrder.Descending)
                {
                    displayPrev  = true;
                    md.FirstItem = extraItem;
                }
                else
                {
                    displayNext = true;
                    md.LastItem = extraItem;
                }
            }

            if (order == ItemOrder.Descending)
            {
                // Is there anything after this page?
                var lastItem = drawItems[0].Position;

                while (++lastItem < totalItems)
                {
                    if (menu.GetItem(lastItem) is { } item && IsSlotItem(GetDrawStyle(menu, client, lastItem, item)))
                    {
                        displayNext = true;
                        md.LastItem = lastItem;

                        break;
                    }
                }
            }
            else
            {
                // Is there anything before this page?
                // (SourceMod never checks item 0 here; we do, so DisplayAt(1) still offers "Back".)
                var lastItem = drawItems[0].Position;

                while (lastItem > 0)
                {
                    lastItem--;

                    if (menu.GetItem(lastItem) is { } item && IsSlotItem(GetDrawStyle(menu, client, lastItem, item)))
                    {
                        displayPrev  = true;
                        md.FirstItem = lastItem;

                        break;
                    }
                }
            }
        }

        var panel    = new MenuPanel();
        var slots    = md.Slots;
        var position = 0;

        Array.Clear(slots);

        void DrawMenuItem(in DrawEntry entry)
        {
            var text = GetDisplayText(menu, client, entry.Position, entry.Item, entry.Style);
            position = panel.DrawItem(text, entry.Style);

            if (position == 0)
            {
                return;
            }

            slots[position].Item = entry.Position;
            slots[position].Type = (entry.Style & (ItemDraw.Disabled | ItemDraw.Spacer | ItemDraw.NoText)) == 0
                ? ItemSelection.Item
                : ItemSelection.None;
        }

        if (order == ItemOrder.Ascending)
        {
            md.ItemOnPage = drawItems[0].Position;

            for (var k = 0; k < foundItems; k++)
            {
                DrawMenuItem(drawItems[k]);
            }
        }
        else
        {
            md.ItemOnPage = drawItems[foundItems - 1].Position;

            for (var k = foundItems - 1; k >= 0; k--)
            {
                DrawMenuItem(drawItems[k]);
            }
        }

        if (pgn != Menu.NoPagination || exitButton)
        {
            var canDrawDisabled = MenuPanel.CanDrawItem(ItemDraw.Disabled | ItemDraw.Control);
            var exitBackButton  = pgn != Menu.NoPagination && menu.ExitBackButton;

            // Slots left over for padding before the control items.
            var padding = Menu.MaxPageItems - maxItems;
            padding += maxItems - foundItems;

            // The exit slot is always reserved, even if no exit button is drawn.
            padding--;

            if (pgn != Menu.NoPagination)
            {
                // Back / Next slots.
                padding -= 2;
            }

            if (exitBackButton)
            {
                if (!displayPrev)
                {
                    displayPrev = true;
                }
                else
                {
                    exitBackButton = false;
                }
            }

            if (exitButton || displayNext || displayPrev)
            {
                // Without Back/Next, pad with invisible slots instead of empty lines.
                var padStyle = !displayNext && !displayPrev ? ItemDraw.NoText : ItemDraw.Spacer;

                for (var p = 0; p < padding; p++)
                {
                    position              = panel.DrawItem(null, padStyle);
                    slots[position].Type = ItemSelection.None;
                }
            }

            // Blank line before the control items.
            if (displayPrev || displayNext || exitButton)
            {
                panel.DrawItem(string.Empty, ItemDraw.RawLine | ItemDraw.Spacer);
            }

            if (pgn != Menu.NoPagination)
            {
                if (displayPrev || displayNext)
                {
                    // Slot 7
                    if (displayPrev || canDrawDisabled)
                    {
                        if (exitBackButton)
                        {
                            position              = panel.DrawItem(labels.ExitBack, ItemDraw.Control);
                            slots[position].Type = ItemSelection.ExitBack;
                        }
                        else
                        {
                            position = panel.DrawItem(labels.Previous,
                                                      (displayPrev ? ItemDraw.Default : ItemDraw.Disabled) | ItemDraw.Control);

                            slots[position].Type = displayPrev ? ItemSelection.Back : ItemSelection.None;
                        }
                    }
                    else if (displayNext || exitButton)
                    {
                        position              = panel.DrawItem(null, ItemDraw.Spacer | ItemDraw.Control);
                        slots[position].Type = ItemSelection.None;
                    }

                    // Slot 8
                    if (displayNext || canDrawDisabled)
                    {
                        position = panel.DrawItem(labels.Next,
                                                  (displayNext ? ItemDraw.Default : ItemDraw.Disabled) | ItemDraw.Control);

                        slots[position].Type = displayNext ? ItemSelection.Next : ItemSelection.None;
                    }
                    else if (exitButton)
                    {
                        position              = panel.DrawItem(null, ItemDraw.Spacer | ItemDraw.Control);
                        slots[position].Type = ItemSelection.None;
                    }
                }
                else
                {
                    // No paging needed: skip slots 7 and 8 so Exit stays on 9.
                    position              = panel.DrawItem(null, ItemDraw.NoText);
                    slots[position].Type = ItemSelection.None;
                    position              = panel.DrawItem(null, ItemDraw.NoText);
                    slots[position].Type = ItemSelection.None;
                }
            }

            // Slot 9
            if (exitButton)
            {
                position              = panel.DrawItem(labels.Exit, ItemDraw.Control);
                slots[position].Type = ItemSelection.Exit;
            }
        }

        // Slot 0 is never a key; DrawItem returns 0 for lines without a slot.
        slots[0] = default;

        var title = MenuCallbacks.BuildTitle(menu, client);

        if (menu.Handler is not null && menu.Actions.HasFlag(MenuAction.Display))
        {
            var context = new MenuActionContext(MenuAction.Display, client)
            {
                FirstItemOnPage = md.ItemOnPage,
                DisplayText     = null,
            };

            MenuCallbacks.Invoke(menu, context);

            if (context.DisplayText is { } overrideTitle)
            {
                title = overrideTitle;
            }
        }

        panel.DrawTitle(title, true);

        return panel;
    }
}
