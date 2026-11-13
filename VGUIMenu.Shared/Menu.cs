using Sharp.Shared.Objects;

namespace VGUIMenu.Shared;

/// <summary>
///     A CS:GO-style radio menu: slots 1-6 are items, 7 = Back, 8 = Next, 9 = Exit.
///     Build it, then show it with <see cref="IVGUIMenuManager.DisplayMenu" />.
///     A menu instance can be shown to many clients at once.
/// </summary>
public class Menu
{
    /// <summary>Slots available on one page (keys 1-9).</summary>
    public const int MaxPageItems = 9;

    /// <summary>Number of slots reserved for Back / Next / Exit.</summary>
    public const int PaginationControls = 3;

    /// <summary>Default and maximum items per page (9 - 3 = 6).</summary>
    public const int DefaultPagination = MaxPageItems - PaginationControls;

    /// <summary>Disable pagination: up to 8 items plus Exit (or 9 without Exit).</summary>
    public const int NoPagination = 0;

    /// <summary>Display time meaning "until closed".</summary>
    public const int TimeForever = 0;

    private readonly List<MenuItem>             _items = [];
    private          Func<IGameClient, string> _titleFactory;
    private          int                        _pagination = DefaultPagination;

    public Menu(MenuHandler? handler = null, MenuAction actions = MenuAction.Default)
    {
        Handler       = handler;
        Actions       = actions;
        _titleFactory = _ => string.Empty;
    }

    public MenuHandler? Handler { get; set; }

    /// <summary>Which optional actions (Start, Display, DrawItem, DisplayItem) are delivered to <see cref="Handler" />.</summary>
    public MenuAction Actions { get; set; }

    public MenuFlags Flags { get; set; } = MenuFlags.ExitButton;

    /// <summary>Free slot for consumer data.</summary>
    public object? Tag { get; set; }

    public bool ExitButton
    {
        get => Flags.HasFlag(MenuFlags.ExitButton);
        set => Flags = value ? Flags | MenuFlags.ExitButton : Flags & ~MenuFlags.ExitButton;
    }

    public bool ExitBackButton
    {
        get => Flags.HasFlag(MenuFlags.ExitBackButton);
        set => Flags = value ? Flags | MenuFlags.ExitBackButton : Flags & ~MenuFlags.ExitBackButton;
    }

    /// <summary>Items per page: 2-6, or <see cref="NoPagination" />. Invalid values are rejected.</summary>
    public int Pagination
    {
        get => _pagination;
        set
        {
            if (!SetPagination(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Pagination must be 0 or between 2 and 6.");
            }
        }
    }

    public bool SetPagination(int itemsPerPage)
    {
        if (itemsPerPage is < 0 or 1 or > DefaultPagination)
        {
            return false;
        }

        _pagination = itemsPerPage;

        return true;
    }

    public IReadOnlyList<MenuItem> Items => _items;

    public int ItemCount => _items.Count;

    /// <summary>Static title. Use '\n' for several lines; lines over 32 characters wrap automatically.</summary>
    public string Title
    {
        set => _titleFactory = _ => value;
    }

    public void SetTitle(string title)
        => _titleFactory = _ => title;

    /// <summary>Per-client title (e.g. localized).</summary>
    public void SetTitle(Func<IGameClient, string> factory)
        => _titleFactory = factory;

    public string BuildTitle(IGameClient client)
        => _titleFactory(client);

    private bool CanAdd()
        => _pagination != NoPagination || _items.Count < MaxPageItems;

    /// <summary>Adds an item. Returns false when a non-paginated menu is full.</summary>
    public bool AddItem(MenuItem item)
    {
        if (!CanAdd())
        {
            return false;
        }

        _items.Add(item);

        return true;
    }

    public bool AddItem(string info, string display, ItemDraw style = ItemDraw.Default)
        => AddItem(new MenuItem(info, display, style));

    public bool AddItem(string display, Action<IGameClient>? onSelect, ItemDraw style = ItemDraw.Default)
        => AddItem(new MenuItem(string.Empty, display, style, onSelect));

    public bool AddItem(Func<IGameClient, string> displayFactory, Action<IGameClient>? onSelect, ItemDraw style = ItemDraw.Default)
        => AddItem(new MenuItem(string.Empty, displayFactory, style, onSelect));

    /// <summary>Adds an empty line that still uses up a slot number.</summary>
    public bool AddSpacer()
        => AddItem(new MenuItem(string.Empty, string.Empty, ItemDraw.Spacer));

    public bool InsertItem(int position, MenuItem item)
    {
        if (!CanAdd() || position < 0 || position >= _items.Count)
        {
            return false;
        }

        _items.Insert(position, item);

        return true;
    }

    public bool RemoveItem(int position)
    {
        if (position < 0 || position >= _items.Count)
        {
            return false;
        }

        _items.RemoveAt(position);

        return true;
    }

    public void RemoveAllItems()
        => _items.Clear();

    public MenuItem? GetItem(int position)
        => position >= 0 && position < _items.Count ? _items[position] : null;
}
