using UnityEngine;

// 투사체의 외형(자식 오브젝트)만 돌린다. 판정 콜라이더는 부모에 있어서 회전해도 흔들리지 않는다.
public class SpinVisual : MonoBehaviour
{
    [SerializeField] private Vector3 localAxis = Vector3.right;
    [SerializeField] private float degreesPerSecond = 900f;

    private void Update()
    {
        transform.Rotate(localAxis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
