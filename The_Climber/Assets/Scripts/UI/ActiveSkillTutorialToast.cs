using UnityEngine;

// 플레이어가 처음으로 액티브 스킬을 획득한 순간 한 번만 사용법을 알려주는 토스트
public class ActiveSkillTutorialToast : MonoBehaviour
{
    [SerializeField] private PlayerSkillManager skillManager;
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private float displayDuration = 6f;

    private float hideTime = -1f;

    private void Awake()
    {
        if (toastRoot != null) toastRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (skillManager != null) skillManager.OnFirstActiveSkillAcquired += HandleFirstActiveSkill;
    }

    private void OnDisable()
    {
        if (skillManager != null) skillManager.OnFirstActiveSkillAcquired -= HandleFirstActiveSkill;
    }

    private void Update()
    {
        if (hideTime < 0f) return;

        // 일시정지 중에도 시간이 흘러야 확인하고 창 닫자마자 사라지지 않는다
        if (Time.unscaledTime >= hideTime)
        {
            hideTime = -1f;
            if (toastRoot != null) toastRoot.SetActive(false);
        }
    }

    private void HandleFirstActiveSkill()
    {
        if (toastRoot == null) return;

        toastRoot.SetActive(true);
        hideTime = Time.unscaledTime + displayDuration;
    }
}
