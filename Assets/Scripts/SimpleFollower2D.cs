using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SimpleFollower2D : MonoBehaviour
{
    public float maxSpeed = 5f;
    public float arriveRadius = 0.8f;
    public float slowDown = 8f;

    private Vector2 velocity;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    public void Follow(Vector2 targetPos)
    {
        Vector2 pos = rb.position;
        Vector2 toTarget = targetPos - pos;
        float dist = toTarget.magnitude;

        if (dist < 0.01f)
        {
            velocity = Vector2.zero;
            rb.velocity = Vector2.zero;
            return;
        }

        float speed = maxSpeed;
        if (dist < arriveRadius)
            speed *= dist / arriveRadius;

        Vector2 desiredVelocity = toTarget.normalized * speed;

        velocity = Vector2.Lerp(
            velocity,
            desiredVelocity,
            Time.fixedDeltaTime * slowDown
        );

        // ⭐ 핵심: Transform 대신 Rigidbody
        rb.velocity = velocity;
    }
}
