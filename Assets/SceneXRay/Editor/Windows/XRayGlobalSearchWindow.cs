using UnityEditor;
using SceneXRay.Editor.UI;
using UnityEngine;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;

namespace SceneXRay.Editor.Windows
{
    public class XRayGlobalSearchWindow : EditorWindow
    {
        private enum SearchMode { Scene, Project, All }

        private TextField _searchTextField;
        private Button _searchBtn;
        private ListView _resultList;
        private Label _resultCountLabel;
        private Label _headerLabel;
        private Label _modeLabel;
        private Label _emptyTitle;
        private Label _emptyHint;
        private Label _dropLabel;
        private VisualElement _emptyState;
        private VisualElement _dropOverlay;
        private bool _isDraggingGameObject;
        private string _lastSelectedName = "";

        private VisualElement _suggestionsContainer;
        private ListView _suggestionsList;
        private List<GameObject> _suggestions = new();
        private List<GameObject> _allObjectsCache = new();

        private ToolbarToggle _sceneToggle;
        private ToolbarToggle _projectToggle;
        private ToolbarToggle _allToggle;

        private SearchMode _currentMode = SearchMode.All;
        private List<DependencyLink> _allResults = new();

        public static void ShowWindow()
        {
            var window = GetWindow<XRayGlobalSearchWindow>();
            window.titleContent = new GUIContent(XRayLocalization.GetText("global_search"));
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("xray-search-root");
            root.EnableInClassList("xray-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("xray-light", !EditorGUIUtility.isProSkin);

            var styleSheet = SceneXRayCompat.LoadStyleSheet();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            _headerLabel = new Label();
            _headerLabel.AddToClassList("xray-search-header");
            root.Add(_headerLabel);

            var modeRow = new VisualElement();
            modeRow.AddToClassList("xray-search-mode-row");
            root.Add(modeRow);

            _modeLabel = new Label();
            _modeLabel.AddToClassList("xray-search-mode-label");
            modeRow.Add(_modeLabel);

            _allToggle = new ToolbarToggle { value = true };
            _allToggle.style.marginRight = 4;
            _allToggle.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue) return;
                _currentMode = SearchMode.All;
                _sceneToggle.SetValueWithoutNotify(false);
                _projectToggle.SetValueWithoutNotify(false);
                OnModeChanged();
            });
            modeRow.Add(_allToggle);

            _sceneToggle = new ToolbarToggle { value = false };
            _sceneToggle.style.marginRight = 4;
            _sceneToggle.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue) return;
                _currentMode = SearchMode.Scene;
                _allToggle.SetValueWithoutNotify(false);
                _projectToggle.SetValueWithoutNotify(false);
                OnModeChanged();
            });
            modeRow.Add(_sceneToggle);

            _projectToggle = new ToolbarToggle { value = false };
            _projectToggle.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue) return;
                _currentMode = SearchMode.Project;
                _allToggle.SetValueWithoutNotify(false);
                _sceneToggle.SetValueWithoutNotify(false);
                OnModeChanged();
            });
            modeRow.Add(_projectToggle);

            var searchWrapper = new VisualElement { style = { flexShrink = 0, marginBottom = 10 } };
            root.Add(searchWrapper);

            var searchRow = new VisualElement();
            searchRow.AddToClassList("xray-search-row");
            searchWrapper.Add(searchRow);

            _searchTextField = new TextField();
            _searchTextField.AddToClassList("xray-search-field");
            _searchTextField.RegisterValueChangedCallback(evt =>
            {
                string newValue = evt.newValue;
                if (newValue != _lastSelectedName)
                {
                    _lastSelectedName = "";
                    if (!string.IsNullOrEmpty(newValue))
                        UpdateSuggestions(newValue);
                    else
                    {
                        HideSuggestions();
                        ShowEmptyState();
                    }
                }
                else
                {
                    HideSuggestions();
                }
            });
            _searchTextField.RegisterCallback<KeyDownEvent>(OnSearchFieldKeyDown);
            searchRow.Add(_searchTextField);

            _searchBtn = new Button(() =>
            {
                if (string.IsNullOrEmpty(_searchTextField.value)) return;
                var target = FindObjectByName(_searchTextField.value);
                if (target != null)
                    ExecuteSearch(target);
            });
            _searchBtn.AddToClassList("xray-search-btn");
            searchRow.Add(_searchBtn);

            _suggestionsContainer = new VisualElement();
            _suggestionsContainer.AddToClassList("xray-search-suggestions");
            searchWrapper.Add(_suggestionsContainer);

            _suggestionsList = new ListView
            {
                selectionType = SelectionType.None,
                style = { flexGrow = 1, backgroundColor = Color.clear }
            };
            _suggestionsList.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("xray-search-suggestion-item");
                label.RegisterCallback<ClickEvent>(evt =>
                {
                    var go = label.userData as GameObject;
                    if (go == null) return;
                    _lastSelectedName = go.name;
                    _searchTextField.value = go.name;
                    HideSuggestions();
                    ExecuteSearch(go);
                    evt.StopPropagation();
                });
                return label;
            };
            _suggestionsList.bindItem = (element, index) =>
            {
                if (element is not Label label || index >= _suggestions.Count) return;
                var go = _suggestions[index];
                string source = go.scene.IsValid()
                    ? XRayLocalization.GetText("source_scene")
                    : XRayLocalization.GetText("source_prefab");
                label.text = $"{go.name} {source}";
                label.userData = go;
            };
            _suggestionsContainer.Add(_suggestionsList);

            _resultCountLabel = new Label();
            _resultCountLabel.AddToClassList("xray-search-count");
            root.Add(_resultCountLabel);

            _resultList = new ListView { selectionType = SelectionType.None };
            _resultList.AddToClassList("xray-search-results");
            _resultList.makeItem = () =>
            {
                var container = new VisualElement();
                container.AddToClassList("xray-search-result-row");

                var nameLabel = new Label();
                nameLabel.AddToClassList("xray-search-result-name");
                container.Add(nameLabel);

                var infoLabel = new Label();
                infoLabel.AddToClassList("xray-search-result-info");
                container.Add(infoLabel);

                var pathLabel = new Label();
                pathLabel.AddToClassList("xray-search-result-path");
                container.Add(pathLabel);

                container.RegisterCallback<ClickEvent>(_ =>
                {
                    var link = container.userData as DependencyLink;
                    if (link?.Source == null) return;
                    Selection.activeGameObject = link.Source;
                    EditorGUIUtility.PingObject(link.Source);
                });

                return container;
            };
            _resultList.bindItem = (element, index) =>
            {
                var link = _resultList.itemsSource[index] as DependencyLink;
                if (link == null) return;

                element.userData = link;
                element.EnableInClassList("missing", link.IsMissing);
                element.EnableInClassList("unityevent", link.IsUnityEvent && !link.IsMissing);
                element.EnableInClassList("direct", link.IsDirect && !link.IsMissing);

                var children = element.Children().ToList();
                if (children.Count < 3) return;

                if (children[0] is Label nameLabel)
                    nameLabel.text = link.Source?.name ?? XRayLocalization.GetText("unknown");

                if (children[1] is Label infoLabel)
                {
                    bool sourceIsScene = link.Source != null && link.Source.scene.IsValid();
                    bool targetIsScene = link.Target != null && link.Target.scene.IsValid();
                    string type = sourceIsScene switch
                    {
                        true when targetIsScene => XRayLocalization.GetText("relation_scene_scene"),
                        false when !targetIsScene => XRayLocalization.GetText("relation_prefab_prefab"),
                        true => XRayLocalization.GetText("relation_scene_prefab"),
                        _ => XRayLocalization.GetText("relation_prefab_scene")
                    };
                    infoLabel.text = type;
                    infoLabel.tooltip = type;
                }

                if (children[2] is Label pathLabel)
                {
                    string path = link.Source != null ? GetHierarchyPath(link.Source) : "";
                    pathLabel.text = path;
                    pathLabel.style.display = string.IsNullOrEmpty(path) ? DisplayStyle.None : DisplayStyle.Flex;
                    pathLabel.tooltip = path;
                }
            };
            root.Add(_resultList);

            _emptyState = new VisualElement();
            _emptyState.AddToClassList("xray-search-empty");
            _emptyTitle = new Label();
            _emptyTitle.AddToClassList("xray-search-empty-title");
            _emptyState.Add(_emptyTitle);
            _emptyHint = new Label();
            _emptyHint.AddToClassList("xray-search-empty-hint");
            _emptyState.Add(_emptyHint);
            root.Add(_emptyState);

            _dropOverlay = new VisualElement();
            _dropOverlay.AddToClassList("xray-search-drop");
            _dropLabel = new Label();
            _dropLabel.AddToClassList("xray-search-drop-label");
            _dropOverlay.Add(_dropLabel);
            root.Add(_dropOverlay);

            EditorApplication.update += OnEditorUpdate;
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);
            root.RegisterCallback<DragExitedEvent>(OnDragExited);
            XRayLocalization.LanguageChanged += ApplyLocalizedTexts;

            ApplyLocalizedTexts();
            RefreshCache();
            ShowEmptyState();
            _searchTextField.Focus();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            XRayLocalization.LanguageChanged -= ApplyLocalizedTexts;
        }

        private void ApplyLocalizedTexts()
        {
            titleContent = new GUIContent(XRayLocalization.GetText("global_search"));
            if (_headerLabel == null) return;

            _headerLabel.text = XRayLocalization.GetText("global_search");
            _modeLabel.text = XRayLocalization.GetText("mode");
            _allToggle.text = XRayLocalization.GetText("mode_all");
            _sceneToggle.text = XRayLocalization.GetText("mode_scene");
            _projectToggle.text = XRayLocalization.GetText("mode_project");
            _searchBtn.text = XRayLocalization.GetText("search");
            _searchTextField.tooltip = XRayLocalization.GetText("tt_search");
            _emptyTitle.text = XRayLocalization.GetText("empty_search_title");
            _emptyHint.text = XRayLocalization.GetText("empty_search_hint");
            _dropLabel.text = XRayLocalization.GetText("drop_here");

            int count = _resultList?.itemsSource?.Count ?? 0;
            _resultCountLabel.text = XRayLocalization.Format("results_found", count);
            _suggestionsList?.RefreshItems();
            _resultList?.RefreshItems();
        }

        private void OnModeChanged()
        {
            RefreshCache();
            if (!string.IsNullOrEmpty(_searchTextField.value))
                UpdateSuggestions(_searchTextField.value);
            if (_allResults.Count > 0)
                ApplyFilter();
        }

        private void RefreshCache()
        {
            _allObjectsCache.Clear();

            if (_currentMode is SearchMode.All or SearchMode.Scene)
            {
                var sceneObjects = SceneXRayCompat.FindAll<GameObject>(FindObjectsInactive.Exclude);
                _allObjectsCache.AddRange(sceneObjects);
            }

            if (_currentMode is SearchMode.All or SearchMode.Project)
            {
                var guids = AssetDatabase.FindAssets("t:Prefab");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && !_allObjectsCache.Contains(prefab))
                        _allObjectsCache.Add(prefab);
                }
            }
        }

        private void UpdateSuggestions(string searchText)
        {
            if (string.IsNullOrEmpty(searchText) || _isDraggingGameObject)
            {
                HideSuggestions();
                return;
            }

            if (searchText == _lastSelectedName)
            {
                HideSuggestions();
                return;
            }

            string lower = searchText.ToLowerInvariant();
            _suggestions = _allObjectsCache
                .Where(go => go != null && go.name.ToLowerInvariant().Contains(lower))
                .Take(10)
                .ToList();

            if (_suggestions.Count > 0)
            {
                _suggestionsList.itemsSource = _suggestions;
                _suggestionsList.RefreshItems();
                _suggestionsContainer.style.display = DisplayStyle.Flex;
                _suggestionsList.selectedIndex = -1;
            }
            else
            {
                HideSuggestions();
            }
        }

        private void HideSuggestions()
        {
            _suggestionsContainer.style.display = DisplayStyle.None;
            _suggestions.Clear();
            _suggestionsList.selectedIndex = -1;
        }

        private void OnSearchFieldKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.DownArrow)
            {
                if (_suggestionsContainer.style.display == DisplayStyle.Flex)
                {
                    _suggestionsList.Focus();
                    if (_suggestionsList.selectedIndex < 0 && _suggestions.Count > 0)
                        _suggestionsList.selectedIndex = 0;
                }
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                HideSuggestions();
                _searchTextField.Focus();
                evt.StopPropagation();
            }
            else if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                if (_suggestionsContainer.style.display == DisplayStyle.Flex && _suggestions.Count > 0)
                {
                    var selected = _suggestionsList.selectedIndex >= 0
                        ? _suggestions[_suggestionsList.selectedIndex]
                        : _suggestions[0];
                    if (selected != null)
                    {
                        _lastSelectedName = selected.name;
                        _searchTextField.value = selected.name;
                        HideSuggestions();
                        ExecuteSearch(selected);
                    }
                }
                evt.StopPropagation();
            }
        }

        private GameObject FindObjectByName(string name)
            => _allObjectsCache.FirstOrDefault(go => go != null && go.name == name);

        private void ExecuteSearch(GameObject target)
        {
            if (target == null) return;
            _searchTextField.value = target.name;
            HideSuggestions();
            _allResults = GlobalReferenceSearch.FindReferences(target);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            List<DependencyLink> filtered = _currentMode switch
            {
                SearchMode.All => _allResults,
                SearchMode.Scene => _allResults.Where(l =>
                    l.Source != null && l.Source.scene.IsValid() &&
                    l.Target != null && l.Target.scene.IsValid() &&
                    !l.IsMissing).ToList(),
                SearchMode.Project => _allResults.Where(l =>
                    (l.Source != null && !l.Source.scene.IsValid()) ||
                    (l.Target != null && !l.Target.scene.IsValid()) ||
                    l.IsMissing).ToList(),
                _ => _allResults
            };

            _resultList.itemsSource = filtered;
            _resultList.RefreshItems();
            _resultCountLabel.text = XRayLocalization.Format("results_found", filtered.Count);
            _emptyState.style.display = filtered.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _resultList.style.display = filtered.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnEditorUpdate()
        {
            bool isDragging = DragAndDrop.objectReferences is { Length: > 0 }
                              && DragAndDrop.objectReferences[0] is GameObject;

            if (isDragging == _isDraggingGameObject) return;
            _isDraggingGameObject = isDragging;
            _dropOverlay.style.display = isDragging ? DisplayStyle.Flex : DisplayStyle.None;
            if (isDragging) HideSuggestions();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences.Length == 0 || DragAndDrop.objectReferences[0] is not GameObject)
                return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            _dropOverlay.style.display = DisplayStyle.Flex;
            HideSuggestions();
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            _dropOverlay.style.display = DisplayStyle.None;
            _isDraggingGameObject = false;

            if (DragAndDrop.objectReferences.Length == 0 || DragAndDrop.objectReferences[0] is not GameObject go)
                return;

            _lastSelectedName = go.name;
            _searchTextField.value = go.name;
            HideSuggestions();
            ExecuteSearch(go);
            evt.StopPropagation();
        }

        private void OnDragExited(DragExitedEvent evt)
        {
            _dropOverlay.style.display = DisplayStyle.None;
            _isDraggingGameObject = false;
        }

        private void ShowEmptyState()
        {
            _resultList.itemsSource = new List<DependencyLink>();
            _resultList.RefreshItems();
            _resultCountLabel.text = XRayLocalization.Format("results_found", 0);
            _emptyState.style.display = DisplayStyle.Flex;
            _resultList.style.display = DisplayStyle.None;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            if (go == null) return "";
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
