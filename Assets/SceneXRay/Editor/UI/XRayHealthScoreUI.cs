using SceneXRay.Editor.Windows;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;
using System.Linq;

namespace SceneXRay.Editor.UI
{
    /// <summary>Toolbar widget showing the scene health score. Consumes pre-collected links (no scanning).</summary>
    // Report accents mirror the graph's link colours so the two read as one tool.
    public class XRayHealthScoreUI : VisualElement
    {
        private readonly Label _scoreLabel;
        private readonly ProgressBar _progressBar;
        private readonly Label _detailsLabel;
        private List<DependencyLink> _links = new();
        private HealthReport _lastReport;

        /// <summary>Raised when the user clicks a god-object name in the details dialog.</summary>
        public System.Action<GameObject> FocusRequested;

        public XRayHealthScoreUI()
        {
            // Styling lives in XRayStyles.uss (.xray-health*) so it follows the editor skin.
            AddToClassList("xray-health");
            tooltip = "Click for missing refs, cycles, and god objects";

            _scoreLabel = new Label(XRayLocalization.GetText("health_score") + ": ");
            _scoreLabel.AddToClassList("xray-health__score");
            Add(_scoreLabel);

            _progressBar = new ProgressBar { lowValue = 0, highValue = 100, value = 100 };
            _progressBar.AddToClassList("xray-health__bar");
            Add(_progressBar);

            _detailsLabel = new Label();
            _detailsLabel.AddToClassList("xray-health__details");
            Add(_detailsLabel);

            RegisterCallback<ClickEvent>(_ => ShowDetails());
            RegisterCallback<DetachFromPanelEvent>(_ => XRayLocalization.LanguageChanged -= OnLanguageChanged);
            XRayLocalization.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            if (_links != null)
                Refresh(_links);
        }

        /// <summary>Updates the widget from already collected links instead of re-scanning the scene.</summary>
        public void Refresh(List<DependencyLink> links)
        {
            _links = links ?? new List<DependencyLink>();
            _lastReport = XRayAnalyzer.Analyze(_links);

            float score = _lastReport.Score;
            _scoreLabel.text = XRayLocalization.GetText("health_score") + $": {score:F1}%";
            _progressBar.value = score;
            _progressBar.title = $"{score:F1}%";

            // Grade drives the colour from USS instead of an inline tint, so the bar keeps
            // its rounded chrome and follows the light/dark skin.
            EnableInClassList("is-good", score > 80f);
            EnableInClassList("is-warn", score > 50f && score <= 80f);
            EnableInClassList("is-bad", score <= 50f);

            bool clean = _lastReport.Missing == 0 && _lastReport.CycleCount == 0;
            _detailsLabel.text = clean
                ? "✓"
                : $"{XRayLocalization.GetText("missing_refs")}: {_lastReport.Missing}  " +
                  $"{XRayLocalization.GetText("cycles_detected")}: {_lastReport.CycleCount}";
            _detailsLabel.EnableInClassList("is-clean", clean);
        }

        private static Color ColMissing => SceneXRaySettings.instance.MissingColor;
        private static Color ColEvent => SceneXRaySettings.instance.EventColor;
        private static Color ColAsset => SceneXRaySettings.instance.AssetColor;

        private void ShowDetails()
        {
            var report = _lastReport.Cycles != null
                ? _lastReport
                : XRayAnalyzer.Analyze(_links);

            var summary = new StringBuilder();
            summary.Append($"Health {report.Score:F1}%  ·  missing {report.Missing}  ·  ")
                   .Append($"cycles {report.CycleCount}  ·  avg {report.AvgDeps:F1} deps/object");

            var pattern = PatternDetector.DetectPattern(_links);
            if (pattern != PatternDetector.ArchitecturePattern.None)
                summary.Append($"  ·  architecture hint: {pattern}");

            var missingRows = _links
                .Where(l => l.IsMissing)
                .Select(l => XRayReportWindow.MakeRow(
                    l.Source,
                    l.Source != null ? l.Source.name : "?",
                    $"{l.SourceComponentName}.{l.SourcePropertyName}",
                    ColMissing))
                .ToList();

            var cycleRows = new List<XRayReportWindow.Row>();
            if (report.Cycles != null)
            {
                foreach (var cycle in report.Cycles)
                {
                    var first = cycle.FirstOrDefault(l => l.Source != null)?.Source;
                    string path = string.Join(" → ", cycle.Where(l => l.Source != null).Select(l => l.Source.name));
                    cycleRows.Add(XRayReportWindow.MakeRow(first, first != null ? first.name : "?", path, ColEvent));
                }
            }

            var godRows = report.GodObjects
                .Where(go => go != null)
                .Select(go => XRayReportWindow.MakeRow(go, go.name,
                    XRayLocalization.GetText("report_god_hint"), ColAsset))
                .ToList();

            XRayReportWindow.Show(
                XRayLocalization.GetText("health_score"),
                summary.ToString(),
                new List<XRayReportWindow.Section>
                {
                    new()
                    {
                        Title = XRayLocalization.GetText("missing_refs"),
                        Rows = missingRows,
                        EmptyText = XRayLocalization.GetText("report_no_missing")
                    },
                    new()
                    {
                        Title = XRayLocalization.GetText("cycles_detected"),
                        Rows = cycleRows,
                        EmptyText = XRayLocalization.GetText("report_no_cycles")
                    },
                    new()
                    {
                        Title = XRayLocalization.GetText("report_god_objects"),
                        Rows = godRows,
                        EmptyText = XRayLocalization.GetText("report_no_god")
                    }
                });

            // Keep the drill-in affordance the old dialog had.
            var firstGod = report.GodObjects.FirstOrDefault(g => g != null);
            if (firstGod != null)
                FocusRequested?.Invoke(firstGod);
        }

    }
}
