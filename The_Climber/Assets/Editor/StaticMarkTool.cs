using UnityEditor;
using UnityEngine;

// Hierarchy에서 선택한 오브젝트(와 그 자식 전부)를 Inspector의 "Static" 체크박스를
// 누른 것과 동일하게 일괄 설정한다. 벽돌처럼 안 움직이는 구조물을 대량 배치했을 때
// 하나하나 체크하지 않고 한 번에 처리하기 위한 툴.
public static class StaticMarkTool
{
    [MenuItem("Tools/선택 오브젝트 Static으로 설정 (자식 포함)")]
    private static void MarkSelectedStatic()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            MarkRecursive(go, true);
        }
    }

    [MenuItem("Tools/선택 오브젝트 Static 해제 (자식 포함)")]
    private static void UnmarkSelectedStatic()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            MarkRecursive(go, false);
        }
    }

    private static void MarkRecursive(GameObject go, bool isStatic)
    {
        GameObjectUtility.SetStaticEditorFlags(go, isStatic ? (StaticEditorFlags)~0 : 0);

        foreach (Transform child in go.transform)
        {
            MarkRecursive(child.gameObject, isStatic);
        }
    }
}
