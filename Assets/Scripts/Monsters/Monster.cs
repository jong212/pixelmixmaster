using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BACKND;
using UnityEngine.UI;

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
    public Image fill;
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
    [SyncVar(hook = nameof(OnAliveChanged))]
    public bool alive = true;
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
    private Vector3 lastPosition;  // ★ 추가: 이전 프레임 위치
    private float stuckTimer = 0f;  // ★ 추가: 막힌 시간 측정

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
        Vector3 directionToTarget = (moveTargetPos - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, moveTargetPos);
        
        // ★ 목적지까지 벽이 있는지 체크
        if (Physics2D.Raycast(transform.position, directionToTarget, distanceToTarget, obstacleLayer))
        {
            // 벽이 있으면 즉시 새로운 목표 설정
            Debug.Log("Patrol: 경로에 벽 감지, 새 목표 설정");
            SetNewPatrolTarget();
            stuckTimer = 0f;
            return;
        }

        // ★ 움직이지 못하는 상태 감지 (0.5초 동안 거의 안 움직이면)
        if (Vector3.Distance(transform.position, lastPosition) < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 0.5f)
            {
                Debug.Log("Patrol: 0.5초간 움직임 없음, 새 목표 설정");
                SetNewPatrolTarget();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;
        
        MoveTo(moveTargetPos, moveSpeed * 0.5f);

        // 목적지 도달
        if (distanceToTarget < 0.1f)
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
        for (int i = 0; i < 10; i++)  // ★ 시도 횟수 10회로 증가
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(1f, patrolRadius);
            Vector3 potentialPos = transform.position + (Vector3)(randomDir * distance);

            Vector3 direction = (potentialPos - transform.position).normalized;
            
            // ★ 벽 체크: 시작점부터 목적지까지 전체 경로 체크
            if (!Physics2D.Raycast(transform.position, direction, distance, obstacleLayer))
            {
                moveTargetPos = potentialPos;
                //Debug.Log($"새 Patrol 목표 설정: {moveTargetPos}");
                return;
            }
        }
        
        // ★ 10번 실패하면 Idle 상태로 전환
        //Debug.Log("Patrol 목표 설정 실패, Idle로 전환");
        ChangeState(State.Idle);
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
        if (!alive) return;

        alive = false;

        aggroTargets.Clear();
        currentTarget = null;

        // 서버 전용 처리
        // - 드랍 생성
        // - 점수 계산
        // - 리스폰 타이머 세팅

    }
 
    private void OnAliveChanged(bool oldValue, bool newValue)
    {
        if (monsterCollider)
            monsterCollider.enabled = newValue;

        if (spriteRenderer)
            spriteRenderer.enabled = newValue;

        if (fill != null)
            fill.transform.parent.gameObject.SetActive(newValue);
    }

    [Server]
    public void ResetForRespawn()
    {
        alive = true;
        currentHealth = maxHealth;

        isStunned = false;

        if (fill != null)
            fill.transform.parent.gameObject.SetActive(true);

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
        if (fill == null) return;

        float ratio = Mathf.Clamp01(newHealth / maxHealth);
        fill.fillAmount = ratio;

        //Debug.Log("MonsterHP" + newHealth);
    }

    // ★ 기즈모로 목적지 시각화
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 현재 상태에 따른 색상
        switch (currentState)
        {
            case State.Idle:
                Gizmos.color = Color.white;
                break;
            case State.Patrol:
                Gizmos.color = Color.green;
                break;
            case State.Chase:
                Gizmos.color = Color.red;
                break;
            case State.Attack:
                Gizmos.color = Color.yellow;
                break;
        }

        // 몬스터 위치에 작은 구
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        // Patrol/Chase 상태일 때 목적지 표시
        if (currentState == State.Patrol || currentState == State.Chase)
        {
            // 목적지 위치
            Gizmos.DrawSphere(moveTargetPos, 0.2f);
            
            // 몬스터 → 목적지 라인
            Gizmos.DrawLine(transform.position, moveTargetPos);
        }

        // Chase 상태일 때 타겟 표시
        if (currentState == State.Chase && currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            Gizmos.DrawWireSphere(currentTarget.transform.position, 0.4f);
        }

        // 감지 범위 표시
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        // 공격 범위 표시
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
} 