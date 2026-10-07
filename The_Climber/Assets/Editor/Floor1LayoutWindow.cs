using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Floor1 던전 설정 창: 1층의 방 크기·복도·방 개수를 미터 단위로 조절하고 던전을 다시 만든다.
public class Floor1LayoutWindow : EditorWindow
{
    private const string ScenePath = "Assets/Scenes/Floor1.unity";
    private const string PrefPrefix = "Floor1Layout.";

    private static bool IsFloorScene(string path) => path.StartsWith("Assets/Scenes/Floor") && path.EndsWith(".unity");

    private float roomWidthM, roomDepthM, corridorWidthM, corridorLengthM, exitWidthM, exitDepthM;
    private int roomCount;
    private bool endCorridor;

    [MenuItem("Tools/Floor1 던전 설정 창")]
    private static void Open()
    {
        var window = GetWindow<Floor1LayoutWindow>("Floor1 던전");
        window.minSize = new Vector2(360f, 330f);
    }

    private void OnEnable()
    {
        var d = DungeonChainBuilder.BigRoomLayout;
        float c = DungeonChainBuilder.CellSize;
        roomWidthM = EditorPrefs.GetFloat(PrefPrefix + "roomWidthM", d.roomWidth * c);
        roomDepthM = EditorPrefs.GetFloat(PrefPrefix + "roomDepthM", d.roomDepth * c);
        corridorWidthM = EditorPrefs.GetFloat(PrefPrefix + "corridorWidthM", d.corridorWidth * c);
        corridorLengthM = EditorPrefs.GetFloat(PrefPrefix + "corridorLengthM", d.corridorLength * c);
        exitWidthM = EditorPrefs.GetFloat(PrefPrefix + "exitWidthM", d.exitWidth * c);
        exitDepthM = EditorPrefs.GetFloat(PrefPrefix + "exitDepthM", d.exitDepth * c);
        roomCount = EditorPrefs.GetInt(PrefPrefix + "roomCount", d.roomCount);
        endCorridor = EditorPrefs.GetInt(PrefPrefix + "endCorridor", d.endCorridor ? 1 : 0) == 1;
    }

    private void SavePrefs()
    {
        EditorPrefs.SetFloat(PrefPrefix + "roomWidthM", roomWidthM);
        EditorPrefs.SetFloat(PrefPrefix + "roomDepthM", roomDepthM);
        EditorPrefs.SetFloat(PrefPrefix + "corridorWidthM", corridorWidthM);
        EditorPrefs.SetFloat(PrefPrefix + "corridorLengthM", corridorLengthM);
        EditorPrefs.SetFloat(PrefPrefix + "exitWidthM", exitWidthM);
        EditorPrefs.SetFloat(PrefPrefix + "exitDepthM", exitDepthM);
        EditorPrefs.SetInt(PrefPrefix + "roomCount", roomCount);
        EditorPrefs.SetInt(PrefPrefix + "endCorridor", endCorridor ? 1 : 0);
    }

    // 칸 단위(짝수)로 맞춘다. 방/복도 폭이 홀수 칸이면 가운데가 반 칸 어긋난다.
    private static int Cells(float meters, int min)
    {
        int cells = Mathf.RoundToInt(meters / DungeonChainBuilder.CellSize / 2f) * 2;
        return Mathf.Max(min, cells);
    }

    private DungeonChainBuilder.Layout CurrentLayout() => new DungeonChainBuilder.Layout
    {
        roomCount = roomCount,
        roomWidth = Cells(roomWidthM, 4),
        roomDepth = Cells(roomDepthM, 4),
        corridorWidth = Cells(corridorWidthM, 2),
        corridorLength = Cells(corridorLengthM, 2),
        endCorridor = endCorridor,
        exitWidth = Cells(exitWidthM, 4),
        exitDepth = exitDepthM < 1f ? 0 : Cells(exitDepthM, 2),
    };

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("1층 던전 범위", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        roomCount = EditorGUILayout.IntSlider("방 개수", roomCount, 1, 8);
        roomWidthM = EditorGUILayout.Slider("방 가로 (m)", roomWidthM, 8f, 120f);
        roomDepthM = EditorGUILayout.Slider("방 세로 (m)", roomDepthM, 8f, 120f);
        corridorWidthM = EditorGUILayout.Slider("복도 폭 (m)", corridorWidthM, 4f, 12f);
        corridorLengthM = EditorGUILayout.Slider("복도 길이 (m)", corridorLengthM, 4f, 40f);
        endCorridor = EditorGUILayout.Toggle("마지막 방 뒤 막다른 복도", endCorridor);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("출구 공간 (다음 층 문을 두는 곳)", EditorStyles.boldLabel);
        exitWidthM = EditorGUILayout.Slider("출구 공간 폭 (m)", exitWidthM, 8f, 60f);
        exitDepthM = EditorGUILayout.Slider("출구 공간 깊이 (m, 0=없음)", exitDepthM, 0f, 60f);
        if (EditorGUI.EndChangeCheck()) SavePrefs();

        DungeonChainBuilder.Layout l = CurrentLayout();
        float c = DungeonChainBuilder.CellSize;
        int tail = l.exitDepth > 0 ? l.exitDepth : (l.endCorridor ? l.corridorLength : 0);
        float totalDepth = (l.roomCount * (l.roomDepth + l.corridorLength) + tail) * c;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            $"실제 적용: 방 {l.roomWidth * c:0}m x {l.roomDepth * c:0}m, 복도 폭 {l.corridorWidth * c:0}m, 복도 길이 {l.corridorLength * c:0}m\n" +
            (l.exitDepth > 0 ? $"출구 공간 {l.exitWidth * c:0}m x {l.exitDepth * c:0}m (다음 층 문이 안쪽 벽 앞에 자동 배치됨)\n" : "출구 공간 없음\n") +
            $"전체 길이 약 {totalDepth:0}m (칸 크기 {c:0}m, 가운데가 맞도록 짝수 칸으로 맞춰집니다)", MessageType.None);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("프리셋: 큰 방 하나")) { Apply(DungeonChainBuilder.BigRoomLayout); }
            if (GUILayout.Button("프리셋: 복도-방 5개 (기존)")) { Apply(DungeonChainBuilder.Floor1Layout); }
        }

        EditorGUILayout.Space();
        bool inFloor1 = IsFloorScene(EditorSceneManager.GetActiveScene().path);
        if (!inFloor1)
        {
            EditorGUILayout.HelpBox("다시 만들기는 층 씬(Floor1~Floor5)이 열려 있을 때만 할 수 있습니다. 지금 열려 있는 층 씬이 다시 만들어집니다.", MessageType.Warning);
            if (GUILayout.Button("Floor1 씬 열기") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        using (new EditorGUI.DisabledScope(!inFloor1))
        {
            if (GUILayout.Button("이 설정으로 던전 다시 만들기", GUILayout.Height(32f)))
            {
                if (EditorUtility.DisplayDialog("던전 다시 만들기",
                        "현재 열린 층의 Floor1_Dungeon(바닥/벽/문/내비메시)을 지우고 이 크기로 다시 만든 뒤 씬을 저장합니다.\n방 안 몬스터 수 같은 RoomEncounter 설정은 유지되고, 방이 줄면 남는 방의 몬스터는 마지막 방에 합쳐집니다.", "만들기", "취소"))
                    Rebuild(CurrentLayout());
            }
        }
    }

    // 프리셋 버튼: 슬라이더 값만 프리셋으로 맞춘다 (던전은 '다시 만들기'를 눌러야 바뀜)
    private void Apply(DungeonChainBuilder.Layout preset)
    {
        float c = DungeonChainBuilder.CellSize;
        roomCount = preset.roomCount;
        roomWidthM = preset.roomWidth * c;
        roomDepthM = preset.roomDepth * c;
        corridorWidthM = preset.corridorWidth * c;
        corridorLengthM = preset.corridorLength * c;
        endCorridor = preset.endCorridor;
        exitWidthM = preset.exitWidth > 0 ? preset.exitWidth * c : exitWidthM;
        exitDepthM = preset.exitDepth * c;
        SavePrefs();
        Repaint();
    }

    private static void Rebuild(DungeonChainBuilder.Layout layout)
    {
        if (!IsFloorScene(EditorSceneManager.GetActiveScene().path))
        {
            Debug.LogError("층 씬(Floor1~Floor5)이 열려 있지 않아 던전을 다시 만들지 않았습니다.");
            return;
        }
        Debug.Log(DungeonChainBuilder.Rebuild(layout));
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}
