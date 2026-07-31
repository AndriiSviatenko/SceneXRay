using System;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    /// <summary>Kind of dependency between two objects.</summary>
    public enum LinkType
    {
        Direct,
        UnityEvent,
        Missing,
        AssetReference
    }

    /// <summary>A single dependency edge discovered by the scanner.</summary>
    [Serializable]
    public class DependencyLink
    {
        public GameObject Source;
        public GameObject Target;
        /// <summary>Referenced non-GameObject asset (e.g. ScriptableObject), if any.</summary>
        public UnityEngine.Object TargetAsset;
        public string SourcePropertyName;
        public string TargetMethodName;
        public string SourceComponentName;
        public LinkType LinkType;

        public bool IsMissing => LinkType == LinkType.Missing;
        public bool IsUnityEvent => LinkType == LinkType.UnityEvent;
        public bool IsDirect => LinkType == LinkType.Direct;
        public bool IsAssetReference => LinkType == LinkType.AssetReference;

        /// <summary>Stable identifier of the source object across editor sessions.</summary>
        public string SourceGlobalId => GetGlobalId(Source);

        /// <summary>Stable identifier of the target object across editor sessions.</summary>
        public string TargetGlobalId => Target != null ? GetGlobalId(Target) : GetGlobalId(TargetAsset);

        public static string GetGlobalId(UnityEngine.Object obj)
        {
            if (obj == null) return string.Empty;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            // Null/default id is non-empty but useless — treat as missing.
            if (id == "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0")
                return string.Empty;
            return id;
        }
    }
}
