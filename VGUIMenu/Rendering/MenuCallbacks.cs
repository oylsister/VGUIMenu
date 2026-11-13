using Microsoft.Extensions.Logging;
using Sharp.Shared.Objects;
using VGUIMenu.Shared;

namespace VGUIMenu.Rendering;

/// <summary>Invokes <see cref="Menu.Handler" /> safely, honouring <see cref="Menu.Actions" />.</summary>
internal static class MenuCallbacks
{
    private const MenuAction AlwaysDelivered = MenuAction.Select | MenuAction.Cancel | MenuAction.End;

    public static ILogger? Logger { get; set; }

    public static void Invoke(Menu menu, MenuActionContext context)
    {
        if (menu.Handler is not { } handler)
        {
            return;
        }

        if ((context.Action & AlwaysDelivered) == 0 && (menu.Actions & context.Action) == 0)
        {
            return;
        }

        try
        {
            handler(menu, context);
        }
        catch (Exception e)
        {
            Logger?.LogError(e, "Menu handler threw during {Action}", context.Action);
        }
    }

    public static void InvokeItemSelect(MenuItem item, IGameClient client)
    {
        if (item.OnSelect is not { } onSelect)
        {
            return;
        }

        try
        {
            onSelect(client);
        }
        catch (Exception e)
        {
            Logger?.LogError(e, "Menu item OnSelect threw");
        }
    }

    public static string BuildTitle(Menu menu, IGameClient client)
    {
        try
        {
            return menu.BuildTitle(client);
        }
        catch (Exception e)
        {
            Logger?.LogError(e, "Menu title factory threw");

            return string.Empty;
        }
    }

    public static string BuildDisplay(MenuItem item, IGameClient client)
    {
        try
        {
            return item.GetDisplay(client);
        }
        catch (Exception e)
        {
            Logger?.LogError(e, "Menu item display factory threw");

            return item.Display;
        }
    }
}
