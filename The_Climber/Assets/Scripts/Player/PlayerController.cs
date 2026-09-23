using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 0.05f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(0f, 0f, speed);
        }
        else if(Input.GetKey(KeyCode.S))
        {
            transform.Translate(0f, 0f, -speed);
        }
        else if(Input.GetKey(KeyCode.D))
        {
            transform.Translate(speed, 0f, 0f);
        }
        else if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(-speed, 0f, 0f);
        }
    }
}
