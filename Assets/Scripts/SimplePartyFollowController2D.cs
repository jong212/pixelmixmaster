using System.Collections.Generic;
using UnityEngine;

public class SimplePartyFollowController2D : MonoBehaviour
{
    public PlayerController mainPlayer;
    public List<SimpleFollower2D> subPlayers = new();

    // 파티 기본 슬롯 (뒤쪽 대형)
    private readonly Vector2[] baseSlots =
    {
        new Vector2(-0.6f, -0.9f),
        new Vector2( 0.6f, -0.9f),
        new Vector2( 0.0f, -1.6f),
    };

    private Vector2 lastMoveDir = Vector2.down;

    void LateUpdate()
    {
        if (mainPlayer == null || subPlayers.Count == 0)
            return;

        Vector2 moveDir = mainPlayer.movement;
        if (moveDir.sqrMagnitude > 0.01f)
            lastMoveDir = moveDir.normalized;

        Vector2 forward = lastMoveDir;
        Vector2 right = new Vector2(forward.y, -forward.x);
        Vector2 mainPos = mainPlayer.transform.position;

        for (int i = 0; i < subPlayers.Count; i++)
        {
            Vector2 slot = baseSlots[i % baseSlots.Length];

            Vector2 worldOffset =
                right * slot.x +
                forward * slot.y;

            Vector2 targetPos = mainPos + worldOffset;
            subPlayers[i].Follow(targetPos);
        }
    }

}
