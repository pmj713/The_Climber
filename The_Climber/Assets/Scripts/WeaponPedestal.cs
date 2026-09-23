using UnityEngine;
using UnityEngine.UI;

// 마을의 무기 받침대. 콜라이더/트리거에 기대지 않고 플레이어(WeaponController)와의 거리로만
// 판정한다 (받침대 3D 모델의 콜라이더는 물리적으로 막는 용도로 그대로 둬야 해서 건드리지 않는다).
// 범위 안에서 상호작용 키를 누르면 이 받침대의 무기를 장착하고, 장식된 무기 모델은 사라진다.
public class WeaponPedestal : MonoBehaviour
{
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private MonoBehaviour weaponToEquip; // 플레이어에 붙어있는 SwordWeapon 또는 BowWeapon 컴포넌트
    [SerializeField] private GameObject displayModel; // 받침대 위에 놓인 장식용 무기 모델
    [Tooltip("플레이어가 이 거리 안으로 들어오면 상호작용 힌트가 뜨고 키 입력을 받는다")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private Text hintText; // "E: 장착" 같은 안내 문구, 없어도 동작함

    private bool playerInRange;

    private void Start()
    {
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (weaponController == null) return;

        bool inRange = Vector3.Distance(transform.position, weaponController.transform.position) <= interactDistance;
        if (inRange != playerInRange)
        {
            playerInRange = inRange;
            if (hintText != null) hintText.gameObject.SetActive(inRange);
        }

        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            Equip();
        }
    }

    private void Equip()
    {
        if (weaponController == null || weaponToEquip == null) return;

        weaponController.EquipWeapon(weaponToEquip);
        if (displayModel != null) displayModel.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
