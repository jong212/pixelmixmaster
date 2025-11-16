using UnityEngine;

public class PetFollower : MonoBehaviour
{
    public PlayerController target;

    public float followDistance = 0.6f;
    public float smoothSpeed = 15f;

    private Vector3 previousPosition;

    private void Start()
    {
        previousPosition = transform.position;
    }

    private void Update()
    {
        if (target.positionHistory.Count < 2) return;

        float traveled = 0f;

        // 플레이어의 이동 경로를 따라가며
        // followDistance 만큼 떨어져있는 지점을 찾기
        for (int i = 0; i < target.positionHistory.Count - 1; i++)
        {
            float segmentDist = Vector3.Distance(target.positionHistory[i], target.positionHistory[i + 1]);

            if (traveled + segmentDist >= followDistance)
            {
                float t = (followDistance - traveled) / segmentDist;

                Vector3 point = Vector3.Lerp(
                    target.positionHistory[i],
                    target.positionHistory[i + 1],
                    t
                );

                // 부드럽게 이동
                transform.position = Vector3.Lerp(transform.position, point, smoothSpeed * Time.deltaTime);

                // 회전 자연스럽게
                Vector3 dir = point - transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                }

                return;
            }

            traveled += segmentDist;
        }
    }
}
