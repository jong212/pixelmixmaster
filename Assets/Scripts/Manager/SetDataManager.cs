using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BackEnd; // 뒤끝 SDK 네임스페이스
using LitJson; // 리스트 파싱용

public class SetDataManager : MonoBehaviour
{

    public bool IsReady { get; private set; }

    // =========================================================
    // [1] 로컬 데이터 변수
    // =========================================================

    [Header("User Info")]
    public int myLv = 1;
    public int myGd = 1000;
    public int myExp = 0;
    private string userIndate = ""; // 업데이트용 식별자

    [Header("Equipment")]
    public List<int> equipList = new List<int>();    // IL
    public List<int> equipSetting = new List<int>(); // IS
    private string equipIndate = "";

    [Header("Monster")]
    public List<int> monsterList = new List<int>();    // ML
    public List<int> monsterSetting = new List<int>(); // MS
    private string monsterIndate = "";

    public void Initialize()
    {
        LoadAllData();
    }

    // =========================================================
    // [2] 초기화 및 데이터 로드 (게임 시작 시 1회 호출)
    // =========================================================
    public void LoadAllData()
    {
        Debug.Log(">>> 전체 데이터 로드 시작");

        // 1. UserInfo 로드
        Backend.GameData.GetMyData("userInfo", new Where(), callback =>
        {
            if (callback.IsSuccess())
            {
                if (callback.FlattenRows().Count > 0)
                {
                    var data = callback.FlattenRows()[0];
                    userIndate = data["inDate"].ToString();
                    myLv = int.Parse(data["Lv"].ToString());
                    myGd = int.Parse(data["Gd"].ToString());
                    myExp = int.Parse(data["Exp"].ToString());
                }
                else InsertInitUserInfo();
            }
            else Debug.LogError("UserInfo 로드 실패");
        });

        // 2. EqueInven 로드
        Backend.GameData.GetMyData("EqueInven", new Where(), callback =>
        {
            if (callback.IsSuccess())
            {
                if (callback.FlattenRows().Count > 0)
                {
                    var data = callback.FlattenRows()[0];
                    equipIndate = data["inDate"].ToString();
                    equipList = JsonToList(data["IL"]);
                    equipSetting = JsonToList(data["IS"]);
                }
                else InsertInitEquip();
            }
        });

        // 3. MonsterInven 로드
        Backend.GameData.GetMyData("MonsterInven", new Where(), callback =>
        {
            if (callback.IsSuccess())
            {
                if (callback.FlattenRows().Count > 0)
                {
                    var data = callback.FlattenRows()[0];
                    monsterIndate = data["inDate"].ToString();
                    monsterList = JsonToList(data["ML"]);
                    monsterSetting = JsonToList(data["MS"]);
                }
                else InsertInitMonster();

                // 로드 완료 후 자동 저장 루틴 시작!
                StartCoroutine(AutoSaveRoutine());
            }
        });
    }

    // =========================================================
    // [3] 신규 유저용 초기 데이터 생성 (Insert)
    // =========================================================
    void InsertInitUserInfo()
    {
        Param param = new Param();
        param.Add("Lv", 1);
        param.Add("Gd", 1000);
        param.Add("Exp", 0);

        Backend.GameData.Insert("userInfo", param, cb => {
            if (cb.IsSuccess()) userIndate = cb.GetInDate();
        });
    }

    void InsertInitEquip()
    {
        equipList = new List<int>() { 0, 0, 0, 0, 0 };
        equipSetting = new List<int>() { 0, 0 };

        Param param = new Param();
        param.Add("IL", equipList);
        param.Add("IS", equipSetting);

        Backend.GameData.Insert("EqueInven", param, cb => {
            if (cb.IsSuccess()) equipIndate = cb.GetInDate();
        });
    }

    void InsertInitMonster()
    {
        monsterList = new List<int>() { 0, 0, 0, 0, 0 };
        monsterSetting = new List<int>() { 0, 0, 0 };

        Param param = new Param();
        param.Add("ML", monsterList);
        param.Add("MS", monsterSetting);

        Backend.GameData.Insert("MonsterInven", param, cb => {
            if (cb.IsSuccess()) monsterIndate = cb.GetInDate();
        });
    }

    // =========================================================
    // [4] 플레이 로직 & 저장 (핵심 로직 수정됨)
    // =========================================================

    // --- A. 사냥 보상 (즉시 저장 X) ---
    public void GetGoldAndExp(int gold, int exp)
    {
        myGd += gold;
        myExp += exp;
        // 저장 안 함 -> 10분 뒤 자동 저장에 맡김
        Debug.Log($"재화 획득! Gd:{myGd}, Exp:{myExp} (서버 저장 대기)");
    }

    // --- B. 장비 업데이트 ---
    // isSpendMoney: 돈을 써서 장비가 변했으면 true, 아니면 false
    public void UpdateEquipment(List<int> newInven, List<int> newSetting, bool isSpendMoney = false)
    {
        // 로컬 갱신
        equipList = newInven;
        equipSetting = newSetting;

        // 1. 장비 테이블은 무조건 저장
        SaveEquipmentImmediate();

        // 2. 돈을 썼다면? 유저 정보(돈)도 같이 저장! (복사 버그 방지)
        if (isSpendMoney)
        {
            SaveUserInfoImmediate();
        }
    }

    // --- C. 몬스터 업데이트 ---
    // isSpendMoney: 돈을 써서 몬스터가 변했으면 true, 아니면 false
    public void UpdateMonster(List<int> newInven, List<int> newSetting, bool isSpendMoney = false)
    {
        // 로컬 갱신
        monsterList = newInven;
        monsterSetting = newSetting;

        // 1. 몬스터 테이블 무조건 저장
        SaveMonsterImmediate();

        // 2. 돈을 썼다면? 유저 정보(돈)도 같이 저장!
        if (isSpendMoney)
        {
            SaveUserInfoImmediate();
        }
    }


    // =========================================================
    // [5] 실제 저장 함수들 (Update)
    // =========================================================

    // 1. [자동 저장] 10분마다 모든 데이터 저장
    IEnumerator AutoSaveRoutine()
    {
        IsReady = true;
        while (true)
        {
            yield return new WaitForSeconds(600f); // 10분 (600초)

            // 10분마다 모든 테이블을 다 저장해서 안전성 확보
            Debug.Log(">>> [자동 저장] 모든 데이터 저장 시작");
            SaveUserInfoImmediate();
            SaveEquipmentImmediate();
            SaveMonsterImmediate();
        }
    }

    // 2. 유저 정보(재화) 즉시 저장
    public void SaveUserInfoImmediate()
    {
        if (string.IsNullOrEmpty(userIndate)) return;

        Param param = new Param();
        param.Add("Lv", myLv);
        param.Add("Gd", myGd);
        param.Add("Exp", myExp);

        Backend.GameData.UpdateV2("userInfo", userIndate, Backend.UserInDate, param, callback => {
            if (callback.IsSuccess()) Debug.Log("UserInfo(재화) 저장 완료");
        });
    }

    // 3. 장비 즉시 저장
    public void SaveEquipmentImmediate()
    {
        if (string.IsNullOrEmpty(equipIndate)) return;

        Param param = new Param();
        param.Add("IL", equipList);    // 리스트 통째로 저장
        param.Add("IS", equipSetting); // 세팅 통째로 저장

        Backend.GameData.UpdateV2("EqueInven", equipIndate, Backend.UserInDate, param, callback => {
            if (callback.IsSuccess()) Debug.Log("장비 데이터 저장 완료");
        });
    }

    // 4. 몬스터 즉시 저장
    public void SaveMonsterImmediate()
    {
        if (string.IsNullOrEmpty(monsterIndate)) return;

        Param param = new Param();
        param.Add("ML", monsterList);
        param.Add("MS", monsterSetting);

        Backend.GameData.UpdateV2("MonsterInven", monsterIndate, Backend.UserInDate, param, callback => {
            if (callback.IsSuccess()) Debug.Log("몬스터 데이터 저장 완료");
        });
    }

    // 5. 비상 저장 (앱 종료 시 전부 저장)
    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            Debug.Log("앱 일시정지 -> 비상 저장 시도");
            SaveEquipmentImmediate();
            SaveMonsterImmediate();
            SaveUserInfoImmediate();
        }
    }

    // =========================================================
    // [유틸리티] JsonData -> List<int> 변환기
    // =========================================================
    List<int> JsonToList(JsonData json)
    {
        List<int> list = new List<int>();
        for (int i = 0; i < json.Count; i++)
        {
            list.Add(int.Parse(json[i].ToString()));
        }
        return list;
    }
}