using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// EEJANAI FreeSwordAnimations의 slash 클립(EEJANAIbot 리그용)을 몬스터 뼈대(Root/Hips/Spine/...)에 맞춰 옮긴다.
// 두 리그를 같은 기본 자세로 두고, 원본 뼈의 '기본 자세 대비 월드 회전 변화량'을 몬스터 뼈의 기본 월드 회전에 적용한다.
public static class MonsterSwingRetargeter
{
    private const string SourceModelPath = "Assets/EEJANAI_Team/Commons/Model/EEJANAIbot.fbx";
    private const string SourceClipFolder = "Assets/EEJANAI_Team/FreeSwordAnimations/Animations/";
    private const string SourcePrefix = "EEJANAIBot:";

    // 상위 뼈부터. 하체도 옮겨야 골반이 비틀릴 때 발이 같이 돌아가지 않고 자세를 잡는다.
    public static readonly string[] Bones =
    {
        "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
        "RightShoulder", "RightArm", "RightForeArm", "RightHand",
        "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
        "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
    };

    public class Rig
    {
        public GameObject root;
        public Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public Dictionary<string, Quaternion> restWorld = new Dictionary<string, Quaternion>();
        public Vector3 hipsRestPos;
        public float hipsRestHeight; // 루트 기준 골반 높이
    }

    // 한 시점의 몬스터 자세: 뼈별 로컬 회전 + 골반 로컬 위치(무게중심 이동)
    public class Pose
    {
        public Dictionary<string, Quaternion> rot = new Dictionary<string, Quaternion>();
        public Vector3 hipsLocalPos;
    }

    public static Rig LoadSource(Scene scene)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        var go = Object.Instantiate(model);
        SceneManager.MoveGameObjectToScene(go, scene);
        return BuildRig(go, SourcePrefix);
    }

    // 몬스터가 지정한 프레임에서 어떻게 보이는지 한 장의 이미지로 렌더링해 확인한다 (srcTimes: 원본 클립 시간)
    public static string RenderFrames(float[] srcTimes, string outPath, string srcClipName = "slash1")
    {
        var pru = new PreviewRenderUtility();
        try
        {
            var c = new Context();
            var srcInstance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath));
            pru.AddSingleGO(srcInstance);
            srcInstance.transform.position = new Vector3(100f, 0f, 0f); // 화면에 안 들어오게 멀리
            c.src = BuildRig(srcInstance, SourcePrefix);
            c.srcClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipFolder + srcClipName + ".anim");

            GameObject goblin = pru.InstantiatePrefabInScene(AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath));
            c.tgtModel = goblin.transform.Find("GoblinModel");
            c.tgt = BuildRig(c.tgtModel.gameObject, "");
            SetupWeapons(c, goblin.transform);
            InitRest(c);

            // 몬스터 뼈대에는 Animator가 있어서 렌더 때 덮어쓰지 않도록 끈다
            var animator = c.tgtModel.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            const int w = 360, h = 460;
            var strip = new Texture2D(w * srcTimes.Length, h, TextureFormat.RGB24, false);
            var log = new StringBuilder();
            for (int i = 0; i < srcTimes.Length; i++)
            {
                Pose pose = PoseAt(c, srcTimes[i]);
                log.AppendLine($"t={srcTimes[i]:F2} hipsWorld={c.tgt.bones["Hips"].position.ToString("F2")} hipsLocal={pose.hipsLocalPos.ToString("F2")} rFoot={c.tgt.bones["RightFoot"].position.ToString("F2")} club={c.club.position.ToString("F2")}");
                pru.camera.transform.position = goblin.transform.position + new Vector3(3.2f, 2.6f, 5.2f);
                pru.camera.transform.LookAt(goblin.transform.position + Vector3.up * 2.0f);
                pru.camera.fieldOfView = 40f;
                pru.camera.backgroundColor = new Color(0.25f, 0.27f, 0.3f);
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.lights[0].intensity = 1.1f;
                pru.BeginPreview(new Rect(0, 0, w, h), GUIStyle.none);
                pru.camera.Render();
                Texture result = pru.EndPreview();
                var rt = RenderTexture.GetTemporary(w, h, 24);
                Graphics.Blit(result, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                strip.ReadPixels(new Rect(0, 0, w, h), i * w, 0);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
            strip.Apply();
            System.IO.File.WriteAllBytes(outPath, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            return "rendered " + outPath + "\n" + log;
        }
        finally
        {
            pru.Cleanup();
        }
    }

    private static void SetupWeapons(Context c, Transform goblinRoot)
    {
        var swordGo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SwordObjPath));
        swordGo.name = "Sword";
        swordGo.transform.SetParent(c.src.bones["RightHand"], false);
        c.sword = swordGo.transform;
        c.srcClip.SampleAnimation(c.src.root, 0f);
        c.swordAxis = WeaponAxisLocal(FirstMesh(c.sword), c.sword.InverseTransformPoint(c.src.bones["RightHand"].position), out _);
        foreach (Transform t in goblinRoot.GetComponentsInChildren<Transform>(true)) if (t.name == "GoblinClub") c.club = t;
        c.clubAxis = WeaponAxisLocal(FirstMesh(c.club), c.club.InverseTransformPoint(c.tgt.bones["RightHand"].position), out _);
    }

    public static Rig BuildRig(GameObject root, string bonePrefix)
    {
        var rig = new Rig { root = root };
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string bone in Bones)
            {
                if (t.name == bonePrefix + bone && !rig.bones.ContainsKey(bone))
                {
                    rig.bones[bone] = t;
                    rig.restWorld[bone] = t.rotation;
                    if (bone == "Hips")
                    {
                        // 루트 기준 좌표로 기록해 두면 모델 루트가 나중에 옮겨져도 이동량을 올바로 계산할 수 있다
                        rig.hipsRestPos = root.transform.InverseTransformPoint(t.position);
                        rig.hipsRestHeight = t.position.y - root.transform.position.y;
                    }
                }
            }
        }
        return rig;
    }

    public static string PathTo(Transform bone, Transform root)
    {
        var parts = new List<string>();
        for (Transform t = bone; t != null && t != root; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    public const string GoblinPrefabPath = "Assets/Prefab/Enemies/MeleeEnemy.prefab";

    public static string DescribeRest(Rig rig, string label)
    {
        Transform Hips = rig.bones["Hips"], head = rig.bones["Head"], rs = rig.bones["RightShoulder"], ls = rig.bones["LeftShoulder"];
        Transform ra = rig.bones["RightArm"], rh = rig.bones["RightHand"], rfa = rig.bones["RightForeArm"];
        Vector3 up = (head.position - Hips.position).normalized;
        Vector3 right = (rs.position - ls.position).normalized;
        Vector3 fwd = Vector3.Cross(right, up).normalized; // 왼손 좌표계: right x up = forward
        return $"{label}: scale={rig.root.transform.lossyScale.x:F2} up={up.ToString("F2")} rightAxis={right.ToString("F2")} fwd={fwd.ToString("F2")} " +
               $"upperArmDir={(rfa.position - ra.position).normalized.ToString("F2")} foreArmDir={(rh.position - rfa.position).normalized.ToString("F2")} " +
               $"hipsH={Hips.position.y - rig.root.transform.position.y:F2} headH={head.position.y - rig.root.transform.position.y:F2}";
    }

    public static string DescribeWeapon(Transform root, string weaponName, Transform hand)
    {
        Transform w = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == weaponName) { w = t; break; }
        if (w == null) return $"weapon '{weaponName}' not found";
        var mf = w.GetComponentInChildren<MeshFilter>(true);
        var smr = w.GetComponentInChildren<SkinnedMeshRenderer>(true);
        string bounds = mf != null ? $"meshFilter bounds center={mf.sharedMesh.bounds.center.ToString("F2")} ext={mf.sharedMesh.bounds.extents.ToString("F2")} on={mf.name}"
                      : smr != null ? $"skinned bounds center={smr.sharedMesh.bounds.center.ToString("F2")} ext={smr.sharedMesh.bounds.extents.ToString("F2")} on={smr.name}" : "no mesh";
        bool underHand = false;
        for (Transform p = w; p != null; p = p.parent) if (p == hand) underHand = true;
        return $"weapon {weaponName}: path={PathTo(w, root)} underHand={underHand} scale={w.lossyScale.x:F2} {bounds}";
    }

    public static string DiagnoseRigs()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject goblinRoot = PrefabUtility.LoadPrefabContents(GoblinPrefabPath);
        try
        {
            Rig src = LoadSource(scene);
            Transform model = goblinRoot.transform.Find("GoblinModel");
            Rig tgt = BuildRig(model.gameObject, "");
            return DescribeRest(src, "SRC") + "\n" + DescribeRest(tgt, "GOBLIN") + $"\nbones goblin={tgt.bones.Count}/{Bones.Length} path RightHand={PathTo(tgt.bones["RightHand"], model)}"
                + "\n" + DescribeWeapon(src.root.transform, "Sword", src.bones["RightHand"])
                + "\n" + DescribeWeapon(goblinRoot.transform, "GoblinClub", tgt.bones["RightHand"]);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(goblinRoot);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private const string SwordObjPath = "Assets/EEJANAI_Team/FreeSwordAnimations/Models/SwordSample/Sword.obj";
    private const string WindupClipPath = "Assets/Animations/Goblin_LungeWindup.anim";
    private const string SwingClipPath = "Assets/Animations/Goblin_LungeSwing.anim";
    private const string ControllerPath = "Assets/Animations/Goblin_Animator.controller";

    // 손잡이에서 끝(머리)쪽으로 향하는, 메시 로컬 좌표계의 무기 축(길이 방향)
    // 손(손잡이)에서 더 멀리 뻗은 쪽을 끝으로 본다. grip은 무기 로컬 좌표계에서의 손 위치.
    private static Vector3 WeaponAxisLocal(Mesh mesh, Vector3 grip, out string info)
    {
        Bounds b = mesh.bounds;
        int axis = b.extents.x >= b.extents.y && b.extents.x >= b.extents.z ? 0 : b.extents.y >= b.extents.z ? 1 : 2;
        float min = b.center[axis] - b.extents[axis], max = b.center[axis] + b.extents[axis];
        float sign = (max - grip[axis]) >= (grip[axis] - min) ? 1f : -1f;
        info = $"axis={axis} range=[{min:F2},{max:F2}] grip={grip[axis]:F2} -> sign {sign}";
        Vector3 result = Vector3.zero;
        result[axis] = sign;
        return result;
    }

    private static Mesh FirstMesh(Transform t)
    {
        var mf = t.GetComponentInChildren<MeshFilter>(true);
        if (mf != null) return mf.sharedMesh;
        var smr = t.GetComponentInChildren<SkinnedMeshRenderer>(true);
        return smr != null ? smr.sharedMesh : null;
    }

    private class Context
    {
        public Rig src, tgt;
        public Transform sword, club;
        public Vector3 swordAxis, clubAxis;
        public AnimationClip srcClip;
        public Transform tgtModel;
        public Dictionary<string, Quaternion> restLocal = new Dictionary<string, Quaternion>();
        public Vector3 hipsRestLocalPos;
        public float yaw0;
    }

    private static void InitRest(Context c)
    {
        foreach (string bone in Bones) c.restLocal[bone] = c.tgt.bones[bone].localRotation;
        c.hipsRestLocalPos = c.tgt.bones["Hips"].localPosition;
        c.srcClip.SampleAnimation(c.src.root, 0f);
        c.yaw0 = ShoulderYaw(c.src);
    }

    private static float ShoulderYaw(Rig rig)
    {
        Vector3 line = rig.bones["RightShoulder"].position - rig.bones["LeftShoulder"].position;
        line.y = 0f;
        return Vector3.SignedAngle(Vector3.right, line, Vector3.up);
    }

    // 원본 클립을 srcTime에 재생한 모습을 몬스터 뼈대에 옮기고, 뼈별 로컬 회전을 돌려준다
    private static Pose PoseAt(Context c, float srcTime)
    {
        c.srcClip.SampleAnimation(c.src.root, srcTime);
        Quaternion q0 = Quaternion.AngleAxis(-c.yaw0, Vector3.up);

        foreach (string bone in Bones) c.tgt.bones[bone].localRotation = c.restLocal[bone];

        // 골반 이동(무게중심이 낮아지거나 앞으로 쏠리는 것): 원본 이동량을 두 몸의 골반 높이 비율로 줄이거나 늘려 적용
        Transform tHips = c.tgt.bones["Hips"];
        tHips.localPosition = c.hipsRestLocalPos;
        float ratio = c.tgt.hipsRestHeight / Mathf.Max(0.01f, c.src.hipsRestHeight);
        Vector3 srcShift = c.src.root.transform.InverseTransformPoint(c.src.bones["Hips"].position) - c.src.hipsRestPos;
        Vector3 hipsShift = q0 * srcShift * ratio;
        tHips.position = c.tgt.root.transform.TransformPoint(c.tgt.hipsRestPos) + hipsShift;

        // 몸통: 원본의 기본 자세 대비 월드 회전 변화량을 몬스터의 기본 월드 회전에 적용
        foreach (string bone in new[] { "Hips", "Spine", "Spine1", "Spine2" })
        {
            Quaternion delta = q0 * c.src.bones[bone].rotation * Quaternion.Inverse(c.src.restWorld[bone]);
            c.tgt.bones[bone].rotation = delta * c.tgt.restWorld[bone];
        }

        foreach (string side in new[] { "Right", "Left" })
        {
            Transform arm = c.tgt.bones[side + "Arm"], fore = c.tgt.bones[side + "ForeArm"], hand = c.tgt.bones[side + "Hand"];
            Transform sArm = c.src.bones[side + "Arm"], sFore = c.src.bones[side + "ForeArm"], sHand = c.src.bones[side + "Hand"];

            Vector3 want = q0 * (sFore.position - sArm.position).normalized;
            arm.rotation = Quaternion.FromToRotation((fore.position - arm.position).normalized, want) * arm.rotation;

            want = q0 * (sHand.position - sFore.position).normalized;
            fore.rotation = Quaternion.FromToRotation((hand.position - fore.position).normalized, want) * fore.rotation;
        }

        // 다리: 허벅지 -> 정강이 -> 발 -> 발끝 방향을 원본에 맞춘다 (발이 골반 비틀림을 따라 돌아가지 않고 자세를 잡는다)
        foreach (string side in new[] { "Right", "Left" })
        {
            string[] chain = { side + "UpLeg", side + "Leg", side + "Foot", side + "ToeBase" };
            for (int i = 0; i < chain.Length - 1; i++)
            {
                Transform bone = c.tgt.bones[chain[i]], child = c.tgt.bones[chain[i + 1]];
                Vector3 want = q0 * (c.src.bones[chain[i + 1]].position - c.src.bones[chain[i]].position).normalized;
                bone.rotation = Quaternion.FromToRotation((child.position - bone.position).normalized, want) * bone.rotation;
            }
        }

        // 오른손: 곤봉 머리 방향을 검 끝 방향에 맞춘다
        Vector3 bladeWant = q0 * c.sword.TransformDirection(c.swordAxis).normalized;
        Transform rightHand = c.tgt.bones["RightHand"];
        rightHand.rotation = Quaternion.FromToRotation(c.club.TransformDirection(c.clubAxis).normalized, bladeWant) * rightHand.rotation;

        var result = new Pose { hipsLocalPos = tHips.localPosition };
        foreach (string bone in Bones) result.rot[bone] = c.tgt.bones[bone].localRotation;
        return result;
    }

    private static AnimationClip MakeClip(Context c, string name, List<(float time, Pose pose)> frames, float length)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f };
        foreach (string bone in Bones)
        {
            string path = PathTo(c.tgt.bones[bone], c.tgtModel);
            var curves = new AnimationCurve[4];
            for (int i = 0; i < 4; i++) curves[i] = new AnimationCurve();
            foreach (var (time, pose) in frames)
            {
                Quaternion q = pose.rot[bone];
                curves[0].AddKey(time, q.x); curves[1].AddKey(time, q.y); curves[2].AddKey(time, q.z); curves[3].AddKey(time, q.w);
            }
            string[] props = { "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
            for (int i = 0; i < 4; i++) clip.SetCurve(path, typeof(Transform), props[i], curves[i]);
        }

        string hipsPath = PathTo(c.tgt.bones["Hips"], c.tgtModel);
        var hx = new AnimationCurve(); var hy = new AnimationCurve(); var hz = new AnimationCurve();
        foreach (var (time, pose) in frames)
        {
            hx.AddKey(time, pose.hipsLocalPos.x); hy.AddKey(time, pose.hipsLocalPos.y); hz.AddKey(time, pose.hipsLocalPos.z);
        }
        clip.SetCurve(hipsPath, typeof(Transform), "localPosition.x", hx);
        clip.SetCurve(hipsPath, typeof(Transform), "localPosition.y", hy);
        clip.SetCurve(hipsPath, typeof(Transform), "localPosition.z", hz);
        clip.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    // slash1: 0~windupEnd 는 무기를 머리 위로 들어올리는 준비 동작, 그 뒤는 베는 동작.
    public static string Build(string srcClipName = "slash1", float windupEnd = 0.3333f, float recoverTime = 0.3f)
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject goblinRoot = PrefabUtility.LoadPrefabContents(GoblinPrefabPath);
        try
        {
            var c = new Context();
            c.src = LoadSource(scene);
            c.srcClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipFolder + srcClipName + ".anim");
            c.tgtModel = goblinRoot.transform.Find("GoblinModel");
            c.tgt = BuildRig(c.tgtModel.gameObject, "");

            var swordGo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SwordObjPath));
            swordGo.name = "Sword";
            swordGo.transform.SetParent(c.src.bones["RightHand"], false);
            c.sword = swordGo.transform;
            // 검 위치/회전은 클립이 정한다. 0초 자세로 맞춘 뒤 손 위치를 검 로컬 좌표로 바꿔 끝 방향을 정한다
            c.srcClip.SampleAnimation(c.src.root, 0f);
            c.swordAxis = WeaponAxisLocal(FirstMesh(c.sword), c.sword.InverseTransformPoint(c.src.bones["RightHand"].position), out string swordInfo);

            foreach (Transform t in goblinRoot.GetComponentsInChildren<Transform>(true)) if (t.name == "GoblinClub") c.club = t;
            c.clubAxis = WeaponAxisLocal(FirstMesh(c.club), c.club.InverseTransformPoint(c.tgt.bones["RightHand"].position), out string clubInfo);
            sb.AppendLine($"sword {swordInfo} | club {clubInfo}");

            InitRest(c);
            sb.AppendLine($"stance yaw0={c.yaw0:F1} hipsHeight src={c.src.hipsRestHeight:F2} goblin={c.tgt.hipsRestHeight:F2}");

            // 점검용: 검 끝 방향(몸 기준)과 몬스터 곤봉 방향
            foreach (float t in new[] { 0f, windupEnd, 0.5f })
            {
                PoseAt(c, t);
                sb.AppendLine($"t={t:F2} bladeSrc={(Quaternion.AngleAxis(-c.yaw0, Vector3.up) * c.sword.TransformDirection(c.swordAxis)).ToString("F2")} clubTgt={c.club.TransformDirection(c.clubAxis).ToString("F2")} handTgtY={c.tgt.bones["RightHand"].position.y:F2}");
            }

            // 준비 동작: 0 ~ windupEnd
            var windup = new List<(float, Pose)>();
            for (float t = 0f; t < windupEnd; t += 1f / 60f) windup.Add((t, PoseAt(c, t)));
            windup.Add((windupEnd, PoseAt(c, windupEnd)));
            AnimationClip windupClip = MakeClip(c, "Goblin_LungeWindup", windup, windupEnd);

            // 휘두르기: windupEnd ~ 끝, 이어서 기본 자세로 돌아오는 구간
            float srcEnd = c.srcClip.length;
            var swing = new List<(float, Pose)>();
            for (float t = windupEnd; t < srcEnd; t += 1f / 60f) swing.Add((t - windupEnd, PoseAt(c, t)));
            Pose last = PoseAt(c, srcEnd);
            float swingLen = srcEnd - windupEnd;
            swing.Add((swingLen, last));
            for (float t = 1f / 60f; t <= recoverTime + 0.0001f; t += 1f / 60f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / recoverTime);
                var pose = new Pose { hipsLocalPos = Vector3.Lerp(last.hipsLocalPos, c.hipsRestLocalPos, k) };
                foreach (string bone in Bones) pose.rot[bone] = Quaternion.Slerp(last.rot[bone], c.restLocal[bone], k);
                swing.Add((swingLen + t, pose));
            }
            AnimationClip swingClip = MakeClip(c, "Goblin_LungeSwing", swing, swingLen + recoverTime);

            Save(windupClip, WindupClipPath);
            Save(swingClip, SwingClipPath);
            sb.AppendLine($"saved windup({windupEnd:F2}s) and swing({swingLen:F2}s + recover {recoverTime:F2}s)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(goblinRoot);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    // Goblin_Animator: 트리거 Windup -> Windup 상태(준비 동작 유지), 기존 Attack 트리거 -> Attack 상태의 클립을 새 휘두르기로 교체
    public static string ApplyToController()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
        var windupClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WindupClipPath);
        var swingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SwingClipPath);
        var sb = new StringBuilder();

        bool hasParam = false;
        foreach (var p in ctrl.parameters) if (p.name == "Windup") hasParam = true;
        if (!hasParam) ctrl.AddParameter("Windup", AnimatorControllerParameterType.Trigger);

        UnityEditor.Animations.AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        UnityEditor.Animations.AnimatorState attack = null, windup = null;
        foreach (var s in sm.states)
        {
            if (s.state.name == "Attack") attack = s.state;
            if (s.state.name == "Windup") windup = s.state;
        }

        attack.motion = swingClip;
        attack.speed = 1f;
        foreach (var t in attack.transitions) { t.exitTime = 0.98f; t.duration = 0.1f; }

        if (windup == null)
        {
            windup = sm.AddState("Windup");
            var anyToWindup = sm.AddAnyStateTransition(windup);
            anyToWindup.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Windup");
            anyToWindup.hasExitTime = false;
            anyToWindup.hasFixedDuration = true;
            anyToWindup.duration = 0.1f;
            anyToWindup.canTransitionToSelf = false;
        }
        windup.motion = windupClip;
        windup.speed = 1f;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        sb.AppendLine($"controller updated: Windup state -> {windupClip.name}, Attack state -> {swingClip.name}");
        return sb.ToString();
    }

    private static void Save(AnimationClip clip, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
    }

    public static string Analyze(string clipName)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var sb = new StringBuilder();
        try
        {
            Rig src = LoadSource(scene);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipFolder + clipName + ".anim");
            sb.AppendLine($"clip {clipName} length={clip.length} bones={src.bones.Count}/{Bones.Length}");
            Transform hips = src.bones["Hips"], hand = src.bones["RightHand"], spine2 = src.bones["Spine2"];
            for (float t = 0f; t <= clip.length + 0.0001f; t += 0.0833f)
            {
                clip.SampleAnimation(src.root, t);
                // 몸(Hips) 기준 좌표: x 오른쪽, y 위, z 앞
                Vector3 local = Quaternion.Inverse(hips.rotation) * (hand.position - hips.position);
                Vector3 hipsFwd = hips.rotation * Vector3.forward;
                sb.AppendLine($"t={t:F2} handRelHips={local.ToString("F2")} hipsYaw={Quaternion.LookRotation(Vector3.ProjectOnPlane(hipsFwd, Vector3.up)).eulerAngles.y:F0} handWorldY={hand.position.y:F2} hipsY={hips.position.y:F2}");
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return sb.ToString();
    }
}
