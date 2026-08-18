using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;

[assembly: InternalsVisibleTo("SceneXRay.Editor.Tests")]

namespace SceneXRay.Editor.UI
{
    public class XRayNode : Node
    {
        public GameObject GameObject { get; private set; }

        public Object Asset { get; private set; }

        public string Title { get; private set; }
        public string PrefabName { get; private set; }

        public string Subtitle { get; private set; }

        private int _dependencyCount;
        private readonly Label _countBadge;

        private Rect _storedPosition;
        private bool _hasStoredPosition;

        public int DependencyCount
        {
            get => _dependencyCount;
            set
            {
                _dependencyCount = value;
                _countBadge.text = value.ToString();
                _countBadge.style.display = value > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public XRayNode(GameObject go, string customTitle = null, Object asset = null)
        {
            GameObject = go;
            Asset = asset;
            Title = customTitle ?? (go != null ? go.name : "Unknown");
            title = Title;

            AddToClassList("graph-node");

            if (titleButtonContainer != null)
                titleButtonContainer.style.display = DisplayStyle.None;

            var icon = new Image { name = "node-icon", scaleMode = ScaleMode.ScaleToFit };
            icon.image = ResolveIcon(go, customTitle, asset);
            titleContainer.Insert(0, icon);

            _countBadge = new Label("") { name = "count-badge" };
            _countBadge.style.display = DisplayStyle.None;
            titleContainer.Add(_countBadge);

            Subtitle = BuildSubtitle(go, customTitle, asset);
            var subtitleLabel = new Label(Subtitle) { name = "node-subtitle", pickingMode = PickingMode.Ignore };
            topContainer.Insert(1, subtitleLabel);

            if (go != null)
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefab != null)
                {
                    PrefabName = prefab.name;
                    AddToClassList("prefab-node");
                }
            }

            RegisterCallback<TooltipEvent>(evt =>
            {
                evt.tooltip = BuildTooltip();
                evt.rect = worldBound;
                evt.StopImmediatePropagation();
            });

            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(GameObject));
            inputPort.portName = "";
            inputPort.AddToClassList("input-port");
            inputContainer.Add(inputPort);

            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(GameObject));
            outputPort.portName = "";
            outputPort.AddToClassList("output-port");
            outputContainer.Add(outputPort);

            RefreshPorts();
        }

        internal string BuildTooltip()
        {
            if (GameObject == null)
            {
                if (Asset == null) return Title + "\n" + Subtitle;
                string path = AssetDatabase.GetAssetPath(Asset);
                return $"{Asset.name}\nType: {Asset.GetType().Name}" +
                       (string.IsNullOrEmpty(path) ? "" : $"\n{path}") +
                       "\n\nDouble-click / Enter = focus";
            }

            var comps = GameObject.GetComponents<Component>();
            string compNames = string.Join(", ", comps.Select(c => c != null ? c.GetType().Name : "Missing script"));
            return $"GameObject: {GameObject.name}\nPath: {Subtitle}\nPrefab: {PrefabName ?? "None"}\n" +
                   $"Components: {compNames}\n\nDouble-click / Enter = focus\nAlt+← Back · − shrink · = expand · Esc up";
        }

        private static string BuildSubtitle(GameObject go, string customTitle, Object asset)
        {
            if (go == null)
            {
                if (asset != null)
                {
                    string path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        string dir = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(dir))
                        {
                            int slash = dir.LastIndexOf('/');
                            return slash >= 0 ? "… ▸ " + dir.Substring(slash + 1) : dir;
                        }
                    }
                    return asset.GetType().Name;
                }
                if (customTitle != null && customTitle.StartsWith("Asset:"))
                    return XRayLocalization.GetText("graph_subtitle_asset");
                return XRayLocalization.GetText("graph_subtitle_missing");
            }

            var parent = go.transform.parent;
            if (parent == null)
                return XRayLocalization.GetText("graph_subtitle_scene");

            var grand = parent.parent;
            if (grand == null)
                return parent.name;
            return (grand.parent != null ? "… ▸ " : "") + grand.name + " ▸ " + parent.name;
        }

        private static Texture ResolveIcon(GameObject go, string customTitle, Object asset)
        {
            if (go != null)
            {
                var thumb = AssetPreview.GetMiniThumbnail(go);
                if (thumb != null) return thumb;
                return EditorGUIUtility.IconContent("GameObject Icon").image;
            }
            if (asset != null)
            {
                var thumb = AssetPreview.GetMiniThumbnail(asset);
                if (thumb != null) return thumb;
            }
            if (asset != null || (customTitle != null && customTitle.StartsWith("Asset:")))
                return EditorGUIUtility.IconContent("ScriptableObject Icon").image;
            return EditorGUIUtility.IconContent("console.erroricon.sml").image;
        }

        public override void SetPosition(Rect newPos)
        {
            _storedPosition = newPos;
            _hasStoredPosition = true;
            base.SetPosition(newPos);
        }

        public override Rect GetPosition()
        {
            if (!_hasStoredPosition)
                return base.GetPosition();

            var live = base.GetPosition();
            float w = !float.IsNaN(live.width) && live.width > 1f ? live.width : 160f;
            float h = !float.IsNaN(live.height) && live.height > 1f ? live.height : 44f;
            return new Rect(_storedPosition.x, _storedPosition.y, w, h);
        }

        public override void OnSelected()
        {
            base.OnSelected();
            var target = GameObject != null ? (Object)GameObject : Asset;
            if (target != null)
                XRaySelectionSync.SelectFromGraph(target);
        }
    }

    internal static class XRaySelectionSync
    {
        public static bool Suppress { get; private set; }

        public static void SelectFromGraph(Object target)
        {
            if (target == null) return;
            Suppress = true;
            try
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
            finally
            {
                EditorApplication.delayCall += () => Suppress = false;
            }
        }
    }
}
