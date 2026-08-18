# SceneXRay Asset Store submission checklist

Prepared against the Unity Asset Store Submission Guidelines dated May 20, 2026.
Intended listing: **free**, with the source additionally available under MIT on GitHub.

## Verified in the package

- [x] All distributed content is under one root: `Assets/SceneXRay`.
- [x] Editor code is namespaced and isolated in editor-only assembly definitions.
- [x] Menus are confined to `Tools/SceneXRay`, `GameObject/SceneXRay`, and `Assets/SceneXRay`.
- [x] No Unity internal API discovery through reflection; no package-manager mutations.
- [x] No executables, DLLs, archives, API keys, analytics, network calls, or external services.
- [x] No third-party content; explicit `Third-Party Notices.txt` included.
- [x] `LICENSE.txt` states that Asset Store distribution is governed by the Asset Store EULA,
      which takes precedence, and that the upstream source is additionally MIT.
- [x] Importing SceneXRay does not change an existing project's behaviour: the build guard is
      **off by default**, so no build is scanned, delayed, or interrupted until the user opts in.
- [x] Only three `EditorPrefs` keys, all prefixed `SceneXRay_`.
- [x] Offline setup, workflows, limitations, storage, troubleshooting, uninstall, and support docs.
- [x] Pipeline-neutral demo scene inside the product root, with no scripts and no URP/HDRP assets.
- [x] Every file and directory has a `.meta`; no orphan metas; no duplicate GUIDs.
- [x] Longest path is 64 characters, well below the 140-character review threshold.
- [x] Styles resolve by GUID, so customers may move or rename the product folder.
- [x] English and Ukrainian dictionaries have identical key sets (202 / 202).
- [x] Self-contained HTML export makes no CDN or other network request.
- [x] Functional AI assistance is disclosed in `Listing.md` for the portal AI field.
- [x] Repository tests are excluded from the archive; Unity Test Framework is not a dependency.
- [x] Documentation describes only features that exist in this build.

## Validation evidence

- [x] `dotnet build SceneXRay.sln`: 0 errors, 0 warnings.
- [x] Unity 6000.0.62f1 compile: `SceneXRay.Editor.dll` produced, 0 errors, 0 package warnings.
- [x] Unity 6000.5.6f1 compile: 0 errors, 0 obsolete-API warnings.
- [x] EditMode: 23/23 passed on Unity 6000.0.62f1, after the version-floor change.
- [x] Declared minimum equals the oldest editor actually tested (Unity 6000.0 LTS).
- [x] Archive inventory: 65 paths, 54 files, one root, no test assembly.
- [x] Every shipped payload byte-identical to the working tree.
- [x] Archive is reproducible: `python Tools~/build-unitypackage.py`.

## Artwork ready in `AssetStore/Marketing`

- [x] Icon PNG: 160×160, no text.
- [x] Card PNG: 420×280, title only.
- [x] Cover PNG: 1950×1300, title and tagline only.
- [x] Social PNG: 1200×630, no text.
- [x] Product screenshot: `SceneXRay-Graph-Screenshot.png`, 1267×864.
- [ ] **Add 2–4 more screenshots.** One screenshot is the bare minimum and is a common reason for
      a "improve your presentation" return. Worth capturing: Fix Missing window, Scene Diff,
      Inspector reverse-reference cards, and the exported HTML graph.

## Must be done in Publisher Portal before Submit

- [ ] Confirm the product title is available and select the final category and free price.
- [ ] Confirm the publisher profile has an active support email and a maintained website.
- [ ] Paste every field from `Listing.md`, including limitations and the AI description.
- [ ] Upload the PNG artwork, not the SVG sources.
- [ ] Run the current Asset Store Tools **Validate** action on the final `.unitypackage`.
- [ ] Do one clean import into a brand-new Unity 6000.0 project that has no Unity Test Framework,
      and confirm `Tools > SceneXRay > Open Graph View` opens and refreshes.
- [ ] Upload from Unity 6000.0.62f1 unless a newer editor upload is specifically required.
- [ ] Download the uploaded draft back from the Portal and verify its contents before submit.

## Optional, only if HDRP support is advertised prominently

- [ ] The demo material uses the built-in `Sprites/Default` shader, which renders magenta in HDRP.
      Either swap the demo's asset-link demonstration to something pipeline-independent, or soften
      the HDRP wording in `Listing.md` to "the tool is pipeline-agnostic; the demo scene uses a
      built-in shader".

Do not submit until every portal-only box above is complete. Acceptance remains Unity's decision;
this checklist removes the preventable technical and presentation causes of rejection, it does not
guarantee approval.
