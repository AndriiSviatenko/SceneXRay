using SceneXRay.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.Windows
{
    /// <summary>First-run walkthrough. Shown once, re-openable from Tools > SceneXRay > Advanced.</summary>
    public class XRayTutorial : EditorWindow
    {
        private static readonly (string Title, string Body)[] Steps =
        {
            ("Welcome to SceneXRay",
                "See every dependency in your scene as a graph — who references what, what is missing, and where the cycles are."),
            ("1 · Select an object",
                "Pick any GameObject. The Scene View overlay draws its dependency links right where the objects are."),
            ("2 · Open the graph",
                "Tools > SceneXRay > Open Graph View (Ctrl+Shift+Alt+X). Nodes are objects, edges are references. Double-click a node to drill in, Backspace to go back."),
            ("3 · Narrow it down",
                "Filter by name, component or hierarchy depth. Hover a node to light up its neighbours and dim the rest."),
            ("4 · Export it",
                "JSON, CSV, HTML, PlantUML, Mermaid or Markdown — share the architecture with your team."),
            ("5 · Watch the health score",
                "The toolbar score drops with every missing reference and dependency cycle it finds."),
            ("You're ready 🎉",
                "Everything lives under Tools > SceneXRay. Bookmarks: Ctrl+Shift+Alt+J.")
        };

        private int _step;
        private Label _title, _body, _counter;
        private Button _nextBtn;
        private VisualElement _dots;

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            if (!EditorPrefs.GetBool("SceneXRay_TutorialShown", false))
                EditorApplication.delayCall += ShowWindow;
        }

        public static void ShowWindow()
        {
            var wnd = GetWindow<XRayTutorial>();
            wnd.titleContent = new GUIContent("Welcome to SceneXRay");
            wnd.minSize = new Vector2(440, 260);
            wnd.Show();
        }

        private void OnEnable()
        {
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

            _counter = new Label();
            _counter.AddToClassList("xray-subtle");
            _counter.style.marginBottom = 2;
            body.Add(_counter);

            _title = new Label();
            _title.AddToClassList("xray-title");
            _title.style.fontSize = 17;
            body.Add(_title);

            _body = new Label();
            _body.AddToClassList("xray-subtle");
            _body.style.fontSize = 12;
            _body.style.marginTop = 6;
            _body.style.flexGrow = 1;
            body.Add(_body);

            _dots = new VisualElement();
            _dots.AddToClassList("xray-step-dots");
            for (int i = 0; i < Steps.Length; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("xray-step-dot");
                _dots.Add(dot);
            }
            body.Add(_dots);

            var buttonRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center }
            };
            var skipBtn = new Button(Close) { text = XRayLocalization.GetText("skip") };
            skipBtn.AddToClassList("xray-btn");
            _nextBtn = new Button(NextStep) { text = XRayLocalization.GetText("next") };
            _nextBtn.AddToClassList("xray-btn");
            _nextBtn.AddToClassList("xray-btn--primary");
            buttonRow.Add(skipBtn);
            buttonRow.Add(_nextBtn);
            body.Add(buttonRow);

            ApplyStep();
            EditorPrefs.SetBool("SceneXRay_TutorialShown", true);
        }

        private void NextStep()
        {
            if (_step >= Steps.Length - 1)
            {
                Close();
                return;
            }
            _step++;
            ApplyStep();
        }

        private void ApplyStep()
        {
            _title.text = Steps[_step].Title;
            _body.text = Steps[_step].Body;
            _counter.text = XRayLocalization.Format("tutorial_step", _step + 1, Steps.Length);
            _nextBtn.text = _step >= Steps.Length - 1
                ? XRayLocalization.GetText("tutorial_finish")
                : XRayLocalization.GetText("next");

            for (int i = 0; i < _dots.childCount; i++)
                _dots[i].EnableInClassList("xray-step-dot--active", i == _step);
        }
    }
}
