using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 플레이어 달리기 클립 점검: 몸이 이동 방향에서 몇 도 틀어져 있는지, 루프 이음매(끝 -> 처음)가 얼마나 튀는지 측정한다.
public static class RunClipTools
{
    private const string ModelPath = "Assets/Models/Player/PlayerDiablo_Run.glb";
    public const string ClipPath = "Assets/Animations/PlayerDiablo_Run_Looped.anim";

    private static Dictionary<string, Transform> FindBones(GameObject root)
    {
        var map = new Dictionary<string, Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            foreach (string bone in MonsterSwingRetargeter.Bones)
                if (t.name == bone && !map.ContainsKey(bone)) map[bone] = t;
        return map;
    }

    // 몸 정면(허벅지 선/어깨 선에 수직)이 루트 정면에서 몇 도 틀어져 있는지
    private static float BodyYaw(Dictionary<string, Transform> b, Transform root)
    {
        float Measure(Transform right, Transform left)
        {
            Vector3 fwd = Vector3.Cross(right.position - left.position, Vector3.up);
            fwd.y = 0f;
            return Vector3.SignedAngle(root.forward, fwd, Vector3.up);
        }
        return (Measure(b["RightUpLeg"], b["LeftUpLeg"]) + Measure(b["RightShoulder"], b["LeftShoulder"])) * 0.5f;
    }

    // 클립이 애니메이션하는 트랜스폼들의 프레임별 자세를 샘플링해 루프하기 좋은 구간을 찾고(apply면) 클립을 다시 쓴다.
    // 같은 에셋(GUID)에 덮어써서 컨트롤러 연결이 유지된다.
    public static string FixRunClip(bool apply, int minLoopFrames = 40, float crossfadeFrames = 4f)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var sb = new StringBuilder();
        try
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            SceneManager.MoveGameObjectToScene(go, scene);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var bones = FindBones(go);

            // 클립 바인딩: 경로별로 어떤 속성(회전/위치/스케일)이 애니메이션되는지
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var pathProps = new Dictionary<string, HashSet<string>>();
            foreach (var b in bindings)
            {
                if (b.type != typeof(Transform)) continue;
                string prop = b.propertyName.Substring(0, b.propertyName.LastIndexOf('.'));
                if (!pathProps.ContainsKey(b.path)) pathProps[b.path] = new HashSet<string>();
                pathProps[b.path].Add(prop);
            }
            var paths = new List<string>(pathProps.Keys);
            var targets = new List<Transform>();
            foreach (string p in paths) targets.Add(p == "" ? go.transform : go.transform.Find(p));
            sb.AppendLine($"animated transforms={paths.Count}, props sample: {string.Join(",", pathProps[paths[0]])}");

            float dt = 1f / 60f;
            int n = Mathf.RoundToInt(clip.length / dt);
            var rot = new Quaternion[n + 1][];
            var pos = new Vector3[n + 1][];
            var scl = new Vector3[n + 1][];
            var yaws = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                clip.SampleAnimation(go, Mathf.Min(i * dt, clip.length));
                rot[i] = new Quaternion[paths.Count]; pos[i] = new Vector3[paths.Count]; scl[i] = new Vector3[paths.Count];
                for (int k = 0; k < paths.Count; k++)
                {
                    rot[i][k] = targets[k].localRotation; pos[i][k] = targets[k].localPosition; scl[i][k] = targets[k].localScale;
                }
                yaws[i] = BodyYaw(bones, go.transform);
            }

            float Pose(int a, int b)
            {
                float worst = 0f;
                for (int k = 0; k < paths.Count; k++) worst = Mathf.Max(worst, Quaternion.Angle(rot[a][k], rot[b][k]));
                return worst;
            }

            // 루프 구간 [s, e]: 끝 N프레임을 '시작 직전 N프레임'으로 서서히 바꿔 이음매를 없앤다.
            // 섞을 때 원래 동작에서 벗어나는 정도(가중 평균 각도)가 가장 작은 구간을 고른다.
            int N = Mathf.Max(1, Mathf.RoundToInt(crossfadeFrames));
            var cands = new List<(float cost, int s, int e)>();
            for (int s = N; s < n; s++)
                for (int e = s + minLoopFrames; e <= n; e++)
                {
                    float cost = 0f;
                    for (int j = 1; j <= N; j++) cost += Mathf.SmoothStep(0f, 1f, (float)j / N) * Pose(e - N + j, s - N + j);
                    cands.Add((cost / N, s, e));
                }
            cands.Sort((x, y) => x.cost.CompareTo(y.cost));
            for (int i = 0; i < 5 && i < cands.Count; i++) sb.AppendLine($"candidate cost={cands[i].cost:F1} s={cands[i].s} e={cands[i].e} len={cands[i].e - cands[i].s}");
            float bestCost = cands[0].cost;
            int bestS = cands[0].s, bestE = cands[0].e;
            foreach (var cd in cands) if (cd.cost <= bestCost + 1.5f && cd.e - cd.s > bestE - bestS) { bestS = cd.s; bestE = cd.e; }
            sb.AppendLine($"chosen s={bestS} e={bestE} frames={bestE - bestS} blend N={N} (uncorrected seam diff {Pose(bestS, bestE):F1} deg)");

            float yawAvg = 0f;
            for (int i = bestS; i < bestE; i++) yawAvg += yaws[i];
            yawAvg /= (bestE - bestS);
            sb.AppendLine($"segment body yaw avg={yawAvg:F2} -> baked as {-yawAvg:F2}");

            if (!apply) return sb.ToString();

            // 새 키: s..e (마지막 키는 첫 키와 같아지도록 끝 몇 프레임을 첫 자세로 섞는다)
            int m = bestE - bestS;
            Quaternion yawFix = Quaternion.AngleAxis(-yawAvg, Vector3.up);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            clip.ClearCurves();

            for (int k = 0; k < paths.Count; k++)
            {
                bool hasRot = pathProps[paths[k]].Contains("m_LocalRotation");
                bool hasPos = pathProps[paths[k]].Contains("m_LocalPosition");
                bool hasScl = pathProps[paths[k]].Contains("m_LocalScale");
                bool isTop = paths[k] == "Root/Hips"; // 몸 전체를 돌리는 최상위 애니메이션 뼈
                var qc = new AnimationCurve[4]; var pc = new AnimationCurve[3]; var sc = new AnimationCurve[3];
                for (int i = 0; i < 4; i++) qc[i] = new AnimationCurve();
                for (int i = 0; i < 3; i++) { pc[i] = new AnimationCurve(); sc[i] = new AnimationCurve(); }

                for (int j = 0; j <= m; j++)
                {
                    int f = bestS + j;
                    // 끝 N프레임: 시작 직전 동작(s-N+jj)으로 섞어서 마지막 키가 첫 키(s)와 정확히 같아진다
                    int jj = j - (m - N);
                    float w = jj > 0 ? Mathf.SmoothStep(0f, 1f, (float)jj / N) : 0f;
                    int pre = jj > 0 ? bestS - N + jj : f;
                    Quaternion q = Quaternion.Slerp(rot[f][k], rot[pre][k], w);
                    Vector3 p = Vector3.Lerp(pos[f][k], pos[pre][k], w);
                    Vector3 s = Vector3.Lerp(scl[f][k], scl[pre][k], w);
                    if (isTop) { q = yawFix * q; p = yawFix * p; }
                    float t = j * dt;
                    qc[0].AddKey(t, q.x); qc[1].AddKey(t, q.y); qc[2].AddKey(t, q.z); qc[3].AddKey(t, q.w);
                    pc[0].AddKey(t, p.x); pc[1].AddKey(t, p.y); pc[2].AddKey(t, p.z);
                    sc[0].AddKey(t, s.x); sc[1].AddKey(t, s.y); sc[2].AddKey(t, s.z);
                }
                string[] qp = { "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
                string[] vp = { "localPosition.x", "localPosition.y", "localPosition.z" };
                string[] sp = { "localScale.x", "localScale.y", "localScale.z" };
                if (hasRot) for (int i = 0; i < 4; i++) clip.SetCurve(paths[k], typeof(Transform), qp[i], qc[i]);
                if (hasPos) for (int i = 0; i < 3; i++) clip.SetCurve(paths[k], typeof(Transform), vp[i], pc[i]);
                if (hasScl) for (int i = 0; i < 3; i++) clip.SetCurve(paths[k], typeof(Transform), sp[i], sc[i]);
            }
            clip.EnsureQuaternionContinuity();
            settings.startTime = 0f;
            settings.stopTime = m * dt;
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            sb.AppendLine($"clip rewritten: {m} frames ({m * dt:F3}s)");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return sb.ToString();
    }

    public static string Analyze()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var sb = new StringBuilder();
        try
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            SceneManager.MoveGameObjectToScene(go, scene);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var bones = FindBones(go);
            sb.AppendLine($"bones {bones.Count}/{MonsterSwingRetargeter.Bones.Length} length={clip.length:F3} frames@60={Mathf.RoundToInt(clip.length * 60)}");

            float dt = 1f / 60f;
            int n = Mathf.RoundToInt(clip.length / dt);
            var yaws = new List<float>();
            var rots = new List<Dictionary<string, Quaternion>>();
            var hips = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                clip.SampleAnimation(go, Mathf.Min(i * dt, clip.length));
                yaws.Add(BodyYaw(bones, go.transform));
                hips.Add(bones["Hips"].position);
                var r = new Dictionary<string, Quaternion>();
                foreach (var kv in bones) r[kv.Key] = kv.Value.localRotation;
                rots.Add(r);
            }

            float avg = 0f, min = float.MaxValue, max = float.MinValue;
            foreach (float y in yaws) { avg += y; min = Mathf.Min(min, y); max = Mathf.Max(max, y); }
            avg /= yaws.Count;
            sb.AppendLine($"body yaw avg={avg:F1} min={min:F1} max={max:F1} (현재 런타임 보정 -80)");
            sb.AppendLine($"hips drift over clip: {(hips[n] - hips[0]).ToString("F3")}");

            // 루프 이음매: 마지막 -> 처음 프레임 회전 차이 vs 평소 프레임 간 차이
            float StepAngle(int a, int b)
            {
                float worst = 0f;
                foreach (var kv in rots[a]) worst = Mathf.Max(worst, Quaternion.Angle(kv.Value, rots[b][kv.Key]));
                return worst;
            }
            float typical = 0f;
            for (int i = 0; i < n; i++) typical += StepAngle(i, i + 1);
            typical /= n;
            string worstBone = "";
            float worstSeam = 0f;
            foreach (var kv in rots[n])
            {
                float a = Quaternion.Angle(kv.Value, rots[0][kv.Key]);
                if (a > worstSeam) { worstSeam = a; worstBone = kv.Key; }
            }
            sb.AppendLine($"loop seam (last->first): worst bone={worstBone} {worstSeam:F1} deg, max over bones={StepAngle(n, 0):F1} deg; typical per-frame max step={typical:F1} deg");
            sb.AppendLine($"hips seam delta={(hips[n] - hips[0]).magnitude:F3} m, yaw seam={yaws[n] - yaws[0]:F1} deg");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return sb.ToString();
    }
}
