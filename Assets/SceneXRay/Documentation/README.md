# SceneXRay documentation

SceneXRay is an editor-only dependency explorer for Unity scenes and prefabs. It visualizes
serialized object references, persistent UnityEvent targets, project-asset references, missing
references, and optional code-level lookups in an interactive graph.

## Requirements and compatibility

- Unity 6000.0 LTS (Unity 6) or newer.
- Verified with Unity 6000.0.62f1 and Unity 6000.5.6f1.
- Built-in Render Pipeline, URP, and HDRP are supported; SceneXRay does not use render-pipeline APIs.
- No third-party packages, DLLs, services, accounts, or API keys are required.
- The product code is compiled only for the Unity Editor and adds no runtime component to builds.

The graph uses Unity's public `UnityEditor.Experimental.GraphView` API. Unity still labels this API
experimental, so compatibility with editor versions newer than those listed above is validated per
release.

## Install and open

Import the complete `Assets/SceneXRay` folder. Do not import individual scripts.

Open `Tools > SceneXRay > Open Graph View`, or use `Ctrl+Shift+Alt+X`. Press **Refresh** to scan
the loaded scenes. All SceneXRay shortcuts can be rebound in `Edit > Shortcuts`.

No bootstrap call, scene component, package-manifest edit, or restart is required.

For a ready-made example, open `Assets/SceneXRay/Samples/SceneXRay Demo.unity`. It contains
component-to-component links and a project-material reference, so the first refresh produces a
small readable graph without requiring any setup.

## Main workflows

### Explore a dependency graph

1. Open the graph and press **Refresh**.
2. Double-click a node, or select it and press **Enter**, to focus its neighborhood.
3. Select radius 1–3 and direction **Both**, **Out**, or **In**.
4. Use **Back**, **Forward**, and the breadcrumb trail to navigate.
5. Use **Layout** to select Auto, Tree, Force, or Radial placement.
6. Save a custom arrangement with `Ctrl+S` if required.

Clicking a scene node selects it in the Hierarchy. Clicking an asset node pings it in the Project
window.

### Find and repair missing references

Open `Tools > SceneXRay > Fix Missing`. The list covers serialized missing references in all loaded
scenes and the active Prefab Mode stage. Selecting a row pings the owning object. **Fix selected**
and **Fix all** use best-effort name matching and register Undo operations.

Always review automatic repairs. Name matching cannot prove semantic intent when several objects
have similar names.

### Compare scene assets

Open `Tools > SceneXRay > Advanced > Scene Diff`, select two scene assets, then choose **Compare**.
The tool opens scenes additively only for the duration of the scan and never closes a scene that
was already open. Select an **Open A/B** action before using a result row to reveal its object.

### Search and inspect references

- `Tools > SceneXRay > Global Search` searches loaded scenes and project prefabs.
- The Inspector integration shows **References** and **Referenced by** for supported selections.
- `Ctrl+Shift+Alt+J` toggles a bookmark; `Ctrl+Shift+Alt+K` opens bookmarks.
- The graph's **Export** action supports JSON, CSV, self-contained interactive HTML, Mermaid,
  and Markdown. Exported HTML makes no network requests.

## What is scanned

| Link type | Source |
| --- | --- |
| Direct | Serialized `GameObject` and `Component` references, including hidden serialized fields |
| UnityEvent | Persistent event targets and method names |
| Asset | Referenced project assets such as materials, meshes, clips, and ScriptableObjects |
| Missing | Serialized object references whose targets no longer resolve |
| In code | Optional generic component lookups plus literal object-name and tag lookups |

`Transform` and `RectTransform` are ignored by default. `m_Script` self-links and same-object
references are omitted to reduce noise. Built-in Unity resources are excluded unless explicitly
enabled.

### Code scan limitations

Code dependency scanning is disabled by default because it reads the source files used by
components in the current scan. It is a fast text analysis, not a Roslyn compilation. It detects
literal patterns such as `FindAnyObjectByType<T>()`, `GetComponent<T>()`, `GameObject.Find("Name")`,
and `GameObject.FindWithTag("Tag")`.

It does not resolve reflection, dependency-injection bindings, generated code, type aliases,
preprocessor branch truth, or dynamically assembled strings. A lookup matching more than the
configured target cap produces no object edges to prevent an unreadable graph. Optional script
nodes still show the declared class when it can be resolved.

## Settings

Open `Edit > Project Settings > SceneXRay`.

| Group | Important settings |
| --- | --- |
| Overlay | Enable and animate Scene View dependency lines |
| Colors | Direct, UnityEvent, Missing, Asset, In-code colors, and line width |
| Inspector | Enable or disable Inspector integration |
| Graph | Maximum nodes shown per page |
| Scanning | Ignored components, asset links, built-ins, code scan, target cap, script nodes |
| Build | Build Guard (off by default) and fail-build behavior for missing references |
| Language | English or Ukrainian editor UI |

Shared settings are stored in `ProjectSettings/SceneXRaySettings.asset`.

**Build Guard is disabled by default.** Importing SceneXRay does not change what your builds do.
Enable it per project when you want a pre-build scan: an interactive build then asks for
confirmation if the scan finds missing references or dependency cycles, and a batchmode build
fails only when **Fail Build On Missing Refs** is also enabled.

## CI and batch mode

```text
Unity -batchmode -quit -projectPath . \
  -executeMethod SceneXRay.Editor.Core.CommandLine.ScanScene \
  -scene Assets/Scenes/Main.unity \
  -output dependencies.json
```

The command writes JSON and exits with code `1` when a configured quality gate fails.

## Files written by SceneXRay

- `ProjectSettings/SceneXRaySettings.asset`: shared project settings.
- `UserSettings/SceneXRay/Layouts/`: per-user saved graph arrangements.
- `Library/SceneXRay/Cache/`: disposable scan cache.
- `Library/SceneXRay/bookmarks.json`: disposable local bookmarks.

SceneXRay makes no network requests, collects no analytics or personal data, and does not write
generated files under `Assets` unless the user explicitly chooses an export destination there.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No menu or window | Confirm Unity 6000.0 or newer and import the complete folder |
| Unstyled window | Reimport the complete `SceneXRay` folder; the shared style is resolved by GUID and can survive a folder move |
| Asset nodes missing | Enable **Scan asset references** and, if needed, **Include built-in assets** |
| Code edges missing | Enable **Scan code dependencies** and review the documented limitations |
| A prefab is absent | Enable **Prefabs**, or use **Show in graph** on that prefab asset |
| Stale graph | Disable **Cache** or choose `Tools > SceneXRay > Advanced > Clear Cache` |
| Very large graph | Reduce the scan scope, use filters, or lower the maximum nodes per page |

## Uninstall

Delete the complete `Assets/SceneXRay` folder. Optional local data can then be removed from
`UserSettings/SceneXRay` and `Library/SceneXRay`. No runtime scene objects need cleanup.

## Support

- Documentation: https://andriisviatenko.github.io/SceneXRay/
- Issues: https://github.com/AndriiSviatenko/SceneXRay/issues

When reporting a problem, include the exact Unity version, operating system, Console stack trace,
render pipeline, and a minimal reproduction where possible.

## License and third-party content

SceneXRay contains no third-party source, binaries, fonts, audio, images, or online services.
See `Third-Party Notices.txt` for the explicit declaration and `LICENSE.txt` for the source license.
