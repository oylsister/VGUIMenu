using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace VGUIMenu;

/// <summary>Another menu system that can be shown to a client at the same time as VGUIMenu.</summary>
internal interface IExternalMenuSource
{
    /// <summary>Id of the client's open external menu (changes every time a new one is shown), or null.</summary>
    ulong? GetOpenMenu(IGameClient client);

    /// <summary>Closes the client's open external menu.</summary>
    void Close(IGameClient client);
}

/// <summary>ModSharp's stock MenuManager (Sharp.Modules.MenuManager).</summary>
internal sealed class StockMenuSource : IExternalMenuSource
{
    private readonly IModSharpModuleInterface<IMenuManager> _menus;

    private StockMenuSource(IModSharpModuleInterface<IMenuManager> menus)
        => _menus = menus;

    /// <summary>
    ///     Returns null when the MenuManager module (or its shared assembly) is not installed.
    ///     All MenuManager types are touched only inside this class, so VGUIMenu still loads without it.
    /// </summary>
    public static IExternalMenuSource? TryCreate(ISharpModuleManager modules, ILogger logger)
    {
        try
        {
            return Resolve(modules);
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            logger.LogInformation("ModSharp MenuManager not installed, VGUIMenu won't coordinate with it");

            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IExternalMenuSource? Resolve(ISharpModuleManager modules)
        => modules.GetOptionalSharpModuleInterface<IMenuManager>(IMenuManager.Identity) is { } menus
            ? new StockMenuSource(menus)
            : null;

    private IMenuManager? Instance
        => _menus.IsAvailable ? _menus.Instance : null;

    public ulong? GetOpenMenu(IGameClient client)
        => Instance is { } menus && menus.TryGetCurrentMenuSessionId(client, out var session) ? session : null;

    public void Close(IGameClient client)
    {
        if (Instance is { } menus && menus.IsInMenu(client))
        {
            menus.QuitMenu(client);
        }
    }
}
