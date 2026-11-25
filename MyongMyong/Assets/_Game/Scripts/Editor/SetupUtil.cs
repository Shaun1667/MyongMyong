using UnityEditor;
using UnityEngine;

namespace Myong.Editor
{
    /// <summary>Setup 메뉴들이 같이 쓰는 작은 도구. Setup은 몇 번을 돌려도 같은 결과여야 한다.</summary>
    public static class SetupUtil
    {
        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            int slash = assetFolder.LastIndexOf('/');
            string parent = assetFolder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, assetFolder.Substring(slash + 1));
        }

        /// <summary>직렬화된(private) 필드에 참조를 넣는다. 값이 같으면 건드리지 않는다.</summary>
        public static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Myong Setup] {target.GetType().Name} has no serialized field '{field}'");
                return;
            }
            if (property.objectReferenceValue == value) return;
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Myong Setup] {target.GetType().Name} has no serialized field '{field}'");
                return;
            }
            if (property.stringValue == value) return;
            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Myong Setup] {target.GetType().Name} has no serialized field '{field}'");
                return;
            }
            if (property.intValue == value) return;
            property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Myong/Setup/Run All", priority = 0)]
        public static void RunAll()
        {
            RenderSetup.Apply();
            DataSetup.CreateDataAssets();
            PixelArtImportRules.ReimportArt();
            PlayerRigBuilder.Build();
            GameSceneBuilder.Build();
        }
    }
}
