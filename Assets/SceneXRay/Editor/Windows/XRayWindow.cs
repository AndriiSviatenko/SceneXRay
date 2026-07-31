using System.Collections.Generic;
using SceneXRay.Editor.UI;
using SceneXRay.Editor.Exporters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;
using System.Linq;
using UnityEditor.UIElements;

namespace SceneXRay.Editor.Windows
{
    /// <summary>Main dependency graph window.</summary>
    public class XRayWindow : EditorWindow
    {
        private const string StyleSheetPath = "Assets/SceneXRay/Editor/Styles/XRayStyles.uss";
        private const string MiniMapPref = "SceneXRay_MiniMap";

        private XRayVirtualGraphView _graphView;
        private List<DependencyLink> _allLinks = new List<DependencyLink>();

        /// <summary>Active graph view — used by shortcuts and smoke tests.</summary>
        public XRayVirtualGraphView GraphView => _graphView;

        /// <summary>Test/helper: force Follow off so Selection noise cannot rewrite history.</summary>
        public void SetFollowEnabled(bool enabled)
        {
            _followToggle?.SetValueWithoutNotify(enabled);
        }

        /// <summary>Test/helper: force Live Mode off so scene churn cannot rebuild mid-test.</summary>
        public void SetLiveModeEnabled(bool enabled)
        {
            LiveModeManager.Enabled = enabled;
            _liveToggle?.SetValueWithoutNotify(enabled);
        }

        private bool _ignoreFollow;
        private TextField _searchField;
        private DropdownField _componentFilter, _linkTypeFilter;
        private IntegerField _depthFilter;
        private ToolbarToggle _prefabToggle, _selectedOnlyToggle, _useCacheToggle, _liveToggle, _miniMapToggle, _followToggle;
        private XRayHealthScoreUI _healthWidget;
        private XRayMiniMap _miniMap;
        private bool _refocusing;
        private bool _liveRefreshPending;
        private bool _settingsRefreshQueued;
        private string _scanFingerprint;
        private Button _refreshBtn, _exportBtn, _moreBtn, _settingsBtn, _searchBtn;
        private Label _searchPlaceholder;

        // %#x = Ctrl+Shift+X (Cmd+Shift+X on macOS). The chord lives on the MenuItem rather than
        // on a [Shortcut] so it is bound the moment the assembly compiles — a [Shortcut] default is
        // only applied to a fresh shortcut profile, so anyone with an existing profile got nothing.
        // Still rebindable: Edit > Shortcuts > Main Menu > Tools/SceneXRay/Open Graph View.
        [MenuItem("Tools/SceneXRay/Open Graph View %#x", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<XRayWindow>();
            window.Show();
            window.Focus();
        }

        /// <summary>Opens the window and focuses the graph on a single GameObject.</summary>
        public static void ShowWindowFocused(GameObject go)
        {
            var window = GetWindow<XRayWindow>();
            window.Show();
            // A prefab *asset* only has nodes when prefab scanning is on — turn it on for the user
            // instead of landing them on an empty "not in the graph" message.
            if (go != null && EditorUtility.IsPersistent(go))
                window._prefabToggle?.SetValueWithoutNotify(true);
            window.RefreshGraph();
            window._graphView.FocusOn(go);
        }

        /// <summary>Opens the window and focuses the graph on an asset (ScriptableObject, material, …).</summary>
        public static void ShowWindowFocusedAsset(Object asset)
        {
            if (asset == null) return;
            var window = GetWindow<XRayWindow>();
            window.Show();
            window.RefreshGraph();
            if (!window._graphView.FocusOnAsset(asset))
                EditorGUIUtility.PingObject(asset);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("SceneXRay Graph");
            minSize = new Vector2(800, 600);

            var root = rootVisualElement;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            else
                Debug.LogWarning($"SceneXRay: Style sheet not found at {StyleSheetPath}.");

            root.EnableInClassList("xray-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("xray-light", !EditorGUIUtility.isProSkin);

            var toolbar = new Toolbar();
            toolbar.AddToClassList("xray-window-toolbar");
            _refreshBtn = new Button(RefreshGraph);
            toolbar.Add(_refreshBtn);

            _searchField = new TextField();
            _searchField.style.width = 160;
            _searchField.RegisterValueChangedCallback(evt => ApplyFilters());
            _searchPlaceholder = new Label { name = "search-placeholder", pickingMode = PickingMode.Ignore };
            _searchField.Add(_searchPlaceholder);
            _searchField.RegisterValueChangedCallback(evt =>
                _searchPlaceholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None);
            toolbar.Add(_searchField);

            _componentFilter = new DropdownField(new List<string> { "All Components" }, 0);
            _componentFilter.RegisterValueChangedCallback(evt => ApplyFilters());
            toolbar.Add(_componentFilter);

            _linkTypeFilter = new DropdownField(new List<string> { "All Links" }, 0);
            _linkTypeFilter.RegisterValueChangedCallback(evt => ApplyFilters());
            toolbar.Add(_linkTypeFilter);

            _depthFilter = new IntegerField { value = -1 };
            _depthFilter.style.width = 36;
            _depthFilter.RegisterValueChangedCallback(evt => ApplyFilters());
            toolbar.Add(_depthFilter);

            toolbar.Add(new ToolbarSpacer());

            _followToggle = new ToolbarToggle();
            toolbar.Add(_followToggle);

            _selectedOnlyToggle = new ToolbarToggle();
            _selectedOnlyToggle.RegisterValueChangedCallback(evt => RefreshGraph());
            toolbar.Add(_selectedOnlyToggle);

            _prefabToggle = new ToolbarToggle();
            _prefabToggle.RegisterValueChangedCallback(evt => RefreshGraph());
            toolbar.Add(_prefabToggle);

            _useCacheToggle = new ToolbarToggle();
            _useCacheToggle.RegisterValueChangedCallback(evt => RefreshGraph());
            toolbar.Add(_useCacheToggle);

            _liveToggle = new ToolbarToggle();
            _liveToggle.SetValueWithoutNotify(LiveModeManager.Enabled);
            _liveToggle.RegisterValueChangedCallback(evt =>
            {
                LiveModeManager.Enabled = evt.newValue;
                if (evt.newValue) RefreshGraph();
            });
            toolbar.Add(_liveToggle);

            _miniMapToggle = new ToolbarToggle();
            _miniMapToggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(MiniMapPref, evt.newValue);
                ToggleMiniMap(evt.newValue);
            });
            toolbar.Add(_miniMapToggle);

            _exportBtn = new Button(() => XRayExporter.ShowExportMenu(_allLinks));
            toolbar.Add(_exportBtn);
            _moreBtn = new Button(ShowMoreMenu);
            toolbar.Add(_moreBtn);
            _settingsBtn = new Button(() => SettingsService.OpenProjectSettings("Project/SceneXRay")) { text = "⚙" };
            toolbar.Add(_settingsBtn);
            _searchBtn = new Button(() => XRayGlobalSearchWindow.ShowWindow()) { text = "🔍" };
            toolbar.Add(_searchBtn);

            root.Add(toolbar);

            ApplyLocalizedTexts();

            _healthWidget = new XRayHealthScoreUI();
            _healthWidget.FocusRequested = go => _graphView?.FocusOn(go);
            root.Add(_healthWidget);

            _graphView = new XRayVirtualGraphView();
            _graphView.FocusFallback = OnFocusFallback;
            _graphView.RequestRefresh = RefreshGraph;
            _graphView.RequestAnalyze = AnalyzeSelected;
            _graphView.RequestExport = () => XRayExporter.ShowExportMenu(_allLinks);
            _graphView.RequestFixMissing = XRayFixMissingWindow.ShowWindow;
            _graphView.RequestFocusSearch = () =>
            {
                _searchField.Focus();
                _searchField.SelectAll();
            };
            root.Add(_graphView);

            if (EditorPrefs.GetBool(MiniMapPref, false))
            {
                _miniMapToggle.SetValueWithoutNotify(true);
                ToggleMiniMap(true);
            }

            LiveModeManager.OnSceneChanged += OnLiveSceneChanged;
            XRayReferenceIndex.IndexUpdated += OnIndexUpdated;
            Selection.selectionChanged += OnSelectionChangedFollow;
            XRayLocalization.LanguageChanged += ApplyLocalizedTexts;
            SceneXRaySettings.SettingsChanged += OnSettingsChanged;

            RefreshGraph();
        }

        private void OnDisable()
        {
            LiveModeManager.OnSceneChanged -= OnLiveSceneChanged;
            XRayReferenceIndex.IndexUpdated -= OnIndexUpdated;
            Selection.selectionChanged -= OnSelectionChangedFollow;
            XRayLocalization.LanguageChanged -= ApplyLocalizedTexts;
            SceneXRaySettings.SettingsChanged -= OnSettingsChanged;
        }

        private void ApplyLocalizedTexts()
        {
            if (_refreshBtn == null) return;

            _refreshBtn.text = XRayLocalization.GetText("refresh");
            _refreshBtn.tooltip = XRayLocalization.GetText("tt_refresh");
            _searchField.tooltip = XRayLocalization.GetText("tt_search");
            _searchPlaceholder.text = XRayLocalization.GetText("search_placeholder");
            _depthFilter.tooltip = XRayLocalization.GetText("tt_depth");

            int compIdx = _componentFilter.index;
            _componentFilter.choices = new List<string>
            {
                XRayLocalization.GetText("all_components"), "Collider", "Rigidbody", "Transform", "Script"
            };
            _componentFilter.index = Mathf.Clamp(compIdx, 0, _componentFilter.choices.Count - 1);
            _componentFilter.tooltip = XRayLocalization.GetText("tt_component");

            int linkIdx = _linkTypeFilter.index;
            _linkTypeFilter.choices = new List<string>
            {
                XRayLocalization.GetText("all_links"),
                XRayLocalization.GetText("direct"),
                XRayLocalization.GetText("unityevent"),
                XRayLocalization.GetText("missing"),
                XRayLocalization.GetText("asset")
            };
            _linkTypeFilter.index = Mathf.Clamp(linkIdx, 0, _linkTypeFilter.choices.Count - 1);
            _linkTypeFilter.tooltip = XRayLocalization.GetText("tt_link_type");

            _followToggle.text = XRayLocalization.GetText("follow");
            _followToggle.tooltip = XRayLocalization.GetText("tt_follow");
            _selectedOnlyToggle.text = XRayLocalization.GetText("selected_only");
            _prefabToggle.text = XRayLocalization.GetText("prefabs");
            _prefabToggle.tooltip = XRayLocalization.GetText("tt_prefabs");
            _useCacheToggle.text = XRayLocalization.GetText("cache");
            _liveToggle.text = XRayLocalization.GetText("live_mode");
            _miniMapToggle.text = XRayLocalization.GetText("minimap");
            _exportBtn.text = XRayLocalization.GetText("export");
            _moreBtn.text = XRayLocalization.GetText("more");
            _moreBtn.tooltip = XRayLocalization.GetText("tt_more");
            _settingsBtn.tooltip = XRayLocalization.GetText("settings");
            _searchBtn.tooltip = XRayLocalization.GetText("tt_global_search");

            _healthWidget?.Refresh(_allLinks);
            ApplyFilters();
        }

        /// <summary>
        /// Scanning settings (asset refs, ignored components, page size) change what the graph
        /// contains, so they need a rescan — colors alone are handled inside the graph view.
        /// Debounced through delayCall: the settings UI saves on every keystroke/drag.
        /// </summary>
        private void OnSettingsChanged()
        {
            if (_settingsRefreshQueued) return;
            _settingsRefreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                _settingsRefreshQueued = false;
                if (this == null) return;

                var current = ScanFingerprint();
                if (current == _scanFingerprint)
                {
                    Repaint();
                    return;
                }
                _scanFingerprint = current;

                // Both caches hold links produced under the previous settings — the graph would
                // otherwise rebuild from stale data and look like the setting did nothing.
                XRayCacheManager.ClearCache();
                XRayReferenceIndex.RebuildImmediate();
                RefreshGraph(resetNavigation: false);
            };
        }

        /// <summary>Everything that changes what the scanner produces or how much of it is shown.</summary>
        private string ScanFingerprint()
        {
            var s = SceneXRaySettings.instance;
            return $"{s.ScanAssetReferences}|{s.IncludeBuiltInAssets}|{s.MaxNodesInGraph}|" +
                   string.Join(",", s.IgnoredComponents);
        }

        private void OnLiveSceneChanged()
        {
            if (!LiveModeManager.Enabled) return;
            _liveRefreshPending = true;
            if (XRayReferenceIndex.IsReady)
                RefreshGraphPreferIndex();
        }

        private void OnIndexUpdated()
        {
            if (!LiveModeManager.Enabled) return;
            if (!_liveRefreshPending && !(_liveToggle != null && _liveToggle.value))
                return;
            _liveRefreshPending = false;
            RefreshGraphPreferIndex();
        }

        private void RefreshGraphPreferIndex()
        {
            // Live updates must never hard-reset Back/Forward history.
            if (_selectedOnlyToggle != null && _selectedOnlyToggle.value)
            {
                RefreshGraph(resetNavigation: false);
                return;
            }
            if (_prefabToggle != null && _prefabToggle.value)
            {
                RefreshGraph(resetNavigation: false);
                return;
            }
            if (!XRayReferenceIndex.IsReady)
            {
                RefreshGraph(resetNavigation: false);
                return;
            }

            _graphView.ClearGraph();
            _allLinks = new List<DependencyLink>(XRayReferenceIndex.AllLinks);

            string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (_useCacheToggle != null && _useCacheToggle.value && !string.IsNullOrEmpty(scenePath))
                XRayCacheManager.Save(scenePath, _allLinks);

            BuildGraph(resetNavigation: false);
            ApplyFilters();
            _healthWidget.Refresh(_allLinks);
        }

        private void ShowMoreMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(XRayLocalization.GetText("fix_missing")), false, XRayFixMissingWindow.ShowWindow);
            menu.AddItem(new GUIContent(XRayLocalization.GetText("global_search")), false, XRayGlobalSearchWindow.ShowWindow);
            menu.AddItem(new GUIContent(XRayLocalization.GetText("bookmarks")), false, XRayBookmarkWindow.ShowWindow);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Advanced/" + XRayLocalization.GetText("clear_cache")), false, () =>
            {
                XRayCacheManager.ClearCache();
                RefreshGraph();
            });
            menu.AddItem(new GUIContent("Advanced/" + XRayLocalization.GetText("tutorial")), false, XRayTutorial.ShowWindow);
            menu.ShowAsContext();
        }

        private void OnSelectionChangedFollow()
        {
            if (_ignoreFollow) return;
            // The graph just set this selection itself — do not re-navigate on our own echo.
            if (XRaySelectionSync.Suppress) return;
            if (_followToggle == null || !_followToggle.value) return;
            if (Selection.activeGameObject == null || _graphView == null) return;
            _graphView.FocusOn(Selection.activeGameObject, recordHistory: false);
        }

        private bool OnFocusFallback(GameObject go)
        {
            if (_refocusing) return false;
            if (XRayReferenceIndex.IsReady &&
                XRayReferenceIndex.GetIncoming(go).Count == 0 &&
                XRayReferenceIndex.GetOutgoing(go).Count == 0)
                return false;

            _refocusing = true;
            try
            {
                _useCacheToggle.SetValueWithoutNotify(false);
                // Keep history — caller is mid-navigation (double-click / Focus).
                RefreshGraph(resetNavigation: false);
                return _graphView.FocusOn(go);
            }
            finally { _refocusing = false; }
        }

        private void ToggleMiniMap(bool enabled)
        {
            if (enabled)
            {
                if (_miniMap == null)
                    _miniMap = new XRayMiniMap(_graphView);
                if (_miniMap.parent == null)
                    _graphView.Add(_miniMap);
            }
            else
            {
                _miniMap?.RemoveFromHierarchy();
            }
        }

        private void ApplyFilters()
        {
            _graphView?.Filter(
                _searchField?.value,
                _componentFilter?.index ?? 0,
                _depthFilter?.value ?? -1,
                _linkTypeFilter?.index ?? 0
            );
        }

        public void RefreshGraph() => RefreshGraph(resetNavigation: true);

        public void RefreshGraph(bool resetNavigation)
        {
            if (resetNavigation)
                _graphView.ResetNavigation();
            _graphView.ClearGraph();
            bool selectedOnly = _selectedOnlyToggle.value;
            bool includePrefabs = _prefabToggle.value;
            bool useCache = _useCacheToggle.value;

            string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            _allLinks = new List<DependencyLink>();

            if (selectedOnly)
            {
                _allLinks.AddRange(SceneScanner.ScanSelectedGameObjects());
            }
            else if (useCache && !string.IsNullOrEmpty(scenePath)
                     && XRayCacheManager.TryLoad(scenePath, out var cachedLinks))
            {
                _allLinks = cachedLinks;
            }
            else if (!includePrefabs && XRayReferenceIndex.IsReady)
            {
                _allLinks = new List<DependencyLink>(XRayReferenceIndex.AllLinks);
                if (useCache && !string.IsNullOrEmpty(scenePath))
                    XRayCacheManager.Save(scenePath, _allLinks);
            }
            else
            {
                _allLinks.AddRange(SceneScanner.ScanAllGameObjects());

                if (includePrefabs)
                    _allLinks.AddRange(PrefabScanner.ScanPrefabAssetsUsedInLoadedScenes());

                if (useCache && !string.IsNullOrEmpty(scenePath))
                    XRayCacheManager.Save(scenePath, _allLinks);
            }

            // Prefabs toggle with cache hit: still merge scene-used prefab assets.
            if (includePrefabs && useCache && !selectedOnly)
                _allLinks.AddRange(PrefabScanner.ScanPrefabAssetsUsedInLoadedScenes());

            BuildGraph(resetNavigation);
            ApplyFilters();
            _healthWidget.Refresh(_allLinks);
        }

        private void BuildGraph(bool resetNavigation = true)
        {
            var nodes = new List<UnityEditor.Experimental.GraphView.Node>();
            var edges = new List<UnityEditor.Experimental.GraphView.Edge>();
            var nodeMap = new Dictionary<GameObject, XRayNode>();

            var allObjects = new HashSet<GameObject>();
            var degrees = XRayAnalyzer.BuildDegreeMap(_allLinks);
            foreach (var l in _allLinks)
            {
                if (l.Source != null) allObjects.Add(l.Source);
                if (l.Target != null) allObjects.Add(l.Target);
            }

            foreach (var go in allObjects)
            {
                var node = new XRayNode(go);
                degrees.TryGetValue(go, out int depCount);
                node.DependencyCount = depCount;
                if (node.DependencyCount > 10)
                    node.AddToClassList("high-dependency");

                var rootGo = go.transform.root.gameObject;
                float hue = Mathf.Abs(rootGo.GetInstanceID() * 0.6180339887f) % 1f;
                var tint = Color.HSVToRGB(hue, 0.55f, EditorGUIUtility.isProSkin ? 0.95f : 0.65f);
                node.style.borderLeftWidth = 3;
                node.style.borderLeftColor = tint;

                nodes.Add(node);
                nodeMap[go] = node;
            }

            foreach (var l in _allLinks.Where(l => l.IsMissing && l.Source != null))
            {
                if (!nodeMap.TryGetValue(l.Source, out var sourceNode)) continue;
                var missingNode = new XRayNode(null, $"Missing: {l.SourcePropertyName}");
                missingNode.AddToClassList("missing-node");
                XRayCustomization.ApplyMissingNodeStyle(missingNode);
                nodes.Add(missingNode);
                edges.Add(new XRayEdge(sourceNode, missingNode, l));
            }

            var assetNodes = new Dictionary<Object, XRayNode>();
            foreach (var l in _allLinks.Where(l => l.IsAssetReference && l.Source != null && l.TargetAsset != null))
            {
                if (!nodeMap.TryGetValue(l.Source, out var sourceNode)) continue;
                if (!assetNodes.TryGetValue(l.TargetAsset, out var assetNode))
                {
                    assetNode = new XRayNode(null, $"Asset: {l.TargetAsset.name}", l.TargetAsset);
                    assetNode.AddToClassList("asset-node");
                    XRayCustomization.ApplyAssetNodeStyle(assetNode);
                    assetNodes[l.TargetAsset] = assetNode;
                    nodes.Add(assetNode);
                }
                edges.Add(new XRayEdge(sourceNode, assetNode, l));
            }

            foreach (var l in _allLinks.Where(l => l.Source != null && l.Target != null && !l.IsMissing))
            {
                if (nodeMap.TryGetValue(l.Source, out var sourceNode) && nodeMap.TryGetValue(l.Target, out var targetNode))
                    edges.Add(new XRayEdge(sourceNode, targetNode, l));
            }

            _graphView.SetContent(nodes, edges, Mathf.Max(10, SceneXRaySettings.instance.MaxNodesInGraph), resetNavigation);
        }

        private void AnalyzeSelected()
        {
            var selected = _graphView.selection;
            if (selected.Count == 0)
            {
                // Inline status beats a modal for something the user can fix in one click.
                _graphView.ShowStatus(XRayLocalization.GetText("graph_select_first"));
                return;
            }

            var rows = new List<XRayReportWindow.Row>();
            foreach (var elem in selected)
            {
                if (elem is not XRayNode node || node.GameObject == null) continue;

                var go = node.GameObject;
                int deps = 0, missing = 0, incoming = 0;
                foreach (var l in _allLinks)
                {
                    bool isSource = l.Source == go;
                    bool isTarget = l.Target == go;
                    if (!isSource && !isTarget) continue;
                    deps++;
                    if (isTarget) incoming++;
                    if (l.IsMissing) missing++;
                }

                rows.Add(XRayReportWindow.MakeRow(go, go.name,
                    XRayLocalization.Format("report_node_stats",
                        deps, deps - incoming, incoming, go.GetComponents<Component>().Length, missing),
                    missing > 0 ? SceneXRaySettings.instance.MissingColor : (Color?)null));
            }

            XRayReportWindow.Show(
                XRayLocalization.GetText("report_analysis"),
                XRayLocalization.Format("report_analysis_summary", rows.Count),
                XRayLocalization.GetText("report_selected_nodes"),
                rows,
                XRayLocalization.GetText("graph_select_first"));
        }
    }

    public static class GraphViewExtensions
    {
        // Component indices match ApplyLocalizedTexts choices: All, Collider, Rigidbody, Transform, Script
        private static readonly string[] ComponentKeys = { null, "Collider", "Rigidbody", "Transform", "Script" };

        public static void Filter(this XRayVirtualGraphView graphView, string searchText, int componentIndex, int depth, int linkTypeIndex)
        {
            var nodes = graphView.GetAllNodes();
            var edges = graphView.GetAllEdges();

            var visibleNodes = new HashSet<UnityEditor.Experimental.GraphView.Node>();
            string searchLower = string.IsNullOrEmpty(searchText) ? null : searchText.ToLowerInvariant();
            string componentType = componentIndex > 0 && componentIndex < ComponentKeys.Length
                ? ComponentKeys[componentIndex]
                : null;

            foreach (var node in nodes)
            {
                bool show = true;
                if (searchLower != null && !node.title.ToLowerInvariant().Contains(searchLower))
                    show = false;

                if (show && componentType != null)
                {
                    var xNode = node as XRayNode;
                    if (!XRayComponentFilter.Matches(xNode?.GameObject, componentType))
                        show = false;
                }

                if (show && depth >= 0)
                {
                    var xNode = node as XRayNode;
                    if (xNode?.GameObject != null)
                    {
                        int d = 0;
                        Transform t = xNode.GameObject.transform;
                        while (t.parent != null) { d++; t = t.parent; }
                        if (d > depth) show = false;
                    }
                }

                node.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) visibleNodes.Add(node);
            }

            foreach (var edge in edges)
            {
                bool showEdge = true;
                if (linkTypeIndex > 0 && edge is XRayEdge re && re.Link != null)
                {
                    // 0 All, 1 Direct, 2 UnityEvent, 3 Missing, 4 Asset
                    showEdge = linkTypeIndex switch
                    {
                        1 => re.Link.IsDirect,
                        2 => re.Link.IsUnityEvent,
                        3 => re.Link.IsMissing,
                        4 => re.Link.IsAssetReference,
                        _ => true
                    };
                }

                var sourceNode = edge.output?.node;
                var targetNode = edge.input?.node;
                if (sourceNode == null || targetNode == null ||
                    !visibleNodes.Contains(sourceNode) || !visibleNodes.Contains(targetNode))
                    showEdge = false;

                edge.style.display = showEdge ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
