using UnityEngine;

// 설정 패널의 탭 전환(키 설정 / 사운드 설정)과 닫기를 담당한다.
public class SettingsMenuController : MonoBehaviour
{
    [SerializeField] private PauseMenuController pauseMenuController;
    [SerializeField] private GameObject keyBindingContent;
    [SerializeField] private GameObject soundContent;

    // 일시정지 메뉴가 아니라 메인 메뉴처럼 PauseMenuController가 없는 곳에서 쓸 때,
    // "닫기"를 누르면 이 패널을 대신 열어준다 (예: 메인 메뉴의 버튼 화면).
    [SerializeField] private GameObject fallbackReturnPanel;

    private void OnEnable()
    {
        OpenKeySettings();
    }

    public void OpenKeySettings()
    {
        if (keyBindingContent != null) keyBindingContent.SetActive(true);
        if (soundContent != null) soundContent.SetActive(false);
    }

    public void OpenSoundSettings()
    {
        if (keyBindingContent != null) keyBindingContent.SetActive(false);
        if (soundContent != null) soundContent.SetActive(true);
    }

    public void Close()
    {
        if (pauseMenuController != null)
        {
            pauseMenuController.CloseSettings();
            return;
        }

        gameObject.SetActive(false);
        if (fallbackReturnPanel != null) fallbackReturnPanel.SetActive(true);
    }
}
