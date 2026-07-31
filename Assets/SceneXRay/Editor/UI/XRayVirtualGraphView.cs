using System;
using SceneXRay.Editor.Exporters;
using SceneXRay.Editor.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SceneXRay.Editor.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.UI
{
    public enum FocusDirection
    {
        Both = 0,
        Outgoing = 1,
        Incoming = 2
    }

    /// <summary>
    /// Paged GraphView with browser-style drill-in focus, camera history, and radius/direction filters.
    /// Back restores the previous view (pan/zoom + page + focus) — it does not re-spawn/relayout the full graph.
    /// </summary>
    public partial class XRayVirtualGraphView : GraphView
    {
        private const float ColumnSpacing = 280f;
        private const float RowSpacing = 86f;
        private const float Padding = 60f;
        private const int MaxRadius = 3;

        private sealed class FocusState
        {
            public GameObject Center; // null = show all
            public string CenterGlobalId;
            /// <summary>Asset (ScriptableObject, material, …) focus — the graph's synthetic asset nodes.</summary>
            public UnityEngine.Object CenterAsset;
            public string CenterAssetGuid;
            public int Radius = 1;
            public FocusDirection Direction = FocusDirection.Both;
            public int Page;
            public bool HasView;
            public Vector3 ViewPosition;
            public Vector3 ViewScale = Vector3.one;

            // Unity returns a non-empty "null" GlobalObjectId for some transient GOs
            // (EditMode tests, unsaved objects). Treating it as a real id made every
            // FocusOn look like SameFocus — Back/history collapsed to one entry.
            private static bool IsUsableGlobalId(string id) =>
                !string.IsNullOrEmpty(id) &&
                id != "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0";

            public bool IsAssetFocus => CenterAsset != null || !string.IsNullOrEmpty(CenterAssetGuid);

            public bool IsShowAll => Center == null && !IsUsableGlobalId(CenterGlobalId) && !IsAssetFocus;

            /// <summary>Identity used for history dedupe and breadcrumb labels.</summary>
            public UnityEngine.Object CenterObject => Center != null ? Center : CenterAsset;

            public bool SameFocus(FocusState other)
            {
                if (other == null) return false;
                if (Radius != other.Radius || Direction != other.Direction) return false;
                if (IsShowAll && other.IsShowAll) return true;
                if (IsAssetFocus || other.IsAssetFocus)
                {
                    if (CenterAsset != null && CenterAsset == other.CenterAsset) return true;
                    return !string.IsNullOrEmpty(CenterAssetGuid) && CenterAssetGuid == other.CenterAssetGuid;
                }
                if (IsUsableGlobalId(CenterGlobalId) && CenterGlobalId == other.CenterGlobalId)
                    return true;
                return Center != null && Center == other.Center;
            }

            public FocusState Clone() => new FocusState
            {
                Center = Center,
                CenterGlobalId = CenterGlobalId,
                CenterAsset = CenterAsset,
                CenterAssetGuid = CenterAssetGuid,
                Radius = Radius,
                Direction = Direction,
                Page = Page,
                HasView = HasView,
                ViewPosition = ViewPosition,
                ViewScale = ViewScale
            };

            public static FocusState ForCenter(GameObject go, int radius, FocusDirection dir)
            {
                string gid = go != null ? SceneXRay.Editor.Core.DependencyLink.GetGlobalId(go) : null;
                if (!IsUsableGlobalId(gid)) gid = null;
                return new FocusState
                {
                    Center = go,
                    CenterGlobalId = gid,
                    Radius = radius,
                    Direction = dir
                };
            }

            public static FocusState ForAsset(UnityEngine.Object asset, int radius, FocusDirection dir)
            {
                string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
                return new FocusState
                {
                    CenterAsset = asset,
                    CenterAssetGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path),
                    Radius = radius,
                    Direction = dir
                };
            }
        }

        private List<Node> _allNodes = new();
        private List<Edge> _allEdges = new();
        private readonly Dictionary<GameObject, Node> _nodeByGo = new();
        private readonly Dictionary<UnityEngine.Object, Node> _nodeByAsset = new();
        private readonly HashSet<Node> _clickWired = new();
        private HashSet<Node> _focusSet;
        private XRayNode _focusCenter;
        private FocusState _currentState = new();
        private int _visibleNodes = 100;
        private int _currentPage;
        private bool _fullGraphLaidOut;
        private readonly Dictionary<Node, Rect> _fullGraphPositions = new();

        private readonly List<FocusState> _history = new();
        private int _historyIndex = -1;

        private Label _pageLabel;
        private Label _emptyLabel;
        private Toolbar _toolbar;
        private Button _backBtn;
        private Button _forwardBtn;
        private Button _prevPageBtn;
        private Button _nextPageBtn;
        private Button _focusBtn;
        private Button _showAllBtn;
        private DropdownField _radiusField;
        private DropdownField _directionField;
        private Label _legendLabel;

        /// <summary>Optional hooks for empty-canvas context menu (wired by XRayWindow).</summary>
        public Action RequestRefresh;
        public Action RequestAnalyze;
        public Action RequestExport;
        public Action RequestFixMissing;

        private readonly Dictionary<Node, int> _nodeDepths = new();
        private readonly Dictionary<Node, List<Node>> _children = new();
        private readonly Dictionary<Node, List<Node>> _parents = new();
        private readonly HashSet<GraphElement> _mountedElements = new();

        public Func<GameObject, bool> FocusFallback;

        public int FocusRadius => _currentState?.Radius ?? 1;
        public FocusDirection CurrentDirection => _currentState?.Direction ?? FocusDirection.Both;
        public bool IsFocused => _focusSet != null;
        public bool CanGoBack => _historyIndex > 0;
        public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;
        public int HistoryCount => _history.Count;
        public int HistoryIndex => _historyIndex;
        public GameObject FocusCenterObject => _focusCenter != null ? _focusCenter.GameObject : null;

        private bool _suppressFocusFallback;

        public XRayVirtualGraphView()
        {
            style.flexGrow = 1;
            focusable = true;
            SetupZoom(0.05f, 2.5f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            CreateToolbar();
            SetupInteraction();
            ApplyToolbarLocalization();
            XRayLocalization.LanguageChanged += ApplyToolbarLocalization;
            RegisterCallback<DetachFromPanelEvent>(_ =>
                XRayLocalization.LanguageChanged -= ApplyToolbarLocalization);

            // Live graph customization: recolor edges / missing nodes when Settings change.
            SceneXRaySettings.SettingsChanged += RefreshCustomization;
            RegisterCallback<DetachFromPanelEvent>(_ =>
                SceneXRaySettings.SettingsChanged -= RefreshCustomization);

            // TrickleDown: beat GraphView's Backspace/Delete before it eats them.
            RegisterCallback<KeyDownEvent>(OnGraphKeyDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseDownEvent>(_ => Focus(), TrickleDown.NoTrickleDown);

            _emptyLabel = new Label();
            _emptyLabel.AddToClassList("empty-hint");
            _emptyLabel.pickingMode = PickingMode.Ignore;
            Add(_emptyLabel);
            UpdateEmptyLabel();

            ResetHistoryToShowAll();
        }

        private void OnGraphKeyDown(KeyDownEvent e)
        {
            var t = e.target as VisualElement;
            if (t != null)
            {
                // Walking up: ignore when editing text.
                for (var ve = t; ve != null; ve = ve.parent)
                {
                    if (ve is TextField || ve is IntegerField)
                        return;
                    var n = ve.GetType().Name;
                    if (n.IndexOf("TextInput", StringComparison.Ordinal) >= 0)
                        return;
                }
            }

            bool handled = true;
            switch (e.keyCode)
            {
                case KeyCode.Backspace:
                    GoBack();
                    break;
                case KeyCode.LeftArrow when e.altKey:
                    GoBack();
                    break;
                case KeyCode.RightArrow when e.altKey:
                    GoForward();
                    break;
                case KeyCode.Escape:
                    NavigateUp();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    FocusSelection();
                    break;
                case KeyCode.Equals when e.ctrlKey || e.commandKey:
                case KeyCode.KeypadPlus when e.ctrlKey || e.commandKey:
                    ZoomIn();
                    break;
                case KeyCode.Minus when e.ctrlKey || e.commandKey:
                case KeyCode.KeypadMinus when e.ctrlKey || e.commandKey:
                    ZoomOut();
                    break;
                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                    ExpandRadius();
                    break;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    ShrinkRadius();
                    break;
                case KeyCode.Alpha0:
                case KeyCode.Keypad0:
                    ResetZoom();
                    break;
                case KeyCode.F when e.ctrlKey || e.commandKey:
                    RequestFocusSearch?.Invoke();
                    break;
                case KeyCode.S when e.ctrlKey || e.commandKey:
                    SaveLayout();
                    break;
                // Fallback for hosts that do not deliver the Undo/Redo command events.
                case KeyCode.Z when (e.ctrlKey || e.commandKey) && e.shiftKey:
                case KeyCode.Y when e.ctrlKey || e.commandKey:
                    if (!UndoJustHandled) RedoLayoutChange();
                    break;
                case KeyCode.Z when e.ctrlKey || e.commandKey:
                    if (!UndoJustHandled) UndoLayoutChange();
                    break;
                // Arrow keys walk between cards; Alt+←/→ is history and is handled above.
                case KeyCode.LeftArrow:
                    handled = MoveSelection(Vector2.left);
                    break;
                case KeyCode.RightArrow:
                    handled = MoveSelection(Vector2.right);
                    break;
                case KeyCode.UpArrow:
                    handled = MoveSelection(new Vector2(0f, -1f));
                    break;
                case KeyCode.DownArrow:
                    handled = MoveSelection(new Vector2(0f, 1f));
                    break;
                case KeyCode.Delete:
                    break; // swallow — viewer only
                default:
                    handled = false;
                    break;
            }

            // StopImmediatePropagation alone marks the key as consumed in Unity 6;
            // PreventDefault() is obsolete for KeyDownEvent and only produced a warning.
            if (handled)
                e.StopImmediatePropagation();
        }

        private void CreateToolbar()
        {
            _toolbar = new Toolbar();
            _toolbar.AddToClassList("xray-graph-toolbar");

            _backBtn = new Button(GoBack) { text = "←" };
            _backBtn.AddToClassList("xray-nav-btn");
            _forwardBtn = new Button(GoForward) { text = "→" };
            _forwardBtn.AddToClassList("xray-nav-btn");
            _toolbar.Add(_backBtn);
            _toolbar.Add(_forwardBtn);
            _toolbar.Add(new ToolbarSpacer());

            CreateLayoutMenu(_toolbar);
            _toolbar.Add(new ToolbarSpacer());

            var radiusLabel = new Label("R") { tooltip = "Focus neighborhood radius (hops)" };
            radiusLabel.AddToClassList("xray-field-label");
            _toolbar.Add(radiusLabel);
            _radiusField = new DropdownField(new List<string> { "1", "2", "3" }, 0);
            _radiusField.style.width = 44;
            _radiusField.RegisterValueChangedCallback(OnRadiusChanged);
            _toolbar.Add(_radiusField);

            var dirLabel = new Label("Dir") { tooltip = "Focus edge direction filter" };
            dirLabel.AddToClassList("xray-field-label");
            _toolbar.Add(dirLabel);
            _directionField = new DropdownField(new List<string> { "Both", "Out", "In" }, 0);
            _directionField.style.width = 72;
            _directionField.RegisterValueChangedCallback(OnDirectionChanged);
            _toolbar.Add(_directionField);

            _toolbar.Add(new ToolbarSpacer());
            _focusBtn = new Button(FocusSelection);
            _showAllBtn = new Button(ShowAll);
            _toolbar.Add(_focusBtn);
            _toolbar.Add(_showAllBtn);

            _breadcrumbs = new ToolbarBreadcrumbs { name = "xray-breadcrumbs" };
            _breadcrumbs.AddToClassList("xray-breadcrumbs");
            _toolbar.Add(_breadcrumbs);

            Add(_toolbar);
            UpdateNavButtons();
        }

        private void ApplyToolbarLocalization()
        {
            if (_backBtn == null) return;

            _backBtn.tooltip = XRayLocalization.GetText("graph_back_tt");
            _forwardBtn.tooltip = XRayLocalization.GetText("graph_forward_tt");
            _prevPageBtn.tooltip = XRayLocalization.GetText("graph_prev_page");
            _nextPageBtn.tooltip = XRayLocalization.GetText("graph_next_page");
            _radiusField.tooltip = XRayLocalization.GetText("graph_radius_tt");
            _directionField.choices = new List<string>
            {
                XRayLocalization.GetText("graph_dir_both"),
                XRayLocalization.GetText("graph_dir_out"),
                XRayLocalization.GetText("graph_dir_in")
            };
            _directionField.SetValueWithoutNotify(_directionField.choices[Mathf.Clamp((int)CurrentDirection, 0, 2)]);
            _directionField.tooltip = XRayLocalization.GetText("graph_dir_tt");
            _focusBtn.text = XRayLocalization.GetText("graph_focus_selected");
            _focusBtn.tooltip = XRayLocalization.GetText("graph_focus_selected_tt");
            _showAllBtn.text = XRayLocalization.GetText("graph_show_all");
            _showAllBtn.tooltip = XRayLocalization.GetText("graph_show_all_tt");
            if (_legendLabel != null)
                _legendLabel.text = XRayLocalization.GetText("graph_legend");
            UpdateLayoutMenuText();
            LocalizeStatusBar();
            UpdateEmptyLabel();
            UpdateCounts();
            UpdateZoomLabel();
            RefreshFocusLabel();
        }

        private void UpdateEmptyLabel()
        {
            if (_emptyLabel != null)
                _emptyLabel.text = XRayLocalization.GetText("graph_empty");
        }

        public Node FindNode(GameObject go)
        {
            return go != null && _nodeByGo.TryGetValue(go, out var node) ? node : null;
        }

        /// <summary>Synthetic node for a referenced asset (ScriptableObject, material, …).</summary>
        public Node FindAssetNode(UnityEngine.Object asset)
        {
            return asset != null && _nodeByAsset.TryGetValue(asset, out var node) ? node : null;
        }

        /// <summary>
        /// Re-apply Settings-driven colors/line width to every edge and missing-reference node.
        /// Called live when the user edits SceneXRay settings.
        /// </summary>
        private void RefreshCustomization()
        {
            // The edge owns its highlight state, so a plain refresh keeps it.
            foreach (var edge in _allEdges)
                if (edge is XRayEdge xEdge)
                    xEdge.RefreshVisual();

            foreach (var node in _allNodes)
            {
                if (node is not XRayNode xNode) continue;
                if (xNode.ClassListContains("missing-node"))
                    XRayCustomization.ApplyMissingNodeStyle(xNode);
                else if (xNode.Asset != null)
                    XRayCustomization.ApplyAssetNodeStyle(xNode);
            }
        }

        private XRayNode ResolveCenterNode(FocusState state)
        {
            if (state == null) return null;
            return state.IsAssetFocus
                ? FindAssetNode(state.CenterAsset) as XRayNode
                : FindNode(state.Center) as XRayNode;
        }

        public void SetContent(List<Node> nodes, List<Edge> edges, int pageSize = 100, bool resetNavigation = true)
        {
            // Snapshot history before node refs die (Live Mode soft refresh).
            List<FocusState> historySnap = null;
            int historyIndexSnap = 0;
            if (!resetNavigation && _history.Count > 0)
            {
                historySnap = _history.Select(h => h.Clone()).ToList();
                historyIndexSnap = _historyIndex;
            }

            _allNodes = nodes;
            _allEdges = edges;
            _visibleNodes = pageSize;
            _currentPage = 0;
            _fullGraphLaidOut = false;
            _fullGraphPositions.Clear();
            _focusSet = null;
            _focusCenter = null;

            _nodeByGo.Clear();
            _nodeByAsset.Clear();
            ClearHighlight();
            _hoverNode = null;
            foreach (var node in nodes)
            {
                RegisterHoverHighlight(node);

                if (node is not XRayNode xNode) continue;

                if (xNode.GameObject != null) _nodeByGo[xNode.GameObject] = node;
                else if (xNode.Asset != null) _nodeByAsset[xNode.Asset] = node;
                else continue; // "Missing: …" placeholders have no identity to drill into

                if (!_clickWired.Add(node)) continue; // soft rebuilds reuse node instances

                var captured = xNode;
                node.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0 && evt.clickCount >= 2)
                    {
                        Focus();
                        FocusOnNode(captured);
                        evt.StopImmediatePropagation();
                    }
                }, TrickleDown.TrickleDown);
            }

            BuildAdjacency();

            if (historySnap != null)
            {
                _history.Clear();
                foreach (var h in historySnap)
                {
                    ResolveCenter(h);
                    _history.Add(h);
                }
                _historyIndex = Mathf.Clamp(historyIndexSnap, 0, _history.Count - 1);
                _currentState = _history[_historyIndex].Clone();
                ResolveCenter(_currentState);
                _suppressFocusFallback = true;
                try
                {
                    ApplyState(_currentState, restoreView: _currentState.HasView, preferFrameCenter: null,
                        restoreFullPositions: false, forceLayout: true);
                }
                finally { _suppressFocusFallback = false; }
            }
            else
            {
                ResetHistoryToShowAll();
                ApplyState(_currentState, restoreView: false, preferFrameCenter: null,
                    restoreFullPositions: false, forceLayout: true);
            }

            // New content: old positions are meaningless, then re-apply the user's own
            // arrangement for this scene if there is one.
            ResetPositionHistory();
            if (AutoRestoreLayout && HasSavedLayout)
                RestoreLayout(silent: true);
            if (!HasPositionBaseline)
                CommitPositions();

            UpdateNavButtons();
            schedule.Execute(Focus).ExecuteLater(1);
        }

        private void ResolveCenter(FocusState state)
        {
            if (state == null || state.IsShowAll) return;

            if (state.IsAssetFocus)
            {
                if (state.CenterAsset != null && _nodeByAsset.ContainsKey(state.CenterAsset))
                    return;

                // Asset instances survive domain reloads, but a rebuilt graph may hold a
                // different instance for the same file — re-resolve through the GUID.
                state.CenterAsset = null;
                if (string.IsNullOrEmpty(state.CenterAssetGuid)) return;
                foreach (var kv in _nodeByAsset)
                {
                    string path = AssetDatabase.GetAssetPath(kv.Key);
                    if (!string.IsNullOrEmpty(path) &&
                        AssetDatabase.AssetPathToGUID(path) == state.CenterAssetGuid)
                    {
                        state.CenterAsset = kv.Key;
                        return;
                    }
                }
                return;
            }

            if (state.Center != null && _nodeByGo.ContainsKey(state.Center))
                return;

            state.Center = null;
            if (string.IsNullOrEmpty(state.CenterGlobalId) ||
                state.CenterGlobalId == "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0")
                return;

            foreach (var kv in _nodeByGo)
            {
                if (SceneXRay.Editor.Core.DependencyLink.GetGlobalId(kv.Key) == state.CenterGlobalId)
                {
                    state.Center = kv.Key;
                    return;
                }
            }
        }

        private (Node source, Node target) ResolveEdge(Edge edge)
        {
            if (edge is XRayEdge re && re.Link != null)
            {
                var source = re.Link.Source != null ? FindNode(re.Link.Source) : null;
                Node target = re.Link.Target != null ? FindNode(re.Link.Target) : re.SyntheticTargetNode;
                return (source, target);
            }
            return (null, null);
        }

        private List<Node> DisplayedNodes()
        {
            if (_focusSet != null)
                return _allNodes.Where(n => _focusSet.Contains(n)).ToList();
            return _allNodes.Skip(_currentPage * _visibleNodes).Take(_visibleNodes).ToList();
        }

        // ── History / navigation ───────────────────────────────────

        private void ResetHistoryToShowAll()
        {
            _history.Clear();
            _currentState = new FocusState();
            _history.Add(_currentState.Clone());
            _historyIndex = 0;
            UpdateNavButtons();
        }

        private void CaptureViewInto(FocusState state)
        {
            if (state == null) return;
            state.Page = _currentPage;
            state.HasView = true;
            state.ViewPosition = viewTransform.position;
            state.ViewScale = viewTransform.scale;
        }

        private void RestoreView(FocusState state)
        {
            if (state == null || !state.HasView) return;
            UpdateViewTransform(state.ViewPosition, state.ViewScale);
        }

        private void SaveFullGraphPositions()
        {
            _fullGraphPositions.Clear();
            foreach (var node in _allNodes)
                _fullGraphPositions[node] = node.GetPosition();
        }

        private void RestoreFullGraphPositions()
        {
            foreach (var kv in _fullGraphPositions)
            {
                if (kv.Key != null)
                    kv.Key.SetPosition(kv.Value);
            }
        }

        private void NavigateTo(FocusState state, bool recordHistory)
        {
            if (state == null)
                state = new FocusState();

            GameObject leavingCenter = _currentState?.Center;
            bool leavingShowAll = _currentState == null || _currentState.IsShowAll;

            // Snapshot full-graph layout before first drill-in so Back can restore it.
            if (leavingShowAll && !state.IsShowAll && _fullGraphLaidOut)
                SaveFullGraphPositions();

            if (recordHistory)
            {
                if (_historyIndex >= 0 && _historyIndex < _history.Count)
                    CaptureViewInto(_history[_historyIndex]);

                // Re-focusing the same object must never stack another crumb — _currentState
                // can drift from the history top after a soft rebuild, so check both.
                var top = _historyIndex >= 0 && _historyIndex < _history.Count ? _history[_historyIndex] : null;
                if (top != null && top.SameFocus(state))
                {
                    ApplyState(top, restoreView: false, preferFrameCenter: null,
                        restoreFullPositions: false, forceLayout: !state.IsShowAll);
                    SyncCurrentFromHistory();
                    return;
                }

                if (_currentState != null && _currentState.SameFocus(state))
                {
                    ApplyState(state, restoreView: false, preferFrameCenter: null,
                        restoreFullPositions: false, forceLayout: !state.IsShowAll);
                    SyncCurrentFromHistory();
                    return;
                }

                if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

                _history.Add(state.Clone());
                _historyIndex = _history.Count - 1;
            }
            else
            {
                // Follow mode: never overwrite the Show-All root at index 0.
                // First drill-in pushes; further Follow hops replace the current focus entry.
                if (_currentState != null && _currentState.SameFocus(state))
                {
                    ApplyState(state, restoreView: false, preferFrameCenter: null,
                        restoreFullPositions: false, forceLayout: !state.IsShowAll);
                    SyncCurrentFromHistory();
                    return;
                }

                if (_historyIndex > 0 && _historyIndex < _history.Count)
                {
                    _history[_historyIndex] = state.Clone();
                }
                else if (!state.IsShowAll)
                {
                    if (_historyIndex >= 0 && _historyIndex < _history.Count)
                        CaptureViewInto(_history[_historyIndex]);
                    if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
                        _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                    _history.Add(state.Clone());
                    _historyIndex = _history.Count - 1;
                }
            }

            ApplyState(
                _historyIndex >= 0 && _historyIndex < _history.Count ? _history[_historyIndex] : state,
                restoreView: false,
                preferFrameCenter: state.IsShowAll ? leavingCenter : null,
                restoreFullPositions: state.IsShowAll && _fullGraphPositions.Count > 0,
                forceLayout: !state.IsShowAll);
            UpdateNavButtons();
        }

        private void SyncCurrentFromHistory()
        {
            if (_historyIndex >= 0 && _historyIndex < _history.Count)
                _currentState = _history[_historyIndex].Clone();
        }

        private void ApplyState(FocusState state, bool restoreView, GameObject preferFrameCenter,
            bool restoreFullPositions, bool forceLayout)
        {
            _currentState = state?.Clone() ?? new FocusState();

            if (_radiusField != null)
                _radiusField.SetValueWithoutNotify(Mathf.Clamp(_currentState.Radius, 1, MaxRadius).ToString());
            if (_directionField != null && _directionField.choices != null && _directionField.choices.Count >= 3)
                _directionField.SetValueWithoutNotify(
                    _directionField.choices[Mathf.Clamp((int)_currentState.Direction, 0, 2)]);

            _focusCenter?.RemoveFromClassList("focus-center");
            _focusCenter = null;
            _focusSet = null;

            if (_currentState.IsShowAll)
            {
                _currentPage = Mathf.Max(0, _currentState.Page);
                ApplyShowAll(restoreView, preferFrameCenter, restoreFullPositions, forceLayout);
                return;
            }

            ResolveCenter(_currentState);
            var go = _currentState.Center;
            var center = ResolveCenterNode(_currentState);
            if (center == null)
            {
                // History navigation must never trigger FocusFallback (RefreshGraph) —
                // that wipes the current graph and resets the stack mid-Back.
                if (!_suppressFocusFallback && go != null && FocusFallback != null && FocusFallback(go))
                    return;
                var missing = _currentState.CenterObject;
                ShowMessage(missing != null
                    ? XRayLocalization.Format("graph_focus_missing", missing.name)
                    : null);
                ApplyShowAll(restoreView, null, restoreFullPositions, forceLayout);
                return;
            }

            _focusSet = BuildEgoNetwork(center, _currentState.Radius, _currentState.Direction);
            _focusCenter = center;
            _focusCenter.AddToClassList("focus-center");
            RefreshFocusLabel();

            ApplyActiveLayout();
            if (restoreView && _currentState.HasView)
                schedule.Execute(() => RestoreView(_currentState)).ExecuteLater(16);
            else
                ScheduleFrameAll();

            UpdateNavButtons();
        }

        private void ApplyShowAll(bool restoreView, GameObject preferFrameCenter,
            bool restoreFullPositions, bool forceLayout)
        {
            RefreshFocusLabel();

            if (preferFrameCenter != null)
                EnsurePageContains(preferFrameCenter);
            else if (_currentState != null)
                _currentPage = Mathf.Clamp(_currentState.Page, 0,
                    Mathf.Max(0, (_allNodes.Count - 1) / Mathf.Max(1, _visibleNodes)));

            if (restoreFullPositions && _fullGraphPositions.Count > 0)
            {
                RestoreFullGraphPositions();
                UpdateView();
            }
            else if (forceLayout || !_fullGraphLaidOut)
            {
                ApplyActiveLayout();
                _fullGraphLaidOut = true;
                SaveFullGraphPositions();
            }
            else
            {
                UpdateView();
            }

            if (restoreView && _currentState.HasView)
            {
                schedule.Execute(() => RestoreView(_currentState)).ExecuteLater(16);
            }
            else if (preferFrameCenter != null && FindNode(preferFrameCenter) is Node n)
            {
                ClearSelection();
                AddToSelection(n);
                schedule.Execute(() => FrameSelection()).ExecuteLater(50);
            }
            else
            {
                ScheduleFrameAll();
            }

            UpdateNavButtons();
        }

        private void EnsurePageContains(GameObject go)
        {
            var node = FindNode(go);
            if (node == null) return;
            int idx = _allNodes.IndexOf(node);
            if (idx < 0) return;
            _currentPage = idx / Mathf.Max(1, _visibleNodes);
            if (_currentState != null && _currentState.IsShowAll)
                _currentState.Page = _currentPage;
        }

        private HashSet<Node> BuildEgoNetwork(Node center, int radius, FocusDirection direction)
        {
            var visited = new HashSet<Node> { center };
            var frontier = new List<Node> { center };
            int hops = Mathf.Clamp(radius, 1, MaxRadius);
            for (int r = 0; r < hops; r++)
            {
                var next = new List<Node>();
                foreach (var n in frontier)
                    foreach (var m in Neighbors(n, direction))
                        if (visited.Add(m)) next.Add(m);
                frontier = next;
            }
            return visited;
        }

        private void RefreshFocusLabel() => RebuildBreadcrumbs();

        private void UpdateNavButtons()
        {
            if (_backBtn != null) _backBtn.SetEnabled(CanGoBack);
            if (_forwardBtn != null) _forwardBtn.SetEnabled(CanGoForward);
            if (_prevPageBtn != null) _prevPageBtn.SetEnabled(!IsFocused && _currentPage > 0);
            if (_nextPageBtn != null)
                _nextPageBtn.SetEnabled(!IsFocused && (_currentPage + 1) * _visibleNodes < _allNodes.Count);
            if (_showAllBtn != null) _showAllBtn.SetEnabled(IsFocused || CanGoBack);
        }

        public void GoBack()
        {
            if (!CanGoBack) return;
            if (_historyIndex >= 0 && _historyIndex < _history.Count)
                CaptureViewInto(_history[_historyIndex]);

            GameObject leaving = _currentState?.Center;
            bool leavingFocus = IsFocused;
            _historyIndex--;
            var target = _history[_historyIndex];
            _suppressFocusFallback = true;
            try
            {
                ApplyState(
                    target,
                    restoreView: target.HasView,
                    preferFrameCenter: target.IsShowAll && leavingFocus ? leaving : null,
                    restoreFullPositions: target.IsShowAll && _fullGraphPositions.Count > 0,
                    forceLayout: !target.IsShowAll);
            }
            finally { _suppressFocusFallback = false; }
            SyncCurrentFromHistory();
            UpdateNavButtons();
        }

        public void GoForward()
        {
            if (!CanGoForward) return;
            if (_historyIndex >= 0 && _historyIndex < _history.Count)
                CaptureViewInto(_history[_historyIndex]);

            bool leavingShowAll = _currentState == null || _currentState.IsShowAll;
            _historyIndex++;
            var target = _history[_historyIndex];
            if (leavingShowAll && !target.IsShowAll && _fullGraphLaidOut)
                SaveFullGraphPositions();

            _suppressFocusFallback = true;
            try
            {
                ApplyState(
                    target,
                    restoreView: target.HasView,
                    preferFrameCenter: null,
                    restoreFullPositions: target.IsShowAll && _fullGraphPositions.Count > 0,
                    forceLayout: !target.IsShowAll);
            }
            finally { _suppressFocusFallback = false; }
            SyncCurrentFromHistory();
            UpdateNavButtons();
        }

        /// <summary>Escape / Backspace: step back one level.</summary>
        public void NavigateUp()
        {
            if (CanGoBack) GoBack();
            else if (IsFocused) ShowAll();
        }

        public bool FocusOn(GameObject go, int radius = -1, FocusDirection? direction = null, bool recordHistory = true)
        {
            if (go == null) return false;

            var center = FindNode(go) as XRayNode;
            if (center == null)
            {
                if (FocusFallback != null && FocusFallback(go))
                    return true;
                ShowMessage(XRayLocalization.Format("graph_focus_missing", go.name));
                return false;
            }

            int r = radius > 0 ? Mathf.Clamp(radius, 1, MaxRadius) : Mathf.Clamp(FocusRadius, 1, MaxRadius);
            var dir = direction ?? CurrentDirection;
            NavigateTo(FocusState.ForCenter(go, r, dir), recordHistory);
            return _focusSet != null;
        }

        /// <summary>Drill into a referenced asset node (ScriptableObject, material, …).</summary>
        public bool FocusOnAsset(UnityEngine.Object asset, int radius = -1, FocusDirection? direction = null,
            bool recordHistory = true)
        {
            if (asset == null) return false;

            if (FindAssetNode(asset) == null)
            {
                ShowMessage(XRayLocalization.Format("graph_focus_missing", asset.name));
                return false;
            }

            int r = radius > 0 ? Mathf.Clamp(radius, 1, MaxRadius) : Mathf.Clamp(FocusRadius, 1, MaxRadius);
            var dir = direction ?? CurrentDirection;
            NavigateTo(FocusState.ForAsset(asset, r, dir), recordHistory);
            return _focusSet != null;
        }

        /// <summary>Focus whatever a node represents — GameObject or asset.</summary>
        public bool FocusOnNode(XRayNode node)
        {
            if (node == null) return false;
            if (node.GameObject != null) return FocusOn(node.GameObject);
            if (node.Asset != null) return FocusOnAsset(node.Asset);
            return false;
        }

        public void ShowAll()
        {
            NavigateTo(new FocusState
            {
                Center = null,
                Radius = FocusRadius,
                Direction = CurrentDirection,
                Page = _currentPage
            }, recordHistory: true);
        }

        /// <summary>Legacy alias — prefer <see cref="ShowAll"/> / <see cref="NavigateUp"/>.</summary>
        public void ClearFocus() => ShowAll();

        /// <summary>True when the current focus has a re-focusable center (GameObject or asset).</summary>
        private bool HasFocusCenter =>
            _currentState != null && (_currentState.Center != null || _currentState.CenterAsset != null);

        /// <summary>Re-apply the current focus with a different radius/direction.</summary>
        private bool RefocusCurrent(int radius, FocusDirection direction)
        {
            if (_currentState == null) return false;
            if (_currentState.IsAssetFocus)
                return FocusOnAsset(_currentState.CenterAsset, radius, direction);
            if (_currentState.Center != null)
                return FocusOn(_currentState.Center, radius, direction);
            return false;
        }

        public void ExpandRadius()
        {
            if (!IsFocused || !HasFocusCenter) return;
            if (_currentState.Radius >= MaxRadius) return;
            RefocusCurrent(_currentState.Radius + 1, _currentState.Direction);
        }

        public void ShrinkRadius()
        {
            if (!IsFocused || !HasFocusCenter)
            {
                NavigateUp();
                return;
            }
            if (_currentState.Radius <= 1)
            {
                // Collapse out of ego-network = Back (previous view), not dump-all.
                NavigateUp();
                return;
            }
            RefocusCurrent(_currentState.Radius - 1, _currentState.Direction);
        }

        public void FocusSelection()
        {
            var graphNode = selection.OfType<XRayNode>()
                .FirstOrDefault(n => n.GameObject != null || n.Asset != null);
            if (graphNode != null)
            {
                FocusOnNode(graphNode);
                return;
            }

            var go = Selection.activeGameObject;
            if (go != null)
            {
                FocusOn(go);
                return;
            }

            // Project-window selection: a ScriptableObject/asset can be a graph node too.
            var asset = Selection.activeObject;
            if (asset != null && FindAssetNode(asset) != null)
            {
                FocusOnAsset(asset);
                return;
            }

            ShowMessage(XRayLocalization.GetText("graph_select_first"));
        }

        private void OnRadiusChanged(ChangeEvent<string> evt)
        {
            if (!int.TryParse(evt.newValue, out int r)) return;
            r = Mathf.Clamp(r, 1, MaxRadius);
            if (!IsFocused || !HasFocusCenter)
            {
                if (_currentState != null) _currentState.Radius = r;
                return;
            }
            RefocusCurrent(r, _currentState.Direction);
        }

        private void OnDirectionChanged(ChangeEvent<string> evt)
        {
            var dir = FocusDirection.Both;
            if (_directionField.choices != null)
            {
                int idx = _directionField.choices.IndexOf(evt.newValue);
                if (idx >= 0) dir = (FocusDirection)idx;
            }

            if (!IsFocused || !HasFocusCenter)
            {
                if (_currentState != null) _currentState.Direction = dir;
                return;
            }
            RefocusCurrent(_currentState.Radius, dir);
        }

        // ── Layouts ───────────────────────────────────────────────

        private void LayoutDisplayed(bool forcePositions)
        {
            var nodes = DisplayedNodes();
            UpdateView();
            if (nodes.Count == 0) return;
            if (!forcePositions && _fullGraphLaidOut && !IsFocused) return;

            BuildDependencyTree(nodes);

            var columns = nodes
                .GroupBy(n => _nodeDepths[n])
                .OrderBy(g => g.Key)
                .Select(g => g.OrderBy(n => n.title, StringComparer.OrdinalIgnoreCase).ToList())
                .ToList();

            var order = new Dictionary<Node, float>();
            foreach (var col in columns)
                for (int i = 0; i < col.Count; i++)
                    order[col[i]] = i;

            float Barycenter(Node n)
            {
                float sum = 0f;
                int count = 0;
                foreach (var p in _parents[n])
                    if (order.TryGetValue(p, out var v)) { sum += v; count++; }
                foreach (var c in _children[n])
                    if (order.TryGetValue(c, out var v)) { sum += v; count++; }
                return count > 0 ? sum / count : order[n];
            }

            for (int sweep = 0; sweep < 2; sweep++)
            {
                foreach (var col in columns)
                {
                    var keyed = col.Select(n => (node: n, key: Barycenter(n))).ToList();
                    keyed.Sort((a, b) => a.key.CompareTo(b.key));
                    for (int i = 0; i < keyed.Count; i++)
                    {
                        col[i] = keyed[i].node;
                        order[col[i]] = i;
                    }
                }
            }

            int maxRows = columns.Max(c => c.Count);
            float totalHeight = maxRows * RowSpacing;
            for (int c = 0; c < columns.Count; c++)
            {
                var col = columns[c];
                float x = Padding + c * ColumnSpacing;
                float yStart = Padding + (totalHeight - col.Count * RowSpacing) * 0.5f;
                for (int i = 0; i < col.Count; i++)
                    col[i].SetPosition(new Rect(x, yStart + i * RowSpacing, 0, 0));
            }
        }

        private void BuildDependencyTree(List<Node> nodes)
        {
            _nodeDepths.Clear();
            _children.Clear();
            _parents.Clear();

            var set = new HashSet<Node>(nodes);
            foreach (var n in nodes)
            {
                _children[n] = new List<Node>();
                _parents[n] = new List<Node>();
            }

            foreach (var edge in _allEdges)
            {
                var (s, t) = ResolveEdge(edge);
                if (s == null || t == null || s == t) continue;
                if (!set.Contains(s) || !set.Contains(t)) continue;
                _children[s].Add(t);
                _parents[t].Add(s);
            }

            var roots = nodes.Where(n => _parents[n].Count == 0).ToList();
            if (roots.Count == 0 && nodes.Count > 0)
                roots.Add(nodes[0]);

            var queue = new Queue<Node>();
            foreach (var root in roots)
            {
                _nodeDepths[root] = 0;
                queue.Enqueue(root);
            }

            int guard = nodes.Count * nodes.Count + 16;
            while (queue.Count > 0 && guard-- > 0)
            {
                var current = queue.Dequeue();
                int nextDepth = _nodeDepths[current] + 1;
                if (nextDepth >= nodes.Count) continue;
                foreach (var child in _children[current])
                {
                    if (!_nodeDepths.TryGetValue(child, out var old) || old < nextDepth)
                    {
                        _nodeDepths[child] = nextDepth;
                        queue.Enqueue(child);
                    }
                }
            }

            foreach (var node in nodes)
                if (!_nodeDepths.ContainsKey(node))
                    _nodeDepths[node] = 0;
        }

        public void ApplyHierarchicalLayout()
        {
            LayoutDisplayed(forcePositions: true);
            AfterLayoutCommit();
        }

        public void ApplyForceLayout()
        {
            ForceLayoutCore();
            AfterLayoutCommit();
        }

        private void ForceLayoutCore()
        {
            // Hierarchical force: keep Tree columns (readable DAG), spread Y with guaranteed gaps.
            // Free 2D FR collapses hubs — never use it here.
            var nodes = DisplayedNodes();
            UpdateView();
            if (nodes.Count == 0) return;

            if (nodes.Count == 1)
            {
                nodes[0].SetPosition(new Rect(Padding, Padding, 0, 0));
                return;
            }

            BuildDependencyTree(nodes);

            const float minSep = 78f;
            var columns = nodes
                .GroupBy(n => _nodeDepths.TryGetValue(n, out var d) ? d : 0)
                .OrderBy(g => g.Key)
                .ToList();

            // Barycenter order within column (related nodes sit near each other), then hard-pack Y.
            var order = new Dictionary<Node, float>(nodes.Count);
            foreach (var col in columns)
            {
                var list = col.OrderBy(n => n.title, StringComparer.OrdinalIgnoreCase).ToList();
                for (int i = 0; i < list.Count; i++)
                    order[list[i]] = i;
            }

            float Bary(Node n)
            {
                float sum = 0f;
                int count = 0;
                if (_parents.TryGetValue(n, out var ps))
                    foreach (var p in ps)
                        if (order.TryGetValue(p, out var v)) { sum += v; count++; }
                if (_children.TryGetValue(n, out var cs))
                    foreach (var c in cs)
                        if (order.TryGetValue(c, out var v)) { sum += v; count++; }
                return count > 0 ? sum / count : order[n];
            }

            foreach (var col in columns)
            {
                var list = col.OrderBy(Bary).ThenBy(n => n.title, StringComparer.OrdinalIgnoreCase).ToList();
                float x = Padding + col.Key * ColumnSpacing;
                float y = Padding;
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].SetPosition(new Rect(x, y, 0, 0));
                    y += minSep;
                }
            }
        }

        /// <summary>Rings around the focus center (also available from the Layout menu).</summary>
        public void ApplyRadialLayout()
        {
            RadialLayoutCore();
            AfterLayoutCommit();
        }

        /// <summary>Clean context menu — no Cut/Copy/Delete/Disconnect from GraphView.</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.ClearItems();

            if (evt.target is XRayNode xNode && xNode.GameObject != null)
            {
                var go = xNode.GameObject;
                evt.menu.AppendAction(XRayLocalization.GetText("graph_focus_this") + "  (Enter)",
                    _ => FocusOn(go));
                evt.menu.AppendAction(XRayLocalization.GetText("graph_expand_radius"),
                    _ =>
                    {
                        if (IsFocused) ExpandRadius();
                        else FocusOn(go, 2);
                    });
                evt.menu.AppendAction(XRayLocalization.GetText("graph_shrink_radius"),
                    _ => ShrinkRadius());
                evt.menu.AppendAction(XRayLocalization.GetText("graph_nav_back") + "  (Backspace)",
                    _ => GoBack(),
                    _ => CanGoBack ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction(XRayLocalization.GetText("graph_select_hierarchy"),
                    _ =>
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    });
                evt.menu.AppendAction(XRayLocalization.GetText("graph_view_refs"),
                    _ => XRayReferencesWindow.ShowFor(go));
                evt.menu.AppendAction(XRayLocalization.GetText("graph_find_missing"),
                    _ => FindMissingOn(go));
                return;
            }

            if (evt.target is XRayNode assetNode && assetNode.Asset != null)
            {
                var asset = assetNode.Asset;
                evt.menu.AppendAction(XRayLocalization.GetText("graph_focus_this") + "  (Enter)",
                    _ => FocusOnAsset(asset));
                evt.menu.AppendAction(XRayLocalization.GetText("graph_expand_radius"),
                    _ =>
                    {
                        if (IsFocused) ExpandRadius();
                        else FocusOnAsset(asset, 2);
                    });
                evt.menu.AppendAction(XRayLocalization.GetText("graph_shrink_radius"), _ => ShrinkRadius());
                evt.menu.AppendAction(XRayLocalization.GetText("graph_nav_back") + "  (Backspace)",
                    _ => GoBack(),
                    _ => CanGoBack ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();
                evt.menu.AppendAction(XRayLocalization.GetText("graph_select_project"),
                    _ =>
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    });
                return;
            }

            evt.menu.AppendAction(XRayLocalization.GetText("graph_focus_selected") + "  (Enter)",
                _ => FocusSelection());
            evt.menu.AppendAction(XRayLocalization.GetText("graph_nav_back") + "  (Backspace)",
                _ => GoBack(),
                _ => CanGoBack ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction(XRayLocalization.GetText("graph_show_all"),
                _ => ShowAll(),
                _ => IsFocused ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction(XRayLocalization.GetText("graph_layout_tree"), _ => ApplyHierarchicalLayout());
            evt.menu.AppendAction(XRayLocalization.GetText("graph_layout_force"), _ => ApplyForceLayout());
            evt.menu.AppendAction(XRayLocalization.GetText("graph_layout_radial"), _ => ApplyRadialLayout());
            evt.menu.AppendAction(XRayLocalization.GetText("graph_fit") + "  (A)", _ => FrameGraph());
            evt.menu.AppendAction(XRayLocalization.GetText("graph_zoom_reset") + "  (0)", _ => ResetZoom());
            evt.menu.AppendSeparator();
            if (RequestAnalyze != null)
                evt.menu.AppendAction("Analyze Selected", _ => RequestAnalyze());
            if (RequestRefresh != null)
                evt.menu.AppendAction(XRayLocalization.GetText("refresh"), _ => RequestRefresh());
            if (RequestExport != null)
                evt.menu.AppendAction(XRayLocalization.GetText("export"), _ => RequestExport());
            if (RequestFixMissing != null)
                evt.menu.AppendAction(XRayLocalization.GetText("fix_missing"), _ => RequestFixMissing());
        }

        private static void FindMissingOn(GameObject go)
        {
            var links = SceneXRay.Editor.Core.SceneScanner.ScanGameObject(go);
            var missing = links.Where(l => l.IsMissing).ToList();

            XRayReportWindow.Show(
                XRayLocalization.GetText("fix_missing"),
                XRayLocalization.Format("report_missing_summary", go.name, missing.Count),
                XRayLocalization.GetText("missing_refs"),
                missing.Select(m => XRayReportWindow.MakeRow(
                    go, m.SourceComponentName, m.SourcePropertyName,
                    SceneXRay.Editor.Core.SceneXRaySettings.instance.MissingColor)),
                XRayLocalization.GetText("report_no_missing"));
        }

        /// <summary>Public status line — lets the owning window post messages into the graph's status bar.</summary>
        public void ShowStatus(string message) => ShowMessage(message);

        private void AfterLayoutCommit()
        {
            if (!IsFocused)
            {
                _fullGraphLaidOut = true;
                SaveFullGraphPositions();
            }
            CommitPositions(); // an explicit re-layout is undoable
            ScheduleFrameAll();
        }

        private void ScheduleFrameAll()
        {
            // One frame later: node sizes are only resolved after the next layout pass.
            schedule.Execute(() => FrameGraph()).ExecuteLater(50);
        }

        // ── View maintenance ───────────────────────────────────────

        private void UpdateView()
        {
            _emptyLabel.style.display = _allNodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ClearHighlight();

            if (_allNodes.Count == 0)
            {
                foreach (var elem in _mountedElements.ToList())
                {
                    if (elem is MiniMap) continue;
                    RemoveElement(elem);
                }
                _mountedElements.Clear();
                _pageLabel.text = "1/1";
                UpdateCounts();
                UpdateNavButtons();
                return;
            }

            var desiredNodes = new HashSet<Node>(DisplayedNodes());
            var desiredEdges = new HashSet<Edge>();
            foreach (var edge in _allEdges)
            {
                var (sourceNode, targetNode) = ResolveEdge(edge);
                if (sourceNode != null && targetNode != null &&
                    desiredNodes.Contains(sourceNode) && desiredNodes.Contains(targetNode))
                    desiredEdges.Add(edge);
            }

            foreach (var elem in _mountedElements.ToList())
            {
                if (elem is MiniMap) continue;
                bool keep = (elem is Node n && desiredNodes.Contains(n))
                            || (elem is Edge e && desiredEdges.Contains(e));
                if (keep) continue;
                RemoveElement(elem);
                _mountedElements.Remove(elem);
            }

            foreach (var node in desiredNodes)
            {
                if (_mountedElements.Contains(node)) continue;
                AddElement(node);
                _mountedElements.Add(node);
            }

            foreach (var edge in desiredEdges)
            {
                if (_mountedElements.Contains(edge)) continue;
                AddElement(edge);
                _mountedElements.Add(edge);
            }

            _pageLabel.text = _focusSet != null
                ? XRayLocalization.GetText("graph_page_focus")
                : XRayLocalization.Format("graph_page",
                    _currentPage + 1,
                    Mathf.Max(1, (_allNodes.Count + _visibleNodes - 1) / _visibleNodes));

            UpdateCounts();
            UpdateHighlight();
            UpdateNavButtons();
        }

        private void PrevPage()
        {
            if (_focusSet != null) return;
            if (_currentPage > 0)
            {
                _currentPage--;
                if (_currentState != null && _currentState.IsShowAll)
                    _currentState.Page = _currentPage;
                UpdateView();
                ScheduleFrameAll();
            }
        }

        private void NextPage()
        {
            if (_focusSet != null) return;
            if ((_currentPage + 1) * _visibleNodes < _allNodes.Count)
            {
                _currentPage++;
                if (_currentState != null && _currentState.IsShowAll)
                    _currentState.Page = _currentPage;
                UpdateView();
                ScheduleFrameAll();
            }
        }

        public void ApplySmartLayout() => ApplyHierarchicalLayout();

        public void ClearGraph()
        {
            foreach (var elem in _mountedElements.ToList())
            {
                if (elem is MiniMap) continue;
                RemoveElement(elem);
            }
            _mountedElements.Clear();

            var leftovers = graphElements.Where(elem => elem is Node or Edge).ToList();
            foreach (var elem in leftovers)
                RemoveElement(elem);

            _allNodes.Clear();
            _allEdges.Clear();
            _nodeByGo.Clear();
            _nodeDepths.Clear();
            _children.Clear();
            _parents.Clear();
            _outAdj.Clear();
            _inAdj.Clear();
            _hoverWired.Clear();
            _hoverNode = null;
            ClearHighlight();
            _focusSet = null;
            _focusCenter = null;
            ShowMessage(null);
            _currentPage = 0;
            _fullGraphLaidOut = false;
            _fullGraphPositions.Clear();
            _pageLabel.text = "1/1";
            UpdateCounts();
            _emptyLabel.style.display = DisplayStyle.Flex;
            // Do not reset navigation history here — SetContent(resetNavigation) owns that.
            // Live Mode soft rebuilds must keep Back/Forward working.
        }

        /// <summary>Hard-reset navigation (user pressed Refresh).</summary>
        public void ResetNavigation() => ResetHistoryToShowAll();

        public List<Node> GetAllNodes() => _allNodes;
        public List<Edge> GetAllEdges() => _allEdges;
    }
}
