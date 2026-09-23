using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 중앙 복도 하나에 방이 좌우 번갈아가며 붙는 구조를 벽돌(기본은 큐브 하나를 반복 사용)로
// 생성하는 툴. Tools > Floor1 벽돌 던전 생성기 에서 연다.
public class DungeonBrickBuilder : EditorWindow
{
    private enum Side { Right, Left }

    [SerializeField] private GameObject brickPrefab; // 비워두면 기본 Cube 사용
    [SerializeField] private Vector2 brickSize = new Vector2(0.5f, 0.25f); // 가로/세로(높이) 벽돌 1개 크기(m)
    [SerializeField] private float cellSize = 2f; // 격자 한 칸의 실제 크기(m)
    [SerializeField] private float wallHeight = 2.5f;

    [SerializeField] private int roomCount = 5;
    [SerializeField] private Side startSide = Side.Right;
    [SerializeField] private int corridorWidth = 1; // 복도 폭(칸)
    [SerializeField] private int corridorPadding = 2; // 입구/출구에서 첫/마지막 방까지 실제로 걸어야 하는 빈 칸 수
    [SerializeField] private int roomSpacing = 5; // 방과 방 사이(칸)
    [SerializeField] private int roomWidth = 4; // 복도 기준 옆으로 뻗는 폭(칸)
    [SerializeField] private int roomDepth = 4; // 복도 방향 깊이(칸)

    [SerializeField] private bool entranceOpen; // 입구(맨 앞) 끝벽 - 체크하면 뚫림, 해제하면 막힌 벽
    [SerializeField] private bool exitOpen; // 출구(맨 뒤) 끝벽 - 체크하면 뚫림, 해제하면 막힌 벽

    [SerializeField] private bool autoCreateRoomEncounters = true;
    [SerializeField] private MeleeEnemyAI defaultMeleePrefab;
    [SerializeField] private RangedEnemyAI defaultRangedPrefab;
    [SerializeField] private int defaultMeleeCount = 3;
    [SerializeField] private int defaultRangedCount = 0;
    [SerializeField] private float defaultTriggerDistance = 8f;

    [MenuItem("Tools/Floor1 벽돌 던전 생성기")]
    private static void Open()
    {
        GetWindow<DungeonBrickBuilder>("벽돌 던전 생성기");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("벽돌", EditorStyles.boldLabel);
        brickPrefab = (GameObject)EditorGUILayout.ObjectField("벽돌 프리팹 (비우면 Cube)", brickPrefab, typeof(GameObject), false);
        brickSize = EditorGUILayout.Vector2Field("벽돌 크기 (가로, 높이)", brickSize);
        cellSize = EditorGUILayout.FloatField("격자 칸 크기(m)", cellSize);
        wallHeight = EditorGUILayout.FloatField("벽 높이(m)", wallHeight);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("구조", EditorStyles.boldLabel);
        roomCount = EditorGUILayout.IntField("방 개수", roomCount);
        startSide = (Side)EditorGUILayout.EnumPopup("첫 방 방향", startSide);
        corridorWidth = Mathf.Max(1, EditorGUILayout.IntField("복도 폭(칸)", corridorWidth));
        corridorPadding = EditorGUILayout.IntField("입구/출구 ~ 방까지 여유 칸", corridorPadding);
        roomSpacing = EditorGUILayout.IntField("방 사이 간격(칸)", roomSpacing);
        roomWidth = EditorGUILayout.IntField("방 폭(칸)", roomWidth);
        roomDepth = EditorGUILayout.IntField("방 깊이(칸)", roomDepth);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("입구/출구", EditorStyles.boldLabel);
        entranceOpen = EditorGUILayout.Toggle("입구(맨 앞) 끝벽 열기", entranceOpen);
        exitOpen = EditorGUILayout.Toggle("출구(맨 뒤) 끝벽 열기", exitOpen);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("몬스터 스폰 구역 (RoomEncounter)", EditorStyles.boldLabel);
        autoCreateRoomEncounters = EditorGUILayout.Toggle("방마다 자동 생성", autoCreateRoomEncounters);
        using (new EditorGUI.DisabledScope(!autoCreateRoomEncounters))
        {
            defaultMeleePrefab = (MeleeEnemyAI)EditorGUILayout.ObjectField("근접 몬스터 프리팹", defaultMeleePrefab, typeof(MeleeEnemyAI), false);
            defaultRangedPrefab = (RangedEnemyAI)EditorGUILayout.ObjectField("원거리 몬스터 프리팹", defaultRangedPrefab, typeof(RangedEnemyAI), false);
            defaultMeleeCount = EditorGUILayout.IntField("근접 몬스터 수", defaultMeleeCount);
            defaultRangedCount = EditorGUILayout.IntField("원거리 몬스터 수", defaultRangedCount);
            defaultTriggerDistance = EditorGUILayout.FloatField("발동 거리(m)", defaultTriggerDistance);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("생성", GUILayout.Height(30)))
        {
            Generate();
        }
    }

    private void Generate()
    {
        var floorCells = new HashSet<Vector2Int>();

        // corridorPadding은 "입구/출구에서 방까지 실제로 걸어야 하는 빈 칸 수"를 의미한다.
        // 방 자체도 roomDepth/2칸만큼 접합부(junction) 앞뒤로 파고들기 때문에, 그만큼을
        // 더해줘야 방이 입구/출구에 바로 붙지 않고 진짜로 padding칸만큼 떨어진다.
        int corridorLength = corridorPadding * 2 + roomDepth + roomSpacing * (roomCount - 1) + 1;

        // 복도 폭: corridorWidth 칸을 가운데(x=0) 기준으로 좌우에 배분
        int leftEdge = -(corridorWidth / 2);
        int rightEdge = corridorWidth - 1 + leftEdge;

        for (int z = 0; z < corridorLength; z++)
        {
            for (int x = leftEdge; x <= rightEdge; x++)
            {
                floorCells.Add(new Vector2Int(x, z));
            }
        }

        // 방: 각 접합부에서 좌/우 번갈아 붙임 (복도 바깥쪽 가장자리부터 시작)
        var roomBounds = new List<RectInt>();
        Side side = startSide;
        for (int i = 0; i < roomCount; i++)
        {
            int junctionZ = corridorPadding + roomDepth / 2 + i * roomSpacing;
            bool goRight = side == Side.Right;
            int startX = goRight ? rightEdge + 1 : leftEdge - 1;
            int dir = goRight ? 1 : -1;

            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;

            for (int dx = 0; dx < roomWidth; dx++)
            {
                for (int dz = -roomDepth / 2; dz <= roomDepth / 2; dz++)
                {
                    int z = junctionZ + dz;
                    if (z < 0) continue;
                    int x = startX + dir * dx;
                    floorCells.Add(new Vector2Int(x, z));

                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
                }
            }

            roomBounds.Add(new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1));
            side = side == Side.Right ? Side.Left : Side.Right;
        }

        GameObject root = new GameObject("Floor1_Dungeon");
        Undo.RegisterCreatedObjectUndo(root, "Generate Dungeon");
        Transform floorParent = new GameObject("Floor").transform;
        floorParent.SetParent(root.transform);
        Transform wallParent = new GameObject("Walls").transform;
        wallParent.SetParent(root.transform);

        int bricksPerCellSide = Mathf.Max(1, Mathf.RoundToInt(cellSize / brickSize.x));
        int bricksPerWallHeight = Mathf.Max(1, Mathf.RoundToInt(wallHeight / brickSize.y));

        foreach (Vector2Int cell in floorCells)
        {
            Vector3 cellCenter = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);
            BuildFloorTile(floorParent, cellCenter, bricksPerCellSide);

            bool skipForwardWall = exitOpen && cell.y == corridorLength - 1;
            bool skipBackwardWall = entranceOpen && cell.y == 0;

            TryBuildWall(floorCells, cell, Vector2Int.up, cellCenter, new Vector3(0, 0, cellSize / 2f), true, wallParent, bricksPerCellSide, bricksPerWallHeight, skipForwardWall);
            TryBuildWall(floorCells, cell, Vector2Int.down, cellCenter, new Vector3(0, 0, -cellSize / 2f), true, wallParent, bricksPerCellSide, bricksPerWallHeight, skipBackwardWall);
            TryBuildWall(floorCells, cell, Vector2Int.right, cellCenter, new Vector3(cellSize / 2f, 0, 0), false, wallParent, bricksPerCellSide, bricksPerWallHeight, false);
            TryBuildWall(floorCells, cell, Vector2Int.left, cellCenter, new Vector3(-cellSize / 2f, 0, 0), false, wallParent, bricksPerCellSide, bricksPerWallHeight, false);
        }

        if (autoCreateRoomEncounters)
        {
            Transform encounterParent = new GameObject("RoomEncounters").transform;
            encounterParent.SetParent(root.transform);

            for (int i = 0; i < roomBounds.Count; i++)
            {
                CreateRoomEncounter(encounterParent, roomBounds[i], i);
            }
        }

        Selection.activeGameObject = root;
        Debug.Log($"벽돌 던전 생성 완료: 바닥 {floorCells.Count}칸, 오브젝트는 Floor1_Dungeon 아래에 있습니다. Tools > 선택 오브젝트 Static으로 설정 을 실행해주세요.");
    }

    private void CreateRoomEncounter(Transform parent, RectInt bounds, int index)
    {
        Vector3 center = new Vector3(
            (bounds.xMin + bounds.xMax - 1) / 2f * cellSize,
            1f,
            (bounds.yMin + bounds.yMax - 1) / 2f * cellSize);
        Vector2 size = new Vector2(bounds.width * cellSize, bounds.height * cellSize);

        GameObject go = new GameObject($"RoomEncounter_{index + 1}");
        Undo.RegisterCreatedObjectUndo(go, "Generate Dungeon");
        go.transform.SetParent(parent);
        go.transform.position = center;

        var encounter = go.AddComponent<RoomEncounter>();
        var so = new SerializedObject(encounter);
        so.FindProperty("roomSize").vector2Value = size;
        so.FindProperty("triggerDistance").floatValue = defaultTriggerDistance;
        so.FindProperty("meleeEnemyPrefab").objectReferenceValue = defaultMeleePrefab;
        so.FindProperty("rangedEnemyPrefab").objectReferenceValue = defaultRangedPrefab;
        so.FindProperty("meleeCount").intValue = defaultMeleeCount;
        so.FindProperty("rangedCount").intValue = defaultRangedCount;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void BuildFloorTile(Transform parent, Vector3 cellCenter, int bricksPerSide)
    {
        float start = -cellSize / 2f + brickSize.x / 2f;
        for (int ix = 0; ix < bricksPerSide; ix++)
        {
            for (int iz = 0; iz < bricksPerSide; iz++)
            {
                Vector3 pos = cellCenter + new Vector3(start + ix * brickSize.x, -brickSize.y / 2f, start + iz * brickSize.x);
                SpawnBrick(parent, pos, Quaternion.identity, new Vector3(brickSize.x, brickSize.y, brickSize.x));
            }
        }
    }

    private void TryBuildWall(HashSet<Vector2Int> floorCells, Vector2Int cell, Vector2Int neighborDir, Vector3 cellCenter, Vector3 edgeOffset, bool horizontalEdge, Transform parent, int bricksPerSide, int bricksPerHeight, bool forceOpen)
    {
        if (forceOpen) return; // 입구/출구를 열어두기로 한 면이면 벽을 안 세움
        if (floorCells.Contains(cell + neighborDir)) return; // 옆에 방/복도가 있으면 벽 없음(통로)

        float start = -cellSize / 2f + brickSize.x / 2f;
        for (int i = 0; i < bricksPerSide; i++)
        {
            for (int h = 0; h < bricksPerHeight; h++)
            {
                float along = start + i * brickSize.x;
                Vector3 pos = horizontalEdge
                    ? cellCenter + edgeOffset + new Vector3(along, brickSize.y / 2f + h * brickSize.y, 0)
                    : cellCenter + edgeOffset + new Vector3(0, brickSize.y / 2f + h * brickSize.y, along);

                Vector3 scale = horizontalEdge
                    ? new Vector3(brickSize.x, brickSize.y, brickSize.y / 2f)
                    : new Vector3(brickSize.y / 2f, brickSize.y, brickSize.x);

                SpawnBrick(parent, pos, Quaternion.identity, scale);
            }
        }
    }

    private void SpawnBrick(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject brick = brickPrefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(brickPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        Undo.RegisterCreatedObjectUndo(brick, "Generate Dungeon");
        brick.transform.SetParent(parent);
        brick.transform.position = position;
        brick.transform.rotation = rotation;
        if (brickPrefab == null) brick.transform.localScale = scale;

        GameObjectUtility.SetStaticEditorFlags(brick, (StaticEditorFlags)~0);
    }
}
