using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// 키 설정 패널의 내용(이동/스킬/회피 재설정 행)을 코드로 생성하고 재설정 입력을 처리한다.
public class KeyBindingPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform content;

    private readonly Dictionary<RebindableAction, Text> keyLabels = new Dictionary<RebindableAction, Text>();
    private RebindableAction? listeningFor;
    private int listenStartFrame;
    private Font uiFont;

    private void Awake()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildRows();
    }

    private void OnEnable()
    {
        RefreshAllLabels();
    }

    private void BuildRows()
    {
        if (content == null) return;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 8f;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (RebindableAction action in KeyBindingManager.AllActions)
        {
            CreateRow(action);
        }
    }

    private void CreateRow(RebindableAction action)
    {
        GameObject row = new GameObject("Row_" + action, typeof(RectTransform));
        row.transform.SetParent(content, false);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.spacing = 10f;
        rowLayout.childForceExpandHeight = true;
        row.AddComponent<LayoutElement>().preferredHeight = 40f;

        Text label = CreateText(row.transform, KeyBindingManager.GetDisplayName(action), TextAnchor.MiddleLeft, 16);
        label.GetComponent<LayoutElement>().flexibleWidth = 1f;

        GameObject buttonObj = new GameObject("RebindButton_" + action, typeof(RectTransform));
        buttonObj.transform.SetParent(row.transform, false);
        buttonObj.AddComponent<LayoutElement>().preferredWidth = 150f;
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        Text keyText = CreateText(buttonObj.transform, KeyBindingManager.GetKey(action).ToString(), TextAnchor.MiddleCenter, 15);
        RectTransform keyTextRect = keyText.GetComponent<RectTransform>();
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.offsetMin = Vector2.zero;
        keyTextRect.offsetMax = Vector2.zero;

        keyLabels[action] = keyText;
        button.onClick.AddListener(() => StartListening(action));
    }

    private Text CreateText(Transform parent, string text, TextAnchor alignment, int fontSize)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.AddComponent<LayoutElement>();
        Text t = obj.AddComponent<Text>();
        t.font = uiFont;
        t.fontSize = fontSize;
        t.alignment = alignment;
        t.color = Color.white;
        t.text = text;
        return t;
    }

    private void StartListening(RebindableAction action)
    {
        listeningFor = action;
        listenStartFrame = Time.frameCount;
        if (keyLabels.TryGetValue(action, out Text label))
        {
            label.text = "키를 누르세요...";
        }
    }

    private void Update()
    {
        if (listeningFor == null) return;

        // 재설정 버튼을 클릭한 바로 그 프레임에는 그 클릭(마우스 왼쪽 버튼)이 아직 눌린 채로
        // 잡혀서 곧바로 Mouse0로 재설정돼버리는 걸 막기 위해 한 프레임은 건너뛴다.
        if (Time.frameCount == listenStartFrame) return;

        RebindableAction action = listeningFor.Value;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            listeningFor = null;
            RefreshLabel(action);
            return;
        }

        foreach (KeyCode candidate in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (!Input.GetKeyDown(candidate)) continue;

            KeyCode oldKey = KeyBindingManager.GetKey(action);
            if (candidate != oldKey)
            {
                // 이미 다른 동작이 쓰고 있는 키면, 그 동작에는 지금 이 동작이 쓰던 키를 넘겨주는
                // 방식으로 맞바꿔서 같은 키가 두 동작에 겹치지 않게 한다.
                RebindableAction? conflicting = KeyBindingManager.FindActionForKey(candidate);
                if (conflicting.HasValue && conflicting.Value != action)
                {
                    KeyBindingManager.Rebind(conflicting.Value, oldKey);
                    RefreshLabel(conflicting.Value);
                }

                KeyBindingManager.Rebind(action, candidate);
            }

            listeningFor = null;
            RefreshLabel(action);
            break;
        }
    }

    private void RefreshLabel(RebindableAction action)
    {
        if (keyLabels.TryGetValue(action, out Text label))
        {
            label.text = KeyBindingManager.GetKey(action).ToString();
        }
    }

    private void RefreshAllLabels()
    {
        listeningFor = null;
        foreach (RebindableAction action in KeyBindingManager.AllActions)
        {
            RefreshLabel(action);
        }
    }
}
