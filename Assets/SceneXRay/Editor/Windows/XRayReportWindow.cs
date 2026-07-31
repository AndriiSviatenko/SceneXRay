using System;
using System.Collections.Generic;
using SceneXRay.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.Windows
{
    /// <summary>
    /// Shared report surface: sectioned, scrollable, rows click through to their object.
    /// Replaces the text-blob EditorUtility.DisplayDialog calls — those could not be
    /// scrolled, copied from, or clicked, and truncated on long lists.
    /// </summary>
    public class XRayReportWindow : EditorWindow
    {
        public sealed class Row
        {
            public string Label;
            public string Meta;
            /// <summary>Clicking the row selects and pings this object.</summary>
            public UnityEngine.Object Target;
            /// <summary>Accent bar colour; defaults to the neutral link colour.</summary>
            public Color? Accent;
        }

        public sealed class Section
        {
            public string Title;
            public List<Row> Rows = new();
            public string EmptyText;
        }

        private string _summary;
        private List<Section> _sections = new();

        /// <summary>Opens (or reuses) the report window.</summary>
        public static XRayReportWindow Show(string title, string summary, List<Section> sections)
        {
            var window = GetWindow<XRayReportWindow>(utility: false, title: "SceneXRay Report", focus: true);
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(460, 320);
            window._summary = summary;
            window._sections = sections ?? new List<Section>();
            window.Rebuild();
            window.Show();
            return window;
        }

        /// <summary>Single-section convenience overload.</summary>
        public static XRayReportWindow Show(string title, string summary, string sectionTitle,
            IEnumerable<Row> rows, string emptyText = null)
        {
            var section = new Section { Title = sectionTitle, EmptyText = emptyText };
            if (rows != null) section.Rows.AddRange(rows);
            return Show(title, summary, new List<Section> { section });
        }

        private void OnEnable() => Rebuild();

        private void Rebuild()
        {
            var root = rootVisualElement;
            root.Clear();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/SceneXRay/Editor/Styles/XRayStyles.uss");
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);

            root.AddToClassList("xray-window");
            root.EnableInClassList("xray-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("xray-light", !EditorGUIUtility.isProSkin);

            var body = new VisualElement();
            body.AddToClassList("xray-window-body");
            root.Add(body);

            var title = new Label(titleContent != null ? titleContent.text : "SceneXRay");
            title.AddToClassList("xray-title");
            body.Add(title);

            if (!string.IsNullOrEmpty(_summary))
            {
                var summary = new Label(_summary);
                summary.AddToClassList("xray-subtle");
                body.Add(summary);
            }

            var panel = new VisualElement();
            panel.AddToClassList("xray-panel");
            body.Add(panel);

            var scroll = new ScrollView { style = { flexGrow = 1 } };
            panel.Add(scroll);

            if (_sections.Count == 0)
            {
                scroll.Add(BuildEmpty(XRayLocalization.GetText("report_nothing")));
                return;
            }

            foreach (var section in _sections)
            {
                scroll.Add(BuildSectionHeader(section));

                if (section.Rows.Count == 0)
                {
                    scroll.Add(BuildEmpty(section.EmptyText ?? XRayLocalization.GetText("report_nothing")));
                    continue;
                }

                foreach (var row in section.Rows)
                    scroll.Add(BuildRow(row));
            }
        }

        private static VisualElement BuildSectionHeader(Section section)
        {
            var header = new VisualElement();
            header.AddToClassList("xray-report-header");

            var label = new Label(section.Title ?? "");
            label.AddToClassList("xray-report-header__text");
            header.Add(label);

            var chip = new Label(section.Rows.Count.ToString());
            chip.AddToClassList("xray-chip");
            header.Add(chip);

            return header;
        }

        private static VisualElement BuildRow(Row row)
        {
            var element = new VisualElement();
            element.AddToClassList("xray-row");

            if (row.Accent.HasValue)
                element.style.borderLeftColor = row.Accent.Value;
            else
                element.AddToClassList("xray-row--neutral");

            if (row.Target != null)
            {
                var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("xray-row__icon");
                icon.image = AssetPreview.GetMiniThumbnail(row.Target);
                element.Add(icon);
            }

            var name = new Label(row.Label ?? "");
            name.AddToClassList("xray-row__name");
            element.Add(name);

            var meta = new Label(row.Meta ?? "");
            meta.AddToClassList("xray-row__meta");
            element.Add(meta);

            if (row.Target != null)
            {
                var target = row.Target;
                element.AddToClassList("xray-row--clickable");
                element.RegisterCallback<ClickEvent>(_ =>
                {
                    Selection.activeObject = target;
                    EditorGUIUtility.PingObject(target);
                });
                element.tooltip = XRayLocalization.GetText("report_click_hint");
            }

            return element;
        }

        private static VisualElement BuildEmpty(string text)
        {
            var label = new Label(text);
            label.AddToClassList("xray-report-empty");
            return label;
        }

        /// <summary>Row helper for the common "object + description" case.</summary>
        public static Row MakeRow(UnityEngine.Object target, string label, string meta, Color? accent = null) =>
            new Row { Target = target, Label = label, Meta = meta, Accent = accent };
    }
}
