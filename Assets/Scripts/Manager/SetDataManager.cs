using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BackEnd; // 뒤끝 SDK
using LitJson; // JSON 파싱 (필수)

public class SetDataManager : MonoBehaviour
{
    // [0] 싱글톤 패턴 (다른 스크립트에서 쉽게 접근하기 위함)
    public static SetDataManager Instance;

    public bool IsReady { get; private set; }

    // =========================================================
    // [1] 로컬 데이터 변수
    // =========================================================

    [Header("User Info")]
    public int myLv = 1;
    public int myGd = 1000;
    public int myExp = 0;
    private string userIndate = "";

    [Header("Equipment")]
    // ★ 핵심 변경: Dictionary<string, object> -> JsonData
    // 이유: Dictionary로 받으면 내부 데이터가 object로 박싱되어 {}로 보이는 문제 해결
    public JsonData equipInventory;

    private string equipIndate = "";

    [Header("Monster")]
    public List<int> monsterList = new List<int>();
    public List<int> monsterSetting = new List<int>();
    private string monsterIndate = "";

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 변경 시 파괴되지 않게 하려면 추가 (선택사항)
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Initialize()
    {
        LoadAllData();
    }

    // =========================================================
    // [2] 초기화 및 데이터 로드
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

        // 2. EqueInven 로드 (장비)
        Backend.GameData.GetMyData("EqueInven", new Where(), callback =>
        {
            if (callback.IsSuccess())
            {
                if (callback.FlattenRows().Count > 0)
                {
                    var data = callback.FlattenRows()[0];
                    equipIndate = data["inDate"].ToString();

                    // 'IL' 컬럼이 있는지 확인
                    if (data.ContainsKey("IL"))
                    {
                        string jsonStr = data["IL"].ToString();

                        // ★ 핵심: 문자열을 JsonData로 바로 변환하여 저장
                        equipInventory = JsonMapper.ToObject(jsonStr);
                        Debug.Log("장비 데이터(JsonData) 로드 완료");
                    }
                    else
                    {
                        // 데이터가 없거나 컬럼이 없으면 초기화
                        InsertInitEquip();
                    }
                }
                else InsertInitEquip(); // 데이터 행 자체가 없으면 초기화
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

                // 로드 완료 후 자동 저장 루틴 시작
                StartCoroutine(AutoSaveRoutine());
            }
        });
    }

    // =========================================================
    // [3] 초기 데이터 생성 (Insert)
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

    // ★ 장비 초기값 생성 (JsonData 사용)
    void InsertInitEquip()
    {
        int totalSlots = 20;

        // JsonData 객체 생성 (Dictionary 역할)
        equipInventory = new JsonData();

        for (int i = 0; i < totalSlots; i++)
        {
            if (i == 0)
            {
                // 0번 슬롯: 기본 아이템 지급
                JsonData item = new JsonData();
                item["itemId"] = "1"; // 또는 Sword_001 (스프라이트 이름과 일치해야 함)
                item["count"] = 1;
                item["isEquip"] = true;

                equipInventory[i.ToString()] = item;
            }
            else
            {
                // 나머지 슬롯: 빈 값 (null)
                equipInventory[i.ToString()] = null;
            }
        }

        // 서버 전송을 위해 JsonData -> string 변환
        string inventoryJson = equipInventory.ToJson();

        Param param = new Param();
        param.Add("IL", inventoryJson); // IL 컬럼에 저장

        Backend.GameData.Insert("EqueInven", param, cb =>
        {
            if (cb.IsSuccess())
            {
                Debug.Log("초기 장비(IL) 데이터 생성 성공!");
                equipIndate = cb.GetInDate();
            }
            else
            {
                Debug.LogError("장비 생성 실패: " + cb);
            }
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
    // [4] 플레이 로직
    // =========================================================

    public void GetGoldAndExp(int gold, int exp)
    {
        myGd += gold;
        myExp += exp;
    }

    // ★ 장비 업데이트 함수 (JsonData를 받도록 변경)
    // 인벤토리에서 아이템 변경 후 이 함수를 호출하여 저장
    public void UpdateEquipment(JsonData newInven, bool isSpendMoney = false)
    {
        equipInventory = newInven;
        SaveEquipmentImmediate(); // 변경 즉시 저장

        if (isSpendMoney) SaveUserInfoImmediate();
    }

    public void UpdateMonster(List<int> newInven, List<int> newSetting, bool isSpendMoney = false)
    {
        monsterList = newInven;
        monsterSetting = newSetting;
        SaveMonsterImmediate();

        if (isSpendMoney) SaveUserInfoImmediate();
    }

    // =========================================================
    // [5] 저장 함수 (Save/Update)
    // =========================================================

    IEnumerator AutoSaveRoutine()
    {
        IsReady = true;
        while (true)
        {
            yield return new WaitForSeconds(600f); // 10분마다 저장
            SaveUserInfoImmediate();
            SaveEquipmentImmediate();
            SaveMonsterImmediate();
        }
    }

    public void SaveUserInfoImmediate()
    {
        if (string.IsNullOrEmpty(userIndate)) return;

        Param param = new Param();
        param.Add("Lv", myLv);
        param.Add("Gd", myGd);
        param.Add("Exp", myExp);

        Backend.GameData.UpdateV2("userInfo", userIndate, Backend.UserInDate, param, callback => {
            // 결과 처리
        });
    }

    // ★ 장비 저장 (JsonData -> String 변환 후 저장)
    public void SaveEquipmentImmediate()
    {
        if (string.IsNullOrEmpty(equipIndate)) return;
        if (equipInventory == null) return;

        Param param = new Param();

        // JsonData를 문자열(JSON String)로 변환
        string jsonString = equipInventory.ToJson();

        param.Add("IL", jsonString);

        Backend.GameData.UpdateV2("EqueInven", equipIndate, Backend.UserInDate, param, callback => {
            if (callback.IsSuccess()) Debug.Log("장비(IL) 저장 완료");
            else Debug.LogError("장비 저장 실패: " + callback);
        });
    }

    public void SaveMonsterImmediate()
    {
        if (string.IsNullOrEmpty(monsterIndate)) return;

        Param param = new Param();
        param.Add("ML", monsterList);
        param.Add("MS", monsterSetting);

        Backend.GameData.UpdateV2("MonsterInven", monsterIndate, Backend.UserInDate, param, callback => {
            // 결과 처리
        });
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveEquipmentImmediate();
            SaveMonsterImmediate();
            SaveUserInfoImmediate();
        }
    }

    // 유틸리티: JsonData -> List<int>
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