using SceneXRay.Editor.UI;
using UnityEditor;
using UnityEngine;
using SceneXRay.Editor.Core;

namespace SceneXRay.Editor.Windows
{
    /// <summary>
    /// Bookmark browser — pin GameObjects for quick jump (deduped by GlobalObjectId).
    /// Hotkeys (Shortcut Manager): Ctrl+Shift+Alt+J toggle, Ctrl+Shift+Alt+K open.
    /// </summary>
    public class XRayBookmarkWindow : EditorWindow
    {
        private const float SidePad = 14f;
        private const float RowH = 36f;

        private static readonly Color Accent = new(0.38f, 0.59f, 0.86f);
        private static readonly Color Danger = new(0.92f, 0.31f, 0.31f);

        private Vector2 _scroll;
        private string _status;

        public static void ShowWindow()
        {
            var w = GetWindow<XRayBookmarkWindow>(false, XRayLocalization.GetText("bookmarks"), true);
            w.minSize = new Vector2(360, 240);
            w.Show();
            w.Focus();
            w.Repaint();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            titleContent = new GUIContent(XRayLocalization.GetText("bookmarks"));
            XRayBookmarks.Changed += OnBookmarksChanged;
            XRayLocalization.LanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            XRayBookmarks.Changed -= OnBookmarksChanged;
            XRayLocalization.LanguageChanged -= OnLanguageChanged;
        }

        private void OnBookmarksChanged() => Repaint();

        private void OnLanguageChanged()
        {
            titleContent = new GUIContent(XRayLocalization.GetText("bookmarks"));
            Repaint();
        }

        private void OnGUI()
        {
            Styles.Ensure();

            if (Event.current.type == EventType.MouseMove)
                Repaint();

            DrawToolbar();
            DrawHero();

            var entries = XRayBookmarks.All;
            if (entries.Count == 0)
            {
                DrawEmpty();
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SidePad);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(8);
                    for (int i = 0; i < entries.Count; i++)
                        DrawRow(entries[i]);
                    GUILayout.Space(12);
                }
                GUILayout.Space(SidePad);
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_status))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(SidePad);
                    GUILayout.Label(_status, Styles.Muted);
                    GUILayout.Space(SidePad);
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);

                if (GUILayout.Button(XRayLocalization.GetText("bm_add"), EditorStyles.toolbarButton, GUILayout.Width(110)))
                    AddFromSelection();

                bool bookmarked = XRayBookmarks.Contains(Selection.activeGameObject);
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                {
                    string toggleLabel = bookmarked
                        ? XRayLocalization.GetText("bm_remove")
                        : XRayLocalization.GetText("bm_toggle");
                    if (GUILayout.Button(toggleLabel, EditorStyles.toolbarButton, GUILayout.Width(120)))
                        ToggleSelection();
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(XRayBookmarks.Count == 0))
                {
                    if (GUILayout.Button(XRayLocalization.GetText("bm_clear"), EditorStyles.toolbarButton, GUILayout.Width(72)))
                    {
                        if (EditorUtility.DisplayDialog(
                                XRayLocalization.GetText("bookmarks"),
                                XRayLocalization.GetText("bm_clear_confirm"),
                                XRayLocalization.GetText("bm_yes"),
                                XRayLocalization.GetText("bm_no")))
                        {
                            XRayBookmarks.Clear();
                            _status = null;
                        }
                    }
                }

                GUILayout.Space(4);
            }
        }

        private void DrawHero()
        {
            Rect bar = GUILayoutUtility.GetRect(1, 40, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var bg = EditorGUIUtility.isProSkin
                    ? new Color(0.16f, 0.165f, 0.20f, 1f)
                    : new Color(0.92f, 0.92f, 0.94f, 1f);
                var inset = new Rect(bar.x + SidePad, bar.y + 4, bar.width - SidePad * 2, bar.height - 8);
                EditorGUI.DrawRect(inset, bg);
                EditorGUI.DrawRect(new Rect(inset.x, inset.y, 3f, inset.height), Accent);
            }

            using (new GUI.GroupScope(bar))
            {
                float x = SidePad + 12f;
                GUI.Label(new Rect(x, 8, bar.width - SidePad * 2 - 24, 24),
                    XRayLocalization.Format("bm_count", XRayBookmarks.Count), Styles.HeroName);
            }
        }

        private void DrawEmpty()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(320)))
                {
                    GUILayout.Label(XRayLocalization.GetText("bm_empty_title"), Styles.HeroName);
                    GUILayout.Space(4);
                    GUILayout.Label(XRayLocalization.GetText("bm_empty_hint"), Styles.Muted);
                    GUILayout.Space(12);
                    if (GUILayout.Button(XRayLocalization.GetText("bm_add") + "  (Ctrl+Shift+Alt+J)", GUILayout.Height(28)))
                        ToggleSelection();
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.FlexibleSpace();
        }

        private void DrawRow(BookmarkEntry entry)
        {
            Rect row = GUILayoutUtility.GetRect(1, RowH, GUILayout.ExpandWidth(true));
            bool hover = row.Contains(Event.current.mousePosition);
            var go = XRayBookmarks.Resolve(entry);
            bool missing = go == null;

            if (Event.current.type == EventType.Repaint)
            {
                var bg = EditorGUIUtility.isProSkin
                    ? new Color(0.18f, 0.185f, 0.22f, hover ? 1f : 0.85f)
                    : new Color(0.96f, 0.96f, 0.97f, hover ? 1f : 0.9f);
                EditorGUI.DrawRect(row, bg);
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), missing ? Danger : Accent);
            }

            var icon = go != null
                ? (AssetPreview.GetMiniThumbnail(go) ?? EditorGUIUtility.IconContent("GameObject Icon").image)
                : EditorGUIUtility.IconContent("console.warnicon.sml").image;

            float actionsW = 118f;
            Rect iconR = new Rect(row.x + 10, row.y + 8, 20, 20);
            Rect nameR = new Rect(row.x + 34, row.y + 4, row.width - actionsW - 42, 16);
            Rect metaR = new Rect(nameR.x, row.y + 18, nameR.width, 14);
            Rect actionsR = new Rect(row.xMax - actionsW - 6, row.y + 6, actionsW, 24);

            if (icon != null)
                GUI.DrawTexture(iconR, icon, ScaleMode.ScaleToFit);

            GUI.Label(nameR, entry.Name ?? "—", missing ? Styles.Missing : Styles.RowName);

            string scene = string.IsNullOrEmpty(entry.ScenePath)
                ? (missing ? XRayLocalization.GetText("bm_missing") : "")
                : System.IO.Path.GetFileNameWithoutExtension(entry.ScenePath);
            if (!string.IsNullOrEmpty(scene))
                GUI.Label(metaR, scene, Styles.Muted);

            // Click body → ping/select
            Rect hit = new Rect(row.x, row.y, row.width - actionsW, row.height);
            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hit.Contains(Event.current.mousePosition))
            {
                if (go != null)
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                    if (Event.current.clickCount >= 2)
                        XRayWindow.ShowWindowFocused(go);
                }
                else
                {
                    _status = XRayLocalization.Format("bm_resolve_fail", entry.Name);
                }
                Event.current.Use();
            }

            using (new GUI.GroupScope(actionsR))
            {
                float ax = 0;
                if (go != null)
                {
                    if (GUI.Button(new Rect(ax, 0, 52, 22), XRayLocalization.GetText("bm_graph"), Styles.TextAction))
                        XRayWindow.ShowWindowFocused(go);
                    ax += 54;
                }

                if (GUI.Button(new Rect(ax, 0, 56, 22), XRayLocalization.GetText("bm_remove"), Styles.TextDanger))
                {
                    XRayBookmarks.Remove(entry.Id);
                    _status = XRayLocalization.Format("bm_removed", entry.Name);
                }
            }

            GUILayout.Space(4);
        }

        private void AddFromSelection()
        {
            if (XRayBookmarks.TryAdd(Selection.activeGameObject, out var msg))
                _status = msg;
            else
            {
                _status = msg;
                if (Selection.activeGameObject == null || msg != null && msg.StartsWith("Already"))
                    EditorUtility.DisplayDialog(XRayLocalization.GetText("bookmarks"), msg, "OK");
            }
        }

        private void ToggleSelection()
        {
            XRayBookmarks.Toggle(Selection.activeGameObject, out var msg);
            _status = msg;
            if (Selection.activeGameObject == null)
                EditorUtility.DisplayDialog(XRayLocalization.GetText("bookmarks"), msg, "OK");
        }

        private static class Styles
        {
            public static GUIStyle HeroName;
            public static GUIStyle RowName;
            public static GUIStyle Muted;
            public static GUIStyle Missing;
            public static GUIStyle TextAction;
            public static GUIStyle TextDanger;

            private static bool _ready;
            private static bool _dark;

            public static void Ensure()
            {
                bool dark = EditorGUIUtility.isProSkin;
                if (_ready && _dark == dark) return;
                _dark = dark;
                _ready = true;

                HeroName = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = dark ? new Color(0.92f, 0.93f, 0.96f) : new Color(0.12f, 0.12f, 0.16f) }
                };

                RowName = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    clipping = TextClipping.Clip,
                    normal = { textColor = dark ? new Color(0.88f, 0.90f, 0.95f) : new Color(0.12f, 0.14f, 0.2f) }
                };

                Muted = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    clipping = TextClipping.Clip,
                    normal = { textColor = dark ? new Color(0.55f, 0.55f, 0.6f) : new Color(0.4f, 0.4f, 0.45f) }
                };

                Missing = new GUIStyle(RowName)
                {
                    normal = { textColor = Danger }
                };

                TextAction = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = dark ? new Color(0.55f, 0.75f, 1f) : new Color(0.15f, 0.4f, 0.8f) },
                    hover = { textColor = dark ? Color.white : new Color(0.05f, 0.3f, 0.7f) }
                };

                TextDanger = new GUIStyle(TextAction)
                {
                    normal = { textColor = dark ? new Color(1f, 0.55f, 0.55f) : Danger },
                    hover = { textColor = dark ? new Color(1f, 0.7f, 0.7f) : new Color(0.75f, 0.1f, 0.1f) }
                };
            }
        }
    }
}
