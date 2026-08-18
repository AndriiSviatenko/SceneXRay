# SceneXRay — Publisher Portal copy

## Product title

SceneXRay – Dependency Graph & Missing Reference Finder

## Category

Tools / Utilities

## Summary

Visualize serialized, UnityEvent, asset, missing, and optional code-level dependencies in Unity
scenes and prefabs. Navigate, repair, compare, export, and enforce scene quality in CI.

## Description

SceneXRay is an editor-only X-ray of your Unity scene wiring. It turns serialized references,
persistent UnityEvent targets, project assets, missing references, and optional code-level lookups
into an interactive dependency graph you can navigate and understand.

Use it to answer practical production questions: What does this object depend on? What references
it? Which link is broken? Which object has become a dependency hub? What changed between two
scenes? Will a missing reference slip into the next build?

Key features:

- Interactive dependency graph with focus radius and incoming/outgoing direction controls.
- Auto, Tree, Force, and Radial layouts, plus saved per-scene arrangements and Undo/Redo.
- Missing-reference detection with reviewed, Undo-aware best-effort repair by object name.
- Persistent UnityEvent target and method discovery.
- Project-asset links for materials, meshes, clips, ScriptableObjects, and similar assets.
- Optional source scan for common `Find*<T>()`, `GetComponent<T>()`, object-name, and tag lookups.
- Reverse-reference Inspector cards, global search, bookmarks, Scene View overlay, and scene diff.
- Health score for missing references, dependency cycles, and high-coupling objects.
- JSON, CSV, Mermaid, Markdown, and self-contained interactive HTML export.
- Opt-in build guard and batchmode command for CI quality gates.
- English and Ukrainian editor UI.
- Built-in first-run tutorial, offline documentation, and a ready-made demo scene.

Setup takes one step: import the complete `Assets/SceneXRay` folder, then open
`Tools > SceneXRay > Open Graph View`. SceneXRay adds no runtime component and no code to player
builds.

Requirements and dependencies:

- Unity 6000.0 LTS (Unity 6) or newer.
- Verified in Unity 6000.0.62f1 and 6000.5.6f1.
- Editor-only; compatible with Built-in Render Pipeline, URP, and HDRP.
- No third-party packages, DLLs, services, accounts, API keys, or network connection.

Important limitations:

- The optional code scan is text analysis, not a C# compiler. It cannot resolve reflection,
  dependency-injection containers, generated code, dynamic strings, or every preprocessor path.
- Automatic missing-reference repair matches candidates by name. Always review repairs when names
  are ambiguous.
- The graph canvas uses Unity's public `UnityEditor.Experimental.GraphView` API. Compatibility with
  editor versions newer than the verified range is checked with each SceneXRay release.
- Global project search and all-build-scene analysis are explicit operations because they can be
  expensive in very large projects.
- The build guard is off by default. Importing SceneXRay does not alter an existing project's
  build behaviour until you enable it in Project Settings.

Documentation: https://andriisviatenko.github.io/SceneXRay/

Support and issue tracker: https://github.com/AndriiSviatenko/SceneXRay/issues

## Technical details

- Version: 1.0.0
- Unity: 6000.0 LTS (Unity 6) or newer
- Verified editors: 6000.0.62f1, 6000.5.6f1
- Render pipelines: Built-in, URP, HDRP (pipeline-agnostic editor tool)
- Platforms: all Unity Editor host platforms supported by Unity
- Runtime footprint: none
- Namespaces: `SceneXRay.Editor`, `SceneXRay.Compat`
- Assemblies: one editor assembly and one unsupported-version notice assembly
- Dependencies: none
- Network/data collection: none
- Localization: English, Ukrainian
- Demo: one scene using Unity primitives and one local material
- Documentation: offline Markdown plus online documentation
- Export formats: JSON, CSV, HTML, Mermaid, Markdown

## Compatibility information

Unity 6000.0 LTS (Unity 6) or newer. Verified in Unity 6000.0.62f1 and 6000.5.6f1. SceneXRay is an
editor-only, render-pipeline-agnostic tool and supports Built-in, URP, and HDRP projects. It does
not add runtime components or player-build dependencies.

## Keywords

dependency graph, missing reference, scene analyzer, editor tool, UnityEvent, prefab, architecture,
visualization, build validation, code dependencies, reverse references, scene diff

## Release notes — 1.0.0

Initial Asset Store release with interactive scene/prefab dependency graph, missing-reference
repair, reference search, bookmarks, scene overlay and diff, optional code dependency analysis,
five export formats, build/CI quality gates, English/Ukrainian localization, demo scene, and offline
documentation.

## AI description field

OpenAI Codex assisted with review and modifications to editor scanning, compatibility shims, UI
localization, tests, documentation, and release validation. The publisher reviewed the resulting
source and verified it in Unity 6000.0.62f1 and 6000.5.6f1; 23 EditMode tests pass. Marketing key
art was assembled as editable SVG from original geometric shapes and rendered to PNG.
