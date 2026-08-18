using SceneXRay.Editor.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;
using UnityEditor.UIElements;

namespace SceneXRay.Editor.Windows
{
    public class XRayFixMissingWindow : EditorWindow
    {
        private List<DependencyLink> _missingLinks = new List<DependencyLink>();
        private ListView _listView;
        private Label _countChip;
        private VisualElement _emptyState;
        private Button _refreshBtn, _fixAllBtn, _fixSelectedBtn;
        private Label _titleLabel, _hintLabel;
        private Label _status;

        public static void ShowWindow() => GetWindow<XRayFixMissingWindow>().Show();

        private void OnEnable()
        {
            titleContent = new GUIContent(XRayLocalization.GetText("fix_missing_title"));
            minSize = new Vector2(460, 260);

            var root = rootVisualElement;
            root.Clear();

            var styleSheet = SceneXRayCompat.LoadStyleSheet();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("xray-window");
            root.EnableInClassList("xray-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("xray-light", !EditorGUIUtility.isProSkin);

            var toolbar = new Toolbar();
            _refreshBtn = new Button(Refresh);
            _refreshBtn.AddToClassList("xray-toolbar-btn");
            toolbar.Add(_refreshBtn);
            _fixAllBtn = new Button(FixAll);
            _fixAllBtn.AddToClassList("xray-toolbar-btn");
            _fixSelectedBtn = new Button(FixSelected);
            _fixSelectedBtn.AddToClassList("xray-toolbar-btn");
            toolbar.Add(_fixAllBtn);
            toolbar.Add(_fixSelectedBtn);
            root.Add(toolbar);

            var body = new VisualElement();
            body.AddToClassList("xray-window-body");
            root.Add(body);

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            _titleLabel = new Label();
            _titleLabel.AddToClassList("xray-title");
            header.Add(_titleLabel);
            _countChip = new Label("0");
            _countChip.AddToClassList("xray-chip");
            _countChip.AddToClassList("xray-chip--danger");
            header.Add(_countChip);
            body.Add(header);

            _hintLabel = new Label();
            _hintLabel.AddToClassList("xray-subtle");
            body.Add(_hintLabel);

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

            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
            XRayLocalization.LanguageChanged += ApplyLocalizedTexts;

            ApplyLocalizedTexts();
            Refresh();
        }

        private void OnDisable()
        {
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
            XRayLocalization.LanguageChanged -= ApplyLocalizedTexts;
        }

        private void ApplyLocalizedTexts()
        {
            titleContent = new GUIContent(XRayLocalization.GetText("fix_missing_title"));
            if (_refreshBtn == null) return;

            _refreshBtn.text = XRayLocalization.GetText("refresh");
            _fixAllBtn.text = XRayLocalization.GetText("fix_all");
            _fixSelectedBtn.text = XRayLocalization.GetText("fix_selected");
            _titleLabel.text = XRayLocalization.GetText("fix_missing");
            _hintLabel.text = XRayLocalization.GetText("fix_missing_hint");
            _emptyState.Q<Label>("empty-title").text = XRayLocalization.GetText("fix_missing_none");
            _emptyState.Q<Label>("empty-hint").text = XRayLocalization.GetText("fix_missing_none_hint");
            _listView?.RefreshItems();
        }

        private void OnPrefabStageChanged(PrefabStage stage)
        {
            EditorApplication.delayCall += () => { if (this != null) Refresh(); };
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

            var tag = new Label { name = "tag" };
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

            element.Q<Label>("name").text = link.Source != null
                ? link.Source.name
                : XRayLocalization.GetText("destroyed");
            element.Q<Label>("meta").text = $"{link.SourceComponentName}.{link.SourcePropertyName}";
            element.Q<Label>("tag").text = XRayLocalization.GetText("missing");
        }

        private static VisualElement BuildEmptyState()
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("xray-empty-state");

            var title = new Label { name = "empty-title" };
            title.AddToClassList("xray-empty-state__title");
            wrap.Add(title);

            var hint = new Label { name = "empty-hint" };
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

        private void SetStatus(int fixedCount, int attempted)
        {
            if (_status == null) return;
            _status.text = XRayLocalization.Format("fix_missing_result", fixedCount, attempted);
            _status.EnableInClassList("is-clean", fixedCount > 0 && fixedCount == attempted);
            _status.style.display = DisplayStyle.Flex;
        }

        private static bool FixLink(DependencyLink link)
        {
            if (link.Source == null || string.IsNullOrEmpty(link.SourceComponentName)) return false;

            var comp = link.Source.GetComponents<Component>()
                .FirstOrDefault(c => c != null && c.GetType().Name == link.SourceComponentName);
            if (comp == null) return false;

            using (var so = new SerializedObject(comp))
            {
                var prop = so.GetIterator();
                while (prop.Next(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.displayName != link.SourcePropertyName) continue;
                    if (!SceneXRayCompat.IsBrokenReference(prop)) continue;

                    string candidateName = link.SourcePropertyName.Replace(" ", "");
                    var candidate = CandidateObjects()
                        .FirstOrDefault(g => string.Equals(g.name.Replace(" ", ""), candidateName, System.StringComparison.OrdinalIgnoreCase));
                    if (candidate == null) return false;

                    Undo.RecordObject(comp, "SceneXRay Fix Missing Reference");
                    prop.objectReferenceValue = candidate;
                    if (!so.ApplyModifiedProperties()) return false;

                    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null)
                        EditorSceneManager.MarkSceneDirty(stage.scene);
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<GameObject> CandidateObjects()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
                return stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject);

            return SceneXRayCompat.FindAll<GameObject>();
        }
    }
}
