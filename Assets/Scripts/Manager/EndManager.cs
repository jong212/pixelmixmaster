using BackEnd;
using LitJson;
using UnityEngine;

public class EndManager : MonoBehaviour
{
    public bool IsReady { get; private set; }

    public void Initialize()
    {
        var bro = Backend.Initialize();
        if (!bro.IsSuccess())
        {
            Debug.LogError("초기화 실패 : " + bro);
            return;
        }

    #if UNITY_ANDROID && !UNITY_EDITOR
            StartGoogleLogin();
    #else
            StartCustomLogin("user1", "1234");
    #endif
    }

    public void StartCustomLogin(string id, string pw)
    {
        var bro = Backend.BMember.CustomLogin(id, pw);

        Debug.Log("커스텀 로그인 결과 : " + bro);

        HandleLoginResult(bro); // ✅ 공통 함수 호출
    }
    public void StartGoogleLogin()
    {
        //TheBackend.ToolKit.GoogleLogin.Android.GoogleLogin(GoogleLoginCallback);
    }
    private void GoogleLoginCallback(bool isSuccess, string errorMessage, string token)
    {
        if (!isSuccess)
        {
            Debug.LogError(errorMessage);
            return;
        }

        Debug.Log("구글 토큰 : " + token);
        var bro = Backend.BMember.AuthorizeFederation(token, FederationType.Google);

        Debug.Log("페데레이션 로그인 결과 : " + bro);

        HandleLoginResult(bro); // ✅ 공통 함수 호출
    }
    private void HandleLoginResult(BackendReturnObject bro)
    {
        if (!bro.IsSuccess())
        {
            Debug.LogError("로그인 처리 실패: " + bro);
            return;
        } else
        {
            IsReady = true;
        }


    }


}
