using SceneXRay.Editor.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;

namespace SceneXRay.Editor.Windows
{
    public class XRaySceneDiffWindow : EditorWindow
    {
        private enum Side { A, B }

        private class Row
        {
            public bool IsHeader;
            public Side Side;
            public string ObjectPath;
            public int ChildCount;
            public SceneDiffUtility.DiffEntry Entry;
        }

        private ObjectField _sceneAField, _sceneBField;
        private ListView _listView;
        private Label _summaryA, _summaryB, _summaryCommon;
        private VisualElement _summaryRow, _emptyState;
        private ToolbarSearchField _filterField;
        private Button _openAButton, _openBButton, _compareButton;
        private Label _titleLabel, _hint;

        private SceneDiffUtility.SceneDiffResult _diff;
        private readonly List<Row> _rows = new();
        private readonly HashSet<string> _collapsed = new();

        public static void ShowWindow() => GetWindow<XRaySceneDiffWindow>().Show();

        private void OnEnable()
        {
            titleContent = new GUIContent(XRayLocalization.GetText("scene_diff"));
            minSize = new Vector2(520, 380);

            var root = rootVisualElement;
            root.Clear();
            var styleSheet = SceneXRayCompat.LoadStyleSheet();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("xray-window");
            root.EnableInClassList("xray-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("xray-light", !EditorGUIUtility.isProSkin);

            var body = new VisualElement();
            body.AddToClassList("xray-window-body");
            body.style.flexGrow = 1;
            root.Add(body);

            _titleLabel = new Label();
            _titleLabel.AddToClassList("xray-title");
            body.Add(_titleLabel);

            _hint = new Label(XRayLocalization.GetText("diff_hint"));
            _hint.AddToClassList("xray-subtle");
            body.Add(_hint);

            _sceneAField = new ObjectField { objectType = typeof(SceneAsset) };
            _sceneBField = new ObjectField { objectType = typeof(SceneAsset) };
            _sceneAField.AddToClassList("xray-scene-field");
            _sceneBField.AddToClassList("xray-scene-field");
            _sceneAField.RegisterValueChangedCallback(_ => UpdateSceneButtons());
            _sceneBField.RegisterValueChangedCallback(_ => UpdateSceneButtons());
            body.Add(_sceneAField);
            body.Add(_sceneBField);

            var actions = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 8, marginBottom = 8 }
            };
            _openAButton = MakeSceneButton(Side.A);
            _openBButton = MakeSceneButton(Side.B);
            actions.Add(_openAButton);
            actions.Add(_openBButton);
            _compareButton = new Button(Compare);
            _compareButton.AddToClassList("xray-btn");
            _compareButton.AddToClassList("xray-btn--primary");
            actions.Add(_compareButton);
            body.Add(actions);

            _summaryRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 8, display = DisplayStyle.None }
            };
            _summaryA = MakeChip("xray-chip--danger");
            _summaryB = MakeChip("xray-chip--danger");
            _summaryCommon = MakeChip("xray-chip--ok");
            _summaryRow.Add(_summaryA);
            _summaryRow.Add(_summaryB);
            _summaryRow.Add(_summaryCommon);
            body.Add(_summaryRow);

            _filterField = new ToolbarSearchField();
            _filterField.style.display = DisplayStyle.None;
            _filterField.style.marginBottom = 4;
            _filterField.RegisterValueChangedCallback(_ => RebuildRows());
            body.Add(_filterField);

            var panel = new VisualElement();
            panel.AddToClassList("xray-panel");
            panel.style.flexGrow = 1;
            body.Add(panel);

            _listView = new ListView
            {
                fixedItemHeight = 24,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows
            };
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnRowActivated;
            panel.Add(_listView);

            _emptyState = BuildEmptyState();
            panel.Add(_emptyState);

            XRayLocalization.LanguageChanged += ApplyLocalizedTexts;
            ApplyLocalizedTexts();
            UpdateSceneButtons();
        }

        private void OnDisable()
        {
            XRayLocalization.LanguageChanged -= ApplyLocalizedTexts;
        }

        private void ApplyLocalizedTexts()
        {
            titleContent = new GUIContent(XRayLocalization.GetText("scene_diff"));
            if (_titleLabel == null) return;

            _titleLabel.text = XRayLocalization.GetText("scene_diff");
            _hint.text = XRayLocalization.GetText("diff_hint");
            _sceneAField.label = XRayLocalization.GetText("diff_scene_a");
            _sceneBField.label = XRayLocalization.GetText("diff_scene_b");
            _openAButton.text = XRayLocalization.GetText("diff_open_a");
            _openBButton.text = XRayLocalization.GetText("diff_open_b");
            _openAButton.tooltip = XRayLocalization.GetText("diff_open_tooltip");
            _openBButton.tooltip = XRayLocalization.GetText("diff_open_tooltip");
            _compareButton.text = XRayLocalization.GetText("compare");
            _emptyState.Q<Label>("empty-title").text = XRayLocalization.GetText("diff_pick_scenes");

            if (_diff != null)
            {
                _summaryA.text = $"{XRayLocalization.GetText("diff_only_a")}: {_diff.OnlyInSceneA.Count}";
                _summaryB.text = $"{XRayLocalization.GetText("diff_only_b")}: {_diff.OnlyInSceneB.Count}";
                _summaryCommon.text = $"{XRayLocalization.GetText("diff_common")}: {_diff.Common.Count}";
            }
            _listView?.RefreshItems();
        }

        private Button MakeSceneButton(Side side)
        {
            var button = new Button(() => OpenScene(side));
            button.AddToClassList("xray-btn");
            return button;
        }

        private static Label MakeChip(string modifier)
        {
            var chip = new Label();
            chip.AddToClassList("xray-chip");
            chip.AddToClassList(modifier);
            return chip;
        }

        private static VisualElement BuildEmptyState()
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("xray-empty-state");

            var title = new Label { name = "empty-title" };
            title.AddToClassList("xray-empty-state__title");
            wrap.Add(title);

            return wrap;
        }

        private string ScenePathOf(Side side)
            => AssetDatabase.GetAssetPath(side == Side.A ? _sceneAField.value : _sceneBField.value);

        private void OpenScene(Side side)
        {
            string path = ScenePathOf(side);
            if (string.IsNullOrEmpty(path)) return;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            UpdateSceneButtons();
            _listView.RefreshItems();
        }

        private void UpdateSceneButtons()
        {
            _openAButton.SetEnabled(!IsOpen(Side.A) && !string.IsNullOrEmpty(ScenePathOf(Side.A)));
            _openBButton.SetEnabled(!IsOpen(Side.B) && !string.IsNullOrEmpty(ScenePathOf(Side.B)));
        }

        private bool IsOpen(Side side)
        {
            string path = ScenePathOf(side);
            if (string.IsNullOrEmpty(path)) return false;
            var scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded;
        }

        private void Compare()
        {
            string pathA = ScenePathOf(Side.A);
            string pathB = ScenePathOf(Side.B);
            if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB))
            {
                _diff = null;
                _summaryRow.style.display = DisplayStyle.None;
                _filterField.style.display = DisplayStyle.None;
                RebuildRows();
                return;
            }

            _diff = SceneDiffUtility.CompareScenes(pathA, pathB);
            _collapsed.Clear();

            _summaryA.text = $"{XRayLocalization.GetText("diff_only_a")}: {_diff.OnlyInSceneA.Count}";
            _summaryB.text = $"{XRayLocalization.GetText("diff_only_b")}: {_diff.OnlyInSceneB.Count}";
            _summaryCommon.text = $"{XRayLocalization.GetText("diff_common")}: {_diff.Common.Count}";
            _summaryRow.style.display = DisplayStyle.Flex;
            _filterField.style.display = DisplayStyle.Flex;

            UpdateSceneButtons();
            RebuildRows();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            if (_diff != null)
            {
                AppendSide(Side.A, _diff.OnlyInSceneA);
                AppendSide(Side.B, _diff.OnlyInSceneB);
            }

            _listView.itemsSource = _rows;
            _listView.RefreshItems();

            bool any = _rows.Count > 0;
            _listView.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyState.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void AppendSide(Side side, List<SceneDiffUtility.DiffEntry> entries)
        {
            string filter = _filterField?.value;
            bool filtered = !string.IsNullOrEmpty(filter);

            var groups = entries
                .Where(e => !filtered || Matches(e, filter))
                .GroupBy(e => e.SourcePath)
                .OrderBy(g => g.Key, System.StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var children = group.OrderBy(e => e.PropertyName, System.StringComparer.OrdinalIgnoreCase).ToList();
                _rows.Add(new Row
                {
                    IsHeader = true,
                    Side = side,
                    ObjectPath = group.Key,
                    ChildCount = children.Count
                });

                if (!filtered && _collapsed.Contains(FoldKey(side, group.Key))) continue;

                foreach (var entry in children)
                    _rows.Add(new Row { Side = side, ObjectPath = group.Key, Entry = entry });
            }
        }

        private static bool Matches(SceneDiffUtility.DiffEntry entry, string filter)
        {
            return Contains(entry.SourcePath, filter)
                   || Contains(entry.TargetPath, filter)
                   || Contains(entry.ComponentName, filter)
                   || Contains(entry.PropertyName, filter);
        }

        private static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack) &&
               haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static string FoldKey(Side side, string path) => side + ":" + path;

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("xray-row");

            var side = new Label { name = "side" };
            side.AddToClassList("xray-row__tag");
            side.style.marginRight = 6;
            row.Add(side);

            var name = new Label { name = "name" };
            name.AddToClassList("xray-row__name");
            row.Add(name);

            var meta = new Label { name = "meta" };
            meta.AddToClassList("xray-row__meta");
            row.Add(meta);

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            var row = _rows[index];

            var side = element.Q<Label>("side");
            var name = element.Q<Label>("name");
            var meta = element.Q<Label>("meta");

            side.text = row.Side == Side.A ? "A" : "B";
            element.EnableInClassList("xray-row--missing",
                !row.IsHeader && row.Entry.LinkType == LinkType.Missing);

            if (row.IsHeader)
            {
                bool collapsed = _collapsed.Contains(FoldKey(row.Side, row.ObjectPath));
                name.text = (collapsed ? "▸ " : "▾ ") + row.ObjectPath;
                meta.text = row.ChildCount.ToString();
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                element.style.paddingLeft = 0;
            }
            else
            {
                name.text = "→ " + row.Entry.TargetPath;
                meta.text = string.IsNullOrEmpty(row.Entry.MethodName)
                    ? $"{row.Entry.ComponentName}.{row.Entry.PropertyName}"
                    : $"{row.Entry.ComponentName}.{row.Entry.PropertyName}() → {row.Entry.MethodName}";
                name.style.unityFontStyleAndWeight = FontStyle.Normal;
                element.style.paddingLeft = 18;
            }
        }

        private void OnRowActivated(IEnumerable<object> selection)
        {
            if (selection.FirstOrDefault() is not Row row) return;

            if (row.IsHeader)
            {
                string key = FoldKey(row.Side, row.ObjectPath);
                if (!_collapsed.Remove(key)) _collapsed.Add(key);
                RebuildRows();
                return;
            }

            Reveal(row);
        }

        private void Reveal(Row row)
        {
            var go = ResolveInLoadedScenes(ScenePathOf(row.Side), row.ObjectPath);
            if (go == null)
            {
                _hint.text = XRayLocalization.Format("diff_open_to_reveal", row.Side, row.ObjectPath);
                return;
            }

            _hint.text = XRayLocalization.GetText("diff_hint");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private static GameObject ResolveInLoadedScenes(string scenePath, string hierarchyPath)
        {
            if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(hierarchyPath)) return null;

            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) return null;

            var segments = hierarchyPath.Split('/');
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != segments[0]) continue;

                var current = root.transform;
                for (int i = 1; i < segments.Length && current != null; i++)
                    current = FindChild(current, segments[i]);

                if (current != null) return current.gameObject;
            }
            return null;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }
    }
}
