using UnityEngine;

// 화상/냉기 스택을 몬스터 머리 위 배지로 보여준다. 상태이상이 없으면 숨긴다.
[RequireComponent(typeof(EnemyStatusEffects))]
public class EnemyStatusIndicator : MonoBehaviour
{
    [SerializeField] private StatusStackIndicatorView viewPrefab;
    [SerializeField] private float headClearance = 0.35f; // 몸 콜라이더 꼭대기에서 띄우는 높이

    private EnemyStatusEffects status;
    private EnemyHealth health;
    private StatusStackIndicatorView view;
    private Transform cameraTransform;

    private void Awake()
    {
        status = GetComponent<EnemyStatusEffects>();
        health = GetComponent<EnemyHealth>();
        if (viewPrefab == null) return;

        float height = 2f;
        if (TryGetComponent<Collider>(out var body)) height = body.bounds.max.y - transform.position.y;

        view = Instantiate(viewPrefab, transform);
        view.transform.localPosition = Vector3.up * (height + headClearance);
        view.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (view == null) return;

        bool dead = health != null && health.IsDead;
        EnemyStatusEffects.StatusType type = status.ActiveStatus;
        int stacks = type == EnemyStatusEffects.StatusType.Burn ? status.BurnStacks : status.FreezeStacks;
        bool visible = !dead && type != EnemyStatusEffects.StatusType.None && (stacks > 0 || status.IsFrozen);

        if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
        if (!visible) return;

        float ratio = type == EnemyStatusEffects.StatusType.Burn ? status.BurnStackRatio : status.FreezeStackRatio;
        view.Show(type, stacks, ratio, status.IsFrozen);

        // 항상 카메라를 정면으로 바라보게
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (cameraTransform != null) view.transform.rotation = cameraTransform.rotation;
    }
}
