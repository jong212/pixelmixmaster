using UnityEngine;
public class ProjectileVisual : MonoBehaviour
{
    [Header("Effect References")]
    public GameObject muzzleEffect; 
    public GameObject hitEffect;
    
    [Header("Trail References")]
    public GameObject[] trails;
    
    private void Awake()
    {
        // ★ 자동 검색 (Inspector에서 설정 안 했을 경우)
        if (muzzleEffect == null || hitEffect == null || trails == null || trails.Length == 0)
        {
            AutoFindEffects();
        }
    }
    
    /// <summary>
    /// 자식에서 이펙트 자동 검색
    /// </summary>
    private void AutoFindEffects()
    {
        var trailList = new System.Collections.Generic.List<GameObject>();
        
        foreach (Transform child in transform)
        {
            string childName = child.name.ToLower();
            
            if (childName.Contains("muzzle"))
            {
                muzzleEffect = child.gameObject;
            }
            else if (childName.Contains("hit"))
            {
                hitEffect = child.gameObject;
            }
            else if (childName.Contains("trail") || 
                     childName.Contains("beam") || 
                     childName.Contains("particle"))
            {
                trailList.Add(child.gameObject);
            }
        }
        
        trails = trailList.ToArray();
        
        Debug.Log($"[ProjectileVisual] {gameObject.name} - Muzzle: {(muzzleEffect != null)}, Hit: {(hitEffect != null)}, Trails: {trails.Length}개");
    }
    
    /// <summary>
    /// Muzzle 이펙트 재생
    /// </summary>
    public void PlayMuzzle()
    {
        if (muzzleEffect == null) return;
        
        muzzleEffect.SetActive(true);
        
        ParticleSystem ps = muzzleEffect.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
        }
    }
    
    /// <summary>
    /// Hit 이펙트 재생 및 분리
    /// </summary>
    public GameObject SpawnHitEffect(Vector3 position, Transform parent = null)
    {
        if (hitEffect == null) return null;
        
        GameObject hit = Instantiate(hitEffect, position, Quaternion.identity);
        
        // ★ 부모 설정
        if (parent != null)
        {
            hit.transform.SetParent(parent, true);
        }
        
        hit.SetActive(true);
        
        ParticleSystem ps = hit.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
            Destroy(hit, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        else
        {
            Destroy(hit, 2f);
        }
        
        return hit;
    }
    
    /// <summary>
    /// Trail 이펙트 정리 (명중 시)
    /// </summary>
    public void HandleTrailsOnHit(Transform newParent = null)
    {
        if (trails == null || trails.Length == 0) return;
        
        foreach (GameObject trail in trails) 
        {
            if (trail == null) continue;
            
            // ★ 부모 설정
            if (newParent != null)
            {
                trail.transform.SetParent(newParent, true);
            }
            
            ParticleSystem ps = trail.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop();
                Destroy(trail, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            
            TrailRenderer tr = trail.GetComponent<TrailRenderer>();
            if (tr != null)
            {
                tr.emitting = false;
                Destroy(trail, tr.time);
            }
        }
    }
}