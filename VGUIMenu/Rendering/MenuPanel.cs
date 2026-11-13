using VGUIMenu.Shared;

namespace VGUIMenu.Rendering;

internal enum PanelLineKind
{
    /// <summary>Selectable item or control (yellow).</summary>
    Item,

    /// <summary>Disabled item or raw text (light yellow).</summary>
    Muted,

    /// <summary>Empty line.</summary>
    Spacer,
}

internal readonly record struct PanelLine(PanelLineKind Kind, string Text, int Slot);

/// <summary>
///     One rendered page. Port of SourceMod's <c>CRadioDisplay</c>: it numbers slots and records lines,
///     and <see cref="HudPresenter" /> turns it into Panorama dialog variables.
/// </summary>
internal sealed class MenuPanel
{
    private readonly List<PanelLine> _lines = [];
    private          int             _nextPos = 1;

    public string Title { get; private set; } = string.Empty;

    public IReadOnlyList<PanelLine> Lines => _lines;

    /// <summary>Bit (n-1) set when slot n is selectable.</summary>
    public int SelectableKeys { get; private set; }

    public void DrawTitle(string text, bool onlyIfEmpty = false)
    {
        if (onlyIfEmpty && Title.Length != 0)
        {
            return;
        }

        Title = text;
    }

    public static bool CanDrawItem(ItemDraw style)
    {
        if ((style & ItemDraw.Ignore) == ItemDraw.Ignore)
        {
            return false;
        }

        // Radio style: disabled control items (greyed-out Back/Next) are not drawn, a spacer is used instead.
        if (style.HasFlag(ItemDraw.Disabled) && style.HasFlag(ItemDraw.Control))
        {
            return false;
        }

        return true;
    }

    /// <summary>Draws an item and returns its slot number (1-9), or 0 when it has no slot.</summary>
    public int DrawItem(string? text, ItemDraw style)
    {
        if (_nextPos > Menu.MaxPageItems || !CanDrawItem(style))
        {
            return 0;
        }

        if (style.HasFlag(ItemDraw.RawLine))
        {
            _lines.Add(style.HasFlag(ItemDraw.Spacer)
                           ? new PanelLine(PanelLineKind.Spacer, string.Empty, 0)
                           : new PanelLine(PanelLineKind.Muted, text ?? string.Empty, 0));

            return 0;
        }

        if (style.HasFlag(ItemDraw.Spacer))
        {
            _lines.Add(new PanelLine(PanelLineKind.Spacer, string.Empty, _nextPos));

            return _nextPos++;
        }

        if (style.HasFlag(ItemDraw.NoText))
        {
            return _nextPos++;
        }

        var line = $"{_nextPos}. {text}";

        if (style.HasFlag(ItemDraw.Disabled))
        {
            _lines.Add(new PanelLine(PanelLineKind.Muted, line, _nextPos));
        }
        else
        {
            _lines.Add(new PanelLine(PanelLineKind.Item, line, _nextPos));
            SelectableKeys |= 1 << (_nextPos - 1);
        }

        return _nextPos++;
    }
}
