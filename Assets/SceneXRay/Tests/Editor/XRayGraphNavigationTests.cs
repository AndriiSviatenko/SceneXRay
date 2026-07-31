using SceneXRay.Editor.Windows;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using SceneXRay.Editor.Core;
using SceneXRay.Editor.UI;

namespace SceneXRay.Editor.Tests
{
    /// <summary>Deterministic graph nav/layout tests (synthetic GOs — no scene required).</summary>
    public class XRayGraphNavigationTests
    {
        GameObject _a, _b, _c;

        [SetUp]
        public void SetUp()
        {
            _a = new GameObject("XRayNav_A");
            _b = new GameObject("XRayNav_B");
            _c = new GameObject("XRayNav_C");
        }

        [TearDown]
        public void TearDown()
        {
            if (_a) Object.DestroyImmediate(_a);
            if (_b) Object.DestroyImmediate(_b);
            if (_c) Object.DestroyImmediate(_c);
        }

        [Test]
        public void Focus_Then_Back_Restores_ShowAll()
        {
            var gv = BuildSyntheticGraph();
            Assert.IsTrue(gv.FocusOn(_a));
            Assert.IsTrue(gv.IsFocused);
            Assert.IsTrue(gv.CanGoBack);
            Assert.AreEqual(2, gv.HistoryCount);

            gv.GoBack();
            Assert.IsFalse(gv.IsFocused);
            Assert.AreEqual(0, gv.HistoryIndex);
        }

        [Test]
        public void Focus_A_Then_B_Then_Back_Returns_To_Focused()
        {
            var gv = BuildSyntheticGraph();
            Assert.IsTrue(gv.FocusOn(_a));
            Assert.AreEqual(_a, gv.FocusCenterObject);
            Assert.IsTrue(gv.FocusOn(_b));
            Assert.AreEqual(_b, gv.FocusCenterObject);
            Assert.AreEqual(3, gv.HistoryCount);
            Assert.AreEqual(2, gv.HistoryIndex);

            gv.GoBack();
            Assert.IsTrue(gv.IsFocused, "Back from B should land on focused A, not Show All");
            Assert.AreEqual(_a, gv.FocusCenterObject);
            Assert.AreEqual(1, gv.HistoryIndex);

            gv.GoBack();
            Assert.IsFalse(gv.IsFocused);
            Assert.AreEqual(0, gv.HistoryIndex);
        }

        [Test]
        public void Soft_SetContent_Preserves_History()
        {
            var gv = BuildSyntheticGraph();
            Assert.IsTrue(gv.FocusOn(_a));
            Assert.IsTrue(gv.CanGoBack);

            gv.SetContent(new List<Node>(gv.GetAllNodes()), new List<Edge>(gv.GetAllEdges()), 500, resetNavigation: false);

            Assert.IsTrue(gv.IsFocused);
            Assert.IsTrue(gv.CanGoBack);
            Assert.AreEqual(_a, gv.FocusCenterObject);
        }

        [Test]
        public void Hard_SetContent_Resets_History()
        {
            var gv = BuildSyntheticGraph();
            Assert.IsTrue(gv.FocusOn(_a));
            gv.SetContent(new List<Node>(gv.GetAllNodes()), new List<Edge>(gv.GetAllEdges()), 500, resetNavigation: true);
            Assert.IsFalse(gv.CanGoBack);
            Assert.IsFalse(gv.IsFocused);
        }

        [Test]
        public void Force_Layout_No_SameColumn_Overlaps()
        {
            var gv = BuildSyntheticGraph();
            gv.ApplyForceLayout();

            const float minSep = 70f;
            var list = gv.GetAllNodes();
            Assert.Greater(list.Count, 0);

            // Positions must be real (not the pre-layout zero trap).
            Assert.IsTrue(list.Any(n =>
            {
                var p = n.GetPosition();
                return Mathf.Abs(p.x) > 0.01f || Mathf.Abs(p.y) > 0.01f;
            }), "Force layout left all nodes at origin — GetPosition/SetPosition broken");

            int overlaps = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var pa = list[i].GetPosition();
                for (int j = i + 1; j < list.Count; j++)
                {
                    var pb = list[j].GetPosition();
                    if (Mathf.Abs(pa.x - pb.x) > 40f) continue;
                    if (Mathf.Abs(pa.y - pb.y) < minSep) overlaps++;
                }
            }
            Assert.AreEqual(0, overlaps);
        }

        [Test]
        public void Back_Restores_FullGraph_Positions()
        {
            var gv = BuildSyntheticGraph();
            gv.ApplyHierarchicalLayout();

            var before = gv.GetAllNodes().ToDictionary(n => n, n => n.GetPosition());
            Assert.IsTrue(gv.FocusOn(_a));
            gv.GoBack();
            Assert.IsFalse(gv.IsFocused);

            foreach (var n in gv.GetAllNodes())
            {
                var now = n.GetPosition();
                var was = before[n];
                Assert.AreEqual(was.x, now.x, 0.5f, n.title + " x");
                Assert.AreEqual(was.y, now.y, 0.5f, n.title + " y");
            }
        }

        [Test]
        public void Legend_Exists()
        {
            var gv = BuildSyntheticGraph();
            Assert.IsNotNull(gv.Q(className: "xray-hotkey-legend"));
        }

        [Test]
        public void Focusing_Same_Node_Twice_Does_Not_Stack_History()
        {
            var gv = BuildSyntheticGraph();
            Assert.IsTrue(gv.FocusOn(_a));
            Assert.AreEqual(2, gv.HistoryCount);

            gv.FocusOn(_a);
            gv.FocusOn(_a);
            Assert.AreEqual(2, gv.HistoryCount, "Re-focusing the same object must not push new history entries");
            Assert.AreEqual(1, gv.HistoryIndex);
        }

        [Test]
        public void Breadcrumb_Click_Returns_To_Show_All()
        {
            var gv = BuildSyntheticGraph();
            Assert.IsTrue(gv.FocusOn(_a));
            Assert.IsTrue(gv.IsFocused);

            gv.NavigateToHistoryIndex(0);
            Assert.IsFalse(gv.IsFocused);
            Assert.AreEqual(0, gv.HistoryIndex);
        }

        [Test]
        public void Node_Tooltip_Is_Built_On_Hover()
        {
            var gv = BuildSyntheticGraph();
            var node = gv.GetAllNodes().OfType<XRayNode>().First(n => n.GameObject == _a);

            // The static tooltip stays empty — the detailed text is produced on demand.
            using (var evt = TooltipEvent.GetPooled())
            {
                evt.target = node;
                node.SendEvent(evt);
                Assert.IsTrue(evt.tooltip.Contains("Components:"),
                    "TooltipEvent did not produce the detailed tooltip: " + evt.tooltip);
                Assert.IsTrue(evt.tooltip.Contains(_a.name));
            }
        }

        [Test]
        public void Radial_Layout_Keeps_Nodes_Apart()
        {
            var gv = BuildSyntheticGraph();
            gv.ApplyRadialLayout();

            var list = gv.GetAllNodes();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var d = list[i].GetPosition().position - list[j].GetPosition().position;
                    Assert.Greater(d.magnitude, 80f,
                        $"{list[i].title} and {list[j].title} overlap in the radial layout");
                }
            }
        }

        XRayVirtualGraphView BuildSyntheticGraph()
        {
            XRayWindow.ShowWindow();
            var win = EditorWindow.GetWindow<XRayWindow>();
            Assert.IsNotNull(win);
            win.SetFollowEnabled(false);
            win.SetLiveModeEnabled(false);
            var gv = win.GraphView;
            Assert.IsNotNull(gv);

            var na = new XRayNode(_a);
            var nb = new XRayNode(_b);
            var nc = new XRayNode(_c);
            var l1 = new DependencyLink
            {
                Source = _a, Target = _b, SourcePropertyName = "refB",
                SourceComponentName = "Test", LinkType = LinkType.Direct
            };
            var l2 = new DependencyLink
            {
                Source = _b, Target = _c, SourcePropertyName = "refC",
                SourceComponentName = "Test", LinkType = LinkType.Direct
            };
            var e1 = new XRayEdge(na, nb, l1);
            var e2 = new XRayEdge(nb, nc, l2);

            gv.SetContent(
                new List<Node> { na, nb, nc },
                new List<Edge> { e1, e2 },
                500,
                resetNavigation: true);

            Assert.AreEqual(3, gv.GetAllNodes().Count);
            Assert.AreEqual(1, gv.HistoryCount, "Fresh graph must start with Show-All root only");
            return gv;
        }
    }
}
