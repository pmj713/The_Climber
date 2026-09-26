using UnityEngine;
using UnityEngine.UI;

// 사운드 설정 패널의 내용(마스터 음량 슬라이더)을 코드로 생성한다.
public class SoundSettingsPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform content;

    private Slider volumeSlider;
    private Text valueLabel;
    private Font uiFont;

    private void Awake()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildContent();
    }

    private void OnEnable()
    {
        if (volumeSlider != null) volumeSlider.value = SoundSettingsManager.MasterVolume;
    }

    private void BuildContent()
    {
        if (content == null) return;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 10f;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject row = new GameObject("MasterVolumeRow", typeof(RectTransform));
        row.transform.SetParent(content, false);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        row.AddComponent<LayoutElement>().preferredHeight = 40f;

        Text label = CreateText(row.transform, "마스터 음량", 16);
        label.GetComponent<LayoutElement>().preferredWidth = 110f;

        GameObject sliderObj = new GameObject("MasterVolumeSlider", typeof(RectTransform));
        sliderObj.transform.SetParent(row.transform, false);
        sliderObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

        GameObject background = new GameObject("Background", typeof(RectTransform));
        background.transform.SetParent(sliderObj.transform, false);
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.3f, 0.6f, 1f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.sizeDelta = new Vector2(16f, 0f);

        volumeSlider = sliderObj.AddComponent<Slider>();
        volumeSlider.fillRect = fillRect;
        volumeSlider.handleRect = handleRect;
        volumeSlider.targetGraphic = handleImage;
        volumeSlider.direction = Slider.Direction.LeftToRight;
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = SoundSettingsManager.MasterVolume;

        valueLabel = CreateText(row.transform, Mathf.RoundToInt(volumeSlider.value * 100) + "%", 16);
        valueLabel.GetComponent<LayoutElement>().preferredWidth = 50f;

        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float value)
    {
        SoundSettingsManager.MasterVolume = value;
        if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(value * 100) + "%";
    }

    private Text CreateText(Transform parent, string text, int fontSize)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.AddComponent<LayoutElement>();
        Text t = obj.AddComponent<Text>();
        t.font = uiFont;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.text = text;
        return t;
    }
}
