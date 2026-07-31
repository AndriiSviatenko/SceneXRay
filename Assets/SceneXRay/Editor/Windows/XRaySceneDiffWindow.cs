using SceneXRay.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;
using System.Linq;
using System.Text;
using UnityEditor.UIElements;

namespace SceneXRay.Editor.Windows
{
    /// <summary>Side-by-side dependency diff of two scene assets (neither scene is opened).</summary>
    public class XRaySceneDiffWindow : EditorWindow
    {
        private ObjectField _sceneAField, _sceneBField;
        private TextField _resultArea;
        private Label _summaryA, _summaryB, _summaryCommon;
        private VisualElement _summaryRow;

        public static void ShowWindow() => GetWindow<XRaySceneDiffWindow>().Show();

        private void OnEnable()
        {
            titleContent = new GUIContent("Scene Diff");
            minSize = new Vector2(460, 320);

            var root = rootVisualElement;
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/SceneXRay/Editor/Styles/XRayStyles.uss");
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("xray-window");
            root.EnableInClassList("xray-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("xray-light", !EditorGUIUtility.isProSkin);

            var body = new VisualElement();
            body.AddToClassList("xray-window-body");
            root.Add(body);

            var title = new Label(XRayLocalization.GetText("scene_diff"));
            title.AddToClassList("xray-title");
            body.Add(title);

            var hint = new Label(XRayLocalization.GetText("diff_hint"));
            hint.AddToClassList("xray-subtle");
            body.Add(hint);

            _sceneAField = new ObjectField("Scene A") { objectType = typeof(SceneAsset) };
            _sceneBField = new ObjectField("Scene B") { objectType = typeof(SceneAsset) };
            body.Add(_sceneAField);
            body.Add(_sceneBField);

            var actions = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 8, marginBottom = 8 }
            };
            var compareBtn = new Button(Compare) { text = XRayLocalization.GetText("compare") };
            compareBtn.AddToClassList("xray-btn");
            compareBtn.AddToClassList("xray-btn--primary");
            actions.Add(compareBtn);
            body.Add(actions);

            _summaryRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 8, display = DisplayStyle.None }
            };
            _summaryA = MakeChip("xray-chip--danger");
            _summaryB = MakeChip("xray-chip--danger");
            _summaryCommon = MakeChip("xray-chip--ok");
            _summaryRow.Add(_summaryA);
            _summaryRow.Add(_summaryB);
            _summaryRow.Add(_summaryCommon);
            body.Add(_summaryRow);

            var panel = new VisualElement();
            panel.AddToClassList("xray-panel");
            body.Add(panel);

            _resultArea = new TextField { multiline = true, isReadOnly = true };
            _resultArea.AddToClassList("xray-readonly-text");
            _resultArea.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_resultArea);
        }

        private static Label MakeChip(string modifier)
        {
            var chip = new Label();
            chip.AddToClassList("xray-chip");
            chip.AddToClassList(modifier);
            return chip;
        }

        private void Compare()
        {
            var pathA = AssetDatabase.GetAssetPath(_sceneAField.value);
            var pathB = AssetDatabase.GetAssetPath(_sceneBField.value);
            if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB))
            {
                _summaryRow.style.display = DisplayStyle.None;
                _resultArea.value = XRayLocalization.GetText("diff_pick_scenes");
                return;
            }

            var diff = SceneDiffUtility.CompareScenes(pathA, pathB);

            _summaryA.text = $"{XRayLocalization.GetText("diff_only_a")}: {diff.OnlyInSceneA.Count}";
            _summaryB.text = $"{XRayLocalization.GetText("diff_only_b")}: {diff.OnlyInSceneB.Count}";
            _summaryCommon.text = $"{XRayLocalization.GetText("diff_common")}: {diff.Common.Count}";
            _summaryRow.style.display = DisplayStyle.Flex;

            var sb = new StringBuilder();
            AppendGroup(sb, XRayLocalization.GetText("diff_only_a"), diff.OnlyInSceneA);
            AppendGroup(sb, XRayLocalization.GetText("diff_only_b"), diff.OnlyInSceneB);
            _resultArea.value = sb.ToString();
        }

        private static void AppendGroup(StringBuilder sb, string header, System.Collections.Generic.List<DependencyLink> links)
        {
            sb.Append("— ").Append(header).Append(" (").Append(links.Count).AppendLine(") —");
            if (links.Count == 0)
                sb.AppendLine("  —");
            foreach (var l in links)
                sb.Append("  ").Append(l.Source != null ? l.Source.name : "?")
                  .Append(" → ").Append(l.Target != null ? l.Target.name : "?")
                  .Append("   (").Append(l.SourcePropertyName).AppendLine(")");
            sb.AppendLine();
        }
    }
}
