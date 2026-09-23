using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private void Start()
    {
        if (PlayerHealth.Instance == null)
        {
            Debug.LogWarning("HealthBarUI: PlayerHealth.Instance를 찾을 수 없습니다.");
            return;
        }

        PlayerHealth.Instance.OnHealthChanged += HandleHealthChanged;
        HandleHealthChanged(PlayerHealth.Instance.CurrentHealth, PlayerHealth.Instance.MaxHealth);
    }

    private void OnDestroy()
    {
        if (PlayerHealth.Instance == null) return;

        PlayerHealth.Instance.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (fillImage == null || maxHealth <= 0) return;

        float ratio = Mathf.Clamp01((float)currentHealth / maxHealth);

        // ExpBarUI와 동일하게 fillAmount 대신 anchorMax.x로 폭을 조절 (스프라이트 여백 문제 회피)
        RectTransform rect = fillImage.rectTransform;
        Vector2 anchorMax = rect.anchorMax;
        anchorMax.x = ratio;
        rect.anchorMax = anchorMax;
    }
}
