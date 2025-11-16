using BACKND;
using System;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BACKND;
public class RootManager : Singleton<RootManager>
{
    //public AddressableCDD AddressableCDD { get; private set; }
    public EndManager EndManager { get; private set; }
    public NetworkConnectionDialog NetworkConnectionDialog { get; private set; }
    //public ChartManager ChartManager { get; private set; }
    //public SetDataManager SetDataManager { get; private set; }
    //public AdManager AdManager { get; private set; }
    //public IAPManager IAPManager { get; private set; }
    // public TileManager TileManager { get; private set; }

    public int AdsCount = 0;
    public bool AdsDelete = false;
    public TMP_InputField tempInput;

    protected override void Awake()
    {
        Debug.Log($"1-0 : 루트매니저 Dondestory 설정");
        base.Awake();
        Init();
    }

    private void Init()
    {
        //AddressableCDD = FindObjectOfType<AddressableCDD>();
        EndManager = new EndManager();
        NetworkConnectionDialog = FindObjectOfType<NetworkConnectionDialog>();
        //ChartManager = new ChartManager();
        //SetDataManager = new SetDataManager();
        //AdManager = FindObjectOfType<AdManager>();
        //IAPManager = FindObjectOfType<IAPManager>();

        StartCoroutine(NextInit());
    }

  public IEnumerator NextInit()
    {
        // 1. Addressable 초기화
        EndManager.Initialize();
        yield return new WaitUntil(() => EndManager.IsReady);
        Debug.Log("Addressable Ready!");

        //NetworkConnectionDialog.Initialize();
        //yield return new WaitUntil(() => NetworkConnectionDialog.IsReady);
        Debug.Log("NetworkConnectionDialog Ready!");

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
    [SerializeField] private Image fadeImage;
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