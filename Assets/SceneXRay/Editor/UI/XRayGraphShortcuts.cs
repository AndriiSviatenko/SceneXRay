using SceneXRay.Editor.Windows;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.UI
{
    /// <summary>
    /// Graph navigation shortcuts scoped to <see cref="XRayWindow"/>.
    /// GraphView built-ins: F = Frame Selection, A = Frame All.
    /// </summary>
    static class XRayGraphShortcuts
    {
        static XRayVirtualGraphView Graph
        {
            get
            {
                var win = EditorWindow.focusedWindow as XRayWindow;
                return win != null ? win.GraphView : null;
            }
        }

        static bool CanHandleGraphKeys()
        {
            var win = EditorWindow.focusedWindow as XRayWindow;
            if (win == null) return false;
            var focused = win.rootVisualElement?.focusController?.focusedElement;
            if (focused is TextField || focused is IntegerField)
                return false;
            return true;
        }

        [Shortcut("SceneXRay/Graph: Navigate Back", typeof(XRayWindow), KeyCode.LeftArrow, ShortcutModifiers.Alt)]
        static void BackAlt(ShortcutArguments _)
        {
            if (CanHandleGraphKeys()) Graph?.GoBack();
        }

        // Backspace = browser-style Back (more reliable than Alt on Windows menus).
        [Shortcut("SceneXRay/Graph: Navigate Back (Backspace)", typeof(XRayWindow), KeyCode.Backspace)]
        static void BackSpace(ShortcutArguments _)
        {
            if (CanHandleGraphKeys()) Graph?.GoBack();
        }

        [Shortcut("SceneXRay/Graph: Navigate Forward", typeof(XRayWindow), KeyCode.RightArrow, ShortcutModifiers.Alt)]
        static void Forward(ShortcutArguments _)
        {
            if (CanHandleGraphKeys()) Graph?.GoForward();
        }

        // Escape / Return are reserved by the Shortcut Manager (it registers them unbound),
        // so those two live only in XRayVirtualGraphView.OnGraphKeyDown.

        [Shortcut("SceneXRay/Graph: Expand Radius", typeof(XRayWindow), KeyCode.Equals)]
        static void Expand(ShortcutArguments _)
        {
            if (CanHandleGraphKeys()) Graph?.ExpandRadius();
        }

        [Shortcut("SceneXRay/Graph: Shrink Radius", typeof(XRayWindow), KeyCode.Minus)]
        static void Shrink(ShortcutArguments _)
        {
            if (CanHandleGraphKeys()) Graph?.ShrinkRadius();
        }
    }
}
