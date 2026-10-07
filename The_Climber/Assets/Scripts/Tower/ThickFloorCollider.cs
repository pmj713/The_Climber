using System.Collections.Generic;
using UnityEngine;

// 던전 바닥 메시는 두께 0짜리 평면이라, 몬스터에 둘러싸여 CharacterController가 아래로 밀리면
// 평면을 통과해 추락한다. 같은 메시를 아래로 압출한 두꺼운 판으로 콜라이더만 교체한다.
[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class ThickFloorCollider : MonoBehaviour
{
    [SerializeField] private float thickness = 2f;

    private void Awake()
    {
        Mesh source = GetComponent<MeshFilter>().sharedMesh;
        if (source == null || !source.isReadable) return;
        GetComponent<MeshCollider>().sharedMesh = BuildSlab(source, thickness);
    }

    private static Mesh BuildSlab(Mesh source, float depth)
    {
        Vector3[] sv = source.vertices;
        int[] st = source.triangles;
        Vector3 down = Vector3.down * depth;

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        // 위쪽 면은 그대로, 아래쪽 면은 뒤집어서 depth만큼 내린다
        for (int t = 0; t < st.Length; t += 3)
        {
            Vector3 a = sv[st[t]], b = sv[st[t + 1]], c = sv[st[t + 2]];
            AddTri(a, b, c);
            AddTri(c + down, b + down, a + down);
        }

        // 한 번만 쓰인 모서리 = 바닥 바깥 경계. 여기에만 옆면을 만든다
        var edgeCount = new Dictionary<(Vector3Int, Vector3Int), int>();
        var edgeInfo = new Dictionary<(Vector3Int, Vector3Int), (Vector3 a, Vector3 b, Vector3 centroid)>();
        for (int t = 0; t < st.Length; t += 3)
        {
            Vector3 centroid = (sv[st[t]] + sv[st[t + 1]] + sv[st[t + 2]]) / 3f;
            for (int e = 0; e < 3; e++)
            {
                Vector3 a = sv[st[t + e]], b = sv[st[t + (e + 1) % 3]];
                var key = EdgeKey(a, b);
                edgeCount[key] = edgeCount.TryGetValue(key, out int n) ? n + 1 : 1;
                edgeInfo[key] = (a, b, centroid);
            }
        }

        foreach (var kv in edgeCount)
        {
            if (kv.Value != 1) continue;
            var (a, b, centroid) = edgeInfo[kv.Key];
            Vector3 away = (a + b) * 0.5f - centroid;
            away.y = 0f;
            if (Vector3.Dot(Vector3.Cross(b - a, b + down - a), away) < 0f) { (a, b) = (b, a); }
            AddTri(a, b, b + down);
            AddTri(a, b + down, a + down);
        }

        var mesh = new Mesh { name = "ThickFloorSlab", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static (Vector3Int, Vector3Int) EdgeKey(Vector3 a, Vector3 b)
    {
        Vector3Int ka = Vector3Int.RoundToInt(a * 100f), kb = Vector3Int.RoundToInt(b * 100f);
        bool aFirst = ka.x != kb.x ? ka.x < kb.x : ka.y != kb.y ? ka.y < kb.y : ka.z <= kb.z;
        return aFirst ? (ka, kb) : (kb, ka);
    }
}
