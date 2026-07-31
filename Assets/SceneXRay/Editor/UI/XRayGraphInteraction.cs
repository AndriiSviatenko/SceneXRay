using SceneXRay.Editor.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using SceneXRay.Editor.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.UI
{
    /// <summary>How displayed nodes are placed. Auto = Tree for the full graph, Radial while focused.</summary>
    public enum XRayLayoutMode
    {
        Auto = 0,
        Tree = 1,
        Force = 2,
        Radial = 3
    }

    /// <summary>
    /// Shader-Graph-style interaction layer: smooth camera, zoom controls, neighborhood
    /// highlighting, zoom LOD, clickable breadcrumbs, status bar and keyboard navigation.
    /// Graph data/history lives in the main <see cref="XRayVirtualGraphView"/> file.
    /// </summary>
    public partial class XRayVirtualGraphView
    {
        // Must match the SetupZoom() range in the constructor.
        private const float ZoomMin = 0.05f;
        private const float ZoomMax = 2.5f;
        private const float ZoomStep = 1.25f;
        private const float FrameMaxScale = 1.15f;

        private const string PrefLayoutMode = "SceneXRay_LayoutMode";
        private const int HighlightNodeBudget = 600;

        // Highlight / dim classes.
        private const string ClassHl = "xray-hl";
        private const string ClassNear = "xray-near";
        private const string ClassDim = "xray-dim";
        private const string ClassEdgeOut = "xray-edge-out";
        private const string ClassEdgeIn = "xray-edge-in";

        private VisualElement _statusBar;
        private Label _statusCounts;
        private Label _statusMessage;
        private Label _zoomLabel;
        private Button _zoomInBtn, _zoomOutBtn, _fitBtn;
        private ToolbarBreadcrumbs _breadcrumbs;
        private int _crumbCount;
        private ToolbarMenu _layoutMenu;

        private readonly Dictionary<Node, List<Node>> _outAdj = new();
        private readonly Dictionary<Node, List<Node>> _inAdj = new();

        private Node _hoverNode;
        private readonly List<VisualElement> _highlighted = new();

        private float _lastZoom = -1f;
        private IVisualElementScheduledItem _cameraAnim;
        private IVisualElementScheduledItem _messageTimer;

        private XRayLayoutMode _layoutMode = XRayLayoutMode.Auto;

        /// <summary>Ctrl+F — wired by <see cref="XRayWindow"/> to focus its search field.</summary>
        public Action RequestFocusSearch;

        public XRayLayoutMode LayoutMode => _layoutMode;

        /// <summary>Current camera zoom (1 = 100%).</summary>
        public float CurrentZoom => viewTransform.scale.x;

        // ── Setup ─────────────────────────────────────────────────

        private void SetupInteraction()
        {
            _layoutMode = (XRayLayoutMode)EditorPrefs.GetInt(PrefLayoutMode, (int)XRayLayoutMode.Auto);

            CreateStatusBar();

            // Zoom is driven by GraphView's own manipulators, so poll instead of guessing
            // which event changed it. Cheap: one float compare per tick.
            schedule.Execute(PollZoom).Every(100);

            // Any manual camera input cancels a running frame animation.
            RegisterCallback<WheelEvent>(_ => StopCameraAnimation(), TrickleDown.TrickleDown);
            RegisterCallback<MouseDownEvent>(_ => StopCameraAnimation(), TrickleDown.TrickleDown);

            // Dragging nodes is the one mutation worth undoing — record it when GraphView
            // reports the move (this is also what marks the arrangement as "custom").
            graphViewChanged = OnGraphViewChanged;

            // Unity routes Ctrl+Z/Ctrl+Y through command events; intercept them so the graph
            // undoes node moves instead of the editor undoing scene changes behind our back.
            RegisterCallback<ValidateCommandEvent>(e =>
            {
                if (e.commandName is "Undo" or "Redo")
                    e.StopPropagation();
            });
            RegisterCallback<ExecuteCommandEvent>(e =>
            {
                if (e.commandName == "Undo") { UndoLayoutChange(); e.StopPropagation(); }
                else if (e.commandName == "Redo") { RedoLayoutChange(); e.StopPropagation(); }
            });
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.movedElements != null && change.movedElements.Count > 0)
                CommitPositions();
            return change;
        }

        // ── Status bar ────────────────────────────────────────────

        private void CreateStatusBar()
        {
            // Keeps the legacy class name: it is the anchor for smoke tests and USS.
            _statusBar = new VisualElement { name = "xray-status-bar" };
            _statusBar.AddToClassList("xray-hotkey-legend");
            _statusBar.AddToClassList("xray-status-bar");

            _prevPageBtn = MakeIconButton("◀", PrevPage);
            _pageLabel = new Label("1/1");
            _pageLabel.AddToClassList("xray-status-page");
            _nextPageBtn = MakeIconButton("▶", NextPage);
            _statusBar.Add(_prevPageBtn);
            _statusBar.Add(_pageLabel);
            _statusBar.Add(_nextPageBtn);

            _statusCounts = new Label();
            _statusCounts.AddToClassList("xray-status-counts");
            _statusBar.Add(_statusCounts);

            _statusBar.Add(MakeSeparator());

            _legendLabel = new Label();
            _legendLabel.AddToClassList("xray-hotkey-legend__text");
            _legendLabel.pickingMode = PickingMode.Ignore;
            _statusBar.Add(_legendLabel);

            _statusMessage = new Label();
            _statusMessage.AddToClassList("xray-status-message");
            _statusMessage.style.display = DisplayStyle.None;
            _statusBar.Add(_statusMessage);

            _statusBar.Add(MakeSeparator());

            _zoomOutBtn = MakeIconButton("−", ZoomOut);
            _statusBar.Add(_zoomOutBtn);

            _zoomLabel = new Label("100%");
            _zoomLabel.AddToClassList("xray-status-zoom");
            _zoomLabel.RegisterCallback<ClickEvent>(_ => ResetZoom());
            _statusBar.Add(_zoomLabel);

            _zoomInBtn = MakeIconButton("+", ZoomIn);
            _statusBar.Add(_zoomInBtn);

            _fitBtn = MakeIconButton("⤢", () => FrameGraph());
            _fitBtn.AddToClassList("xray-status-fit");
            _statusBar.Add(_fitBtn);

            Add(_statusBar);
            LocalizeStatusBar();
        }

        private void LocalizeStatusBar()
        {
            if (_zoomInBtn == null) return;
            _zoomInBtn.tooltip = XRayLocalization.GetText("graph_zoom_in");
            _zoomOutBtn.tooltip = XRayLocalization.GetText("graph_zoom_out");
            _fitBtn.tooltip = XRayLocalization.GetText("graph_fit");
            _zoomLabel.tooltip = XRayLocalization.GetText("graph_zoom_reset");
        }

        private static Button MakeIconButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("xray-status-btn");
            return b;
        }

        private static VisualElement MakeSeparator()
        {
            var v = new VisualElement();
            v.AddToClassList("xray-status-sep");
            v.pickingMode = PickingMode.Ignore;
            return v;
        }

        /// <summary>Transient status line (replaces the hotkey hint for a few seconds).</summary>
        private void ShowMessage(string message)
        {
            if (_statusMessage == null) return;

            bool has = !string.IsNullOrEmpty(message);
            _statusMessage.text = message ?? string.Empty;
            _statusMessage.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            if (_legendLabel != null)
                _legendLabel.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;

            _messageTimer?.Pause();
            if (has)
            {
                var timer = schedule.Execute(() => ShowMessage(null));
                timer.ExecuteLater(5000);
                _messageTimer = timer;
            }
        }

        private void UpdateCounts()
        {
            if (_statusCounts == null) return;

            int shown = 0, links = 0;
            foreach (var el in _mountedElements)
            {
                if (el is Node) shown++;
                else if (el is Edge) links++;
            }
            _statusCounts.text = XRayLocalization.Format("graph_counts", shown, _allNodes.Count, links);
        }

        private void UpdateZoomLabel()
        {
            if (_zoomLabel != null)
                _zoomLabel.text = Mathf.RoundToInt(CurrentZoom * 100f) + "%";
        }

        // ── Camera ────────────────────────────────────────────────

        private void StopCameraAnimation() => _cameraAnim?.Pause();

        private void AnimateViewTo(Vector3 targetPos, Vector3 targetScale, float duration = 0.18f)
        {
            _cameraAnim?.Pause();

            // No panel (tests) or a collapsed window: jump, never animate into nothing.
            if (panel == null || contentRect.width < 60f || duration <= 0f)
            {
                UpdateViewTransform(targetPos, targetScale);
                UpdateZoomLabel();
                return;
            }

            var startPos = viewTransform.position;
            var startScale = viewTransform.scale;
            double startTime = EditorApplication.timeSinceStartup;

            _cameraAnim = schedule.Execute(() =>
            {
                float k = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - startTime) / duration));
                float eased = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
                UpdateViewTransform(
                    Vector3.Lerp(startPos, targetPos, eased),
                    Vector3.Lerp(startScale, targetScale, eased));
                UpdateZoomLabel();
                if (k >= 1f) _cameraAnim?.Pause();
            }).Every(12);
        }

        private static Rect Union(Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.xMin, b.xMin);
            float yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax);
            float yMax = Mathf.Max(a.yMax, b.yMax);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Rect NodeBounds(Node n)
        {
            var p = n.GetPosition();
            float w = !float.IsNaN(p.width) && p.width > 1f ? p.width : 190f;
            float h = !float.IsNaN(p.height) && p.height > 1f ? p.height : 54f;
            return new Rect(p.x, p.y, w, h);
        }

        private bool TryComputeFrame(IReadOnlyList<Node> nodes, out Vector3 pos, out Vector3 scale)
        {
            pos = viewTransform.position;
            scale = viewTransform.scale;

            var view = contentRect;
            if (nodes == null || nodes.Count == 0 || view.width < 80f || view.height < 80f)
                return false;

            Rect bounds = default;
            bool any = false;
            foreach (var n in nodes)
            {
                if (n == null || n.resolvedStyle.display == DisplayStyle.None) continue;
                var nb = NodeBounds(n);
                bounds = any ? Union(bounds, nb) : nb;
                any = true;
            }
            if (!any) return false;

            const float insetTop = 26f;    // in-graph toolbar
            const float insetBottom = 30f; // status bar
            const float margin = 44f;

            float availW = Mathf.Max(60f, view.width - margin * 2f);
            float availH = Mathf.Max(60f, view.height - insetTop - insetBottom - margin);

            float s = Mathf.Min(availW / Mathf.Max(1f, bounds.width), availH / Mathf.Max(1f, bounds.height));
            s = Mathf.Clamp(s, ZoomMin, FrameMaxScale);

            var viewCenter = new Vector2(
                view.width * 0.5f,
                insetTop + (view.height - insetTop - insetBottom) * 0.5f);

            pos = new Vector3(viewCenter.x - bounds.center.x * s, viewCenter.y - bounds.center.y * s, 0f);
            scale = new Vector3(s, s, 1f);
            return true;
        }

        /// <summary>Fit everything currently displayed into view (smooth).</summary>
        public void FrameGraph(bool animated = true)
        {
            var nodes = DisplayedNodes();
            if (!TryComputeFrame(nodes, out var p, out var s))
            {
                FrameAll();
                UpdateZoomLabel();
                return;
            }
            AnimateViewTo(p, s, animated ? 0.18f : 0f);
        }

        /// <summary>Center the camera on one node without changing zoom.</summary>
        public void CenterOn(Node node, bool animated = true)
        {
            if (node == null) return;
            var view = contentRect;
            if (view.width < 80f) return;

            float s = CurrentZoom;
            var b = NodeBounds(node);
            var target = new Vector3(
                view.width * 0.5f - b.center.x * s,
                view.height * 0.5f - b.center.y * s, 0f);
            AnimateViewTo(target, new Vector3(s, s, 1f), animated ? 0.16f : 0f);
        }

        /// <summary>Pan only if the node sits outside the comfortable viewport area.</summary>
        private void EnsureVisible(Node node)
        {
            if (node == null) return;
            var view = contentRect;
            if (view.width < 80f) return;

            float s = CurrentZoom;
            var b = NodeBounds(node);
            var origin = viewTransform.position;
            var screen = new Rect(b.x * s + origin.x, b.y * s + origin.y, b.width * s, b.height * s);

            const float pad = 60f;
            bool outside = screen.xMin < pad || screen.yMin < pad + 26f ||
                           screen.xMax > view.width - pad || screen.yMax > view.height - pad - 30f;
            if (outside) CenterOn(node);
        }

        // ── Zoom ──────────────────────────────────────────────────

        private void ZoomAtCenter(float newScale, float duration = 0.12f)
        {
            newScale = Mathf.Clamp(newScale, ZoomMin, ZoomMax);
            float old = CurrentZoom;
            if (old <= 0.0001f) old = 1f;

            var view = contentRect;
            var center = new Vector2(view.width * 0.5f, view.height * 0.5f);
            var current = new Vector2(viewTransform.position.x, viewTransform.position.y);
            var contentAtCenter = (center - current) / old;
            var target = center - contentAtCenter * newScale;

            AnimateViewTo(new Vector3(target.x, target.y, 0f), new Vector3(newScale, newScale, 1f), duration);
        }

        public void ZoomIn() => ZoomAtCenter(CurrentZoom * ZoomStep);
        public void ZoomOut() => ZoomAtCenter(CurrentZoom / ZoomStep);
        public void ResetZoom() => ZoomAtCenter(1f);

        private void PollZoom()
        {
            float s = CurrentZoom;
            if (Mathf.Abs(s - _lastZoom) < 0.005f) return;
            _lastZoom = s;

            // Zoom LOD: strip node detail when zoomed out so the graph stays readable.
            EnableInClassList("xray-zoom-far", s < 0.4f);
            EnableInClassList("xray-zoom-mid", s >= 0.4f && s < 0.75f);
            UpdateZoomLabel();
        }

        // ── Adjacency (shared by highlight, ego network and layouts) ──

        private void BuildAdjacency()
        {
            _outAdj.Clear();
            _inAdj.Clear();

            foreach (var edge in _allEdges)
            {
                var (s, t) = ResolveEdge(edge);
                if (s == null || t == null || s == t) continue;

                if (!_outAdj.TryGetValue(s, out var outs))
                    _outAdj[s] = outs = new List<Node>();
                outs.Add(t);

                if (!_inAdj.TryGetValue(t, out var ins))
                    _inAdj[t] = ins = new List<Node>();
                ins.Add(s);
            }
        }

        private IEnumerable<Node> Neighbors(Node n, FocusDirection direction)
        {
            if (direction is FocusDirection.Both or FocusDirection.Outgoing &&
                _outAdj.TryGetValue(n, out var outs))
                foreach (var m in outs) yield return m;

            if (direction is FocusDirection.Both or FocusDirection.Incoming &&
                _inAdj.TryGetValue(n, out var ins))
                foreach (var m in ins) yield return m;
        }

        // ── Neighborhood highlight ────────────────────────────────

        private readonly HashSet<Node> _hoverWired = new();

        private void RegisterHoverHighlight(Node node)
        {
            // SetContent can be called repeatedly with the same node instances (Live Mode).
            if (node == null || !_hoverWired.Add(node)) return;
            node.RegisterCallback<MouseEnterEvent>(_ => SetHoverNode(node));
            node.RegisterCallback<MouseLeaveEvent>(_ => { if (_hoverNode == node) SetHoverNode(null); });
        }

        private void SetHoverNode(Node node)
        {
            if (_hoverNode == node) return;
            _hoverNode = node;
            UpdateHighlight();
        }

        private void ClearHighlight()
        {
            foreach (var ve in _highlighted)
            {
                if (ve == null) continue;
                if (ve is XRayEdge xEdge)
                    xEdge.SetHighlight(XRayEdgeHighlight.None); // back to the Settings color
                ve.RemoveFromClassList(ClassHl);
                ve.RemoveFromClassList(ClassNear);
                ve.RemoveFromClassList(ClassDim);
                ve.RemoveFromClassList(ClassEdgeOut);
                ve.RemoveFromClassList(ClassEdgeIn);
            }
            _highlighted.Clear();
        }

        private void Mark(VisualElement ve, string cls)
        {
            ve.AddToClassList(cls);
            _highlighted.Add(ve);
        }

        private void UpdateHighlight()
        {
            ClearHighlight();
            if (_mountedElements.Count > HighlightNodeBudget) return;

            Node focusNode = _hoverNode;
            if (focusNode == null)
                focusNode = selection.OfType<Node>().FirstOrDefault();
            if (focusNode == null) return;

            var near = new HashSet<Node>();
            foreach (var m in Neighbors(focusNode, FocusDirection.Both))
                near.Add(m);

            foreach (var el in _mountedElements)
            {
                switch (el)
                {
                    case Node n when n == focusNode:
                        Mark(n, ClassHl);
                        break;
                    case Node n when near.Contains(n):
                        Mark(n, ClassNear);
                        break;
                    case Node n:
                        Mark(n, ClassDim);
                        break;
                    case Edge e:
                    {
                        var (s, t) = ResolveEdge(e);
                        if (s == focusNode)
                        {
                            Mark(e, ClassEdgeOut);
                            if (e is XRayEdge xe) xe.SetHighlight(XRayEdgeHighlight.Outgoing);
                        }
                        else if (t == focusNode)
                        {
                            Mark(e, ClassEdgeIn);
                            if (e is XRayEdge xe) xe.SetHighlight(XRayEdgeHighlight.Incoming);
                        }
                        else Mark(e, ClassDim);
                        break;
                    }
                }
            }
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            UpdateHighlight();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            UpdateHighlight();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            UpdateHighlight();
        }

        // ── Breadcrumbs ───────────────────────────────────────────

        private void RebuildBreadcrumbs()
        {
            if (_breadcrumbs == null) return;

            while (_crumbCount > 0)
            {
                _breadcrumbs.PopItem();
                _crumbCount--;
            }

            PushCrumb(XRayLocalization.GetText("graph_crumb_all"), 0);

            // Collapse repeated drill-ins on the same object — history can legitimately
            // contain them (Follow mode, re-focus after a rebuild) but they read as noise.
            var trail = new List<int>();
            UnityEngine.Object last = null;
            for (int i = 1; i <= _historyIndex && i < _history.Count; i++)
            {
                var s = _history[i];
                if (s == null || s.IsShowAll || s.CenterObject == null) continue;
                if (s.CenterObject == last) continue;
                last = s.CenterObject;
                trail.Add(i);
            }

            const int maxCrumbs = 4;
            int start = 0;
            if (trail.Count > maxCrumbs)
            {
                start = trail.Count - maxCrumbs;
                PushCrumb("…", trail[start - 1]);
            }

            for (int k = start; k < trail.Count; k++)
            {
                int idx = trail[k];
                var s = _history[idx];
                string label = s.CenterObject.name;
                if (idx == _historyIndex && _focusSet != null)
                    label += XRayLocalization.Format("graph_crumb_meta", s.Radius, _focusSet.Count);
                PushCrumb(label, idx);
            }
        }

        private void PushCrumb(string label, int historyIndex)
        {
            _breadcrumbs.PushItem(label, () => NavigateToHistoryIndex(historyIndex));
            _crumbCount++;
        }

        /// <summary>Jump straight to a history entry (breadcrumb click).</summary>
        public void NavigateToHistoryIndex(int index)
        {
            if (index < 0 || index >= _history.Count || index == _historyIndex) return;

            if (_historyIndex >= 0 && _historyIndex < _history.Count)
                CaptureViewInto(_history[_historyIndex]);

            bool leavingFocus = IsFocused;
            var leaving = _currentState?.Center;
            bool leavingShowAll = _currentState == null || _currentState.IsShowAll;

            var target = _history[index];
            if (leavingShowAll && !target.IsShowAll && _fullGraphLaidOut)
                SaveFullGraphPositions();

            _historyIndex = index;
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

        // ── Layout mode ───────────────────────────────────────────

        private void CreateLayoutMenu(Toolbar toolbar)
        {
            _layoutMenu = new ToolbarMenu();
            _layoutMenu.AddToClassList("xray-layout-menu");
            toolbar.Add(_layoutMenu);
        }

        private void AppendLayoutChoice(XRayLayoutMode mode, string key)
        {
            _layoutMenu.menu.AppendAction(
                XRayLocalization.GetText(key),
                _ => SetLayoutMode(mode),
                _ => _layoutMode == mode ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        public void SetLayoutMode(XRayLayoutMode mode)
        {
            _layoutMode = mode;
            EditorPrefs.SetInt(PrefLayoutMode, (int)mode);
            UpdateLayoutMenuText();
            RelayoutNow();
        }

        private void UpdateLayoutMenuText()
        {
            if (_layoutMenu == null) return;

            // Rebuilt on every language change — DropdownMenu labels are baked at append time.
            _layoutMenu.menu.ClearItems();
            AppendLayoutChoice(XRayLayoutMode.Auto, "graph_layout_auto");
            AppendLayoutChoice(XRayLayoutMode.Tree, "graph_layout_tree");
            AppendLayoutChoice(XRayLayoutMode.Force, "graph_layout_force");
            AppendLayoutChoice(XRayLayoutMode.Radial, "graph_layout_radial");
            _layoutMenu.menu.AppendSeparator();
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_relayout"), _ => RelayoutNow());
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_fit"), _ => FrameGraph());

            _layoutMenu.menu.AppendSeparator();
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_layout_save") + "  (Ctrl+S)",
                _ => SaveLayout());
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_layout_restore"),
                _ => RestoreLayout(),
                _ => HasSavedLayout ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_layout_clear"),
                _ => ClearSavedLayout(),
                _ => HasSavedLayout ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_layout_autorestore"),
                _ => AutoRestoreLayout = !AutoRestoreLayout,
                _ => AutoRestoreLayout ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            _layoutMenu.menu.AppendSeparator();
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_undo") + "  (Ctrl+Z)",
                _ => UndoLayoutChange(),
                _ => CanUndoLayout ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            _layoutMenu.menu.AppendAction(XRayLocalization.GetText("graph_redo") + "  (Ctrl+Y)",
                _ => RedoLayoutChange(),
                _ => CanRedoLayout ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            string key = _layoutMode switch
            {
                XRayLayoutMode.Tree => "graph_layout_tree",
                XRayLayoutMode.Force => "graph_layout_force",
                XRayLayoutMode.Radial => "graph_layout_radial",
                _ => "graph_layout_auto"
            };
            _layoutMenu.text = XRayLocalization.GetText("graph_layout") + ": " + XRayLocalization.GetText(key);
            _layoutMenu.tooltip = XRayLocalization.GetText("graph_layout_tt");
        }

        /// <summary>Re-run the active layout on what is on screen right now.</summary>
        public void RelayoutNow()
        {
            ApplyActiveLayout();
            AfterLayoutCommit();
        }

        /// <summary>Layout used by navigation — no framing side effects.</summary>
        private void ApplyActiveLayout()
        {
            var mode = _layoutMode;
            if (mode == XRayLayoutMode.Auto)
                mode = IsFocused ? XRayLayoutMode.Radial : XRayLayoutMode.Tree;

            switch (mode)
            {
                case XRayLayoutMode.Force:
                    ForceLayoutCore();
                    break;
                case XRayLayoutMode.Radial:
                    RadialLayoutCore();
                    break;
                default:
                    LayoutDisplayed(forcePositions: true);
                    break;
            }
        }

        /// <summary>Rings by hop distance around the focus center — reads like a mind map.</summary>
        private void RadialLayoutCore()
        {
            var nodes = DisplayedNodes();
            UpdateView();
            if (nodes.Count == 0) return;

            var set = new HashSet<Node>(nodes);
            Node center = _focusCenter != null && set.Contains(_focusCenter)
                ? _focusCenter
                : nodes.OrderByDescending(n => Neighbors(n, FocusDirection.Both).Count(set.Contains)).First();

            var ring = new Dictionary<Node, int> { [center] = 0 };
            var queue = new Queue<Node>();
            queue.Enqueue(center);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int next = ring[cur] + 1;
                foreach (var m in Neighbors(cur, FocusDirection.Both))
                {
                    if (!set.Contains(m) || ring.ContainsKey(m)) continue;
                    ring[m] = next;
                    queue.Enqueue(m);
                }
            }

            // Anything the BFS could not reach (filtered edges) goes to an outer ring.
            int maxRing = ring.Count > 0 ? ring.Values.Max() : 0;
            foreach (var n in nodes)
                if (!ring.ContainsKey(n))
                    ring[n] = maxRing + 1;

            var origin = new Vector2(Padding + 520f, Padding + 360f);
            center.SetPosition(new Rect(origin.x, origin.y, 0, 0));

            foreach (var group in ring.Where(kv => kv.Value > 0).GroupBy(kv => kv.Value).OrderBy(g => g.Key))
            {
                var members = group
                    .Select(kv => kv.Key)
                    .OrderBy(n => n.title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Grow the radius when a ring is crowded so cards never overlap.
                float radius = Mathf.Max(240f * group.Key, members.Count * 82f / (2f * Mathf.PI));
                float step = 2f * Mathf.PI / members.Count;
                float offset = group.Key % 2 == 0 ? 0f : step * 0.5f;

                for (int i = 0; i < members.Count; i++)
                {
                    float a = offset + i * step;
                    members[i].SetPosition(new Rect(
                        origin.x + Mathf.Cos(a) * radius * 1.25f, // wider than tall: cards are wide
                        origin.y + Mathf.Sin(a) * radius,
                        0, 0));
                }
            }
        }

        // ── Keyboard navigation between nodes ─────────────────────

        private static Vector2 CenterOf(Node n)
        {
            var b = NodeBounds(n);
            return b.center;
        }

        /// <summary>Arrow keys walk the graph spatially (like moving a cursor between cards).</summary>
        private bool MoveSelection(Vector2 dir)
        {
            var displayed = DisplayedNodes();
            if (displayed.Count == 0) return false;

            var current = selection.OfType<Node>().FirstOrDefault() ?? _focusCenter;
            if (current == null || !displayed.Contains(current))
            {
                SelectNode(displayed[0]);
                return true;
            }

            var from = CenterOf(current);
            Node best = null;
            float bestScore = float.MaxValue;

            foreach (var n in displayed)
            {
                if (n == current || n.resolvedStyle.display == DisplayStyle.None) continue;
                var d = CenterOf(n) - from;
                float along = Vector2.Dot(d, dir);
                if (along <= 1f) continue;
                float perp = Mathf.Abs(d.x * -dir.y + d.y * dir.x);
                if (perp > along * 2.5f + 40f) continue;

                float score = along + perp * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }

            if (best == null) return false;
            SelectNode(best);
            return true;
        }

        private void SelectNode(Node node)
        {
            ClearSelection();
            AddToSelection(node);
            EnsureVisible(node);
        }

        // ── Undo / redo of node arrangement ───────────────────────

        private const int UndoDepth = 32;
        private const string PrefAutoRestore = "SceneXRay_AutoRestoreLayout";

        private readonly List<Dictionary<Node, Rect>> _undoStack = new();
        private readonly List<Dictionary<Node, Rect>> _redoStack = new();
        private Dictionary<Node, Rect> _committed;
        private double _lastUndoTime;

        public bool CanUndoLayout => _undoStack.Count > 0;
        public bool CanRedoLayout => _redoStack.Count > 0;

        /// <summary>True once a baseline arrangement has been recorded for undo.</summary>
        private bool HasPositionBaseline => _committed != null;

        private Dictionary<Node, Rect> SnapshotPositions()
        {
            var map = new Dictionary<Node, Rect>(_allNodes.Count);
            foreach (var n in _allNodes)
                if (n != null) map[n] = n.GetPosition();
            return map;
        }

        /// <summary>Call right after node positions changed (drag, layout, restore).</summary>
        private void CommitPositions()
        {
            if (_committed != null)
            {
                _undoStack.Add(_committed);
                if (_undoStack.Count > UndoDepth) _undoStack.RemoveAt(0);
                _redoStack.Clear();
            }
            _committed = SnapshotPositions();
        }

        /// <summary>Reset the undo baseline (new graph content — old positions are meaningless).</summary>
        private void ResetPositionHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _committed = null;
        }

        private void ApplyPositions(Dictionary<Node, Rect> snapshot)
        {
            foreach (var kv in snapshot)
            {
                if (kv.Key == null) continue;
                kv.Key.SetPosition(kv.Value);
            }
            if (!IsFocused)
            {
                _fullGraphLaidOut = true;
                SaveFullGraphPositions();
            }
        }

        public void UndoLayoutChange()
        {
            if (_undoStack.Count == 0)
            {
                ShowMessage(XRayLocalization.GetText("graph_undo_empty"));
                return;
            }

            _redoStack.Add(SnapshotPositions());
            var previous = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            ApplyPositions(previous);
            _committed = previous;
            _lastUndoTime = EditorApplication.timeSinceStartup;
            ShowMessage(XRayLocalization.GetText("graph_undo_done"));
        }

        public void RedoLayoutChange()
        {
            if (_redoStack.Count == 0) return;

            _undoStack.Add(SnapshotPositions());
            var next = _redoStack[^1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            ApplyPositions(next);
            _committed = next;
            _lastUndoTime = EditorApplication.timeSinceStartup;
            ShowMessage(XRayLocalization.GetText("graph_redo_done"));
        }

        /// <summary>Guard so the KeyDown fallback does not repeat an Undo already run by the command event.</summary>
        private bool UndoJustHandled => EditorApplication.timeSinceStartup - _lastUndoTime < 0.15;

        // ── Saved layouts (per scene) ─────────────────────────────

        public static bool AutoRestoreLayout
        {
            get => EditorPrefs.GetBool(PrefAutoRestore, true);
            set => EditorPrefs.SetBool(PrefAutoRestore, value);
        }

        public bool HasSavedLayout => XRayLayoutStore.HasLayout(XRayLayoutStore.CurrentSceneKey);

        /// <summary>
        /// Stable layout keys for every node in one pass. GlobalObjectId lookups are the
        /// expensive part (the API is literally named …Slow), so scene objects go through the
        /// batch overload instead of one call per card.
        /// </summary>
        private Dictionary<Node, string> BuildLayoutKeys()
        {
            var keys = new Dictionary<Node, string>(_allNodes.Count);
            var sceneNodes = new List<XRayNode>();
            var sceneObjects = new List<UnityEngine.Object>();

            foreach (var n in _allNodes)
            {
                if (n is not XRayNode x) continue;

                if (x.GameObject != null)
                {
                    sceneNodes.Add(x);
                    sceneObjects.Add(x.GameObject);
                }
                else if (x.Asset != null)
                {
                    string path = AssetDatabase.GetAssetPath(x.Asset);
                    string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
                    keys[n] = "asset:" + (string.IsNullOrEmpty(guid) ? x.Asset.name : guid);
                }
                else
                {
                    keys[n] = "missing:" + x.Title;
                }
            }

            if (sceneObjects.Count > 0)
            {
                var ids = new GlobalObjectId[sceneObjects.Count];
                GlobalObjectId.GetGlobalObjectIdsSlow(sceneObjects.ToArray(), ids);
                for (int i = 0; i < sceneNodes.Count; i++)
                {
                    string gid = ids[i].ToString();
                    keys[sceneNodes[i]] = IsUsableId(gid)
                        ? "go:" + gid
                        : "path:" + HierarchyPath(sceneNodes[i].GameObject);
                }
            }

            return keys;
        }

        private static bool IsUsableId(string id) =>
            !string.IsNullOrEmpty(id) && id != "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0";

        private static string HierarchyPath(GameObject go)
        {
            var sb = new System.Text.StringBuilder(go.name);
            for (var t = go.transform.parent; t != null; t = t.parent)
                sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        /// <summary>Store the current arrangement for this scene.</summary>
        public bool SaveLayout()
        {
            if (_allNodes.Count == 0)
            {
                ShowMessage(XRayLocalization.GetText("graph_layout_empty"));
                return false;
            }

            var keys = BuildLayoutKeys();
            var map = new Dictionary<string, Vector2>(_allNodes.Count);
            foreach (var n in _allNodes)
            {
                if (!keys.TryGetValue(n, out string key) || string.IsNullOrEmpty(key)) continue;
                var p = n.GetPosition();
                map[key] = new Vector2(p.x, p.y);
            }

            XRayLayoutStore.Save(XRayLayoutStore.CurrentSceneKey, map);
            ShowMessage(XRayLocalization.Format("graph_layout_saved", map.Count, XRayLayoutStore.CurrentSceneName));
            return true;
        }

        /// <summary>Re-apply the stored arrangement. Returns how many nodes were moved.</summary>
        public int RestoreLayout(bool silent = false)
        {
            var saved = XRayLayoutStore.Load(XRayLayoutStore.CurrentSceneKey);
            if (saved.Count == 0)
            {
                if (!silent) ShowMessage(XRayLocalization.GetText("graph_layout_none"));
                return 0;
            }

            var keys = BuildLayoutKeys();
            int moved = 0;
            foreach (var n in _allNodes)
            {
                if (!keys.TryGetValue(n, out string key) || !saved.TryGetValue(key, out var pos)) continue;
                n.SetPosition(new Rect(pos.x, pos.y, 0, 0));
                moved++;
            }

            if (moved > 0)
            {
                if (!IsFocused)
                {
                    _fullGraphLaidOut = true;
                    SaveFullGraphPositions();
                }
                CommitPositions();
                UpdateView();
                if (!silent) ScheduleFrameAll();
            }

            if (!silent)
                ShowMessage(XRayLocalization.Format("graph_layout_restored", moved));
            return moved;
        }

        public void ClearSavedLayout()
        {
            XRayLayoutStore.Delete(XRayLayoutStore.CurrentSceneKey);
            ShowMessage(XRayLocalization.GetText("graph_layout_cleared"));
        }
    }
}
