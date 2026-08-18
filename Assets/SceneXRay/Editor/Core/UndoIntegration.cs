using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public static class UndoIntegration
    {
        public static void RecordObject(Object obj, string name) => Undo.RecordObject(obj, name);
        public static void RecordGameObject(GameObject go, string name)
        {
            Undo.RecordObject(go, name);
            foreach (var comp in go.GetComponents<Component>())
                Undo.RecordObject(comp, name);
        }
        public static void PerformUndoableAction(System.Action action, string name)
        {
            Undo.SetCurrentGroupName(name);
            int group = Undo.GetCurrentGroup();
            action?.Invoke();
            Undo.CollapseUndoOperations(group);
        }
    }
}
