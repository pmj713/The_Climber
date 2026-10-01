using UnityEngine;

// 몬스터 발밑 바닥에 눕혀서 보여주는 체력바. 화면에서 항상 가로로 보이도록 카메라 방향에 맞춰 돌리고,
// 몸에 가리지 않게 카메라 쪽으로 살짝 앞에 둔다.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private EnemyHealthBarView viewPrefab;
    [SerializeField] private float widthScale = 1f; // 엘리트는 크게
    [SerializeField] private Color fillColor = new Color(0.85f, 0.15f, 0.12f);
    [SerializeField] private Color frameColor = new Color(0f, 0f, 0f, 0.7f);
    [SerializeField] private float groundHeight = 0.05f;

    private EnemyHealth health;
    private EnemyHealthBarView view;
    private Transform cameraTransform;
    private float forwardOffset = 0.4f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (viewPrefab == null) return;

        if (TryGetComponent<Collider>(out var body))
            forwardOffset = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z) * 1.05f;

        // 몸이 돌아도 바는 따로 돌려야 해서 부모에 붙이지 않고 매 프레임 위치만 따라간다
        view = Instantiate(viewPrefab);
        view.transform.localScale = new Vector3(widthScale, 1f, 1f);
        view.Configure(fillColor, frameColor);
        view.SetFraction(1f);
    }

    private void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }

    private void LateUpdate()
    {
        if (view == null) return;
        if (health.IsDead) { view.gameObject.SetActive(false); return; }

        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        Vector3 towardCamera = facing * Vector3.back;

        view.transform.SetPositionAndRotation(
            transform.position + towardCamera * forwardOffset + Vector3.up * groundHeight,
            facing * Quaternion.Euler(90f, 0f, 0f));
        view.SetFraction(health.HealthFraction);
    }
}
