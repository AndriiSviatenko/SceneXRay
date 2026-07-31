using UnityEditor;
using SceneXRay.Editor.Windows;
using UnityEngine;
using SceneXRay.Editor.Core;
using System.Collections.Generic;
using System.Linq;

namespace SceneXRay.Editor.UI
{
    /// <summary>
    /// Inspector dependency strip — padded to match Transform fields, graph-like cards.
    /// </summary>
    [InitializeOnLoad]
    public static class XRayInspectorIntegration
    {
        private const string PrefsPrefix = "SceneXRay_Inspector_";
        private const int CacheLimit = 32;
        private const float RowH = 22f;
        private const float CardPad = 3f;
        private const float ArrowW = 20f;
        private const float FieldW = 112f;
        /// <summary>Match Unity inspector content inset so headers/cards don't kiss the window edge.</summary>
        private const float SidePad = 14f;

        private class CacheEntry
        {
            public List<DependencyLink> Outgoing;
            public LinkedListNode<int> LruNode;
        }

        private static readonly Dictionary<int, CacheEntry> _cache = new();
        private static readonly LinkedList<int> _lru = new();

        private static readonly Color ColDirect = new(0.38f, 0.59f, 0.86f);
        private static readonly Color ColEvent = new(0.90f, 0.76f, 0.27f);
        private static readonly Color ColMissing = new(0.92f, 0.31f, 0.31f);
        private static readonly Color ColAsset = new(0.65f, 0.49f, 0.98f);

        static XRayInspectorIntegration()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnFinishedHeaderGUI;
            XRayReferenceIndex.IndexUpdated += OnIndexUpdated;
        }

        private static void OnIndexUpdated()
        {
            _cache.Clear();
            _lru.Clear();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static CacheEntry TouchCache(int id)
        {
            if (_cache.TryGetValue(id, out var entry))
            {
                _lru.Remove(entry.LruNode);
                _lru.AddFirst(entry.LruNode);
                return entry;
            }

            var node = _lru.AddFirst(id);
            entry = new CacheEntry { LruNode = node };
            _cache[id] = entry;

            while (_cache.Count > CacheLimit)
            {
                int evicted = _lru.Last.Value;
                _lru.RemoveLast();
                _cache.Remove(evicted);
            }
            return entry;
        }

        private static List<DependencyLink> GetOutgoing(GameObject go, int id)
        {
            if (XRayReferenceIndex.IsReady)
            {
                var indexed = XRayReferenceIndex.GetOutgoing(go);
                return indexed as List<DependencyLink> ?? indexed.ToList();
            }

            var entry = TouchCache(id);
            if (entry.Outgoing != null)
                return entry.Outgoing;

            entry.Outgoing = SceneScanner.ScanGameObject(go);
            return entry.Outgoing;
        }

        private static IReadOnlyList<DependencyLink> GetIncoming(GameObject go)
        {
            if (!XRayReferenceIndex.IsReady)
                XRayReferenceIndex.RebuildImmediate();
            return XRayReferenceIndex.GetIncoming(go);
        }

        private static void OnFinishedHeaderGUI(UnityEditor.Editor editor)
        {
            if (!SceneXRaySettings.instance.EnableInspectorIntegration) return;
            if (editor.targets.Length > 1) return;

            if (editor.target is not GameObject go)
            {
                DrawAssetStrip(editor.target);
                return;
            }
            if (EditorUtility.IsPersistent(go))
            {
                DrawPrefabAssetStrip(go);
                return;
            }

            if (!XRayReferenceIndex.IsReady)
                XRayReferenceIndex.RebuildImmediate();

            int id = go.GetInstanceID();
            var outgoing = GetOutgoing(go, id);
            var incoming = GetIncoming(go);
            if (outgoing.Count == 0 && incoming.Count == 0) return;

            Styles.Ensure();

            // Outer inset — prevents header/cards from bleeding into the inspector chrome.
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SidePad);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(4);

                    if (outgoing.Count > 0)
                        DrawSection("References", outgoing, PrefsPrefix + "out_", go, incoming: false);

                    if (incoming.Count > 0)
                    {
                        GUILayout.Space(4);
                        DrawSection("Referenced By", incoming, PrefsPrefix + "in_", go, incoming: true);
                    }

                    GUILayout.Space(6);
                }
                GUILayout.Space(SidePad);
            }
        }

        /// <summary>
        /// Prefab asset inspectors: what the whole prefab tree references (materials, meshes,
        /// clips, ScriptableObjects…) plus the scene objects that point at the prefab.
        /// The tree scan is LRU-cached and invalidated with the reference index.
        /// </summary>
        private static void DrawPrefabAssetStrip(GameObject prefabRoot)
        {
            if (!PrefabUtility.IsPartOfPrefabAsset(prefabRoot)) return;
            // Only the asset root — child inspectors would repeat the same list.
            if (prefabRoot.transform.parent != null) return;

            int id = prefabRoot.GetInstanceID();
            var entry = TouchCache(id);
            entry.Outgoing ??= PrefabScanner.ScanPrefab(prefabRoot);

            var outgoing = entry.Outgoing;
            var incoming = XRayReferenceIndex.IsReady
                ? XRayReferenceIndex.GetIncoming(prefabRoot)
                    .Concat(XRayReferenceIndex.GetIncomingForAsset(prefabRoot))
                    .ToList()
                : (IReadOnlyList<DependencyLink>)System.Array.Empty<DependencyLink>();

            if (outgoing.Count == 0 && incoming.Count == 0) return;

            Styles.Ensure();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SidePad);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(4);

                    if (outgoing.Count > 0)
                        DrawSection("References", outgoing, PrefsPrefix + "prefab_out_", prefabRoot, incoming: false);

                    if (incoming.Count > 0)
                    {
                        GUILayout.Space(4);
                        DrawSection("Referenced By", incoming, PrefsPrefix + "prefab_in_", prefabRoot, incoming: true);
                    }

                    GUILayout.Space(6);
                }
                GUILayout.Space(SidePad);
            }
        }

        /// <summary>
        /// Asset inspectors (ScriptableObject, material, …) get a "Referenced By" strip listing
        /// the scene objects that point at them. Read-only: never forces an index rebuild, so
        /// clicking through the Project window stays cheap.
        /// </summary>
        private static void DrawAssetStrip(Object asset)
        {
            if (asset == null) return;
            if (asset is AssetImporter || asset is GameObject) return;
            if (!EditorUtility.IsPersistent(asset)) return;
            if (!XRayReferenceIndex.IsReady) return;

            var incoming = XRayReferenceIndex.GetIncomingForAsset(asset);
            if (incoming.Count == 0) return;

            Styles.Ensure();

            string key = PrefsPrefix + "asset_" + asset.GetInstanceID();
            bool open = EditorPrefs.GetBool(key, true);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SidePad);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(4);

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(20)))
                    {
                        var titleContent = new GUIContent("Referenced By",
                            open ? "Click to collapse" : "Click to expand");
                        if (GUILayout.Button(titleContent, open ? Styles.HeaderOpen : Styles.HeaderClosed,
                                GUILayout.ExpandWidth(false)))
                        {
                            open = !open;
                            EditorPrefs.SetBool(key, open);
                        }

                        GUILayout.Space(8);
                        DrawCountBadge(incoming.Count);
                        GUILayout.FlexibleSpace();

                        if (TextAction("Graph", "Focus this asset in SceneXRay Graph"))
                            XRayWindow.ShowWindowFocusedAsset(asset);
                    }

                    if (open)
                    {
                        GUILayout.Space(2);
                        foreach (var link in incoming
                                     .OrderBy(l => ObjectName(l, true))
                                     .ThenBy(FieldLabel))
                        {
                            DrawCardRow(link, null, incoming: true);
                            GUILayout.Space(CardPad);
                        }
                    }

                    GUILayout.Space(6);
                }
                GUILayout.Space(SidePad);
            }
        }

        private static void DrawSection(
            string title,
            IReadOnlyList<DependencyLink> links,
            string keyPrefix,
            GameObject current,
            bool incoming)
        {
            string key = keyPrefix + current.GetInstanceID();
            bool open = EditorPrefs.GetBool(key, true);

            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(20)))
            {
                var titleContent = new GUIContent(title, open ? "Click to collapse" : "Click to expand");
                if (GUILayout.Button(titleContent, open ? Styles.HeaderOpen : Styles.HeaderClosed, GUILayout.ExpandWidth(false)))
                {
                    open = !open;
                    EditorPrefs.SetBool(key, open);
                }

                GUILayout.Space(8);
                DrawCountBadge(links.Count);
                GUILayout.FlexibleSpace();

                if (TextAction("Graph", "Focus this object in SceneXRay Graph"))
                    XRayWindow.ShowWindowFocused(current);
                GUILayout.Space(4);
                if (TextAction("Refs", "Open the references browser"))
                    XRayReferencesWindow.ShowFor(current);
            }

            if (!open) return;

            GUILayout.Space(2);
            foreach (var link in links
                         .OrderBy(l => l.IsMissing ? 0 : 1)
                         .ThenBy(l => FieldLabel(l))
                         .ThenBy(l => ObjectName(l, incoming)))
            {
                DrawCardRow(link, current, incoming);
                GUILayout.Space(CardPad);
            }
        }

        private static void DrawCountBadge(int count)
        {
            string text = count.ToString();
            Vector2 size = Styles.Badge.CalcSize(new GUIContent(text));
            float w = Mathf.Max(18f, size.x + 10f);
            Rect r = GUILayoutUtility.GetRect(w, 16f, GUILayout.Width(w), GUILayout.Height(16f));
            if (Event.current.type != EventType.Repaint) return;
            var bg = EditorGUIUtility.isProSkin
                ? new Color(0.35f, 0.50f, 0.94f, 0.55f)
                : new Color(0.35f, 0.50f, 0.94f, 0.35f);
            EditorGUI.DrawRect(r, bg);
            Styles.Badge.Draw(r, text, false, false, false, false);
        }

        private static bool TextAction(string label, string tip)
        {
            var content = new GUIContent(label, tip);
            Vector2 size = Styles.Action.CalcSize(content);
            Rect r = GUILayoutUtility.GetRect(size.x + 8f, 16f, GUILayout.Width(size.x + 8f), GUILayout.Height(16f));
            bool hover = r.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint && hover)
                EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.06f)
                    : new Color(0f, 0f, 0f, 0.06f));
            return GUI.Button(r, content, Styles.Action);
        }

        private static void DrawCardRow(DependencyLink link, GameObject current, bool incoming)
        {
            Rect row = GUILayoutUtility.GetRect(1, RowH, GUILayout.ExpandWidth(true));
            Color accent = TypeColor(link);
            bool dark = EditorGUIUtility.isProSkin;

            if (Event.current.type == EventType.Repaint)
            {
                var fill = dark
                    ? new Color(0.16f, 0.165f, 0.20f, 1f)
                    : new Color(0.94f, 0.94f, 0.96f, 1f);
                EditorGUI.DrawRect(row, fill);
                EditorGUI.DrawRect(new Rect(row.x, row.y + 2, 3f, row.height - 4), accent);
            }

            float padL = row.x + 10f;
            float padR = row.xMax - 6f;
            float chipY = row.y + 2f;
            float chipH = row.height - 4f;

            if (incoming)
            {
                float chipMax = Mathf.Max(80f, padR - padL - FieldW - ArrowW - 8f);
                Rect chip = DrawObjectChip(new Rect(padL, chipY, chipMax, chipH), link.Source, current, accent);
                Rect arrow = new Rect(chip.xMax + 2f, row.y, ArrowW, row.height);
                DrawArrow(arrow, accent);
                Rect field = new Rect(arrow.xMax + 2f, row.y, padR - (arrow.xMax + 2f), row.height);
                GUI.Label(field, FieldLabel(link), Styles.Field);
            }
            else
            {
                Rect field = new Rect(padL, row.y, FieldW, row.height);
                GUI.Label(field, FieldLabel(link), Styles.Field);
                Rect arrow = new Rect(field.xMax + 2f, row.y, ArrowW, row.height);
                DrawArrow(arrow, accent);
                Rect chipArea = new Rect(arrow.xMax + 2f, chipY, padR - (arrow.xMax + 2f), chipH);
                if (link.IsMissing)
                    GUI.Label(chipArea, "Missing", Styles.Missing);
                else if (link.TargetAsset != null)
                    DrawAssetChip(chipArea, link.TargetAsset, accent);
                else
                    DrawObjectChip(chipArea, link.Target, current, accent);
            }
        }

        private static void DrawArrow(Rect slot, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = color;
            Styles.Arrow.Draw(slot, "→", false, false, false, false);
            GUI.color = prev;
        }

        private static Rect DrawObjectChip(Rect area, GameObject obj, GameObject current, Color accent)
        {
            if (obj == null || obj == current)
            {
                GUI.Label(area, "—", Styles.Muted);
                return area;
            }

            float w = Mathf.Min(area.width, Styles.Chip.CalcSize(new GUIContent(obj.name)).x + 28f);
            Rect chip = new Rect(area.x, area.y, w, area.height);
            bool hover = chip.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                var bg = EditorGUIUtility.isProSkin
                    ? new Color(0.22f, 0.24f, 0.30f, hover ? 1f : 0.92f)
                    : new Color(1f, 1f, 1f, hover ? 1f : 0.9f);
                EditorGUI.DrawRect(chip, bg);
                if (hover)
                    EditorGUI.DrawRect(new Rect(chip.x + 2, chip.yMax - 2, chip.width - 4, 2), accent);
            }

            var icon = AssetPreview.GetMiniThumbnail(obj)
                       ?? EditorGUIUtility.IconContent("GameObject Icon").image;
            if (GUI.Button(chip, new GUIContent("  " + obj.name, icon, obj.name), Styles.Chip))
            {
                Selection.activeGameObject = obj;
                EditorGUIUtility.PingObject(obj);
                Event.current.Use();
            }
            return chip;
        }

        private static void DrawAssetChip(Rect area, Object asset, Color accent)
        {
            float w = Mathf.Min(area.width, Styles.Chip.CalcSize(new GUIContent(asset.name)).x + 28f);
            Rect chip = new Rect(area.x, area.y, w, area.height);
            bool hover = chip.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                var bg = EditorGUIUtility.isProSkin
                    ? new Color(0.26f, 0.22f, 0.34f, hover ? 1f : 0.92f)
                    : new Color(0.93f, 0.90f, 0.98f, 1f);
                EditorGUI.DrawRect(chip, bg);
                if (hover)
                    EditorGUI.DrawRect(new Rect(chip.x + 2, chip.yMax - 2, chip.width - 4, 2), accent);
            }

            var icon = AssetPreview.GetMiniThumbnail(asset);
            if (GUI.Button(chip, new GUIContent("  " + asset.name, icon, AssetDatabase.GetAssetPath(asset)), Styles.Chip))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                Event.current.Use();
            }
        }

        private static string ObjectName(DependencyLink link, bool incoming)
        {
            if (incoming) return link.Source != null ? link.Source.name : "";
            if (link.Target != null) return link.Target.name;
            if (link.TargetAsset != null) return link.TargetAsset.name;
            return "";
        }

        private static string FieldLabel(DependencyLink link)
        {
            if (!string.IsNullOrEmpty(link.TargetMethodName))
                return $"{link.SourcePropertyName}.{link.TargetMethodName}()";
            return link.SourcePropertyName ?? "";
        }

        private static Color TypeColor(DependencyLink link)
        {
            if (link.IsMissing) return ColMissing;
            if (link.IsUnityEvent) return ColEvent;
            if (link.IsAssetReference) return ColAsset;
            return ColDirect;
        }

        private static class Styles
        {
            public static GUIStyle HeaderOpen;
            public static GUIStyle HeaderClosed;
            public static GUIStyle Field;
            public static GUIStyle Muted;
            public static GUIStyle Missing;
            public static GUIStyle Chip;
            public static GUIStyle Badge;
            public static GUIStyle Action;
            public static GUIStyle Arrow;

            private static bool _ready;
            private static bool _dark;

            public static void Ensure()
            {
                bool dark = EditorGUIUtility.isProSkin;
                if (_ready && _dark == dark) return;
                _dark = dark;
                _ready = true;

                HeaderOpen = MakeHeader(dark, true);
                HeaderClosed = MakeHeader(dark, false);

                Field = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    normal = { textColor = dark ? new Color(0.62f, 0.62f, 0.66f) : new Color(0.35f, 0.35f, 0.4f) }
                };

                Muted = new GUIStyle(Field) { normal = { textColor = new Color(0.5f, 0.5f, 0.52f) } };

                Missing = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = ColMissing }
                };

                Chip = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 6, 0, 0),
                    clipping = TextClipping.Clip,
                    imagePosition = ImagePosition.ImageLeft,
                    normal = { textColor = dark ? new Color(0.82f, 0.86f, 0.95f) : new Color(0.12f, 0.22f, 0.42f) },
                    hover = { textColor = dark ? Color.white : new Color(0.05f, 0.35f, 0.75f) },
                    active = { textColor = dark ? new Color(0.7f, 0.85f, 1f) : new Color(0.0f, 0.3f, 0.65f) }
                };

                Badge = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.92f, 0.94f, 1f) }
                };

                Action = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 0, 0),
                    normal = { textColor = dark ? new Color(0.55f, 0.62f, 0.78f) : new Color(0.25f, 0.35f, 0.55f) },
                    hover = { textColor = dark ? new Color(0.75f, 0.82f, 1f) : new Color(0.1f, 0.35f, 0.75f) }
                };

                Arrow = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }

            private static GUIStyle MakeHeader(bool dark, bool open)
            {
                var c = dark ? new Color(0.85f, 0.85f, 0.88f) : new Color(0.15f, 0.15f, 0.18f);
                var hover = dark ? new Color(0.55f, 0.75f, 1f) : new Color(0.1f, 0.4f, 0.8f);
                if (!open) c.a = 0.7f;
                return new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 2, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip,
                    stretchWidth = false,
                    normal = { textColor = c },
                    hover = { textColor = hover },
                    active = { textColor = hover }
                };
            }
        }
    }
}
