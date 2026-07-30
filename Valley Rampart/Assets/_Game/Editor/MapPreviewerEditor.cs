using UnityEditor;
using UnityEngine;

/// <summary>
/// MapPreviewer 的 Inspector 扩展（3.3.4）。在默认 Inspector 下方加三个按钮，点击即可生成/加载/清除地图预览。
/// </summary>
[CustomEditor(typeof(MapPreviewer))]
public class MapPreviewerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var mp = (MapPreviewer)target;
        GUILayout.Space(8);

        GUILayout.Label("操作", EditorStyles.boldLabel);
        if (GUILayout.Button("生成地图预览（用上方 seed/size/difficulty）", GUILayout.Height(28)))
            mp.GeneratePreview();

        if (GUILayout.Button("从存档加载预览（用上方 slotId）", GUILayout.Height(28)))
            mp.LoadFromSave();

        if (GUILayout.Button("清除预览", GUILayout.Height(24)))
            mp.ClearPreview();
    }
}
