using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Floor1 씬을 복사해 Floor2~Floor5 씬을 만들고, 층마다 던전 크기·몬스터 구성·난이도 배율·다음 층 문의 목적지를 설정한다.
// 이미 만들어진 층 씬이 있어도 같은 설정으로 다시 만든다 (Tools > Floor2~5 씬 만들기).
public static class FloorSceneGenerator
{
    private const string SceneDir = "Assets/Scenes/";
    private const string Floor1Path = SceneDir + "Floor1.unity";
    private const string EnemyDir = "Assets/Prefab/Enemies/";

    private class Spec
    {
        public int number;
        public int roomWidth, roomDepth;    // 칸 (1칸 = 2m)
        public float health, damage;        // 층 난이도 배율
        public int melee, ranged;
        public string[] elites = { };       // 마지막 일반 웨이브에 섞는 엘리트 프리팹 이름 (각 1마리씩 count번)
        public int eliteCount;
        public string boss;                 // 마지막에 단독 웨이브로 소환할 보스 프리팹 이름
        public string nextScene;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { number = 2, roomWidth = 20, roomDepth = 20, health = 1.25f, damage = 1.15f, melee = 20, ranged = 8,  nextScene = "Floor3" },
        new Spec { number = 3, roomWidth = 22, roomDepth = 22, health = 1.5f,  damage = 1.3f,  melee = 18, ranged = 16, nextScene = "Floor4" },
        new Spec { number = 4, roomWidth = 24, roomDepth = 24, health = 1.8f,  damage = 1.45f, melee = 16, ranged = 12,
                   elites = new[] { "EliteOrcWarrior", "EliteGoblinShaman" }, eliteCount = 2, nextScene = "Floor5" },
        new Spec { number = 5, roomWidth = 24, roomDepth = 24, health = 2.1f,  damage = 1.6f,  melee = 12, ranged = 8,
                   boss = "BossOrcWarlord", nextScene = "Village" },
    };

    [MenuItem("Tools/Floor2~5 씬 만들기")]
    private static void Menu()
    {
        if (!EditorUtility.DisplayDialog("Floor2~5 씬 만들기",
                "Floor1을 복사해 Floor2~Floor5 씬을 만들고(이미 있으면 같은 설정으로 다시 만들고), Floor1의 다음 층 문이 Floor2로 가게 합니다. 열려 있는 씬은 저장됩니다.", "만들기", "취소"))
            return;
        Debug.Log(Generate());
    }

    public static string Generate()
    {
        var sb = new StringBuilder();
        EditorSceneManager.SaveOpenScenes();

        // Floor1: 다음 층 문 -> Floor2
        EditorSceneManager.OpenScene(Floor1Path, OpenSceneMode.Single);
        sb.AppendLine("Floor1: " + SetDoorDestination("Floor2"));
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (Spec spec in Specs)
        {
            string path = $"{SceneDir}Floor{spec.number}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null && !AssetDatabase.CopyAsset(Floor1Path, path))
            {
                sb.AppendLine($"Floor{spec.number}: 씬 복사 실패");
                continue;
            }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine($"Floor{spec.number}: " + Configure(spec));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            if (!buildScenes.Exists(s => s.path == path)) buildScenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();

        EditorSceneManager.OpenScene(Floor1Path, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    private static string Configure(Spec spec)
    {
        var layout = DungeonChainBuilder.BigRoomLayout;
        layout.roomWidth = spec.roomWidth;
        layout.roomDepth = spec.roomDepth;
        string rebuildLog = DungeonChainBuilder.Rebuild(layout);

        var manager = Object.FindFirstObjectByType<FloorManager>(FindObjectsInactive.Include);
        var managerSo = new SerializedObject(manager);
        managerSo.FindProperty("floorNumber").intValue = spec.number;
        managerSo.FindProperty("healthMultiplier").floatValue = spec.health;
        managerSo.FindProperty("damageMultiplier").floatValue = spec.damage;
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        var encounter = Object.FindFirstObjectByType<RoomEncounter>(FindObjectsInactive.Include);
        var so = new SerializedObject(encounter);
        so.FindProperty("meleeCount").intValue = spec.melee;
        so.FindProperty("rangedCount").intValue = spec.ranged;

        var extras = new List<(GameObject prefab, int count, bool ownWave)>();
        foreach (string elite in spec.elites)
            extras.Add((AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyDir}{elite}.prefab"), spec.eliteCount, false));
        if (!string.IsNullOrEmpty(spec.boss))
            extras.Add((AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyDir}{spec.boss}.prefab"), 1, true));
        SerializedProperty extraProp = so.FindProperty("extraSpawns");
        extraProp.arraySize = extras.Count;
        for (int i = 0; i < extras.Count; i++)
        {
            SerializedProperty el = extraProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("prefab").objectReferenceValue = extras[i].prefab;
            el.FindPropertyRelative("count").intValue = extras[i].count;
            el.FindPropertyRelative("ownWave").boolValue = extras[i].ownWave;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        string door = SetDoorDestination(spec.nextScene);
        return $"melee {spec.melee}, ranged {spec.ranged}, extras {extras.Count}, hp x{spec.health}, dmg x{spec.damage}, door -> {door}\n{rebuildLog}";
    }

    // '모든 방을 정리해야 열리는' 다음 층 문(TowerEntrance)의 목적지 씬을 바꾼다
    private static string SetDoorDestination(string scene)
    {
        foreach (var entrance in Object.FindObjectsByType<TowerEntrance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(entrance);
            if (!so.FindProperty("requireFloorCleared").boolValue) continue;
            so.FindProperty("destinationScene").stringValue = scene;
            so.ApplyModifiedPropertiesWithoutUndo();
            return scene;
        }
        return "(다음 층 문 없음)";
    }
}
