# SceneXRay release validation report

Date: 2026-08-18
Project editor: Unity 6000.0.62f1
Highest editor compiled against: Unity 6000.5.6f1
Declared minimum: **Unity 6000.0 LTS (Unity 6)**

## Result

The package compiles clean, tests green, and imports as a single self-contained root on every
editor that is actually installed on the build machine. No package-origin compile error, warning,
obsolete-API warning, failed test, missing `.meta`, duplicate GUID, hard-coded root path, or
external runtime dependency remains.

## Automated checks

| Check | Result |
| --- | --- |
| `dotnet build SceneXRay.sln` | Passed, 0 warnings, 0 errors |
| `Tools~/verify-unity-versions.ps1` on 6000.0.62f1 | Passed, 0 obsolete-API warnings |
| `Tools~/verify-unity-versions.ps1` on 6000.5.6f1 | Passed, 0 obsolete-API warnings |
| Unity 6000.0.62f1 in-editor compile | `SceneXRay.Editor.dll` built; assembly is not skipped by its defineConstraint |
| Unity 6000.0.62f1 EditMode tests | 23 passed, 0 failed, 0 skipped, 0 inconclusive |
| Localization parity | 202 English / 202 Ukrainian keys, none missing on either side |
| Package metadata | 0 missing `.meta`, 0 orphan `.meta`, 0 duplicate GUIDs |
| Longest package path | 64 characters |
| Archive vs working tree | Every shipped `asset` and `asset.meta` byte-identical to the source tree |

Final archive: `SceneXRay-1.0.0.unitypackage`
SHA-256: `E22DF82ED5278EFC615BC2E99AF2D75EF1FD006D667A7AB33A766F74F0EFDF40`
Inventory: 65 paths — 54 files, 11 folders, one root, no test assembly.

Rebuild it with `python Tools~/build-unitypackage.py`. The archive is byte-reproducible from a
clean checkout, so "is the shipped archive the same as the source?" is answerable with a diff.

## Scope and compatibility

- Package root: `Assets/SceneXRay`
- Editor-only; no player-build assembly reference; no runtime component
- Pipeline-agnostic: the product code touches no render-pipeline API
- No third-party source, binaries, services, accounts, network calls, or package dependency
- The 23 development tests live in the repository and are excluded from the customer archive, so
  Unity Test Framework is not a customer dependency
- Below the floor, `SceneXRay.Editor` is skipped by its `UNITY_6000_0_OR_NEWER` defineConstraint
  and `SceneXRay.Compat.UnsupportedVersionNotice` logs one explanatory warning instead of a wall
  of compile errors

## Why the floor is Unity 6000.0 and not 2022.3

Nothing in the source requires an API newer than 6000.0 — the floor is a *testing* claim, not a
code claim. The earlier 2022.3 claim was never validated: the build machine has no working 2022.3
editor (its Unity Hub entries contain only `PlaybackEngines`, with no `Unity.exe` and no managed
assemblies), so no 2022.3 compile, import, or test run ever happened.

Advertising an untested minimum is a preventable rejection and a preventable refund. The declared
floor is therefore the oldest editor the package is genuinely verified on.

To lower it again: install the target LTS, run `Tools~/verify-unity-versions.ps1`, run the EditMode
tests there, do one clean-project import, then change `defineConstraints` in both asmdefs,
`SceneXRayCompat.MinimumSupportedVersion`, and the version line in `Listing.md`, `README.md`,
`docs/index.html`, and `Assets/SceneXRay/Documentation/README.md`.

## Known items accepted for 1.0.0

- The graph canvas is built on `UnityEditor.Experimental.GraphView`. It compiles clean and is not
  marked obsolete on 6000.5, but it is Unity's experimental namespace and is the one dependency
  that could force a real port rather than a `#if`. Disclosed in the listing.
- The demo material uses the built-in `Sprites/Default` shader on four `MeshRenderer`s. This
  renders correctly in the Built-in pipeline and in URP. In HDRP, built-in shaders render magenta,
  so the demo scene's cubes would appear pink there — the tool itself is unaffected.
- EditMode tests were run on 6000.0.62f1 only. 6000.5.6f1 is verified by compile, not by test run;
  opening the project in 6000.5 would upgrade the project files in the repository.

## Remaining external gates

These are not assumed passes. See `SubmissionChecklist.md`.

- Asset Store Tools **Validate** on the final `.unitypackage`.
- One clean-project import into a new Unity 6000.0 project without Unity Test Framework.
- Publisher Portal fields, artwork upload, and draft re-download verification.
