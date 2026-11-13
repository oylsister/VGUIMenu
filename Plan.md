# VGUIMenu — Implementation Plan

A CS:GO / CS:S style "radio" menu for CS2. It renders with a Panorama `custom_hud_layout` (`.vxml` + `.vcss`), runs on ModSharp (C#), and exposes a shared API (`VGUIMenu.Shared`) that other modules can use.

Source spec: [docs/VGUIMenu.docx](docs/VGUIMenu.docx)

---

## 0. Reference material

| What | Where | Use it for |
|---|---|---|
| Render/pagination algorithm | `D:\Github\sourcemod\core\MenuManager.cpp` → `RenderMenu()` | Porting slot layout (items, spacer, Back/Next/Exit) |
| Key press / state machine | `D:\Github\sourcemod\core\MenuStyle_Base.cpp` → `ClientPressedKey`, `DoClientMenu`, `RedoClientMenu`, `_CancelClientMenu`, `ProcessWatchList` | Selection, cancel, timeout, interrupt flow |
| Radio display | `D:\Github\sourcemod\core\MenuStyle_Radio.cpp` → `CRadioDisplay::DrawItem`, `CRadioMenu` | Drawing rules per `ITEMDRAW_*`, pagination = max − 3 |
| Public API shape | `D:\Github\sourcemod\plugins\include\menus.inc`, `public\IMenuManager.h` | Enums: `ITEMDRAW_*`, `MENUFLAG_*`, `MenuCancel_*`, `MenuEnd_*`, `MenuAction_*` |
| Menu sounds | `D:\Github\sourcemod\gamedata\core.games\common.games.txt` (`MenuItemSound`, `MenuExitSound`, `MenuExitBackSound`) | Sound mapping per selection type |
| Panorama usage (working) | `D:\Github\MS-EntWatch\MS-EntWatch\PanoramaService.cs` | `IPanoramaManager.CreateLayout`, `SetDialogVariableStringForPlayer`, `SetClassOverrideForPlayer`, `Kill()` |
| ModSharp module conventions | `D:\Github\modsharp-public\Sharp.Modules\MenuManager\{Shared,src}` | `IModSharpModule`, `RegisterSharpModuleInterface`, `InstallCommandListener`, `IClientListener`, csproj layout |
| Layout example | [.asset/panorama/layout/custom_game/entwatch_ed.vxml](.asset/panorama/layout/custom_game/entwatch_ed.vxml), [.asset/panorama/styles/custom_game/entwatch_ed.vcss](.asset/panorama/styles/custom_game/entwatch_ed.vcss) | `{s:var}` dialog variables + `Disabled` class toggle |

Panorama API available in `ModSharp.Sharp.Shared 2.1.227` (checked against the DLL):
`IPanoramaManager { GetManagedLayouts, CreateLayout, InstallClickListener/RemoveClickListener, InstallClickCallback/RemoveClickCallback }`,
`ICustomHudLayout { SetDialogVariableString[ForPlayer], ClearDialogVariableStringForPlayer, SetClassOverride[ForPlayer], Kill, IsValid }`, `HudPanelClassStatus`.

---

## 1. Requirements (from spec)

- [x] Rendered with `custom_hud_layout` + `.vxml` / `.vcss`. No HTML center text.
- [x] Semi-transparent black background.
- [x] Lines longer than **32 characters** wrap to a new line.
- [x] Menu sits on the **left side** of the screen (below the money HUD, as in the screenshots).
- [x] **Title** is light yellow (it can span several lines, like the ZMarket example).
- [x] **Slots 1–6** are choices, drawn in yellow.
- [x] **Slot 7 = Back**, **slot 8 = Next**. They appear only when the menu has more than 6 items.
- [x] **Slot 9 = Exit**, always.
- [x] Choices 1–6 can be **Disabled** (light yellow, can't be selected) or a **Spacer** (empty line that still uses up a number).
- [x] A sound plays on select, exit, and back/next.
- [x] Players pick an option with chat commands `!1`–`!9`; the silent form `/1`–`/9` also works.
- [x] Logic lives in ModSharp. The public API lives in a separate assembly, **`VGUIMenu.Shared`**.

---

## 2. Solution layout

- [x] `VGUIMenu.slnx` — *also `VGUIMenu.Tests/` (xunit)*
  - [x] `VGUIMenu.Shared/` — interfaces, `Menu` builder, enums, delegates. References `ModSharp.Sharp.Shared` with `Private=false`, `ExcludeAssets=runtime`.
  - [x] `VGUIMenu/` (core module) — `IModSharpModule` implementation, renderer, player state, commands, sounds. References Shared.
- [x] Set up both csprojs like ModSharp's MenuManager: `EnableDynamicLoading`, `PlatformTarget x64`, `net10.0`. Change `ModSharp.Sharp.Shared` to `Private=false` / `ExcludeAssets=runtime`. — *core also has a `PackageModule` target that lays out `build/`*
- [x] Delete the stub `VGUIMenu/Program.cs`.
- [x] `.asset/` → `panorama/layout/custom_game/vguimenu.vxml`, `panorama/styles/custom_game/vguimenu.vcss`.

---

## 3. Panorama layout (`vguimenu.vxml` / `.vcss`)

> **As built:** the layout uses `title` + 12 generic `line1..line12` labels instead of `slot1..9` + `gap`, because padding spacers, the gap line and the control slots move between pages. Disabled lines use the `Muted` class. Keep `HudPresenter.MaxLines` in sync with the `.vxml`.

Every line is its own fixed Label, so each line can carry its own style (yellow / light yellow / hidden) through class overrides. This avoids one big text blob.

```xml
<root>
  <styles><include src="s2r://panorama/styles/custom_game/vguimenu.vcss_c" /></styles>
  <Panel class="Root">
    <Panel id="vguimenu" class="Hidden">
      <Label id="title" class="Title" text="{s:title}" />
      <Label id="slot1" class="Item" text="{s:slot1}" />
      ... slot2 – slot6 ...
      <Label id="gap"   class="Item Spacer" text=" " />
      <Label id="slot7" class="Item" text="{s:slot7}" />
      <Label id="slot8" class="Item" text="{s:slot8}" />
      <Label id="slot9" class="Item" text="{s:slot9}" />
    </Panel>
  </Panel>
</root>
```

- [x] `#vguimenu`: `horizontal-align: left`, `vertical-align: center` (or tune `margin-top`), `margin-left`, `flow-children: down`, `background-color: rgba(0,0,0,0.5)`, `padding`, `border-radius`.
- [x] `.Title`: light yellow (`#FFF3C4`, sampled), bold.
- [x] `.Item`: yellow/orange (`#E5A34C`, sampled), bold, `text-shadow`.
- [x] `.Item.Disabled`: light yellow / dimmed (same tone as title).
- [x] `.Spacer`: keeps line height, empty text.
- [x] `.Collapsed`: `visibility: collapse` for unused slots (7/8 when not paginated, the gap line, etc.).
- [x] `#vguimenu.Hidden`: `opacity: 0` (with a transition like entwatch).
- [ ] Compile with CS2 Workshop Tools → `vguimenu.vxml_c` / `vguimenu.vcss_c`. Ship them in the server's workshop addon so clients download them.
- [x] **Spike:** check whether `\n` inside a dialog variable renders as a line break in a Label. If it doesn't, give wrapped lines their own Labels or set `html="true"` and use `<br>`. — *confirmed: `\n` breaks the line, so wrapped text stays one label per item*
- [x] **Spike:** check whether one shared layout with `…ForPlayer` overrides is enough for all players, or whether each player needs their own layout. EntWatch creates one per player. Pick whichever avoids leaking one player's menu to others. — *decided: one layout per player, private to its owner. `ITransmitManager.AddEntityHooks(layout, defaultTransmit: false)` + `SetEntityState(layout, ownerController, true)`, and `Observable = false` so spectators can't see it either. The hook is removed on disconnect*

---

## 4. Shared API (`VGUIMenu.Shared`)

Modeled on SourceMod `menus.inc`, written in idiomatic C#.

> **As built:** `Menu` is a concrete class (not an `IVGUIMenu` interface) that consumers `new` up, and display / cancel live on `IVGUIMenuManager` (`DisplayMenu`, `DisplayMenuAtItem`, `CancelMenu`). The handler is `void MenuHandler(Menu, MenuActionContext)`: the context carries item, reasons, and the `Style` / `DisplayText` overrides instead of SM's `param2` + return value. `GetClientMenu` returns `Menu?` (no `MenuSource`, since there are no raw panels yet).

- [x] `IVGUIMenuManager` (`const string Identity = nameof(IVGUIMenuManager)`)
  - [x] `IVGUIMenu CreateMenu(MenuHandler handler, MenuAction actions = MenuAction.Default)` — *`new Menu(handler, actions)`*
  - [x] `MenuSource GetClientMenu(IGameClient client, out IVGUIMenu? menu)` — *`Menu? GetClientMenu(client)`*
  - [x] `bool CancelClientMenu(IGameClient client, bool autoIgnore = false)`
  - [x] `bool IsInMenu(IGameClient client)`
- [x] `IVGUIMenu` — *`Menu` class*
  - [x] `Title` / `SetTitle(string)` / `SetTitle(Func<IGameClient,string>)` (per-client / localized)
  - [x] `AddItem(string info, string display, ItemDraw style = Default)`, `InsertItem`, `RemoveItem`, `RemoveAllItems`, `GetItem`, `ItemCount`
  - [x] `Pagination` (default 6; `0` = no pagination, up to 8 items + Exit)
  - [x] `ExitButton` (default true), `ExitBackButton`, `OptionFlags` (`MenuFlags.NoSound`)
  - [x] `Display(IGameClient client, int time = 0)`, `DisplayAt(client, int firstItem, int time)` — *on the manager: `DisplayMenu` / `DisplayMenuAtItem`*
  - [x] `Cancel()` (closes the menu for everyone viewing it) — *`IVGUIMenuManager.CancelMenu(menu)`*
- [ ] Optional: `IVGUIPanel` for raw panels (`SetTitle`, `DrawItem`, `DrawText`, `Send`), like SM `Panel`. **Phase 2.**
- [x] Enums
  - [x] `ItemDraw` flags: `Default, Disabled, RawLine, NoText, Spacer, Ignore, Control`
  - [x] `MenuAction` flags: `Start, Display, Select, Cancel, End, DrawItem, DisplayItem`
  - [x] `MenuCancelReason`: `Disconnected, Interrupted, Exit, NoDisplay, Timeout, ExitBack`
  - [x] `MenuEndReason`: `Selected, Cancelled, Exit, ExitBack`
  - [ ] `MenuSource`: `None, External, Normal, RawPanel` — *not needed until raw panels exist*
- [x] `delegate int MenuHandler(IVGUIMenu menu, MenuAction action, IGameClient client, int param2)`, keeping SM semantics. Also offer a convenience `AddItem(string display, Action<IGameClient>? onSelect)` overload for simple use. — *see "As built" above; `AddItem(display, onSelect)` and a per-item `OnSelect` exist*
- ~~Out of scope: vote menus (`VoteMenu`, `NoVoteButton`) and shuffle/client mapping. Revisit later.~~ — *dropped: no vote menus*

---

## 5. Core module (`VGUIMenu`)

### 5.1 Module wiring
- [x] `VGUIMenu : IModSharpModule, IClientListener` with constructor `(ISharedSystem, string dllPath, string sharpPath, Version, IConfiguration, bool hotReload)`.
- [x] `Init()`: get `IPanoramaManager`, `IClientManager`, `IModSharp` (for timers/frame hooks), and install the client listener.
- [x] `PostInit()`: `RegisterSharpModuleInterface<IVGUIMenuManager>(this, Identity, impl)`.
- [x] `Shutdown()`: kill all HUDs, remove listeners and commands.
- [x] Map change / round start: HUD entities get destroyed, so mark layouts invalid and recreate them when needed (EntWatch's `KillAllHud` + `IsValid()` check). — *`OnGameDeactivate` forgets layouts; `Think` recreates a lost layout for open menus*

### 5.2 Per-player state (`MenuPlayerState`, port of `menu_states_t` + `CBaseMenuPlayer`)
- [x] `InMenu`, `AutoIgnore`, `Menu`, `Handler`, `FirstItem`, `LastItem`, `ItemOnPage`
- [x] `Slots[10]` → `{ ItemSelection Type; int Item }`, with `ItemSelection { None, Item, Back, Next, Exit, ExitBack }`
- [x] `MenuStartTime`, `MenuHoldTime`, `ICustomHudLayout? Hud`

### 5.3 Renderer (port of `MenuManager::RenderMenu`)
- [x] Port the ascending/descending page walk (`ItemOrder`) that sets `FirstItem`, `LastItem`, `displayPrev`, `displayNext`. — *fixed an off-by-one in the descending reset (`startItem + 1 < maxItems`)*
- [x] Fire `MenuAction.DrawItem` (style override) and `MenuAction.DisplayItem` (text override).
- [x] Fill a `RenderedPage` with title lines, plus `slot1..9` text and class (`Item` / `Disabled` / `Spacer` / `Collapsed`). — *`MenuPanel` with `PanelLine`s*
- [x] Control slots: 7 = Back (or "Back" when ExitBack), 8 = Next, 9 = Exit. When nothing is paginated, collapse 7/8 and keep Exit at 9, like the leader-menu screenshot.
- [ ] Translate "Back", "Next", "Exit" through `LocalizerManager` if it's available, and fall back to English. — *labels are configurable in `vguimenu.jsonc`; LocalizerManager integration not done (it is a separate ModSharp module, not part of Sharp.Shared)*
- [x] Text wrapping: split anything over 32 chars at word boundaries, hard-breaking words that are longer than 32. Continuation lines are indented under the number prefix. — *`TextWrapper`, counts text elements so emoji are never split*
- [x] Unit-test the renderer as a pure function (no engine needed): 0 items, 1 item, 6, 7, 13, disabled, spacer, ExitBack, no-pagination.

### 5.4 Display / HUD push (port of `CRadioDisplay::SendRawDisplay`)
- [x] `EnsureHud(client)`: create `vguimenu.vxml_c` if it's missing or invalid.
- [x] Push `title`, `slot1..9` with `SetDialogVariableStringForPlayer`.
- [x] Set per-slot classes with `SetClassOverrideForPlayer` (`Disabled`, `Spacer`, `Collapsed`).
- [x] Toggle `#vguimenu` `Hidden` off on show and on at close.
- [x] Diff against the last page sent so only changed variables/classes go over the network.

### 5.5 Input (port of `BaseMenuStyle::ClientPressedKey`)
- [x] Register commands `1`–`9`, which makes `!1`–`!9` and silent `/1`–`/9` work. Confirm how ModSharp's command system maps chat triggers. — *`InstallCommandCallback("1".."9")`; chat-trigger mapping still to confirm in-game*
- [x] When the player isn't in a menu, let the command fall through (don't swallow chat). — *returns `Skipped`*
- [x] Handle selection by slot type:
  - [x] `Item` → close, then `Select(item)` → `End(Selected)`
  - [x] `Back` / `Next` → re-render descending/ascending. If rendering fails, cancel with `NoDisplay`.
  - [x] `Exit` / `None` → `Cancel(Exit)` → `End(Exit)` (matches SM: an empty slot exits) — ***changed:** an empty or disabled slot does nothing and the menu stays open, as in CS:GO radio menus (their key mask blocked invalid keys). Only Exit closes it*
  - [x] `ExitBack` → `Cancel(ExitBack)` → `End(ExitBack)`
- ~~Optional: hook `IPanoramaManager.InstallClickCallback` so lines can be clicked later (`CustomHudClicked`). **Phase 2.**~~ — *dropped: input is chat keys only*

### 5.6 Lifecycle (port of `DoClientMenu`, `_CancelClientMenu`, `ProcessWatchList`)
- [x] Display: `Start` → reject bots / not-in-game / `AutoIgnore` with `Cancel(NoDisplay)` + `End(Cancelled)` → cancel the old menu with `Interrupted` → render → send.
- [x] `AutoIgnore` guard so a handler that opens a new menu inside a callback doesn't recurse.
- [x] Timeout: a 0.25 s timer checks `curtime > start + hold` and fires `Cancel(Timeout)`.
- [x] Disconnect → `Cancel(Disconnected)`, then free the HUD.
- [x] `IVGUIMenu.Cancel()` → `Interrupted` for every viewer. Destroying a menu that's still open is deferred, as in `CBaseMenu::Destroy`.

### 5.7 Sounds (port of `MenuManager::GetMenuSound`)
- [x] Config entries `SelectSound`, `ExitSound`, `ExitBackSound` (CS2 sound event names), plus a `menu_sounds` on/off ConVar. — *the ConVar is `ms_vguimenu_sounds`*
- [x] Item/Back/Next → select sound, Exit → exit sound, ExitBack → exit-back sound.
- [x] Emit to the selecting client only. Skip when `MenuFlags.NoSound` is set.
- [ ] Precache / soundevent registration on map load if CS2 needs it. — *the UIPanorama.* defaults are stock sounds; custom ones still need checking*

---

## 6. Testing & delivery

- [x] Renderer unit tests (section 5.3). — *plus `MenuSystemTests`: select / exit / back / next / ExitBack, interrupt, re-entrancy guard, timeout, disconnect, bots, per-player isolation, layout recovery*
- [x] Example consumer module (or a debug command in core, e.g. `ms_vguimenu_test`) that recreates the two spec screenshots: — *`!vguimenu_test [leader|market|styles|wrap]` in core*
  - [x] "[ZLeader] Leader Menu": 6 items + Exit, no pagination
  - [x] "ZMarket" style: multi-line title + 13 items (pages + Back/Next/Exit)
  - [x] A menu with Disabled and Spacer items, and a 40+ char item to check wrapping
- [ ] In-game checks: position/colours at 16:9 and 4:3, two players with different menus at once (no leaking), map change mid-menu, disconnect mid-menu, timeout.
- [x] README: how to consume `IVGUIMenuManager` from another module, plus asset install steps.
- [ ] Optional: publish `VGUIMenu.Shared` as a NuGet package.

---

## 7. Open questions

- [x] Exact colours: sample them from the screenshots, or pick values? — *sampled: title / disabled `#FFF3C4`, items / controls `#E5A34C`, background `rgba(0,0,0,0.23)`*
- [x] Is the default timeout forever (`0`)? — *yes, `Menu.TimeForever` is the default*
- [x] Should VGUIMenu step aside when the stock ModSharp `MenuManager` menu is open (or the other way round)? — *yes, the newest menu wins. A stock menu opened after a VGUIMenu closes it (`Interrupted`, checked in `Think`), and opening a VGUIMenu quits any open stock menu. See `ExternalMenus.cs`; optional, so VGUIMenu still runs without MenuManager*
- [ ] Sound event names to use in CS2.
