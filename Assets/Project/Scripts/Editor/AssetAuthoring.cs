using UnityEditor;
using UnityEngine;

namespace EndlessDescent.EditorTools
{
    // Every definition field is a private [SerializeField], so generated content has to be written
    // through SerializedObject rather than by assignment
    public class AssetAuthoring
    {
        readonly SerializedObject serialized;

        public AssetAuthoring(Object target) => serialized = new SerializedObject(target);

        public AssetAuthoring Str(string field, string value) => Apply(field, p => p.stringValue = value);
        public AssetAuthoring Int(string field, int value) => Apply(field, p => p.intValue = value);
        public AssetAuthoring Float(string field, float value) => Apply(field, p => p.floatValue = value);
        public AssetAuthoring Bool(string field, bool value) => Apply(field, p => p.boolValue = value);
        public AssetAuthoring Enum(string field, int value) => Apply(field, p => p.enumValueIndex = value);
        public AssetAuthoring Ref(string field, Object value) => Apply(field, p => p.objectReferenceValue = value);
        public AssetAuthoring Vec2Int(string field, Vector2Int value) => Apply(field, p => p.vector2IntValue = value);
        public AssetAuthoring Vec2(string field, Vector2 value) => Apply(field, p => p.vector2Value = value);
        public AssetAuthoring Bounds(string field, Bounds value) => Apply(field, p => p.boundsValue = value);
        public AssetAuthoring Colour(string field, Color value) => Apply(field, p => p.colorValue = value);

        public AssetAuthoring Refs(string field, params Object[] values)
        {
            return Apply(field, p =>
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            });
        }

        public AssetAuthoring Enums(string field, params int[] values)
        {
            return Apply(field, p =>
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).enumValueIndex = values[i];
            });
        }

        public SerializedProperty Property(string field) => serialized.FindProperty(field);

        public AssetAuthoring Apply(string field, System.Action<SerializedProperty> write)
        {
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
                Debug.LogWarning($"No serialized field '{field}' on {serialized.targetObject.GetType().Name}");
            else
                write(property);

            return this;
        }

        public void Save()
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Object target = serialized.targetObject;

            if (target == null || !AssetDatabase.Contains(target))
                return;

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
