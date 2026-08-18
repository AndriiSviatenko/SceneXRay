using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.UI
{
    public class XRayMiniMap : MiniMap
    {
        public XRayMiniMap(GraphView graphView)
        {
            this.graphView = graphView;

            capabilities |= Capabilities.Movable | Capabilities.Resizable;

            AddToClassList("xray-minimap");
            style.position = Position.Absolute;
            style.right = 12;
            style.bottom = 38;
            style.width = 220;
            style.height = 160;
            maxWidth = 220;
            maxHeight = 160;
        }
    }
}
