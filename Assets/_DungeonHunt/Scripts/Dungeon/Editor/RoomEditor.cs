using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Room))]
[CanEditMultipleObjects]
public class RoomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Bake"))
        {
            foreach (Object selected in targets)
                RoomBaker.Bake((Room)selected);
        }
    }
}
