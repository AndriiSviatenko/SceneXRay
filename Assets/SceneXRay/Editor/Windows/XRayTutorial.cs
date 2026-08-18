using SceneXRay.Editor.UI;
using SceneXRay.Editor.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.Windows
{
    public class XRayTutorial : EditorWindow
    {
        private static readonly (string TitleKey, string BodyKey)[] Steps =
        {
            ("tutorial_welcome_title", "tutorial_welcome_body"),
            ("tutorial_select_title", "tutorial_select_body"),
            ("tutorial_graph_title", "tutorial_graph_body"),
            ("tutorial_filter_title", "tutorial_filter_body"),
            ("tutorial_export_title", "tutorial_export_body"),
            ("tutorial_health_title", "tutorial_health_body"),
            ("tutorial_ready_title", "tutorial_ready_body")
        };

        private int _step;
        private Label _title, _body, _counter;
        private Button _nextBtn, _skipBtn;
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
            wnd.titleContent = new GUIContent(XRayLocalization.GetText("tutorial_window_title"));
            wnd.minSize = new Vector2(440, 260);
            wnd.Show();
        }

        private void OnEnable()
        {
            var root = rootVisualElement;
            root.Clear();

            var styleSheet = SceneXRayCompat.LoadStyleSheet();
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
            _skipBtn = new Button(Close);
            _skipBtn.AddToClassList("xray-btn");
            _nextBtn = new Button(NextStep);
            _nextBtn.AddToClassList("xray-btn");
            _nextBtn.AddToClassList("xray-btn--primary");
            buttonRow.Add(_skipBtn);
            buttonRow.Add(_nextBtn);
            body.Add(buttonRow);

            XRayLocalization.LanguageChanged += ApplyStep;
            ApplyStep();
            EditorPrefs.SetBool("SceneXRay_TutorialShown", true);
        }

        private void OnDisable()
        {
            XRayLocalization.LanguageChanged -= ApplyStep;
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
            titleContent = new GUIContent(XRayLocalization.GetText("tutorial_window_title"));
            _title.text = XRayLocalization.GetText(Steps[_step].TitleKey);
            _body.text = XRayLocalization.GetText(Steps[_step].BodyKey);
            _counter.text = XRayLocalization.Format("tutorial_step", _step + 1, Steps.Length);
            _skipBtn.text = XRayLocalization.GetText("skip");
            _nextBtn.text = _step >= Steps.Length - 1
                ? XRayLocalization.GetText("tutorial_finish")
                : XRayLocalization.GetText("next");

            for (int i = 0; i < _dots.childCount; i++)
                _dots[i].EnableInClassList("xray-step-dot--active", i == _step);
        }
    }
}
