using UnityEngine;
using BackEnd;
using LitJson;
using System.Collections.Generic;
using System.Collections;
using TMPro;
// ��Ʈ �ٿ� ���� �� ���� ���赵
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

// �ٿ� ���� ��Ʈ�� �����͸� ���� ���赵
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
    public int Hp { get; private set; }
    public int AnimIdx { get; private set; }
    public float ARange { get; private set; }
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
        Hp = int.Parse(json["Hp"].ToString());
        AnimIdx = int.Parse(json["AnimIdx"].ToString());
        ARange = float.Parse(json["ARange"].ToString());
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
    public List<InvenInfo> InvenInfoList                       // ĳ�� -�׽�Ʈ
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

        var bro = Backend.Chart.GetChartListByFolder(2838);                                 // ��Ʈ �Ŵ��� ���� ���� 
        string chartManagerFileId = bro.FlattenRows()[0]["selectedChartFileId"].ToString(); // CSV ���� ���ε� �� �� �ο� �� ���� ���� ID ���� ������ ex 145150, // �ش� �������� chartManager ��Ʈ �ϳ��� ������ ���̹Ƿ� 0���� �����մϴ�.
        string chartManagerName = bro.FlattenRows()[0]["chartName"].ToString();
        var serverChartBro = Backend.Chart.GetChartContents(chartManagerFileId);            // �������� ChartManager ��Ʈ�� �ҷ��ɴϴ�. ��⿡ ���������� �ʽ��ϴ�.
        if (serverChartBro.IsSuccess() == false)                                            // �������� �ҷ����� ���� ��쿡�� ������ ���� ������ ���� ������ �����մϴ�.
        {
            Debug.Log($"1-4 TO DO : ��Ʈ ��� ���� ");
            yield break;
        }


        JsonData newChartManagerJson = serverChartBro.FlattenRows();                        // �������� �ҷ��� ChartManager�� �𸶼��Ͽ� JsonData ���·� ĳ���մϴ�.
        Dictionary<string, ChartInfo> chartInfoDic = new Dictionary<string, ChartInfo>();   // ��Ʈ �̸����� �����͸� �˻��� ���̱� ������ Dictnary�� �����մϴ�, // �ش� Dictnary�� �ֽ� �������� ������Ʈ�� ��Ʈ ����Ʈ�� ���˴ϴ�.(�ֽ� �����̶�� �ش� ����Ʈ���� ����)


        foreach (JsonData chartInfoJson in newChartManagerJson)                             // csv �� �� = charinfojson
        {
            ChartInfo chartInfo = new ChartInfo(chartInfoJson);
            chartInfoDic.Add(chartInfo.chartName, chartInfo);
            GetCharLocalListname.Add(chartInfo.chartName);
        }
        string deviceChartManagerString = Backend.Chart.GetLocalChartData(chartManagerName);// ��⿡ ����� chartManager ��Ʈ�� �ҷ��ɴϴ�.
        if (string.IsNullOrEmpty(deviceChartManagerString) == false)                        // ��⿡�� string ���·� ������ �Ǹ�, ����Ǿ����� ���� ��� string.Empty�� ��ȯ�˴ϴ�.
        {
            JsonData deviceChartManagerJson = JsonMapper.ToObject(deviceChartManagerString);// ��⿡ ����� chartManager ��Ʈ�� �����Ѵٸ� // ��⿡ ����� string������ chartManager�� Json ���·� ����
            deviceChartManagerJson = BackendReturnObject.Flatten(deviceChartManagerJson);

            foreach (JsonData deviceChartJson in deviceChartManagerJson["rows"])            // ��⿡ ����� chartManager ��Ʈ �� ��Ʈ���� �������� �ҷ��� �����Ϳ� �����մϴ�.
            {
                ChartInfo deviceChartInfo = new ChartInfo(deviceChartJson);
                if (chartInfoDic.ContainsKey(deviceChartInfo.chartName))                    // �̹� ��⿡ ����Ǿ� �ִ� ��Ʈ�� �ִ��� Ȯ���մϴ�.
                {
                    if (chartInfoDic[deviceChartInfo.chartName].updateDate == deviceChartInfo.updateDate)// ��⿡ ����Ǿ� �ִ� ��Ʈ�� ���� ��¥(updateDate)�� ��ġ�ϴ��� Ȯ���մϴ�.
                    {
                        chartInfoDic.Remove(deviceChartInfo.chartName);                     // ������¥���� ��ġ�� ���, ��ٿ�ε� ����Ʈ(chartInfoDic)���� �����մϴ�.
                    }
                }
            }
        }

        if (chartInfoDic.Count > 0)                                                         // ��ٿ�ε��� ��Ʈ ����Ʈ���� ��Ʈ�� �ϳ��� �����ϴ��� Ȯ���մϴ�.
        {
            Debug.Log($"1-4 : �ٿ� ���� ���ο� ��Ʈ Ȯ��");
            RootManager.Instance.AddressableCDD._statusText.text = "���� ������ �ٿ�ε���...";
            foreach (var downloadChartInfo in chartInfoDic)                                 // ��Ʈ�� ��ٿ�ε��Ͽ� ��⿡ �����ϴ�.
            {

                var bro2 = Backend.Chart.GetOneChartAndSave(
                    downloadChartInfo.Value.chartFileId,
                    downloadChartInfo.Value.chartName
                );

                if (!bro2.IsSuccess())
                {
                    Debug.Log($"1-5 TO DO : �ٿ� ���� ���ο� �ٿ� ���� ");
                    continue;
                }
            }
            // chartManager ��Ʈ�� �ֽ�ȭ�մϴ�.(��������)
            var chartManagerBro = Backend.Chart.GetOneChartAndSave(chartManagerFileId, chartManagerName);
            if (chartManagerBro.IsSuccess())
            {
                RootManager.Instance.AddressableCDD._statusText.text = "���� ������ �ٿ�ε� �Ϸ�";
                Debug.Log("1-5 : Chart��� ��Ʈ �ٿ�ε� �Ϸ�");

            }
            else
            {
                Debug.Log("1-5 TO DO : Chart �ٿ�ε� ����");
            }
        }
        else
        {
            RootManager.Instance.AddressableCDD._statusText.text = "���� ������ �ֽ� ����..";
            Debug.Log($"1-5 : �ٿ� ���� ��Ʈ ����");
        }
        foreach (var chartName in GetCharLocalListname)
        {
            LoadChart(chartName);
        }
    }
    private void LoadChart(string chartName)
    {
        RootManager.Instance.AddressableCDD._statusText.text = "������ ĳ����...";
        Debug.Log("1-6 : ��Ʈ ������ ������ �־����");
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

            //  0.1�� �������� ���� (�ʿ� ������ �ּ�)
        }
        yield return new WaitForSeconds(0.1f);
        RootManager.Instance.AddressableCDD._statusText.text = "���� �غ� �Ϸ�";

    }*/
}
