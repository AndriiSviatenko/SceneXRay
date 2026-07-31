using SceneXRay.Editor.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;
using UnityEditor.UIElements;

namespace SceneXRay.Editor.Windows
{
    /// <summary>Lists missing references in loaded scenes and applies best-effort name-based fixes.</summary>
    public class XRayFixMissingWindow : EditorWindow
    {
        private List<DependencyLink> _missingLinks = new List<DependencyLink>();
        private ListView _listView;
        private Label _countChip;
        private VisualElement _emptyState;
        private Button _fixAllBtn, _fixSelectedBtn;
        private Label _status;

        public static void ShowWindow() => GetWindow<XRayFixMissingWindow>().Show();

        private void OnEnable()
        {
            titleContent = new GUIContent("Fix Missing References");
            minSize = new Vector2(460, 260);

            var root = rootVisualElement;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/SceneXRay/Editor/Styles/XRayStyles.uss");
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("xray-window");
            root.EnableInClassList("xray-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("xray-light", !EditorGUIUtility.isProSkin);

            var toolbar = new Toolbar();
            toolbar.Add(new Button(Refresh) { text = XRayLocalization.GetText("refresh") });
            _fixAllBtn = new Button(FixAll) { text = "Fix All" };
            _fixSelectedBtn = new Button(FixSelected) { text = "Fix Selected" };
            toolbar.Add(_fixAllBtn);
            toolbar.Add(_fixSelectedBtn);
            root.Add(toolbar);

            var body = new VisualElement();
            body.AddToClassList("xray-window-body");
            root.Add(body);

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var title = new Label(XRayLocalization.GetText("fix_missing"));
            title.AddToClassList("xray-title");
            header.Add(title);
            _countChip = new Label("0");
            _countChip.AddToClassList("xray-chip");
            _countChip.AddToClassList("xray-chip--danger");
            header.Add(_countChip);
            body.Add(header);

            var hint = new Label(XRayLocalization.GetText("fix_missing_hint"));
            hint.AddToClassList("xray-subtle");
            body.Add(hint);

            _status = new Label { style = { display = DisplayStyle.None } };
            _status.AddToClassList("xray-health__details");
            _status.style.marginBottom = 6;
            body.Add(_status);

            var panel = new VisualElement();
            panel.AddToClassList("xray-panel");
            body.Add(panel);

            _listView = new ListView();
            _listView.style.flexGrow = 1;
            _listView.selectionType = SelectionType.Multiple;
            _listView.fixedItemHeight = 26;
            _listView.makeItem = MakeRow;
            _listView.bindItem = BindRow;
            _listView.selectionChanged += objs =>
            {
                if (objs.FirstOrDefault() is DependencyLink link && link.Source != null)
                {
                    Selection.activeGameObject = link.Source;
                    EditorGUIUtility.PingObject(link.Source);
                }
            };
            panel.Add(_listView);

            _emptyState = BuildEmptyState();
            panel.Add(_emptyState);

            Refresh();
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("xray-row");
            row.AddToClassList("xray-row--missing");

            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("xray-row__icon");
            row.Add(icon);

            var name = new Label { name = "name" };
            name.AddToClassList("xray-row__name");
            row.Add(name);

            var meta = new Label { name = "meta" };
            meta.AddToClassList("xray-row__meta");
            row.Add(meta);

            var tag = new Label("Missing") { name = "tag" };
            tag.AddToClassList("xray-row__tag");
            row.Add(tag);

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _missingLinks.Count) return;
            var link = _missingLinks[index];

            var icon = element.Q<Image>("icon");
            icon.image = link.Source != null
                ? AssetPreview.GetMiniThumbnail(link.Source)
                : EditorGUIUtility.IconContent("console.erroricon.sml").image;

            element.Q<Label>("name").text = link.Source != null ? link.Source.name : "<destroyed>";
            element.Q<Label>("meta").text = $"{link.SourceComponentName}.{link.SourcePropertyName}";
        }

        private static VisualElement BuildEmptyState()
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("xray-empty-state");

            var title = new Label(XRayLocalization.GetText("fix_missing_none"));
            title.AddToClassList("xray-empty-state__title");
            wrap.Add(title);

            var hint = new Label(XRayLocalization.GetText("fix_missing_none_hint"));
            hint.AddToClassList("xray-empty-state__hint");
            wrap.Add(hint);

            return wrap;
        }

        private void Refresh()
        {
            _missingLinks = SceneScanner.ScanAllGameObjects().Where(l => l.IsMissing).ToList();
            _listView.itemsSource = _missingLinks;
            _listView.RefreshItems();

            bool any = _missingLinks.Count > 0;
            _countChip.text = _missingLinks.Count.ToString();
            _countChip.EnableInClassList("xray-chip--danger", any);
            _countChip.EnableInClassList("xray-chip--ok", !any);
            _listView.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyState.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
            _fixAllBtn.SetEnabled(any);
            _fixSelectedBtn.SetEnabled(any);
        }

        private void FixSelected()
        {
            var selected = _listView.selectedItems.Cast<DependencyLink>().ToList();
            int fixedCount = 0;
            // One undo step for the whole batch — otherwise Ctrl+Z unwinds them one by one.
            UndoIntegration.PerformUndoableAction(
                () => fixedCount = selected.Count(FixLink), "SceneXRay Fix Missing References");
            Refresh();
            SetStatus(fixedCount, selected.Count);
        }

        private void FixAll()
        {
            int total = _missingLinks.Count;
            int fixedCount = 0;
            UndoIntegration.PerformUndoableAction(
                () => fixedCount = _missingLinks.Count(FixLink), "SceneXRay Fix All Missing References");
            Refresh();
            SetStatus(fixedCount, total);
        }

        /// <summary>Inline result line — a modal for "fixed 3 of 5" was pure friction.</summary>
        private void SetStatus(int fixedCount, int attempted)
        {
            if (_status == null) return;
            _status.text = XRayLocalization.Format("fix_missing_result", fixedCount, attempted);
            _status.EnableInClassList("is-clean", fixedCount > 0 && fixedCount == attempted);
            _status.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Best-effort fix: finds the broken ObjectReference property on the source component
        /// and tries to rebind it to a scene object whose name matches the property name.
        /// </summary>
        private static bool FixLink(DependencyLink link)
        {
            if (link.Source == null || string.IsNullOrEmpty(link.SourceComponentName)) return false;

            var comp = link.Source.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == link.SourceComponentName);
            if (comp == null) return false;

            using (var so = new SerializedObject(comp))
            {
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.displayName != link.SourcePropertyName) continue;
                    if (prop.objectReferenceValue != null || prop.objectReferenceInstanceIDValue == 0) continue;

                    // Heuristic: a scene object named like the property (e.g. "Player Target" -> "PlayerTarget").
                    string candidateName = link.SourcePropertyName.Replace(" ", "");
                    var candidate = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .FirstOrDefault(g => string.Equals(g.name.Replace(" ", ""), candidateName, System.StringComparison.OrdinalIgnoreCase));
                    if (candidate == null) return false;

                    Undo.RecordObject(comp, "SceneXRay Fix Missing Reference");
                    prop.objectReferenceValue = candidate;
                    return so.ApplyModifiedProperties();
                }
            }
            return false;
        }
    }
}
