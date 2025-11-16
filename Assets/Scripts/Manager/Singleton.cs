using UnityEngine;

/// <summary>
/// Unity 기반 싱글톤 패턴 (DontDestroyOnLoad 포함)
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();

                if (_instance == null)
                {
                    Debug.LogError($"[Singleton] {typeof(T)} 인스턴스가 씬에 존재하지 않습니다!");
                }
            }
            return _instance;
        }
    }


    protected virtual void Awake()
    {
        // 중복 제거
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
}
