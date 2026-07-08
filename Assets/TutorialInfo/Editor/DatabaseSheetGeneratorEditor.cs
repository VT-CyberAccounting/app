using UnityEditor;

[CustomEditor(typeof(DatabaseSheetGenerator))]
public class DatabaseSheetGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        DrawPropertiesExcluding(serializedObject, "m_Script", "maximumSheetXOrZ");
        serializedObject.ApplyModifiedProperties();
    }
}
