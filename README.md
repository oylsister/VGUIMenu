# VGUIMenu

A CS:GO / CS:S style radio menu for CS2, built on [ModSharp](https://github.com/Kxnrl/modsharp-public).
It's drawn with a Panorama `custom_hud_layout` (no center HTML text), sits on the left of the screen, and players use it through chat:
`!1` – `!9` (or the silent `/1` – `/9`).

```
[ZLeader] Leader Menu        <- title (light yellow, may span several lines)
1. Defend Here               <- items 1-6 (yellow)
2. Follow Me
3. Toggle Beacon             <- disabled items are light yellow; spacers leave an empty numbered line
...
7. Back                      <- only when the menu has more than one page
8. Next
9. Exit                      <- always
```

Lines longer than 32 characters wrap automatically. Each player has their own HUD layout entity, sent only to that player. Other players and spectators never receive it.

## Install

1. Build: `dotnet build -c Release`. The `PackageModule` target lays out a ready-to-copy folder in `build/`:
   ```
   build/modules/VGUIMenu/VGUIMenu.dll
   build/shared/VGUIMenu.Shared/VGUIMenu.Shared.dll
   build/configs/vguimenu.jsonc
   ```
   Copy its contents into the server's `sharp/` folder.
2. Compile the Panorama assets with the CS2 Workshop Tools and ship them in the server's workshop addon so clients download them:
   - `.asset/panorama/layout/custom_game/vguimenu.vxml` → `vguimenu.vxml_c`
   - `.asset/panorama/styles/custom_game/vguimenu.vcss` → `vguimenu.vcss_c`

## Configuration

`sharp/configs/vguimenu.jsonc` is created on first start and re-read on every map start.

| Key | Default | Meaning |
|---|---|---|
| `LayoutPath` | `panorama/layout/custom_game/vguimenu.vxml_c` | Compiled layout to load |
| `WrapLength` | `32` | Wrap lines longer than this |
| `Sounds.Enabled` / `Select` / `Exit` / `ExitBack` | on / `UIPanorama.*` | Sound event names (leave one empty to mute it) |
| `Labels.Previous` / `ExitBack` / `Next` / `Exit` | `Back` / `Back` / `Next` / `Exit` | Text of slots 7, 8, 9 |
| `EnableTestCommand` | `true` | Registers `!vguimenu_test`. Turn it off on live servers |

ConVar: `ms_vguimenu_sounds 0/1` switches sounds off or on at runtime.

Test menus: `!vguimenu_test [leader|market|styles|wrap]`.

## Using it from another module

Reference `VGUIMenu.Shared` with `Private=false` / `ExcludeAssets=runtime` (ModSharp loads it from `sharp/shared`):

```xml
<Reference Include="VGUIMenu.Shared">
  <HintPath>path\to\VGUIMenu.Shared.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Get the interface in `OnAllModulesLoaded` (or later):

```csharp
var vgui = _modules.GetRequiredSharpModuleInterface<IVGUIMenuManager>(IVGUIMenuManager.Identity).Instance!;
```

### Simple menu

```csharp
var menu = new Menu();
menu.SetTitle("[ZLeader] Leader Menu");
menu.AddItem("Defend Here", client => Defend(client));
menu.AddItem("Follow Me",   client => Follow(client));
menu.AddItem("Toggle Beacon", null, ItemDraw.Disabled); // shown, not selectable
menu.AddSpacer();                                       // empty line, uses a number

vgui.DisplayMenu(client, menu);            // until closed
vgui.DisplayMenu(client, menu, time: 20);  // closes after 20 s
```

### SourceMod-style handler

```csharp
var menu = new Menu(OnMenu, MenuAction.Default | MenuAction.DrawItem) { ExitBackButton = true };
menu.SetTitle(client => $"Hello {client.Name}\nPick a weapon:");   // per-client, multi-line
menu.AddItem("weapon_ak47", "AK-47");
menu.AddItem("weapon_m4a1", "M4A4");

void OnMenu(Menu menu, MenuActionContext ctx)
{
    switch (ctx.Action)
    {
        case MenuAction.DrawItem when IsRestricted(ctx.MenuItem!.Info):
            ctx.Style = ItemDraw.Disabled;
            break;
        case MenuAction.Select:
            Give(ctx.Client, ctx.MenuItem!.Info);
            vgui.DisplayMenuAtItem(ctx.Client, menu, ctx.FirstItemOnPage); // reopen on the same page
            break;
        case MenuAction.Cancel when ctx.CancelReason == MenuCancelReason.ExitBack:
            OpenParentMenu(ctx.Client);
            break;
    }
}
```

`Select`, `Cancel` and `End` always reach the handler. `Start`, `Display`, `DrawItem` and `DisplayItem` only do when they're included in `actions`.

| Member | Notes |
|---|---|
| `Menu.Pagination` | Items per page, 2–6 (default 6). `Menu.NoPagination` shows up to 8 items + Exit on one page |
| `Menu.ExitButton` | Slot 9 Exit (default on) |
| `Menu.ExitBackButton` | Slot 7 shows "Back" on the first page and cancels with `ExitBack` |
| `Menu.Flags \|= MenuFlags.NoSound` | No sounds for this menu |
| `IVGUIMenuManager.CancelMenu(menu)` | Close for everyone viewing it |
| `CancelClientMenu`, `IsInMenu`, `GetClientMenu` | Per-client control |

Opening a menu while another is open cancels the old one with `Interrupted`. This also covers ModSharp's own MenuManager, if it's installed: the newest menu wins. Opening a VGUIMenu closes an open MenuManager menu, and a MenuManager menu opened later closes the VGUIMenu (within 0.25 s). A menu opened from inside that cancel callback is blocked (`NoDisplay`), as in SourceMod.

## Development

```
dotnet test
```

The renderer, text wrapping and the display / select / cancel flow are covered without the engine.
