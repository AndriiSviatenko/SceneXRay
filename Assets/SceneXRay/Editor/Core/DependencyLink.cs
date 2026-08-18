using System;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.Core
{
    public enum LinkType
    {
        Direct,
        UnityEvent,
        Missing,
        AssetReference,

        Implicit
    }

    [Serializable]
    public class DependencyLink
    {
        public GameObject Source;
        public GameObject Target;

        public UnityEngine.Object TargetAsset;
        public string SourcePropertyName;
        public string TargetMethodName;
        public string SourceComponentName;
        public LinkType LinkType;

        public bool IsMissing => LinkType == LinkType.Missing;
        public bool IsUnityEvent => LinkType == LinkType.UnityEvent;
        public bool IsDirect => LinkType == LinkType.Direct;
        public bool IsAssetReference => LinkType == LinkType.AssetReference;
        public bool IsImplicit => LinkType == LinkType.Implicit;

        public string SourceGlobalId => GetGlobalId(Source);

        public string TargetGlobalId => Target != null ? GetGlobalId(Target) : GetGlobalId(TargetAsset);

        public static string GetGlobalId(UnityEngine.Object obj)
        {
            if (obj == null) return string.Empty;
            string id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();

            if (id == "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0")
                return string.Empty;
            return id;
        }
    }
}
