namespace VGUIMenu.Shared;

/// <summary>
///     How a menu item is drawn. Mirrors SourceMod's <c>ITEMDRAW_*</c> flags.
/// </summary>
[Flags]
public enum ItemDraw
{
    /// <summary>Drawn normally and selectable.</summary>
    Default = 0,

    /// <summary>Drawn in the muted (light yellow) colour and cannot be selected.</summary>
    Disabled = 1 << 0,

    /// <summary>Raw line without a slot number. Only meaningful for panels; menus skip it (same as SourceMod).</summary>
    RawLine = 1 << 1,

    /// <summary>Consumes a slot number but draws nothing.</summary>
    NoText = 1 << 2,

    /// <summary>Consumes a slot number and draws an empty line.</summary>
    Spacer = 1 << 3,

    /// <summary>Item is completely ignored (not drawn, no slot).</summary>
    Ignore = RawLine | NoText,

    /// <summary>Control item (Back / Next / Exit). Used internally.</summary>
    Control = 1 << 4,
}

/// <summary>
///     Actions passed to a <see cref="MenuHandler" />. Mirrors SourceMod's <c>MenuAction</c>.
/// </summary>
[Flags]
public enum MenuAction
{
    None = 0,

    /// <summary>The menu is about to be shown to a client.</summary>
    Start = 1 << 0,

    /// <summary>A page is about to be sent. Set <see cref="MenuActionContext.DisplayText" /> to override the title.</summary>
    Display = 1 << 1,

    /// <summary>An item was selected. <see cref="MenuActionContext.Item" /> is the item index.</summary>
    Select = 1 << 2,

    /// <summary>The menu was cancelled. See <see cref="MenuActionContext.CancelReason" />.</summary>
    Cancel = 1 << 3,

    /// <summary>The menu display has fully ended for this client. See <see cref="MenuActionContext.EndReason" />.</summary>
    End = 1 << 4,

    /// <summary>An item is being drawn. Change <see cref="MenuActionContext.Style" /> to override its draw style.</summary>
    DrawItem = 1 << 8,

    /// <summary>An item's text is being drawn. Set <see cref="MenuActionContext.DisplayText" /> to override it.</summary>
    DisplayItem = 1 << 9,

    Default = Select | Cancel | End,
    All     = Start | Display | Select | Cancel | End | DrawItem | DisplayItem,
}

/// <summary>Why a menu was cancelled. Values match SourceMod's <c>MenuCancel_*</c>.</summary>
public enum MenuCancelReason
{
    None         = 0,
    Disconnected = -1,
    Interrupted  = -2,
    Exit         = -3,
    NoDisplay    = -4,
    Timeout      = -5,
    ExitBack     = -6,
}

/// <summary>Why a menu ended. Values match SourceMod's <c>MenuEnd_*</c>.</summary>
public enum MenuEndReason
{
    Selected  = 0,
    Cancelled = -3,
    Exit      = -4,
    ExitBack  = -5,
}

/// <summary>Menu option flags. Mirrors SourceMod's <c>MENUFLAG_*</c>.</summary>
[Flags]
public enum MenuFlags
{
    None = 0,

    /// <summary>Slot 9 shows "Exit".</summary>
    ExitButton = 1 << 0,

    /// <summary>Slot 7 shows "Back" on the first page and exits with <see cref="MenuCancelReason.ExitBack" />.</summary>
    ExitBackButton = 1 << 1,

    /// <summary>Do not play select / exit sounds.</summary>
    NoSound = 1 << 2,
}
