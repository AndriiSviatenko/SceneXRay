using UnityEngine;

namespace SceneXRay.Editor.Core
{
    /// <summary>Shared component-filter matching for graph toolbar and tests.</summary>
    public static class XRayComponentFilter
    {
        public static bool Matches(GameObject go, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter.StartsWith("All"))
                return true;
            if (go == null)
                return false;

            switch (filter)
            {
                case "Collider":
                    return go.GetComponent<Collider>() != null;
                case "Rigidbody":
                    return go.GetComponent<Rigidbody>() != null;
                case "Transform":
                    return true;
                case "Script":
                    return go.GetComponent<MonoBehaviour>() != null;
                default:
                    return go.GetComponent(filter) != null;
            }
        }
    }
}
