using SimpleInputNamespace;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMover : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Joystick joystick;

    public List<Vector3> positionHistory = new List<Vector3>();
    public float recordInterval = 0.02f;
    private float recordTimer = 0f;

    private Rigidbody2D rb;
    private Vector2 input;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // 입력 받기만
        input = new Vector2(joystick.xAxis.value, joystick.yAxis.value);

        // 위치 기록
        recordTimer += Time.deltaTime;
        if (recordTimer >= recordInterval)
        {
            positionHistory.Insert(0, transform.position);
            recordTimer = 0f;
        }

        // 메모리 제한
        if (positionHistory.Count > 3000)
            positionHistory.RemoveRange(2000, positionHistory.Count - 2000);
    }

    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + input.normalized * moveSpeed * Time.fixedDeltaTime);
    }
}
