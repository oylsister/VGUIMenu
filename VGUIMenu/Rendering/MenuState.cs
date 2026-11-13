using VGUIMenu.Shared;

namespace VGUIMenu.Rendering;

internal enum ItemOrder
{
    Ascending,
    Descending,
}

/// <summary>What pressing a slot does. Port of SourceMod's <c>ItemSelection</c>.</summary>
internal enum ItemSelection
{
    None,
    Item,
    Back,
    Next,
    Exit,
    ExitBack,
}

internal struct MenuSlot
{
    public ItemSelection Type;
    public int           Item;
}

/// <summary>Per-client paging state. Port of SourceMod's <c>menu_states_t</c>.</summary>
internal sealed class MenuState
{
    public Menu? Menu;

    /// <summary>Item to start from when going back a page.</summary>
    public int FirstItem;

    /// <summary>Item to start from when going forward a page.</summary>
    public int LastItem;

    /// <summary>First item index on the page currently shown.</summary>
    public int ItemOnPage;

    /// <summary>Indexed by key (1-9). Index 0 is unused.</summary>
    public readonly MenuSlot[] Slots = new MenuSlot[Menu.MaxPageItems + 1];

    public void Reset()
    {
        Menu       = null;
        FirstItem  = 0;
        LastItem   = 0;
        ItemOnPage = 0;
        Array.Clear(Slots);
    }
}
