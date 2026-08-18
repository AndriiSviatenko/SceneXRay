using UnityEditor.Experimental.GraphView;
using SceneXRay.Editor.Core;
using UnityEngine.UIElements;
using UnityEngine;

namespace SceneXRay.Editor.UI
{
    public enum XRayEdgeHighlight
    {
        None = 0,
        Outgoing = 1,
        Incoming = 2
    }

    public class XRayEdge : Edge
    {
        public DependencyLink Link { get; private set; }

        public XRayNode SyntheticTargetNode { get; private set; }

        private XRayEdgeHighlight _highlight;

        public XRayEdge(XRayNode source, XRayNode target, DependencyLink link)
        {
            Link = link;
            if (target.GameObject == null)
                SyntheticTargetNode = target;

            output = source.outputContainer.Q<Port>();
            input = target.inputContainer.Q<Port>();
            output.Connect(this);
            input.Connect(this);

            string targetName = link.Target != null ? link.Target.name
                : link.TargetAsset != null ? link.TargetAsset.name
                : "Missing";
            tooltip = $"From: {link.Source?.name}\nTo: {targetName}\nProperty: {link.SourcePropertyName}\nType: {link.LinkType}";

            if (link.IsMissing) AddToClassList("missing-edge");
            else if (link.IsUnityEvent) AddToClassList("unityevent-edge");
            else if (link.IsAssetReference) AddToClassList("asset-edge");
            else if (link.IsImplicit) AddToClassList("implicit-edge");
            else AddToClassList("direct-edge");

            RegisterCallback<AttachToPanelEvent>(_ => ApplyVisual());
            RegisterCallback<GeometryChangedEvent>(_ => ApplyVisual());
        }

        public void SetHighlight(XRayEdgeHighlight state)
        {
            if (_highlight == state) return;
            _highlight = state;
            ApplyVisual();
        }

        public void RefreshVisual() => ApplyVisual();

        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (panel == null || edgeControl == null) return;
            if (input?.panel == null || output?.panel == null) return;

            var color = _highlight switch
            {
                XRayEdgeHighlight.Outgoing => XRayCustomization.HighlightOutColor,
                XRayEdgeHighlight.Incoming => XRayCustomization.HighlightInColor,
                _ => XRayCustomization.ColorFor(Link)
            };

            int width = Mathf.Max(1, Mathf.RoundToInt(
                XRayCustomization.LineWidth + (_highlight == XRayEdgeHighlight.None ? 0f : 1f)));

            edgeControl.inputColor = color;
            edgeControl.outputColor = color;
            edgeControl.edgeWidth = width;
            MarkDirtyRepaint();
        }
    }

    internal static class XRayCustomization
    {
        public static Color DirectColor => SceneXRaySettings.instance.DirectColor;
        public static Color EventColor => SceneXRaySettings.instance.EventColor;
        public static Color MissingColor => SceneXRaySettings.instance.MissingColor;
        public static Color AssetColor => SceneXRaySettings.instance.AssetColor;
        public static Color ImplicitColor => SceneXRaySettings.instance.ImplicitColor;
        public static float LineWidth => SceneXRaySettings.instance.LineWidth;

        public static readonly Color HighlightOutColor = new(1f, 0.69f, 0.25f);
        public static readonly Color HighlightInColor = new(0.38f, 0.78f, 1f);

        public static Color ColorFor(DependencyLink link)
        {
            if (link == null) return DirectColor;
            if (link.IsMissing) return MissingColor;
            if (link.IsUnityEvent) return EventColor;
            if (link.IsAssetReference) return AssetColor;
            if (link.IsImplicit) return ImplicitColor;
            return DirectColor;
        }

        public static void ApplyNodeAccent(VisualElement node, Color accent)
        {
            node.style.borderTopColor = accent;
            node.style.borderRightColor = accent;
            node.style.borderBottomColor = accent;
            node.style.borderLeftColor = accent;

            var title = node.Q("title");
            if (title != null)
                title.style.backgroundColor = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f);
        }

        public static void ApplyMissingNodeStyle(VisualElement node) => ApplyNodeAccent(node, MissingColor);
        public static void ApplyAssetNodeStyle(VisualElement node) => ApplyNodeAccent(node, AssetColor);
        public static void ApplyScriptNodeStyle(VisualElement node) => ApplyNodeAccent(node, ImplicitColor);
    }
}
