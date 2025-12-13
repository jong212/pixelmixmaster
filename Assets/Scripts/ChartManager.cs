using UnityEngine;
using BackEnd;
using LitJson;
using System.Collections.Generic;
using System.Collections;
using TMPro;
// 차트 다운 받을 떄 쓰는 설계도
public class ChartInfo
{
    public string chartName;
    public string chartFileId;
    public string updateDate;

    public ChartInfo(JsonData json)
    {
        chartName = json["chartName"].ToString();
        chartFileId = json["chartFileId"].ToString();
        updateDate = json["updateDate"].ToString();
    }
}

// 다운 받은 차트의 데이터를 넣을 설계도
public class InvenInfo
{
    public int ItemId { get; private set; }
    public string Type { get; private set; }
    public string Name { get; private set; }
    public float Atk { get; private set; }
    public int CanLv { get; private set; }
    public float SetLevel { get; private set; }
    public string Direct { get; private set; }
    public float Sx { get; private set; }
    public float Sy { get; private set; }
    public InvenInfo(JsonData json)
    {
        ItemId = int.Parse(json["ItemId"].ToString());
        Type = json["Type"].ToString();
        Name = json["Name"].ToString();
        Atk = float.Parse(json["Atk"].ToString());
        CanLv = int.Parse(json["CanLv"].ToString());
        Direct = json["Direct"].ToString();
        Sx = float.Parse(json["Sx"].ToString());
        Sy = float.Parse(json["Sy"].ToString());

    }
}

public class ChartManager
{
    public bool IsReady { get; private set; }

    private List<string> _getCharLocalListname = new List<string>();
    public List<string> GetCharLocalListname
    {
        get => _getCharLocalListname;
        set => _getCharLocalListname = value;
    }

    private List<InvenInfo> _invenList = new List<InvenInfo>();
    public List<InvenInfo> InvenInfoList                       // 캐싱 -테스트
    {
        get => _invenList;
        set => _invenList = value;
    }

    public void Initialize()
    {
        RootManager.Instance.Coroutine_Action(0.1f, () => RootManager.Instance.StartCoroutine(ServerCharLoad()));

    }

    IEnumerator ServerCharLoad()
    {

        var bro = Backend.Chart.GetChartListByFolder(2838);                                 // 차트 매니저 폴더 접근 
        string chartManagerFileId = bro.FlattenRows()[0]["selectedChartFileId"].ToString(); // CSV 파일 업로드 할 때 부여 된 고유 파일 ID 값을 가져옴 ex 145150, // 해당 폴더에는 chartManager 차트 하나만 존재할 것이므로 0으로 접근합니다.
        string chartManagerName = bro.FlattenRows()[0]["chartName"].ToString();
        var serverChartBro = Backend.Chart.GetChartContents(chartManagerFileId);            // 서버에서 ChartManager 차트를 불러옵니다. 기기에 저장하지는 않습니다.
        if (serverChartBro.IsSuccess() == false)                                            // 서버에서 불러오지 못할 경우에는 데이터 꼬임 방지를 위해 진행을 중지합니다.
        {
            Debug.Log($"1-4 TO DO : 차트 통신 실패 ");
            yield break;
        }


        JsonData newChartManagerJson = serverChartBro.FlattenRows();                        // 서버에서 불러온 ChartManager을 언마샬하여 JsonData 형태로 캐싱합니다.
        Dictionary<string, ChartInfo> chartInfoDic = new Dictionary<string, ChartInfo>();   // 차트 이름으로 데이터를 검색할 것이기 때문에 Dictnary로 생성합니다, // 해당 Dictnary는 최신 버전으로 업데이트할 차트 리스트로 사용됩니다.(최신 버전이라면 해당 리스트에서 제외)


        foreach (JsonData chartInfoJson in newChartManagerJson)                             // csv 한 줄 = charinfojson
        {
            ChartInfo chartInfo = new ChartInfo(chartInfoJson);
            chartInfoDic.Add(chartInfo.chartName, chartInfo);
            GetCharLocalListname.Add(chartInfo.chartName);
        }
        string deviceChartManagerString = Backend.Chart.GetLocalChartData(chartManagerName);// 기기에 저장된 chartManager 차트를 불러옵니다.
        if (string.IsNullOrEmpty(deviceChartManagerString) == false)                        // 기기에는 string 형태로 저장이 되며, 저장되어있지 않을 경우 string.Empty가 반환됩니다.
        {
            JsonData deviceChartManagerJson = JsonMapper.ToObject(deviceChartManagerString);// 기기에 저장된 chartManager 차트가 존재한다면 // 기기에 저장된 string형태의 chartManager를 Json 형태로 변경
            deviceChartManagerJson = BackendReturnObject.Flatten(deviceChartManagerJson);

            foreach (JsonData deviceChartJson in deviceChartManagerJson["rows"])            // 기기에 저장된 chartManager 차트 속 차트들을 서버에서 불러온 데이터와 대조합니다.
            {
                ChartInfo deviceChartInfo = new ChartInfo(deviceChartJson);
                if (chartInfoDic.ContainsKey(deviceChartInfo.chartName))                    // 이미 기기에 저장되어 있는 차트가 있는지 확인합니다.
                {
                    if (chartInfoDic[deviceChartInfo.chartName].updateDate == deviceChartInfo.updateDate)// 기기에 저장되어 있는 차트의 수정 날짜(updateDate)가 일치하는지 확인합니다.
                    {
                        chartInfoDic.Remove(deviceChartInfo.chartName);                     // 수정날짜까지 일치할 경우, 재다운로드 리스트(chartInfoDic)에서 제외합니다.
                    }
                }
            }
        }

        if (chartInfoDic.Count > 0)                                                         // 재다운로드할 차트 리스트에서 차트가 하나라도 존재하는지 확인합니다.
        {
            Debug.Log($"1-4 : 다운 받을 새로운 차트 확인");
            RootManager.Instance.AddressableCDD._statusText.text = "게임 데이터 다운로드중...";
            foreach (var downloadChartInfo in chartInfoDic)                                 // 차트를 재다운로드하여 기기에 덮어씌웁니다.
            {

                var bro2 = Backend.Chart.GetOneChartAndSave(
                    downloadChartInfo.Value.chartFileId,
                    downloadChartInfo.Value.chartName
                );

                if (!bro2.IsSuccess())
                {
                    Debug.Log($"1-5 TO DO : 다운 받을 새로운 다운 실패 ");
                    continue;
                }
            }
            // chartManager 차트를 최신화합니다.(로컬저장)
            var chartManagerBro = Backend.Chart.GetOneChartAndSave(chartManagerFileId, chartManagerName);
            if (chartManagerBro.IsSuccess())
            {
                RootManager.Instance.AddressableCDD._statusText.text = "게임 데이터 다운로드 완료";
                Debug.Log("1-5 : Chart모든 차트 다운로드 완료");

            }
            else
            {
                Debug.Log("1-5 TO DO : Chart 다운로드 실패");
            }
        }
        else
        {
            RootManager.Instance.AddressableCDD._statusText.text = "게임 데이터 최신 상태..";
            Debug.Log($"1-5 : 다운 받을 차트 없음");
        }
        foreach (var chartName in GetCharLocalListname)
        {
            LoadChart(chartName);
        }
    }
    private void LoadChart(string chartName)
    {
        RootManager.Instance.AddressableCDD._statusText.text = "데이터 캐싱중...";
        Debug.Log("1-6 : 차트 데이터 변수에 넣어놓기");
        string chartDataString = Backend.Chart.GetLocalChartData(chartName);
        JsonData chartJson = JsonMapper.ToObject(chartDataString);
        chartJson = BackendReturnObject.Flatten(chartJson);

        switch (chartName)
        {
            case nameof(InvenInfo):
                foreach (JsonData row in chartJson["rows"])
                {
                    InvenInfo classRef = new InvenInfo(row);
                    InvenInfoList.Add(classRef);
                    Debug.Log(InvenInfoList.Count);
                }
                break;
     /*       case nameof(Notice):
                foreach (JsonData row in chartJson["rows"])
                {
                    Notice classRef = new Notice(row);
                    NoticeList.Add(classRef);
                }
                break;*/
        }
        IsReady = true;
       // RootManager.Instance.AddressableCDD._nextScene.gameObject.SetActive(true);
        /* RootManager.Instance.StartCoroutine(WriteNotice());
         RootManager.Instance.NextInit("AdManager");*/
    }

   /* private IEnumerator WriteNotice()
    {
        yield return new WaitForSeconds(0.2f);

        var notiList = RootManager.Instance.ChartManager.NoticeList;

        foreach (var notice in notiList)
        {
            string regDate = notice.Regdate;
            string notiText = notice.Text;

            var NoticeText = RootManager.Instance.AddressableCDD.NoticeTextObj;
            var NoticeTextParent = RootManager.Instance.AddressableCDD.NoticeParent;

            var newText = UnityEngine.Object.Instantiate(NoticeText, NoticeTextParent);

            newText.GetChild(0).GetComponent<TextMeshProUGUI>().text = regDate;
            newText.GetChild(1).GetComponent<TextMeshProUGUI>().text = notiText;

            //  0.1초 간격으로 생성 (필요 없으면 주석)
        }
        yield return new WaitForSeconds(0.1f);
        RootManager.Instance.AddressableCDD._statusText.text = "게임 준비 완료";

    }*/
}
