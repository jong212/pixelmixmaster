//
//
//NOTES:
//
//This script is used for DEMONSTRATION porpuses of the Projectiles. I recommend everyone to create their own code for their own projects.
//THIS IS JUST A BASIC EXAMPLE PUT TOGETHER TO DEMONSTRATE VFX ASSETS.
//
//




#pragma warning disable 0168
#pragma warning disable 0219
#pragma warning disable 0414

using System.Collections;
using UnityEngine;
using BACKND; // ★ 추가

public class ProjectileMoveScript : NetworkBehaviour
{
    [Header("Settings")]
    public float speed = 10f;
    public float hitDistance = 0.3f;
    public float maxLifetime = 5f;

    private GameObject target;
    private int damage;
    private GameObject owner;
    
    // ★★ SyncVar로 변경 (자동 동기화)
    [SyncVar(hook = nameof(OnVisualEffectChanged))]
    private string visualEffectName = "";
    
    private bool hasSpawnedVisual = false;

    // ★ 초기화
    [Server]
    public void Initialize(GameObject targetMonster, int attackDamage, GameObject attacker)
    {
        target = targetMonster;
        damage = attackDamage;
        owner = attacker;
    }

    // ★ 비주얼 이펙트 이름 설정 (SyncVar 사용)
    [Server]
    public void SetVisualEffect(string effectName)
    {
        visualEffectName = effectName; // ★ SyncVar 값 변경 → 자동으로 클라에 전달!
    }

    // ★ Hook: SyncVar 변경 시 자동 호출 (서버 + 모든 클라이언트)
    private void OnVisualEffectChanged(string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(newValue) || hasSpawnedVisual) return;
        
        SpawnVisual(newValue);
    }

    // ★ 비주얼 생성 (모든 클라에서 실행)
    private void SpawnVisual(string effectName)
    {
        if (hasSpawnedVisual) return;
        hasSpawnedVisual = true;

        GameObject visualPrefab = RootManager.Instance.AddressableCDD.GetEffectPrefab(effectName);
        if (visualPrefab != null)
        {
            GameObject visual = Instantiate(visualPrefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            
            Debug.Log($"[{(isServer ? "Server" : "Client")}] 비주얼 생성: {effectName}");
        }
        else
        {
            Debug.LogWarning($"비주얼 프리팹 없음: {effectName}");
        }
    }

    private void Start()
    {
        // ★ Start 시점에 이미 SyncVar가 동기화되어 있으면 수동 호출
        if (!string.IsNullOrEmpty(visualEffectName) && !hasSpawnedVisual)
        {
            SpawnVisual(visualEffectName);
        }

        Destroy(gameObject, maxLifetime);
    }

    private void Update()
    {
        if (!isServer) return;

        // 타겟 유효성 검사
        if (target == null || !IsTargetValid(target))
        {
            NetworkServer.Destroy(gameObject);
            return;
        }

        // 거리 체크
        float distance = Vector2.Distance(transform.position, target.transform.position);
        if (distance < hitDistance)
        {
            OnHit();
            return;
        }

        // 이동
        Vector3 direction = (target.transform.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        // 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private bool IsTargetValid(GameObject target)
    {
        Monster monster = target.GetComponent<Monster>();
        return monster != null && monster.alive;
    }

    [Server]
    private void OnHit()
    {
        Monster monster = target.GetComponent<Monster>();
        if (monster != null && monster.alive)
        {
            monster.TakeDamage(damage, owner);
        }

        NetworkServer.Destroy(gameObject);
    }
}
