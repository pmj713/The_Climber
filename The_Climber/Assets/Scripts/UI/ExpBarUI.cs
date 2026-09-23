using UnityEngine;
using UnityEngine.UI;

public class ExpBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Text levelText;
    [SerializeField] private Color levelUpFlashColor = Color.yellow;
    [SerializeField] private float levelUpFlashDuration = 0.3f;

    private Color originalTextColor;
    private float flashTimer;

    private void Awake()
    {
        if (levelText != null) originalTextColor = levelText.color;
    }

    private void Start()
    {
        if (PlayerLevel.Instance == null)
        {
            Debug.LogWarning("ExpBarUI: PlayerLevel.Instance를 찾을 수 없습니다.");
            return;
        }

        PlayerLevel.Instance.OnExpChanged += HandleExpChanged;
        PlayerLevel.Instance.OnLevelUp += HandleLevelUp;
        PlayerLevel.Instance.OnLevelReset += UpdateLevelText;

        HandleExpChanged(PlayerLevel.Instance.CurrentExp, PlayerLevel.Instance.ExpToNextLevel);
        UpdateLevelText(PlayerLevel.Instance.Level);
    }

    private void OnDestroy()
    {
        if (PlayerLevel.Instance == null) return;

        PlayerLevel.Instance.OnExpChanged -= HandleExpChanged;
        PlayerLevel.Instance.OnLevelUp -= HandleLevelUp;
        PlayerLevel.Instance.OnLevelReset -= UpdateLevelText;
    }

    private void Update()
    {
        if (flashTimer <= 0f || levelText == null) return;

        flashTimer -= Time.deltaTime;
        levelText.color = flashTimer > 0f ? levelUpFlashColor : originalTextColor;
    }

    private void HandleExpChanged(int currentExp, int expToNextLevel)
    {
        if (fillImage == null || expToNextLevel <= 0) return;

        float ratio = Mathf.Clamp01((float)currentExp / expToNextLevel);

        // Image.fillAmount 대신 anchorMax.x로 실제 폭을 조절 - 빌트인 스프라이트의
        // 여백 때문에 fillAmount로는 양옆에 빈 공간이 남는 문제를 피할 수 있다.
        RectTransform rect = fillImage.rectTransform;
        Vector2 anchorMax = rect.anchorMax;
        anchorMax.x = ratio;
        rect.anchorMax = anchorMax;
    }

    private void HandleLevelUp(int newLevel)
    {
        UpdateLevelText(newLevel);
        flashTimer = levelUpFlashDuration;
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null) levelText.text = $"Lv. {level}";
    }
}
