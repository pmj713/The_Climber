using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 복도 -> 방 -> 복도 -> 방 ... 이 일직선으로 이어지는 던전을 만든다 (Tools > Floor1 던전 다시 만들기).
//
//     |    |        <- 복도
//   ---    ---      <- 방 아래/위 벽 가운데가 문(복도 폭만큼 뚫림)
//   |        |      <- 방
//   ---    ---
//     |    |
//
// 바닥과 벽은 칸마다 오브젝트를 만들지 않고 메시 하나씩으로 합쳐서 만든다. 재질은 어두운 벽돌(텍스처+노멀맵).
// 기존 RoomEncounter(몬스터 설정 포함)는 지우지 않고 새 방 가운데로 옮겨서 재사용한다.
public static class DungeonChainBuilder
{
    private const float Cell = 2f;          // 격자 한 칸(m)
    private const float WallHeight = 2.5f;
    private const string MaterialPath = "Assets/Materials/DungeonDarkBrick.mat";
    private const string TopMaterialPath = "Assets/Materials/DungeonDarkBrickTop.mat"; // 벽 윗면: 바닥과 구분되게 더 어둡게
    private const string AlbedoPath = "Assets/Textures/DungeonDarkBrick_Albedo.png";
    private const string NormalPath = "Assets/Textures/DungeonDarkBrick_Normal.png";

    public struct Layout
    {
        public int roomCount;      // 방 개수
        public int corridorWidth;  // 복도 폭(칸, 짝수 권장)
        public int corridorLength; // 방 사이 복도 길이(칸)
        public int roomWidth;      // 방 가로(칸, 짝수 권장)
        public int roomDepth;      // 방 세로(칸)
    }

    public static readonly Layout Floor1Layout = new Layout
    {
        // 벽 두께가 1칸이라 방 사이 복도는 앞뒤 방 벽 2칸을 빼고 남는 만큼 '복도'로 보인다 (4칸 -> 2칸이 복도)
        roomCount = 5, corridorWidth = 2, corridorLength = 4, roomWidth = 8, roomDepth = 5
    };

    [MenuItem("Tools/Floor1 던전 다시 만들기 (복도-방 일자 연결)")]
    private static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("던전 다시 만들기",
                "현재 열린 씬의 Floor1_Dungeon과 바닥 Plane을 지우고 복도-방 일자 구조로 다시 만듭니다. 계속할까요?", "만들기", "취소"))
            return;
        Debug.Log(Rebuild(Floor1Layout));
    }

    public static string Rebuild(Layout layout)
    {
        var log = new System.Text.StringBuilder();
        Material material = GetOrCreateMaterial();

        // ---------- 1) 칸 배치 ----------
        var floor = new HashSet<Vector2Int>();
        var rooms = new List<RectInt>();
        int z = 0;
        AddRect(floor, -layout.corridorWidth / 2, z, layout.corridorWidth, layout.corridorLength); // 시작 복도
        z += layout.corridorLength;
        for (int i = 0; i < layout.roomCount; i++)
        {
            var room = new RectInt(-layout.roomWidth / 2, z, layout.roomWidth, layout.roomDepth);
            AddRect(floor, room.x, room.y, room.width, room.height);
            rooms.Add(room);
            z += layout.roomDepth;
            AddRect(floor, -layout.corridorWidth / 2, z, layout.corridorWidth, layout.corridorLength); // 다음 복도 (마지막은 막다른 복도)
            z += layout.corridorLength;
        }
        int totalDepth = z;
        // 전체가 원점 기준 남북 가운데 오도록 이동
        float zOffset = -totalDepth * Cell / 2f;
        Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * Cell, 0f, (c.y + 0.5f) * Cell + zOffset);

        // 벽 = 바닥에 붙어 있는(8방향) 바닥 아닌 칸. 칸 단위 덩어리로 쌓아서 모서리가 겹치지 않는다.
        var solid = new HashSet<Vector2Int>();
        foreach (var c in floor)
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dz);
                    if (!floor.Contains(n)) solid.Add(n);
                }

        // ---------- 2) 기존 것 정리 (RoomEncounter는 살려둠) ----------
        var keptEncounters = new List<RoomEncounter>(Object.FindObjectsByType<RoomEncounter>(FindObjectsInactive.Include));
        keptEncounters.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
        foreach (var enc in keptEncounters) enc.transform.SetParent(null, true);

        var oldDungeon = GameObject.Find("Floor1_Dungeon");
        if (oldDungeon != null) { Object.DestroyImmediate(oldDungeon); log.AppendLine("removed old Floor1_Dungeon"); }
        var plane = GameObject.Find("Plane");
        if (plane != null && plane.GetComponent<NavMeshSurface>() != null)
        {
            var oldData = plane.GetComponent<NavMeshSurface>().navMeshData;
            string oldDataPath = oldData != null ? AssetDatabase.GetAssetPath(oldData) : null;
            Object.DestroyImmediate(plane);
            if (!string.IsNullOrEmpty(oldDataPath)) AssetDatabase.DeleteAsset(oldDataPath);
            log.AppendLine("removed old ground Plane + its NavMesh data");
        }

        // ---------- 3) 메시 생성 ----------
        var root = new GameObject("Floor1_Dungeon");
        var floorGo = CreateMeshObject("Floor", root.transform, BuildFloorMesh(floor, CellCenter), material);
        var wallGo = CreateMeshObject("Walls", root.transform, BuildWallMesh(floor, solid, CellCenter), material, GetOrCreateTopMaterial(material));
        log.AppendLine($"floor cells {floor.Count}, wall blocks {solid.Count}, length {totalDepth * Cell}m");

        // ---------- 4) RoomEncounter 재배치 ----------
        var encounterParent = new GameObject("RoomEncounters").transform;
        encounterParent.SetParent(root.transform);
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomEncounter enc = i < keptEncounters.Count ? keptEncounters[i] : new GameObject().AddComponent<RoomEncounter>();
            enc.gameObject.name = $"RoomEncounter_{i + 1}";
            enc.transform.SetParent(encounterParent, true);
            Vector2 roomCenter = new Vector2((rooms[i].x + rooms[i].width / 2f) * Cell, (rooms[i].y + rooms[i].height / 2f) * Cell + zOffset);
            enc.transform.position = new Vector3(roomCenter.x, 1f, roomCenter.y);
            var so = new SerializedObject(enc);
            so.FindProperty("roomSize").vector2Value = new Vector2(rooms[i].width * Cell - 2f, rooms[i].height * Cell - 2f); // 벽에서 1m 띄워 스폰
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        for (int i = rooms.Count; i < keptEncounters.Count; i++) Object.DestroyImmediate(keptEncounters[i].gameObject);
        log.AppendLine($"room encounters placed: {rooms.Count} (reused {Mathf.Min(rooms.Count, keptEncounters.Count)})");

        // ---------- 5) 플레이어 시작 위치: 시작 복도 ----------
        var spawn = Object.FindAnyObjectByType<FloorSpawnPoint>(FindObjectsInactive.Include);
        if (spawn != null)
        {
            spawn.transform.position = new Vector3(0f, spawn.transform.position.y, zOffset + Cell * 0.75f);
            log.AppendLine("player spawn moved to " + spawn.transform.position);
        }

        // ---------- 6) 내비메시 굽기 ----------
        var surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();
        string scenePath = EditorSceneManager.GetActiveScene().path;
        string dataDir = Path.Combine(Path.GetDirectoryName(scenePath), Path.GetFileNameWithoutExtension(scenePath)).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dataDir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'), Path.GetFileNameWithoutExtension(scenePath));
        string dataPath = dataDir + "/NavMesh-Floor1_Dungeon.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(dataPath) != null) AssetDatabase.DeleteAsset(dataPath);
        AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
        log.AppendLine("navmesh baked -> " + dataPath + " (verts " + UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length + ")");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    private static void AddRect(HashSet<Vector2Int> cells, int x, int z, int w, int d)
    {
        for (int ix = 0; ix < w; ix++)
            for (int iz = 0; iz < d; iz++)
                cells.Add(new Vector2Int(x + ix, z + iz));
    }

    private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, params Material[] materials)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
        return go;
    }

    // 바닥: 칸마다 위를 향한 사각형. UV는 월드 좌표 2m = 텍스처 1장.
    private static Mesh BuildFloorMesh(HashSet<Vector2Int> floor, System.Func<Vector2Int, Vector3> center)
    {
        var b = new MeshBuilder();
        float h = Cell / 2f;
        foreach (var c in floor)
        {
            Vector3 p = center(c);
            b.Quad(p + new Vector3(-h, 0, -h), p + new Vector3(-h, 0, h), p + new Vector3(h, 0, h), p + new Vector3(h, 0, -h), Vector3.up, WorldUV.XZ);
        }
        return b.ToMesh("DungeonFloor");
    }

    // 벽: 벽 칸마다 덩어리. 윗면은 항상, 옆면은 이웃이 벽 덩어리가 아닐 때만 만든다(안쪽 면 생략).
    private static Mesh BuildWallMesh(HashSet<Vector2Int> floor, HashSet<Vector2Int> solid, System.Func<Vector2Int, Vector3> center)
    {
        var b = new MeshBuilder();
        float h = Cell / 2f;
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.right, Vector2Int.left };
        foreach (var c in solid)
        {
            Vector3 p = center(c);
            Vector3 top = p + Vector3.up * WallHeight;
            b.Quad(top + new Vector3(-h, 0, -h), top + new Vector3(-h, 0, h), top + new Vector3(h, 0, h), top + new Vector3(h, 0, -h), Vector3.up, WorldUV.XZ, 1);
            foreach (var d in dirs)
            {
                if (solid.Contains(c + d)) continue;
                Vector3 n = new Vector3(d.x, 0, d.y);
                Vector3 side = new Vector3(-d.y, 0, d.x); // 바깥에서 볼 때 오른쪽
                Vector3 baseCenter = p + n * h;
                Vector3 a0 = baseCenter - side * h, a1 = baseCenter + side * h;
                // 바깥(바닥 쪽)에서 봤을 때 시계방향이 되도록: 왼아래 -> 왼위 -> 오른위 -> 오른아래
                b.Quad(a0, a0 + Vector3.up * WallHeight, a1 + Vector3.up * WallHeight, a1, n, d.x != 0 ? WorldUV.ZY : WorldUV.XY);
            }
        }
        return b.ToMesh("DungeonWalls");
    }

    private enum WorldUV { XZ, XY, ZY }

    private class MeshBuilder
    {
        private readonly List<Vector3> v = new List<Vector3>();
        private readonly List<Vector3> n = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int>[] t = { new List<int>(), new List<int>() }; // 0: 기본, 1: 벽 윗면

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, WorldUV mode, int submesh = 0)
        {
            int i = v.Count;
            foreach (var p in new[] { a, b, c, d })
            {
                v.Add(p); n.Add(normal);
                Vector2 w = mode == WorldUV.XZ ? new Vector2(p.x, p.z) : mode == WorldUV.XY ? new Vector2(p.x, p.y) : new Vector2(p.z, p.y);
                uv.Add(w / Cell);
            }
            t[submesh].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = t[1].Count > 0 ? 2 : 1;
            m.SetTriangles(t[0], 0);
            if (t[1].Count > 0) m.SetTriangles(t[1], 1);
            m.RecalculateBounds(); m.RecalculateTangents();
            string path = $"Assets/Models/Generated/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }

    // ---------- 어두운 벽돌 재질 (텍스처를 직접 생성) ----------
    public static Material GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;

        GenerateBrickTextures();
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
        mat.SetFloat("_BumpScale", 1.2f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Smoothness", 0.12f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }

    private static Material GetOrCreateTopMaterial(Material baseMaterial)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(TopMaterialPath);
        if (mat != null) return mat;
        mat = new Material(baseMaterial);
        mat.SetColor("_BaseColor", new Color(0.42f, 0.42f, 0.46f)); // 같은 벽돌을 훨씬 어둡게
        AssetDatabase.CreateAsset(mat, TopMaterialPath);
        return mat;
    }

    // 텍스처 1장 = 2m x 2m. 벽돌 0.5m x 0.25m (4개 x 8줄), 줄마다 반 장씩 어긋나게. 이음매 없이 반복된다.
    private static void GenerateBrickTextures()
    {
        const int N = 1024, cols = 4, rows = 8;
        var height = new float[N * N];
        var albedo = new Color32[N * N];
        Color mortar = new Color(0.055f, 0.05f, 0.048f);

        for (int y = 0; y < N; y++)
        {
            float v = (y + 0.5f) / N;
            int row = Mathf.FloorToInt(v * rows);
            float fy = v * rows - row;
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N;
                float us = u * cols + ((row & 1) == 1 ? 0.5f : 0f);
                int col = Mathf.FloorToInt(us);
                float fx = us - col;
                int colWrapped = ((col % cols) + cols) % cols;
                int brickId = row * 31 + colWrapped * 7;

                // 벽돌 가장자리까지 거리(m) -> 줄눈 + 모서리 깎임
                float ex = Mathf.Min(fx, 1f - fx) * (2f / cols);
                float ey = Mathf.Min(fy, 1f - fy) * (2f / rows);
                float chip = Fbm(u * 24f, v * 24f, 24, 3, brickId) * 0.012f;
                float e = Mathf.Min(ex, ey) - chip;
                float bevel = Smooth(0.006f, 0.03f, e);

                float grain = Fbm(u * 32f, v * 32f, 32, 4, 11);
                float stain = Fbm(u * 6f, v * 6f, 6, 3, 23);
                float r = Hash(brickId, 3), g2 = Hash(brickId, 5);
                Color brick = Color.Lerp(new Color(0.15f, 0.14f, 0.135f), new Color(0.2f, 0.17f, 0.15f), r);           // 어두운 회갈색
                brick = Color.Lerp(brick, new Color(0.12f, 0.13f, 0.15f), g2 * 0.5f);                                  // 일부는 푸른 회색
                brick *= 0.72f + 0.55f * grain;
                brick *= Mathf.Lerp(0.75f, 1.05f, stain);

                Color c = Color.Lerp(mortar * (0.8f + 0.4f * grain), brick, bevel);
                albedo[y * N + x] = (Color32)new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
                height[y * N + x] = bevel * (0.75f + 0.25f * grain);
            }
        }

        var normals = new Color32[N * N];
        const float strength = 6f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float hl = height[y * N + (x - 1 + N) % N], hr = height[y * N + (x + 1) % N];
                float hd = height[((y - 1 + N) % N) * N + x], hu = height[((y + 1) % N) * N + x];
                Vector3 nrm = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                normals[y * N + x] = new Color32((byte)((nrm.x * 0.5f + 0.5f) * 255), (byte)((nrm.y * 0.5f + 0.5f) * 255), (byte)((nrm.z * 0.5f + 0.5f) * 255), 255);
            }

        SavePng(AlbedoPath, albedo, N, false);
        SavePng(NormalPath, normals, N, true);
    }

    private static void SavePng(string path, Color32[] px, int n, bool normalMap)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
    }

    private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    private static float Hash(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    // 주기적(타일링되는) 값 노이즈
    private static float ValueNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float Corner(int cx, int cy) => Hash(((cx % period) + period) % period + seed * 1013, ((cy % period) + period) % period);
        float a = Corner(x0, y0), b = Corner(x0 + 1, y0), c = Corner(x0, y0 + 1), d = Corner(x0 + 1, y0 + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Fbm(float x, float y, int period, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(x, y, period, seed + o) * amp;
            norm += amp; amp *= 0.5f; x *= 2f; y *= 2f; period *= 2;
        }
        return sum / norm;
    }
}
