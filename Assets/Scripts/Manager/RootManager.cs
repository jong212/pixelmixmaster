using BACKND;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
public class RootManager : Singleton<RootManager>
{
    // 리소스 다운
    public AddressableCDD AddressableCDD { get; private set; }

    public ChartManager ChartManager { get; private set; }
    // 구글 로그인
    public EndManager EndManager { get; private set; }

    // 서버 데이터 다운
    public GameNetworkManager GameNetworkManager { get; private set; }

    // 맵 세팅
    public GameDataManager GameDataManager{ get; private set; }

    // 뒤끝 유저 데이터 가져오기
    public SetDataManager SetDataManager { get; private set; }
    public UI UI { get; private set; }
    //public AdManager AdManager { get; private set; }
    //public IAPManager IAPManager { get; private set; }
    // public TileManager TileManager { get; private set; }


    protected override void Awake()
    {
        Debug.Log($"1-0 : 루트매니저 Dondestory 설정");
        base.Awake();

       Init();
    }
    private void Init()
    {
        AddressableCDD = FindObjectOfType<AddressableCDD>();

#if UNITY_SERVER
        GameNetworkManager = FindObjectOfType<GameNetworkManager>();
        GameDataManager = FindObjectOfType<GameDataManager>();
#else

        ChartManager = new ChartManager();
        EndManager = new EndManager();
        GameNetworkManager = FindObjectOfType<GameNetworkManager>();
        SetDataManager     = FindObjectOfType<SetDataManager>();
        GameDataManager    = FindObjectOfType<GameDataManager>();
        UI                 = FindObjectOfType<UI>();
#endif


        StartCoroutine(NextInit());
    }

    public IEnumerator NextInit()
    {
        AddressableCDD.Initialize();
        yield return new WaitUntil(() => AddressableCDD.IsReady);
        Debug.Log("어드레서블 리소스 다운 완료!");



        EndManager.Initialize();
        yield return new WaitUntil(() => EndManager.IsReady);
        Debug.Log("구글 로그인 완료 !");
        ChartManager.Initialize();
        yield return new WaitUntil(() => ChartManager.IsReady);
        SetDataManager.Initialize();
        yield return new WaitUntil(() => SetDataManager.IsReady);
        Debug.Log("뒤끝에서 유저정보, 몬스터 정보, 인벤정보 등 가져와서 변수에 세팅완료");

        UI.Initialize();
        yield return new WaitUntil(() => SetDataManager.IsReady);
        Debug.Log("UI Canvas 세팅완료 !");
    }
    /// <summary>
    /// COROUTINE 모노 안 받는 매니저에서 코루틴 못 쓸때 이거 호출하기
    /// </summary>    
    public void Coroutine_Action(float timer, Action action)
    {
        StartCoroutine(Action_Coroutine(action, timer));
    }
    IEnumerator Action_Coroutine(Action action, float timer)
    {
        yield return new WaitForSeconds(timer);
        action?.Invoke();
    }

    /// <summary>
    /// FADE IN OUT
    /// </summary>
    [SerializeField] private UnityEngine.UI.Image fadeImage;
    [SerializeField] private float fadeTime;

    public void FadeInOut(float waitTime)
    {
        StartCoroutine(FadeStart(waitTime));

    }
    public IEnumerator FadeStart(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        //  어두워지기
        yield return Fade(0f, 1f);

        //  잠깐 대기 (어두운 상태 유지)
        SceneManager.LoadScene("Stage01");
        yield return new WaitForSeconds(0.1f);

        //  다시 밝아지기
        yield return Fade(1f, 0f);
    }

    private IEnumerator Fade(float start, float end)
    {
        float elapsed = 0f;
        Color color = fadeImage.color;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(start, end, elapsed / fadeTime);
            fadeImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(color.r, color.g, color.b, end);
    }

    public void SetLevelTempTest()
    {
       /* RootManager.Instance.SetDataManager.SetStageLevel(int.Parse(tempInput.text));
        SceneManager.LoadScene(1);*/
    }

    public void OnClickSceneChange()
    {
        SceneManager.LoadScene("SampleScene");
    }
}