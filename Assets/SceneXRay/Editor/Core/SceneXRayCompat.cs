using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneXRay.Editor.Core
{
    public static class SceneXRayCompat
    {
        private const string StyleSheetGuid = "a5c7b2903b61e6f428e50d71a40d6a0f";

        public const string MinimumSupportedVersion = "6000.0";

        public const string HighestVerifiedVersion = "6000.5";

        public static bool IsUnity6OrNewer => true;

        public static bool IsUnity65OrNewer =>
#if SCENEXRAY_UNITY_6_5_OR_NEWER
            true;
#else
            false;
#endif

        public static string EditorVersion => Application.unityVersion;

        public static StyleSheet LoadStyleSheet()
        {
            string path = AssetDatabase.GUIDToAssetPath(StyleSheetGuid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
        }

        public static string StyleSheetPath => AssetDatabase.GUIDToAssetPath(StyleSheetGuid);

        public static ulong IdOf(Object obj)
        {
            if (obj == null) return 0;
#if SCENEXRAY_UNITY_6_5_OR_NEWER
            return EntityId.ToULong(obj.GetEntityId());
#else
            return unchecked((ulong)(long)obj.GetInstanceID());
#endif
        }

        public static bool IsBrokenReference(SerializedProperty prop)
        {
            if (prop == null || prop.objectReferenceValue != null) return false;
#if SCENEXRAY_UNITY_6_5_OR_NEWER
            return prop.objectReferenceEntityIdValue != EntityId.None;
#else
            return prop.objectReferenceInstanceIDValue != 0;
#endif
        }

        public static Object ObjectFromChange(ChangeGameObjectOrComponentPropertiesEventArgs args)
        {
#if SCENEXRAY_UNITY_6_5_OR_NEWER
            return EditorUtility.EntityIdToObject(args.entityId);
#else
            return EditorUtility.InstanceIDToObject(args.instanceId);
#endif
        }

        public static Vector3 ReadViewPosition(VisualElement viewTransform)
        {
            if (viewTransform == null) return Vector3.zero;
#if SCENEXRAY_UNITY_6_5_OR_NEWER
            return viewTransform.resolvedStyle.translate;
#else
            return viewTransform.transform.position;
#endif
        }

        public static Vector3 ReadViewScale(VisualElement viewTransform)
        {
            if (viewTransform == null) return Vector3.one;
#if SCENEXRAY_UNITY_6_5_OR_NEWER
            return viewTransform.resolvedStyle.scale.value;
#else
            return viewTransform.transform.scale;
#endif
        }

        public static T[] FindAll<T>(FindObjectsInactive inactive = FindObjectsInactive.Include)
            where T : Object
        {
#if SCENEXRAY_UNITY_6_5_OR_NEWER
            return Object.FindObjectsByType<T>(inactive);
#else
            return Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None);
#endif
        }
    }
}
