using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.UI
{
    /// <summary>
    /// Bottom-right overview map: drag to move, drag the corner to resize.
    /// </summary>
    public class XRayMiniMap : MiniMap
    {
        public XRayMiniMap(GraphView graphView)
        {
            this.graphView = graphView;
            // Left un-anchored (Unity's default) so it can be dragged; anchoring is
            // available from the MiniMap's own context menu.
            capabilities |= Capabilities.Movable | Capabilities.Resizable;

            AddToClassList("xray-minimap");
            style.position = Position.Absolute;
            style.right = 12;
            style.bottom = 38; // clear of the status bar
            style.width = 220;
            style.height = 160;
            maxWidth = 220;
            maxHeight = 160;
        }
    }
}
