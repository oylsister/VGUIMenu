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

- [ ] Rendered with `custom_hud_layout` + `.vxml` / `.vcss`. No HTML center text.
- [ ] Semi-transparent black background.
- [ ] Lines longer than **32 characters** wrap to a new line.
- [ ] Menu sits on the **left side** of the screen (below the money HUD, as in the screenshots).
- [ ] **Title** is light yellow (it can span several lines, like the ZMarket example).
- [ ] **Slots 1–6** are choices, drawn in yellow.
- [ ] **Slot 7 = Back**, **slot 8 = Next**. They appear only when the menu has more than 6 items.
- [ ] **Slot 9 = Exit**, always.
- [ ] Choices 1–6 can be **Disabled** (light yellow, can't be selected) or a **Spacer** (empty line that still uses up a number).
- [ ] A sound plays on select, exit, and back/next.
- [ ] Players pick an option with chat commands `!1`–`!9`; the silent form `/1`–`/9` also works.
- [ ] Logic lives in ModSharp. The public API lives in a separate assembly, **`VGUIMenu.Shared`**.

---

## 2. Solution layout

- [ ] `VGUIMenu.slnx`
  - [ ] `VGUIMenu.Shared/` — interfaces, `Menu` builder, enums, delegates. References `ModSharp.Sharp.Shared` with `Private=false`, `ExcludeAssets=runtime`.
  - [ ] `VGUIMenu/` (core module) — `IModSharpModule` implementation, renderer, player state, commands, sounds. References Shared.
- [ ] Set up both csprojs like ModSharp's MenuManager: `EnableDynamicLoading`, `PlatformTarget x64`, `net10.0`. Change `ModSharp.Sharp.Shared` to `Private=false` / `ExcludeAssets=runtime`.
- [ ] Delete the stub `VGUIMenu/Program.cs`.
- [ ] `.asset/` → `panorama/layout/custom_game/vguimenu.vxml`, `panorama/styles/custom_game/vguimenu.vcss`.

---

## 3. Panorama layout (`vguimenu.vxml` / `.vcss`)

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

- [ ] `#vguimenu`: `horizontal-align: left`, `vertical-align: center` (or tune `margin-top`), `margin-left`, `flow-children: down`, `background-color: rgba(0,0,0,0.5)`, `padding`, `border-radius`.
- [ ] `.Title`: light yellow (≈ `#FFF2C2`), bold.
- [ ] `.Item`: yellow/orange (≈ `#F0A830`), bold, `text-shadow`.
- [ ] `.Item.Disabled`: light yellow / dimmed (same tone as title).
- [ ] `.Spacer`: keeps line height, empty text.
- [ ] `.Collapsed`: `visibility: collapse` for unused slots (7/8 when not paginated, the gap line, etc.).
- [ ] `#vguimenu.Hidden`: `opacity: 0` (with a transition like entwatch).
- [ ] Compile with CS2 Workshop Tools → `vguimenu.vxml_c` / `vguimenu.vcss_c`. Ship them in the server's workshop addon so clients download them.
- [ ] **Spike:** check whether `\n` inside a dialog variable renders as a line break in a Label. If it doesn't, give wrapped lines their own Labels or set `html="true"` and use `<br>`.
- [ ] **Spike:** check whether one shared layout with `…ForPlayer` overrides is enough for all players, or whether each player needs their own layout. EntWatch creates one per player. Pick whichever avoids leaking one player's menu to others.

---

## 4. Shared API (`VGUIMenu.Shared`)

Modeled on SourceMod `menus.inc`, written in idiomatic C#.

- [ ] `IVGUIMenuManager` (`const string Identity = nameof(IVGUIMenuManager)`)
  - [ ] `IVGUIMenu CreateMenu(MenuHandler handler, MenuAction actions = MenuAction.Default)`
  - [ ] `MenuSource GetClientMenu(IGameClient client, out IVGUIMenu? menu)`
  - [ ] `bool CancelClientMenu(IGameClient client, bool autoIgnore = false)`
  - [ ] `bool IsInMenu(IGameClient client)`
- [ ] `IVGUIMenu`
  - [ ] `Title` / `SetTitle(string)` / `SetTitle(Func<IGameClient,string>)` (per-client / localized)
  - [ ] `AddItem(string info, string display, ItemDraw style = Default)`, `InsertItem`, `RemoveItem`, `RemoveAllItems`, `GetItem`, `ItemCount`
  - [ ] `Pagination` (default 6; `0` = no pagination, up to 8 items + Exit)
  - [ ] `ExitButton` (default true), `ExitBackButton`, `OptionFlags` (`MenuFlags.NoSound`)
  - [ ] `Display(IGameClient client, int time = 0)`, `DisplayAt(client, int firstItem, int time)`
  - [ ] `Cancel()` (closes the menu for everyone viewing it)
- [ ] Optional: `IVGUIPanel` for raw panels (`SetTitle`, `DrawItem`, `DrawText`, `Send`), like SM `Panel`. **Phase 2.**
- [ ] Enums
  - [ ] `ItemDraw` flags: `Default, Disabled, RawLine, NoText, Spacer, Ignore, Control`
  - [ ] `MenuAction` flags: `Start, Display, Select, Cancel, End, DrawItem, DisplayItem`
  - [ ] `MenuCancelReason`: `Disconnected, Interrupted, Exit, NoDisplay, Timeout, ExitBack`
  - [ ] `MenuEndReason`: `Selected, Cancelled, Exit, ExitBack`
  - [ ] `MenuSource`: `None, External, Normal, RawPanel`
- [ ] `delegate int MenuHandler(IVGUIMenu menu, MenuAction action, IGameClient client, int param2)`, keeping SM semantics. Also offer a convenience `AddItem(string display, Action<IGameClient>? onSelect)` overload for simple use.
- [ ] Out of scope: vote menus (`VoteMenu`, `NoVoteButton`) and shuffle/client mapping. Revisit later.

---

## 5. Core module (`VGUIMenu`)

### 5.1 Module wiring
- [ ] `VGUIMenu : IModSharpModule, IClientListener` with constructor `(ISharedSystem, string dllPath, string sharpPath, Version, IConfiguration, bool hotReload)`.
- [ ] `Init()`: get `IPanoramaManager`, `IClientManager`, `IModSharp` (for timers/frame hooks), and install the client listener.
- [ ] `PostInit()`: `RegisterSharpModuleInterface<IVGUIMenuManager>(this, Identity, impl)`.
- [ ] `Shutdown()`: kill all HUDs, remove listeners and commands.
- [ ] Map change / round start: HUD entities get destroyed, so mark layouts invalid and recreate them when needed (EntWatch's `KillAllHud` + `IsValid()` check).

### 5.2 Per-player state (`MenuPlayerState`, port of `menu_states_t` + `CBaseMenuPlayer`)
- [ ] `InMenu`, `AutoIgnore`, `Menu`, `Handler`, `FirstItem`, `LastItem`, `ItemOnPage`
- [ ] `Slots[10]` → `{ ItemSelection Type; int Item }`, with `ItemSelection { None, Item, Back, Next, Exit, ExitBack }`
- [ ] `MenuStartTime`, `MenuHoldTime`, `ICustomHudLayout? Hud`

### 5.3 Renderer (port of `MenuManager::RenderMenu`)
- [ ] Port the ascending/descending page walk (`ItemOrder`) that sets `FirstItem`, `LastItem`, `displayPrev`, `displayNext`.
- [ ] Fire `MenuAction.DrawItem` (style override) and `MenuAction.DisplayItem` (text override).
- [ ] Fill a `RenderedPage` with title lines, plus `slot1..9` text and class (`Item` / `Disabled` / `Spacer` / `Collapsed`).
- [ ] Control slots: 7 = Back (or "Back" when ExitBack), 8 = Next, 9 = Exit. When nothing is paginated, collapse 7/8 and keep Exit at 9, like the leader-menu screenshot.
- [ ] Translate "Back", "Next", "Exit" through `LocalizerManager` if it's available, and fall back to English.
- [ ] Text wrapping: split anything over 32 chars at word boundaries, hard-breaking words that are longer than 32. Continuation lines are indented under the number prefix.
- [ ] Unit-test the renderer as a pure function (no engine needed): 0 items, 1 item, 6, 7, 13, disabled, spacer, ExitBack, no-pagination.

### 5.4 Display / HUD push (port of `CRadioDisplay::SendRawDisplay`)
- [ ] `EnsureHud(client)`: create `vguimenu.vxml_c` if it's missing or invalid.
- [ ] Push `title`, `slot1..9` with `SetDialogVariableStringForPlayer`.
- [ ] Set per-slot classes with `SetClassOverrideForPlayer` (`Disabled`, `Spacer`, `Collapsed`).
- [ ] Toggle `#vguimenu` `Hidden` off on show and on at close.
- [ ] Diff against the last page sent so only changed variables/classes go over the network.

### 5.5 Input (port of `BaseMenuStyle::ClientPressedKey`)
- [ ] Register commands `1`–`9`, which makes `!1`–`!9` and silent `/1`–`/9` work. Confirm how ModSharp's command system maps chat triggers.
- [ ] When the player isn't in a menu, let the command fall through (don't swallow chat).
- [ ] Handle selection by slot type:
  - [ ] `Item` → close, then `Select(item)` → `End(Selected)`
  - [ ] `Back` / `Next` → re-render descending/ascending. If rendering fails, cancel with `NoDisplay`.
  - [ ] `Exit` / `None` → `Cancel(Exit)` → `End(Exit)` (matches SM: an empty slot exits)
  - [ ] `ExitBack` → `Cancel(ExitBack)` → `End(ExitBack)`
- [ ] Optional: hook `IPanoramaManager.InstallClickCallback` so lines can be clicked later (`CustomHudClicked`). **Phase 2.**

### 5.6 Lifecycle (port of `DoClientMenu`, `_CancelClientMenu`, `ProcessWatchList`)
- [ ] Display: `Start` → reject bots / not-in-game / `AutoIgnore` with `Cancel(NoDisplay)` + `End(Cancelled)` → cancel the old menu with `Interrupted` → render → send.
- [ ] `AutoIgnore` guard so a handler that opens a new menu inside a callback doesn't recurse.
- [ ] Timeout: a ~0.5 s timer checks `curtime > start + hold` and fires `Cancel(Timeout)`.
- [ ] Disconnect → `Cancel(Disconnected)`, then free the HUD.
- [ ] `IVGUIMenu.Cancel()` → `Interrupted` for every viewer. Destroying a menu that's still open is deferred, as in `CBaseMenu::Destroy`.

### 5.7 Sounds (port of `MenuManager::GetMenuSound`)
- [ ] Config entries `SelectSound`, `ExitSound`, `ExitBackSound` (CS2 sound event names), plus a `menu_sounds` on/off ConVar.
- [ ] Item/Back/Next → select sound, Exit → exit sound, ExitBack → exit-back sound.
- [ ] Emit to the selecting client only. Skip when `MenuFlags.NoSound` is set.
- [ ] Precache / soundevent registration on map load if CS2 needs it.

---

## 6. Testing & delivery

- [ ] Renderer unit tests (section 5.3).
- [ ] Example consumer module (or a debug command in core, e.g. `ms_vguimenu_test`) that recreates the two spec screenshots:
  - [ ] "[ZLeader] Leader Menu": 6 items + Exit, no pagination
  - [ ] "ZMarket" style: multi-line title + 13 items (pages + Back/Next/Exit)
  - [ ] A menu with Disabled and Spacer items, and a 40+ char item to check wrapping
- [ ] In-game checks: position/colours at 16:9 and 4:3, two players with different menus at once (no leaking), map change mid-menu, disconnect mid-menu, timeout.
- [ ] README: how to consume `IVGUIMenuManager` from another module, plus asset install steps.
- [ ] Optional: publish `VGUIMenu.Shared` as a NuGet package.

---

## 7. Open questions

- [ ] Exact colours: sample them from the screenshots, or pick values?
- [ ] Is the default timeout forever (`0`)?
- [ ] Should VGUIMenu step aside when the stock ModSharp `MenuManager` menu is open (or the other way round)?
- [ ] Sound event names to use in CS2.
