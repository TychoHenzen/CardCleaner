# e2e: drive the real game on a hidden desktop

`tools/e2e/e2e.mjs` starts the game with full rendering on a hidden Win32 desktop and drives it over a local socket:
keyboard and mouse input, symbolic moves (walk to a node, look at a point, use a node), scene inspection and
screenshots. Nothing appears on the user's screen at any point: no window, no taskbar button, no focus change, no
cursor clip.

## Quick start

```bash
node tools/e2e/e2e.mjs start --scene res://Scenes/Gameplay/ShopScene.tscn
node tools/e2e/e2e.mjs send state
node tools/e2e/e2e.mjs send use node=World/Markers/PcLocation/PcTerminal
node tools/e2e/e2e.mjs send press text=Stock
node tools/e2e/e2e.mjs shot pc-stock
node tools/e2e/e2e.mjs stop
```

Screenshots land in `reports/e2e/<session>/shots/<name>.png`. Open them with the Read tool to inspect them.

## CLI

Every verb takes `--session <name>` (default `default`). Each session has its own port, folder, hidden desktop and
save data, so several can run at once.

| Verb | What it does |
|------|--------------|
| `start [--scene res://..tscn] [--resolution 1280x720] [--no-build] [--keep-data] [--timeout-min 120]` | Builds the launcher (when stale) and the game, starts Godot on a hidden desktop, waits for the agent to answer `ping` (up to 180 s). Without `--keep-data` the session's save data is wiped first. `--timeout-min` kills the game after that long. |
| `send <cmd> [json \| key=value ...]` | Sends one command and prints the reply. `key=value` values are parsed as JSON when they can be (`yaw=90`, `target=[1,0,2]`), else kept as strings. Exits 1 when the reply is an error. |
| `shot <name> [width]` | `screenshot` into the session's `shots/` folder. |
| `run <script.json>` | Runs a JSON array of `{"cmd": ..., ...args}` steps and stops at the first error. A `screenshot` step may give `name` instead of `path`. |
| `status` | Prints the session record, whether the launcher is still running, and any breaches. |
| `log [n]` | Tail of the session's `godot.log`. |
| `stop` | Sends `quit`, then kills the launcher (which kills Godot with it). |

The Godot binary is `%GODOT%`, or `C:\Development\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe`.

## Commands

`send help` lists them with their arguments. Node paths are absolute (`/root/Root/...` in the shop, whose root node is
named `Root`), relative to the current scene, or a bare node name found anywhere under it. Prefer relative paths from
Git Bash: it rewrites an argument that starts with `/` into a Windows path (`C:/Program Files/Git/root/...`), so an
absolute path needs `MSYS_NO_PATHCONV=1` in front of the command.

| Group | Commands |
|-------|----------|
| Core | `ping`, `wait {frames?, seconds?}`, `scene {path}`, `state`, `pause {paused}`, `quit {code?}` |
| Input | `key {key, mode?: tap\|down\|up, frames?, shift?, ctrl?, alt?}`, `action {name, mode?, frames?}`, `type {text}`, `mouse_move {dx, dy} \| {x, y}`, `click {x?, y?, button?, double?}`, `wheel {steps, x?, y?}` |
| Motion | `look {yaw?, pitch?, tolerance?}`, `look_at {target \| node}`, `nav_path {target \| node}`, `walk_to {target \| node, stop?, timeout?}`, `use {node, distance?, button?}` |
| Inspect | `tree {node?, depth?}`, `find {group? \| type? \| name?}`, `get {node, property}`, `set {node, property, value}`, `call {node, method, args?}`, `ui {all?}`, `press {text \| node}` |
| Capture | `screenshot {path, width?, viewport?}` (`viewport` renders one SubViewport, such as the PC desktop) |

Motion goes through the player's real controls, so it tests them: `look` sends relative mouse motion and learns the
degrees-per-pixel gain from how far the camera actually turned; `walk_to` takes a path from the navigation map, holds
`ui_up` and steers with mouse motion, and fails with `blocked at ...` when the player makes no progress for 2.5 s;
`use` walks within `distance`, aims at the node and clicks, as the interact input does. Yaw 0 looks along -Z and
positive yaw turns left; pitch is positive up.

## How it stays hidden

- `tools/HiddenDesktop` (`HiddenDesktop.exe run ... -- <exe> <args>`) creates a separate Win32 desktop and starts Godot
  on it with `CREATE_NO_WINDOW`. Windows on another desktop are never shown, focused or listed in the taskbar, yet
  Vulkan renders there normally, so screenshots are real frames.
- Godot sits in a kill-on-close job object: when the launcher dies (or `stop` kills it), Godot dies too. No invisible
  game is left behind.
- `APPDATA` points at `reports/e2e/<session>/appdata`, so `user://` (saves, `settings.cfg`, logs) is the session's own,
  never the player's.
- The game never captures the mouse in an automation run (`AutomationLaunch.SafeMouseMode`): a captured mouse calls
  `ClipCursor`, which clips the user's real cursor even from another desktop. Mouse look reads relative motion, which
  the agent sends directly.
- The launcher watches the user's desktop every 100 ms for a window owned by the game, the game holding the
  foreground, or a cursor clip, releases any clip, and logs each breach to `reports/e2e/<session>/breaches.jsonl`.
  The CLI prints `HIDDEN-DESKTOP BREACH` lines after any command that saw one; there should never be any.

## The in-game agent

`Scripts/Features/Automation/AutomationAgent.cs` is an autoload that does nothing unless the build is a debug build
and the command line carries `-- --automation-port=<n>`. Then it listens on `127.0.0.1:<n>` for JSON lines
(`{"id": 1, "cmd": "look", "args": {"yaw": 90}}`) and answers each with `{"id", "ok", "result"}` or
`{"id", "ok": false, "error"}`. Commands run one at a time on the main thread and may span frames. It keeps running
while the tree is paused.

`Tests/Features/Automation` covers the protocol and drives `look` and `use` against the real shop scene headless.

## Limits

- Windows only (the hidden desktop is a Win32 feature).
- Audio uses the Dummy driver.
- Screenshots need a rendering run; a `--headless` game answers `screenshot` with an error.
