using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace VGUIMenu.Rendering;

/// <summary>
///     Shows a <see cref="MenuPanel" /> on one player's Panorama layout (vguimenu.vxml).
///     Each player owns their own layout entity. Only changed dialog variables / classes are sent.
/// </summary>
internal sealed class HudPresenter
{
    /// <summary>Root panel id in vguimenu.vxml. All dialog variables are set on it.</summary>
    public const string RootPanel = "vguimenu";

    public const string TitlePanel = "title";

    /// <summary>Number of body line labels (line1 … lineN) in vguimenu.vxml.</summary>
    public const int MaxLines = 12;

    public const string HiddenClass    = "Hidden";
    public const string CollapsedClass = "Collapsed";
    public const string MutedClass     = "Muted";

    /// <summary>Indent for wrapped continuation lines of an item.</summary>
    private const string ContinuationIndent = "    ";

    private static readonly string[] LineIds  = Enumerable.Range(1, MaxLines).Select(n => $"line{n}").ToArray();
    private static readonly string[] LineVars = Enumerable.Range(1, MaxLines).Select(n => $"line{n}").ToArray();

    private readonly IPanoramaManager           _panorama;
    private readonly Func<MenuConfig>           _config;
    private readonly ILogger                    _logger;
    private readonly Dictionary<string, string> _sentVars    = new (StringComparer.Ordinal);
    private readonly Dictionary<string, bool>   _sentClasses = new (StringComparer.Ordinal);

    private ICustomHudLayout? _layout;
    private MenuPanel?        _current;

    public HudPresenter(IPanoramaManager panorama, Func<MenuConfig> config, ILogger logger)
    {
        _panorama = panorama;
        _config   = config;
        _logger   = logger;
    }

    public bool IsShowing => _current is not null;

    public void Show(IGameClient client, MenuPanel panel)
    {
        _current = panel;
        Push(client);
    }

    public void Hide(IGameClient client)
    {
        _current = null;

        if (IsLayoutValid())
        {
            SetClass(client, RootPanel, HiddenClass, true);
        }
    }

    /// <summary>Re-sends the current page if the layout entity was destroyed (round restart, cleanup…).</summary>
    public void EnsureAlive(IGameClient client)
    {
        if (_current is not null && !IsLayoutValid())
        {
            Push(client);
        }
    }

    /// <summary>Removes the layout entity (client leaving).</summary>
    public void Kill()
    {
        if (IsLayoutValid())
        {
            _layout!.Kill();
        }

        Forget();
    }

    /// <summary>Drops the layout reference without touching it (map change already destroyed it).</summary>
    public void Forget()
    {
        _layout  = null;
        _current = null;
        _sentVars.Clear();
        _sentClasses.Clear();
    }

    private bool IsLayoutValid()
        => _layout is not null && _layout.IsValid();

    private bool EnsureLayout()
    {
        if (IsLayoutValid())
        {
            return true;
        }

        _sentVars.Clear();
        _sentClasses.Clear();

        var path = _config().LayoutPath;
        _layout = _panorama.CreateLayout(path, null);

        if (_layout is null)
        {
            _logger.LogError("Failed to create custom HUD layout '{Path}'", path);

            return false;
        }

        return true;
    }

    private void Push(IGameClient client)
    {
        if (_current is not { } panel || !EnsureLayout())
        {
            return;
        }

        var wrap = _config().WrapLength;

        var title = TextWrapper.Wrap(panel.Title, wrap);
        SetVar(client, TitlePanel, title);
        SetClass(client, TitlePanel, CollapsedClass, title.Length == 0);

        var lines = panel.Lines;

        if (lines.Count > MaxLines)
        {
            _logger.LogWarning("Menu page has {Count} lines, only {Max} are shown", lines.Count, MaxLines);
        }

        for (var n = 0; n < MaxLines; n++)
        {
            var id = LineIds[n];

            if (n >= lines.Count)
            {
                SetClass(client, id, CollapsedClass, true);

                continue;
            }

            var line = lines[n];

            // A single space keeps the label's height for empty lines.
            var text = line.Kind == PanelLineKind.Spacer || line.Text.Length == 0
                ? " "
                : TextWrapper.Wrap(line.Text, wrap, line.Slot != 0 ? ContinuationIndent : string.Empty);

            SetVar(client, LineVars[n], text);
            SetClass(client, id, MutedClass, line.Kind == PanelLineKind.Muted);
            SetClass(client, id, CollapsedClass, false);
        }

        SetClass(client, RootPanel, HiddenClass, false);
    }

    private void SetVar(IGameClient client, string variable, string value)
    {
        if (_sentVars.TryGetValue(variable, out var sent) && sent == value)
        {
            return;
        }

        _layout!.SetDialogVariableStringForPlayer(client, RootPanel, variable, value);
        _sentVars[variable] = value;
    }

    private void SetClass(IGameClient client, string panelId, string className, bool enabled)
    {
        var key = string.Concat(panelId, "|", className);

        if (_sentClasses.TryGetValue(key, out var sent) && sent == enabled)
        {
            return;
        }

        _layout!.SetClassOverrideForPlayer(client,
                                           panelId,
                                           className,
                                           enabled ? HudPanelClassStatus.ForceEnable : HudPanelClassStatus.ForceDisable);

        _sentClasses[key] = enabled;
    }
}
