using System.IO;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SceneNameAttribute))]
public class SceneNamePropertyDrawer : PropertyDrawer
{
    private const float HelpHeight = 34f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        return property.propertyType == SerializedPropertyType.String && Warning(property.stringValue) != null
            ? line + HelpHeight + 2f
            : line;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "[SceneName]은 string 필드에만 쓸 수 있습니다");
            return;
        }

        Rect fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        SceneAsset current = FindScene(property.stringValue);

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        var picked = (SceneAsset)EditorGUI.ObjectField(fieldRect, label, current, typeof(SceneAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            property.stringValue = picked != null ? picked.name : "";
        }
        EditorGUI.EndProperty();

        string warning = Warning(property.stringValue);
        if (warning != null)
        {
            Rect help = new Rect(position.x, fieldRect.yMax + 2f, position.width, HelpHeight);
            EditorGUI.HelpBox(help, warning, MessageType.Warning);
        }
    }

    private static SceneAsset FindScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return null;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == sceneName) return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
        return null;
    }

    private static string Warning(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return "이동할 씬을 지정해주세요.";
        if (FindScene(sceneName) == null) return $"'{sceneName}' 씬 파일을 찾을 수 없습니다.";
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && Path.GetFileNameWithoutExtension(s.path) == sceneName) return null;
        return $"'{sceneName}' 씬이 빌드 설정에 없어서 이동할 수 없습니다. File > Build Profiles의 씬 목록에 추가해주세요.";
    }
}
