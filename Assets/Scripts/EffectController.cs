using UnityEngine;

public class EffectController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string triggerName = "Play";
    [SerializeField] private bool autoDestroy = true;

    private Animator[] animators;
    private float maxDuration = 0f; // ★ 자동 계산

    private void Awake()
    {
        animators = GetComponentsInChildren<Animator>(true);
        
        if (animators.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name}: Animator가 없습니다!");
            return;
        }

        // ★ 모든 Animator 중 가장 긴 애니메이션 길이 찾기
        foreach (var animator in animators)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.length > maxDuration)
                    {
                        maxDuration = clip.length;
                    }
                }
            }
        }

        if (maxDuration <= 0f)
        {
            maxDuration = 1f; // 폴백
            Debug.LogWarning($"{gameObject.name}: 애니메이션 길이를 찾을 수 없어 1초로 설정");
        }

        Debug.Log($"{gameObject.name}: 최대 애니메이션 길이 = {maxDuration}초");
    }

    private void Start()
    {
        Play();
    }

    public void Play()
    {
        // ★ 모든 Animator에 트리거 전송
        foreach (var animator in animators)
        {
            if (animator != null)
            {
                animator.SetTrigger(triggerName);
            }
        }

        if (autoDestroy)
        {
            Destroy(gameObject, maxDuration);
        }
    }
}