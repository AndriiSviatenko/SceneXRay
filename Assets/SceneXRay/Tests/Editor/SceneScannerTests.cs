using SceneXRay.Editor.Windows;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using SceneXRay.Editor.Core;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace SceneXRay.Editor.Tests
{
    public class SceneScannerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SceneScanner.ClearTypeCaches();
            CodeDependencyScanner.ClearCache();
        }

        [Test]
        public void ScanGameObject_FindsDirectReference()
        {
            var source = Spawn("Source");
            var target = Spawn("Target");
            source.AddComponent<TestComponent>().reference = target;

            var links = SceneScanner.ScanGameObject(source);

            Assert.AreEqual(1, links.Count);
            Assert.AreEqual(target, links[0].Target);
            Assert.AreEqual(LinkType.Direct, links[0].LinkType);
        }

        [Test]
        public void ScanGameObject_DetectsMissingReference()
        {
            var source = Spawn("Source");
            var doomed = new GameObject("Doomed");
            source.AddComponent<TestComponent>().reference = doomed;
            Object.DestroyImmediate(doomed);

            var links = SceneScanner.ScanGameObject(source);

            Assert.IsTrue(links.Any(l => l.IsMissing), "Destroyed target should surface as a Missing link.");
        }

        [Test]
        public void ScanGameObject_DeduplicatesUnityEventTargets()
        {
            var source = Spawn("Source");
            var target = Spawn("Target");
            var eventComp = source.AddComponent<EventComponent>();
            var receiver = target.AddComponent<EventReceiver>();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(eventComp.onSomething, receiver.Receive);

            var links = SceneScanner.ScanGameObject(source);
            var toTarget = links.Where(l => l.Target == target).ToList();

            Assert.AreEqual(1, toTarget.Count, "UnityEvent target must not be duplicated by the serialized pass.");
            Assert.AreEqual(LinkType.UnityEvent, toTarget[0].LinkType);
            Assert.AreEqual("Receive", toTarget[0].TargetMethodName);
        }

        [Test]
        public void FindCycles_DetectsKnownCycle()
        {
            var a = Spawn("A");
            var b = Spawn("B");
            a.AddComponent<TestComponent>().reference = b;
            b.AddComponent<TestComponent>().reference = a;

            var links = SceneScanner.ScanGameObject(a);
            links.AddRange(SceneScanner.ScanGameObject(b));

            var cycles = XRayAnalyzer.FindCycles(links);

            Assert.AreEqual(1, cycles.Count);
            var sources = cycles[0].Select(l => l.Source).ToList();
            Assert.IsTrue(sources.Contains(a) && sources.Contains(b),
                "Cycle path must contain every participant, not just the stack top.");
        }

        [Test]
        public void ReferenceIndex_ReverseMapIsCorrect()
        {
            var source = Spawn("IndexSource");
            var target = Spawn("IndexTarget");
            source.AddComponent<TestComponent>().reference = target;

            XRayReferenceIndex.RebuildImmediate();

            Assert.IsTrue(XRayReferenceIndex.IsReady);
            var incoming = XRayReferenceIndex.GetIncoming(target);
            Assert.IsTrue(incoming.Any(l => l.Source == source),
                "Reverse index must map target -> incoming link from source.");
            var outgoing = XRayReferenceIndex.GetOutgoing(source);
            Assert.IsTrue(outgoing.Any(l => l.Target == target),
                "Forward index must map source -> outgoing link to target.");
        }

        [Test]
        public void Analyze_ComputesScoreMissingAndCyclesInOnePass()
        {
            var a = Spawn("A");
            var b = Spawn("B");
            a.AddComponent<TestComponent>().reference = b;
            b.AddComponent<TestComponent>().reference = a;

            var links = SceneScanner.ScanGameObject(a);
            links.AddRange(SceneScanner.ScanGameObject(b));

            var report = XRayAnalyzer.Analyze(links);

            Assert.AreEqual(1, report.CycleCount);
            Assert.AreEqual(0, report.Missing);
            Assert.AreEqual(XRayAnalyzer.CalculateHealthScore(links), report.Score);
            Assert.AreEqual(XRayAnalyzer.FindCycles(links).Count, report.CycleCount);
        }

        [Test]
        public void BuildDegreeMap_CountsBothEnds()
        {
            var a = Spawn("DegA");
            var b = Spawn("DegB");
            var c = Spawn("DegC");
            var links = new List<DependencyLink>
            {
                new DependencyLink { Source = a, Target = b, LinkType = LinkType.Direct },
                new DependencyLink { Source = a, Target = c, LinkType = LinkType.Direct },
            };

            var degrees = XRayAnalyzer.BuildDegreeMap(links);

            Assert.AreEqual(2, degrees[a]);
            Assert.AreEqual(1, degrees[b]);
            Assert.AreEqual(1, degrees[c]);
        }

        [Test]
        public void ComponentFilter_MatchesColliderAndScriptTaxonomy()
        {
            var go = Spawn("FilterGo");
            go.AddComponent<BoxCollider>();
            go.AddComponent<TestComponent>();

            Assert.IsTrue(XRayComponentFilter.Matches(go, "All Components"));
            Assert.IsTrue(XRayComponentFilter.Matches(go, "Collider"));
            Assert.IsTrue(XRayComponentFilter.Matches(go, "Script"));
            Assert.IsTrue(XRayComponentFilter.Matches(go, "Transform"));
            Assert.IsFalse(XRayComponentFilter.Matches(go, "Rigidbody"));
            Assert.IsFalse(XRayComponentFilter.Matches(null, "Collider"));
        }

        [Test]
        public void Analyze_PenalizesMissingRefs()
        {
            var source = Spawn("MissingSource");
            var doomed = new GameObject("DoomedMissing");
            source.AddComponent<TestComponent>().reference = doomed;
            Object.DestroyImmediate(doomed);

            var links = SceneScanner.ScanGameObject(source);
            var report = XRayAnalyzer.Analyze(links);

            Assert.Greater(report.Missing, 0);
            Assert.Less(report.Score, 100f);
        }

        [Test]
        public void ScanGameObject_FindsHiddenSerializedReference()
        {
            var source = Spawn("HiddenSource");
            var target = Spawn("HiddenTarget");
            source.AddComponent<HiddenReferenceComponent>().hiddenReference = target;

            var links = SceneScanner.ScanGameObject(source);

            Assert.IsTrue(links.Any(l => l.Target == target),
                "[HideInInspector] references are serialized and must remain visible to the scanner.");
        }

        [Test]
        public void ScanGameObject_DoesNotCacheEmptyCollectionAsReferenceFreeType()
        {
            var empty = Spawn("EmptyCollection");
            empty.AddComponent<CollectionReferenceComponent>();
            SceneScanner.ScanGameObject(empty);

            var source = Spawn("PopulatedCollection");
            var target = Spawn("CollectionTarget");
            source.AddComponent<CollectionReferenceComponent>().references.Add(target);

            var links = SceneScanner.ScanGameObject(source);

            Assert.IsTrue(links.Any(l => l.Target == target),
                "An empty collection on one instance must not suppress populated instances of the same type.");
        }

        [Test]
        public void CodeDependencyScanner_SeparatesNamesFromTags()
        {
            string path = Path.Combine(Path.GetTempPath(), $"SceneXRay-{System.Guid.NewGuid():N}.cs");
            try
            {
                File.WriteAllText(path,
                    "class Lookup { void Run() { GameObject.Find(\"PlayerRoot\"); " +
                    "GameObject.FindWithTag(\"Player\"); } }");

                var dependencies = CodeDependencyScanner.GetDependencies(path);

                Assert.IsTrue(dependencies.ObjectNames.Contains("PlayerRoot"));
                Assert.IsFalse(dependencies.ObjectNames.Contains("Player"));
                Assert.IsTrue(dependencies.ObjectTags.Contains("Player"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private class TestComponent : MonoBehaviour
        {
            public GameObject reference;
        }

        private class EventComponent : MonoBehaviour
        {
            public UnityEvent onSomething = new UnityEvent();
        }

        private class EventReceiver : MonoBehaviour
        {
            public void Receive() { }
        }

        private class HiddenReferenceComponent : MonoBehaviour
        {
            [HideInInspector] public GameObject hiddenReference;
        }

        private class CollectionReferenceComponent : MonoBehaviour
        {
            public List<GameObject> references = new List<GameObject>();
        }
    }
}
