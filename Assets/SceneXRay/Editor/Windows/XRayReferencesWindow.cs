using SceneXRay.Editor.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SceneXRay.Editor.Core;

namespace SceneXRay.Editor.Windows
{
    public class XRayReferencesWindow : EditorWindow
    {
        private const float SidePad = 14f;
        private const float RowH = 24f;
        private const float ArrowW = 20f;
        private const float FieldW = 120f;

        private static readonly Color ColDirect = new(0.38f, 0.59f, 0.86f);
        private static readonly Color ColEvent = new(0.90f, 0.76f, 0.27f);
        private static readonly Color ColMissing = new(0.92f, 0.31f, 0.31f);
        private static readonly Color ColAsset = new(0.65f, 0.49f, 0.98f);

        private GameObject _target;
        private List<DependencyLink> _outgoing = new();
        private IReadOnlyList<DependencyLink> _incoming = Array.Empty<DependencyLink>();
        private Vector2 _scroll;
        private bool _outOpen = true;
        private bool _inOpen = true;

        public static void ShowFor(GameObject go)
        {
            var window = GetWindow<XRayReferencesWindow>();
            window.titleContent = new GUIContent(XRayLocalization.GetText("references_window_title"));
            window.minSize = new Vector2(420, 280);
            window.SetTarget(go);
            window.Show();
        }

        public static void ShowEmpty() => ShowFor(null);

        private void OnEnable()
        {
            wantsMouseMove = true;
            XRayReferenceIndex.IndexUpdated += OnIndexUpdated;
            XRayLocalization.LanguageChanged += OnLanguageChanged;
            titleContent = new GUIContent(XRayLocalization.GetText("references_window_title"));
        }

        private void OnDisable()
        {
            XRayReferenceIndex.IndexUpdated -= OnIndexUpdated;
            XRayLocalization.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            titleContent = new GUIContent(XRayLocalization.GetText("references_window_title"));
            Repaint();
        }

        private void OnIndexUpdated()
        {
            if (_target == null) return;
            if (!XRayReferenceIndex.IsReady)
                XRayReferenceIndex.RebuildImmediate();
            _incoming = XRayReferenceIndex.GetIncoming(_target);
            _outgoing = XRayReferenceIndex.GetOutgoing(_target).ToList();
            Repaint();
        }

        private void SetTarget(GameObject go)
        {
            _target = go;
            if (go == null)
            {
                _outgoing = new List<DependencyLink>();
                _incoming = Array.Empty<DependencyLink>();
                Repaint();
                return;
            }

            if (!XRayReferenceIndex.IsReady)
                XRayReferenceIndex.RebuildImmediate();

            if (EditorUtility.IsPersistent(go) && PrefabUtility.IsPartOfPrefabAsset(go))
            {
                _outgoing = PrefabScanner.ScanPrefab(go);

                _incoming = XRayReferenceIndex.GetIncoming(go)
                    .Concat(XRayReferenceIndex.GetIncomingForAsset(go))
                    .ToList();
                Repaint();
                return;
            }

            _outgoing = XRayReferenceIndex.GetOutgoing(go).ToList();
            if (_outgoing.Count == 0)
                _outgoing = SceneScanner.ScanGameObject(go);
            _incoming = XRayReferenceIndex.GetIncoming(go);
            Repaint();
        }

        private void OnGUI()
        {
            Styles.Ensure();

            if (Event.current.type == EventType.MouseMove)
                Repaint();

            DrawToolbar();

            if (_target == null)
            {
                DrawEmptyState();
                return;
            }

            DrawHero();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SidePad);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(8);
                    DrawSection(XRayLocalization.GetText("references"), _outgoing, false, ref _outOpen);
                    GUILayout.Space(10);
                    DrawSection(XRayLocalization.GetText("referenced_by"), _incoming, true, ref _inOpen);
                    GUILayout.Space(12);
                }
                GUILayout.Space(SidePad);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                var picked = (GameObject)EditorGUILayout.ObjectField(_target, typeof(GameObject), true);
                if (picked != _target) SetTarget(picked);

                if (GUILayout.Button(XRayLocalization.GetText("use_selection"), EditorStyles.toolbarButton, GUILayout.Width(108)))
                    SetTarget(Selection.activeGameObject);
                if (GUILayout.Button(XRayLocalization.GetText("refresh"), EditorStyles.toolbarButton, GUILayout.Width(70)))
                    SetTarget(_target);
                if (GUILayout.Button(XRayLocalization.GetText("open_graph"), EditorStyles.toolbarButton, GUILayout.Width(92)) && _target != null)
                    XRayWindow.ShowWindowFocused(_target);
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
                EditorGUI.DrawRect(new Rect(inset.x, inset.y, 3f, inset.height), ColDirect);
            }

            float y = bar.y + 4f;
            float h = bar.height - 8f;
            float x = bar.x + SidePad + 10f;

            var icon = AssetPreview.GetMiniThumbnail(_target)
                       ?? EditorGUIUtility.IconContent("GameObject Icon").image;
            if (icon != null)
                GUI.DrawTexture(new Rect(x, y + (h - 18) * 0.5f, 18, 18), icon, ScaleMode.ScaleToFit);
            x += 24f;

            GUI.Label(new Rect(x, y, 220f, h), _target.name, Styles.HeroName);

            int missing = _outgoing.Count(l => l.IsMissing);
            string summary = XRayLocalization.Format("reference_summary", _outgoing.Count, _incoming.Count)
                             + (missing > 0
                                 ? XRayLocalization.Format("reference_missing_suffix", missing)
                                 : "");
            Vector2 sumSize = Styles.Muted.CalcSize(new GUIContent(summary));
            GUI.Label(new Rect(bar.xMax - SidePad - sumSize.x - 4f, y, sumSize.x, h), summary, Styles.Muted);
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(XRayLocalization.GetText("no_object_selected"), Styles.HeroName);
                    GUILayout.Space(4);
                    GUILayout.Label(XRayLocalization.GetText("pick_object_hint"), Styles.Muted);
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.FlexibleSpace();
        }

        private void DrawSection(string title, IReadOnlyList<DependencyLink> links, bool isIncoming, ref bool open)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(20)))
            {
                var tip = XRayLocalization.GetText(open ? "collapse" : "expand");
                if (GUILayout.Button(new GUIContent(title, tip), open ? Styles.HeaderOpen : Styles.HeaderClosed, GUILayout.ExpandWidth(false)))
                    open = !open;

                GUILayout.Space(8);
                DrawBadge(links.Count);
                GUILayout.FlexibleSpace();
            }

            if (!open) return;

            GUILayout.Space(4);

            if (links.Count == 0)
            {
                Rect empty = GUILayoutUtility.GetRect(1, 28, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    var fill = EditorGUIUtility.isProSkin
                        ? new Color(0.14f, 0.145f, 0.17f, 1f)
                        : new Color(0.94f, 0.94f, 0.96f, 1f);
                    EditorGUI.DrawRect(empty, fill);
                }
                GUI.Label(new Rect(empty.x + 12, empty.y, empty.width - 12, empty.height), XRayLocalization.GetText("none"), Styles.Muted);
                return;
            }

            int i = 0;
            foreach (var link in links.OrderBy(l => l.IsMissing ? 0 : 1))
            {
                DrawCard(link, isIncoming);
                if (i++ < links.Count - 1)
                    GUILayout.Space(3);
            }
        }

        private void DrawBadge(int count)
        {
            string text = count.ToString();
            float w = Mathf.Max(18f, Styles.Badge.CalcSize(new GUIContent(text)).x + 10f);
            Rect r = GUILayoutUtility.GetRect(w, 16f, GUILayout.Width(w), GUILayout.Height(16f));
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin
                ? new Color(0.35f, 0.50f, 0.94f, 0.55f)
                : new Color(0.35f, 0.50f, 0.94f, 0.35f));
            Styles.Badge.Draw(r, text, false, false, false, false);
        }

        private void DrawCard(DependencyLink link, bool isIncoming)
        {
            Rect row = GUILayoutUtility.GetRect(1, RowH, GUILayout.ExpandWidth(true));
            Color accent = TypeColor(link);
            bool dark = EditorGUIUtility.isProSkin;
            bool hover = row.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                var fill = dark
                    ? new Color(0.16f, 0.165f, 0.20f, hover ? 1f : 0.95f)
                    : new Color(0.94f, 0.94f, 0.96f, 1f);
                EditorGUI.DrawRect(row, fill);
                EditorGUI.DrawRect(new Rect(row.x, row.y + 3, 3f, row.height - 6), accent);
            }

            float padL = row.x + 12f;
            float padR = row.xMax - 8f;
            float chipY = row.y + 3f;
            float chipH = row.height - 6f;

            string property = string.IsNullOrEmpty(link.TargetMethodName)
                ? link.SourcePropertyName
                : $"{link.SourcePropertyName}.{link.TargetMethodName}()";

            if (isIncoming)
            {
                float chipMax = Mathf.Max(100f, padR - padL - FieldW - ArrowW - 8f);
                Rect chip = DrawObjectChip(new Rect(padL, chipY, chipMax, chipH), link.Source, accent);
                DrawArrow(new Rect(chip.xMax + 2f, row.y, ArrowW, row.height), accent);
                GUI.Label(new Rect(chip.xMax + ArrowW + 4f, row.y, padR - chip.xMax - ArrowW - 4f, row.height), property, Styles.Field);
            }
            else
            {
                GUI.Label(new Rect(padL, row.y, FieldW, row.height), property, Styles.Field);
                Rect arrow = new Rect(padL + FieldW + 2f, row.y, ArrowW, row.height);
                DrawArrow(arrow, accent);
                Rect chipArea = new Rect(arrow.xMax + 2f, chipY, padR - arrow.xMax - 2f, chipH);

                if (link.IsMissing)
                    GUI.Label(chipArea, XRayLocalization.GetText("missing"), Styles.Missing);
                else if (link.TargetAsset != null)
                    DrawAssetChip(chipArea, link.TargetAsset, accent);
                else
                    DrawObjectChip(chipArea, link.Target, accent);
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

        private static Rect DrawObjectChip(Rect area, GameObject obj, Color accent)
        {
            if (obj == null)
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
                if (Event.current.clickCount >= 2)
                    Selection.activeGameObject = obj;
                EditorGUIUtility.PingObject(obj);
                Event.current.Use();
            }
            return chip;
        }

        private static void DrawAssetChip(Rect area, UnityEngine.Object asset, Color accent)
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

        private static Color TypeColor(DependencyLink link)
        {
            if (link.IsMissing) return ColMissing;
            if (link.IsUnityEvent) return ColEvent;
            if (link.IsAssetReference) return ColAsset;
            return ColDirect;
        }

        private static class Styles
        {
            public static GUIStyle HeroName;
            public static GUIStyle HeaderOpen;
            public static GUIStyle HeaderClosed;
            public static GUIStyle Field;
            public static GUIStyle Muted;
            public static GUIStyle Missing;
            public static GUIStyle Chip;
            public static GUIStyle Badge;
            public static GUIStyle Arrow;

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
                    clipping = TextClipping.Clip,
                    normal = { textColor = dark ? new Color(0.92f, 0.93f, 0.96f) : new Color(0.12f, 0.12f, 0.16f) }
                };

                HeaderOpen = MakeHeader(dark, true);
                HeaderClosed = MakeHeader(dark, false);

                Field = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    normal = { textColor = dark ? new Color(0.62f, 0.62f, 0.66f) : new Color(0.35f, 0.35f, 0.4f) }
                };

                Muted = new GUIStyle(Field) { normal = { textColor = new Color(0.5f, 0.5f, 0.55f) } };

                Missing = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = ColMissing }
                };

                Chip = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 6, 0, 0),
                    clipping = TextClipping.Clip,
                    imagePosition = ImagePosition.ImageLeft,
                    normal = { textColor = dark ? new Color(0.82f, 0.86f, 0.95f) : new Color(0.12f, 0.22f, 0.42f) },
                    hover = { textColor = dark ? Color.white : new Color(0.05f, 0.35f, 0.75f) }
                };

                Badge = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.92f, 0.94f, 1f) }
                };

                Arrow = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            private static GUIStyle MakeHeader(bool dark, bool open)
            {
                var c = dark ? new Color(0.85f, 0.85f, 0.88f) : new Color(0.15f, 0.15f, 0.18f);
                var hover = dark ? new Color(0.55f, 0.75f, 1f) : new Color(0.1f, 0.4f, 0.8f);
                if (!open) c.a = 0.7f;
                return new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    stretchWidth = false,
                    normal = { textColor = c },
                    hover = { textColor = hover },
                    active = { textColor = hover }
                };
            }
        }
    }
}
