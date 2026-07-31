<div align="center">

# SceneXRay

### See every dependency in your Unity scene — before it breaks the build

Unity 2022.3+ · verified on Unity 6000.0 · editor-only · no external dependencies · MIT

**[Documentation](https://andriisviatenko.github.io/SceneXRay/)** · **[Issues](https://github.com/AndriiSviatenko/SceneXRay/issues)**

</div>

---

## What is this?

An editor-only X-ray of your scene's wiring. Every serialized reference, every UnityEvent target,
every ScriptableObject and material a prefab pulls in — drawn as an interactive graph you can
drill into, arrange, and keep. Plus the boring-but-critical parts: missing-reference detection,
dependency cycles, a health score, and a build guard that fails CI when something is broken.

No runtime code, no external packages, no services.

---

## Install

**Into an existing project** — copy `Assets/SceneXRay/` anywhere under your `Assets/`.
It ships its own assembly definition (`autoReferenced: false`), so it compiles on its own and
never leaks into your runtime assemblies.

**As a sample project** — clone this repo and open it in Unity `6000.0.62f1` or newer:

```bash
git clone https://github.com/AndriiSviatenko/SceneXRay.git
```

There is no bootstrap call, no prefab to drop in a scene, and no settings to fill in.

---

## Quick start

```
1. Tools ▸ SceneXRay ▸ Open Graph View        (Ctrl+Shift+X)
2. Press Refresh — the loaded scenes are scanned
3. Double-click any node to drill into it, Backspace to go back
```

---

## Features

| System | What it does |
|--------|-------------|
| **Dependency graph** | GraphView canvas of the loaded scenes — nodes are objects, edges are references |
| **Drill-in focus** | Double-click a node for its ego network; radius 1–3, direction In / Out / Both |
| **Browser navigation** | Back / Forward with full camera + layout restore, clickable breadcrumb trail |
| **Neighborhood highlight** | Hover a node — neighbours light up, outgoing edges go amber, incoming cyan, rest dims |
| **Four layouts** | Auto · Tree (layered DAG) · Force (packed columns) · Radial (rings around the focus) |
| **Saved arrangements** | Arrange the graph your way, save it per scene, auto-restore next time you open it |
| **Undo / Redo** | `Ctrl+Z` / `Ctrl+Y` for node moves and re-layouts, 32 steps deep |
| **Zoom LOD** | Cards shed detail and grow their titles as you zoom out — readable at 20% |
| **MiniMap** | Draggable, resizable overview of the whole graph |
| **Health score** | Missing references, dependency cycles and god objects, scored live in the toolbar |
| **Fix Missing** | Lists every broken reference and rebinds by name where it can |
| **Global search** | Find what references any object across scenes and the project |
| **Bookmarks** | Pin objects for quick jumps — `Ctrl+Shift+Alt+J` to toggle, `…+K` to open |
| **Inspector strip** | References / Referenced By cards on GameObjects, prefab assets and ScriptableObjects |
| **Scene overlay** | Dependency lines drawn in the Scene View, colour-coded by link type |
| **Snapshots & diff** | Checkpoint the graph, compare two snapshots, or diff two scene assets |
| **Export** | JSON · CSV · HTML (D3.js) · PlantUML · Mermaid · Markdown |
| **Build guard + CLI** | Quality gates in batchmode, non-zero exit code when the scene is broken |
| **Localization** | English + Ukrainian, switchable in Project Settings |

---

## Graph controls

| Input | Action |
|-------|--------|
| `Double-click` / `Enter` | Focus the node (its ego network) |
| `Backspace` / `Alt+←` | Back · `Alt+→` Forward · `Esc` up one level |
| `=` / `−` | Grow / shrink the focus radius |
| `←↑↓→` | Move the selection between cards, camera follows |
| `Ctrl+wheel` | Zoom · `MMB drag` or `Alt+drag` pan · `A` fit · `0` reset zoom |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo node arrangement |
| `Ctrl+S` | Save this scene's arrangement |
| `Ctrl+F` | Jump to the search field |
| `Right-click` | Focus · expand · select in Hierarchy/Project · view references |

Clicking a node selects the underlying object in the Hierarchy (or pings the asset in the
Project window), so the Inspector always follows what you are looking at.

Every shortcut is rebindable under `Edit ▸ Shortcuts` — the in-graph ones under the `SceneXRay`
category, the window/bookmark chords under `Main Menu`.

---

## What gets scanned

| Link type | Source | Example |
|-----------|--------|---------|
| **Direct** | serialized `GameObject` / `Component` field | `PlayerController.visualRoot → Visual` |
| **UnityEvent** | persistent listener target + method | `Button.m_OnClick → GameManager.Restart()` |
| **Asset** | any referenced project asset | `MeshRenderer → M_Player`, `TMP_Text → LiberationSans SDF` |
| **Missing** | serialized reference whose target is gone | `Spawner.target → Missing` |

Components listed in **Ignored Components** are skipped (`Transform`, `RectTransform` by default),
`m_Script` self-links are never recorded, and Unity's built-in resources are excluded unless you
opt in — so the graph shows *your* wiring, not engine noise.

---

## Settings

`Project Settings ▸ SceneXRay`

| Group | Setting | Effect |
|-------|---------|--------|
| Overlay | Enable / Animate | Scene View dependency lines |
| Colors | Direct · UnityEvent · Missing · Asset · Line Width | Applies to **both** the overlay and the graph edges, live |
| Inspector | Inspector Integration | The References / Referenced By strip |
| Graph | Max Nodes In Graph | Page size for very large scenes |
| Scanning | Ignored Components · Scan Asset References · Include Built-in Assets | What the scanner records — changing these rescans immediately |
| Build | Build Guard · Fail Build On Missing Refs | CI enforcement |
| Language | English / Українська | Editor UI language |

---

## CI / batchmode

```bash
Unity -batchmode -quit -projectPath . \
      -executeMethod SceneXRay.Editor.Core.CommandLine.ScanScene \
      -scene Assets/Scenes/Main.unity \
      -output dependencies.json
```

Writes every link as JSON and exits with code `1` when a quality gate fails, so a broken
reference stops the pipeline instead of shipping.

---

## Repository layout

```
Assets/SceneXRay/          the asset — copy this folder into your own project
  Editor/Core/             scanning, reverse index, analysis, storage
  Editor/UI/               graph canvas, inspector strip, scene overlay
  Editor/Windows/          graph, search, bookmarks, references, fix-missing, diff
  Editor/Exporters/        JSON · CSV · HTML · PlantUML · Mermaid · Markdown
  Tests/Editor/            20 EditMode tests
docs/index.html            the documentation page, published via GitHub Pages
Assets/Scenes/             sample scene for trying the tool
```

Saved arrangements live in `UserSettings/SceneXRay/`, caches and snapshots in `Library/SceneXRay/` —
nothing SceneXRay writes ends up in version control.

---

## Requirements

- Unity **2022.3 LTS or newer** (verified on Unity 6000.0)
- No packages, no third-party libraries, no runtime footprint

---

## License

[MIT](LICENSE) — free for personal and commercial use.

---

<div align="center">
Editor tooling for Unity developers who inherited someone else's scene
</div>
