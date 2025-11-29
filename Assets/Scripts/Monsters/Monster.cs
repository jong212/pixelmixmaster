using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BACKND;

public class Monster : NetworkBehaviour
{
    // ★ Return 상태 삭제
    public enum State { Idle, Patrol, Chase, Attack }

    [Header("Identity")]
    public int monsterId;
    public string zoneId;

    [Header("Stats")]
    public float maxHealth = 100f;
    [SyncVar(hook = nameof(OnHealthChanged))]
    public float currentHealth;

    public float moveSpeed = 2.0f;
    public float attackRange = 1.2f;
    public float attackRate = 1.5f;
    public int damage = 10;

    [Header("AI Settings")]
    public float patrolRadius = 3f;     // 현 위치 기준 배회 반경
    public float detectRadius = 5f;
    public float aggroTimeout = 5f;     // 추격 포기 시간

    public LayerMask obstacleLayer;     // 벽 레이어

    [Header("State Sync")]
    [SyncVar] public bool alive = true;
    [SyncVar] public bool isStunned = false;
    public float nextRespawnTime = 0f;

    // --- 내부 로직 변수 ---
    private State currentState = State.Idle;
    // private Vector3 anchorPosition;  <-- 삭제됨
    private SpriteRenderer spriteRenderer;
    private Collider2D monsterCollider;

    // 어그로 시스템
    private List<GameObject> aggroTargets = new List<GameObject>();
    private GameObject currentTarget;

    // 타이머 및 타겟 좌표
    private float lastAttackTime;
    private float lastAggroTime;
    private float stateTimer;
    private Vector3 moveTargetPos;

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentHealth = maxHealth;
        // anchorPosition 설정 로직 삭제
        ChangeState(State.Idle);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        monsterCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (!isServer || !alive) return;
        if (isStunned) return;

        switch (currentState)
        {
            case State.Idle: ProcessIdle(); break;
            case State.Patrol: ProcessPatrol(); break;
            case State.Chase: ProcessChase(); break;
            case State.Attack: ProcessAttack(); break;
                // Return 케이스 삭제
        }
    }

    // ========================================================================
    // 1. 상태별 행동 로직
    // ========================================================================

    [Server]
    private void ProcessIdle()
    {
        stateTimer += Time.deltaTime;

        // 2~4초 쉬고 다시 배회
        if (stateTimer > Random.Range(2f, 4f))
        {
            SetNewPatrolTarget();
            ChangeState(State.Patrol);
        }
    }

    [Server]
    private void ProcessPatrol()
    {
        MoveTo(moveTargetPos, moveSpeed * 0.5f);

        if (Vector3.Distance(transform.position, moveTargetPos) < 0.1f)
        {
            ChangeState(State.Idle);
        }
    }

    [Server]
    private void ProcessChase()
    {
        UpdateBestTarget();

        // ★ 거리 체크(IsTooFarFromAnchor) 삭제됨. 
        // 오직 타겟이 없거나 시간이 지났을 때만 포기.
        if (currentTarget == null || IsAggroTimeout())
        {
            GiveUpChase(); // 복귀(Return) 대신 그냥 포기
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

        if (dist <= attackRange)
        {
            ChangeState(State.Attack);
        }
        else
        {
            MoveTo(currentTarget.transform.position, moveSpeed);
        }
    }

    [Server]
    private void ProcessAttack()
    {
        if (currentTarget == null || !currentTarget.activeSelf)
        {
            ChangeState(State.Chase);
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

        if (dist > attackRange)
        {
            ChangeState(State.Chase);
            return;
        }

        if (Time.time - lastAttackTime >= attackRate)
        {
            DoAttack();
            lastAttackTime = Time.time;
        }
    }

    // ProcessReturn() 함수 통째로 삭제됨

    // ========================================================================
    // 2. 행동 함수
    // ========================================================================

    [Server]
    private void MoveTo(Vector3 target, float speed)
    {
        Vector3 direction = (target - transform.position).normalized;
        float distToTarget = Vector3.Distance(transform.position, target);
        float moveDist = speed * Time.deltaTime;

        if (moveDist > distToTarget) moveDist = distToTarget;

        if (!Physics2D.Raycast(transform.position, direction, 0.5f, obstacleLayer))
        {
            transform.position += direction * moveDist;
        }

        if (direction.x < -0.01f) spriteRenderer.flipX = true;
        else if (direction.x > 0.01f) spriteRenderer.flipX = false;
    }

    [Server]
    private void DoAttack()
    {
        if (currentTarget == null) return;

        PlayerController pc = currentTarget.GetComponent<PlayerController>();
        if (pc != null)
        {
            // pc.TakeDamage(damage); 
        }

        lastAggroTime = Time.time;
        RpcPlayAttackEffect();
    }

    [ClientRpc]
    private void RpcPlayAttackEffect()
    {
        // 애니메이션 등
    }

    [Server]
    public void TakeDamage(int damageAmount, GameObject attacker)
    {
        if (!alive) return;

        currentHealth -= damageAmount;

        if (attacker != null && !aggroTargets.Contains(attacker))
        {
            aggroTargets.Add(attacker);
        }

        lastAggroTime = Time.time;

        // Idle이든 Patrol이든 맞으면 바로 추격
        if (currentState == State.Idle || currentState == State.Patrol)
        {
            ChangeState(State.Chase);
        }

        StartCoroutine(FlashColor());

        if (currentHealth <= 0) Die();
    }

    // ========================================================================
    // 3. 판단 및 유틸리티
    // ========================================================================

    [Server]
    private void ChangeState(State newState)
    {
        currentState = newState;
        stateTimer = 0f;
    }

    // ★ 이름 변경: GiveUpAndReturn -> GiveUpChase
    [Server]
    private void GiveUpChase()
    {
        currentTarget = null;
        aggroTargets.Clear();

        // ★ 집으로 안 감. 그냥 그 자리에서 바로 Idle 상태가 됨.
        // 이러면 자연스럽게 그 주변을 다시 배회하기 시작함.
        ChangeState(State.Idle);
    }

    [Server]
    private void SetNewPatrolTarget()
    {
        // 현재 위치 기준으로 랜덤 이동 (앵커 거리 체크 삭제됨)
        for (int i = 0; i < 5; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(1f, patrolRadius);
            Vector3 potentialPos = transform.position + (Vector3)(randomDir * distance);

            // 벽 체크만 수행
            if (!Physics2D.Raycast(transform.position, (potentialPos - transform.position).normalized, distance, obstacleLayer))
            {
                moveTargetPos = potentialPos;
                return;
            }
        }
        moveTargetPos = transform.position;
    }

    [Server]
    private void UpdateBestTarget()
    {
        aggroTargets.RemoveAll(t => t == null || !t.activeSelf);

        if (aggroTargets.Count == 0)
        {
            currentTarget = null;
            return;
        }

        GameObject bestTarget = null;
        float minDist = float.MaxValue;

        foreach (var t in aggroTargets)
        {
            float d = Vector3.Distance(transform.position, t.transform.position);
            if (d < minDist)
            {
                minDist = d;
                bestTarget = t;
            }
        }
        currentTarget = bestTarget;
    }

    [Server]
    private bool IsAggroTimeout()
    {
        return Time.time - lastAggroTime > aggroTimeout;
    }

    [Server]
    private void Die()
    {
        alive = false;
        aggroTargets.Clear();
        if (monsterCollider) monsterCollider.enabled = false;
    }

    [Server]
    public void ResetForRespawn()
    {
        alive = true;
        currentHealth = maxHealth;
        isStunned = false;
        aggroTargets.Clear();
        currentTarget = null;

        if (monsterCollider) monsterCollider.enabled = true;

        // 앵커 초기화 삭제됨
        ChangeState(State.Idle);
    }

    private IEnumerator FlashColor()
    {
        if (spriteRenderer) spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (spriteRenderer) spriteRenderer.color = Color.white;
    }

    private void OnHealthChanged(float oldHealth, float newHealth)
    {
    }
}