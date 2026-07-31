using System;
using System.Collections.Generic;

namespace SceneXRay.Editor.Core
{
    /// <summary>Serializable snapshot of a scene dependency graph (node IDs are GlobalObjectId strings).</summary>
    [Serializable]
    public class SceneXRaySnapshot
    {
        public string Id;
        public string ScenePath;
        public string Author;
        /// <summary>Round-trip ("o") string — JsonUtility cannot serialize <see cref="DateTime"/>.</summary>
        public string TimestampIso;
        public string Comment;
        public List<SnapshotNode> Nodes = new List<SnapshotNode>();
        public List<SnapshotEdge> Edges = new List<SnapshotEdge>();
        public float HealthScore;
        public int MissingCount;
        public int CycleCount;

        public DateTime Timestamp
        {
            get => DateTime.TryParse(TimestampIso, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt
                : DateTime.MinValue;
            set => TimestampIso = value.ToString("o");
        }
    }

    /// <summary>Plain floats — Vector3 serializes as a normalized-property loop.</summary>
    [Serializable]
    public struct SnapshotVec3
    {
        public float X;
        public float Y;
        public float Z;

        public SnapshotVec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static SnapshotVec3 From(UnityEngine.Vector3 v) => new SnapshotVec3(v.x, v.y, v.z);
        public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(X, Y, Z);
    }

    [Serializable]
    public class SnapshotNode
    {
        public string Id;
        public string Name;
        public string Type;
        public SnapshotVec3 Position;
        public List<string> Components;
    }

    [Serializable]
    public class SnapshotEdge
    {
        public string SourceId;
        public string TargetId;
        public string Label;
        public string Type;
    }
}
