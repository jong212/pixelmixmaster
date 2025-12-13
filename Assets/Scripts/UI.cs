using BackEnd;
using BackEnd.Quobject.SocketIoClientDotNet.Client;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public bool IsReady { get; private set; }
    public GameObject[] _sprites;


    public void Initialize()
    {

        SetSprites();
        SetUIData();
    }
    private void SetSprites()
    {
        var uiSprites = RootManager.Instance.AddressableCDD.SpriteCache;

        foreach (var spriteObj in _sprites)
        {
            var img = spriteObj.GetComponent<Image>();
            var sr = spriteObj.GetComponent<SpriteRenderer>();

            string key = spriteObj.name;

            if (!uiSprites.TryGetValue(key, out var sprite))
            {
                Debug.LogError($"? SpriteCache에 '{key}' 없음 | 현재 캐시: {string.Join(", ", uiSprites.Keys)}");
                continue;
            }

            if (img != null)
            {
                img.sprite = sprite;
                Debug.Log($"? Image 적용: {key}");
            }
            else if (sr != null)
            {
                sr.sprite = sprite;
                Debug.Log($"? SpriteRenderer 적용: {key}");
            }
            else
            {
                Debug.LogWarning($"?? '{key}'에 Image도 SpriteRenderer도 없음");
            }
        }
        IsReady = true;
    }
    private void SetUIData()
    {

    }
    public enum UIPanelType
    {
        Inventory,
        Info,
        Pet,
        Setting,
        Shop
    }

    [Header("Hierarchy에 있는 패널들을 넣어주세요")]
    public List<BasePanel> panels; // GameObject 대신 BasePanel 스크립트를 리스트로 받음

    private Dictionary<UIPanelType, BasePanel> panelDic;
    private void Awake()
    {
        panelDic = new Dictionary<UIPanelType, BasePanel>();

        foreach (var p in panels)
        {
            if (p == null) continue;

            // 딕셔너리에 타입별로 등록
            if (!panelDic.ContainsKey(p.panelType))
            {
                panelDic.Add(p.panelType, p);
            }

            // 시작할 때 다 닫기
            p.Close();
        }
    }

    public void Show(UIPanelType type)
    {
        // 1. 모든 패널 닫기 (하나만 켜지는 모드)
        foreach (var kv in panelDic)
        {
            kv.Value.Close();
        }

        // 2. 원하는 패널만 열기 (Open 함수 호출로 데이터 갱신 가능)
        if (panelDic.ContainsKey(type))
        {
            panelDic[type].Open();
        }
    }

    public void Toggle(UIPanelType type)
    {
        if (!panelDic.ContainsKey(type)) return;

        bool isActive = panelDic[type].gameObject.activeSelf;

        if (isActive)
        {
            panelDic[type].Close(); // 닫기
        }
        else
        {
            Show(type); // 열기 (나머지는 닫힘)
        }
    }
    public void OnClickInventory()
    {
        Toggle(UIPanelType.Inventory);
    }
    public void OnClickInfo()
    {
        Toggle(UIPanelType.Info);
    }
    public void OnClickPet()
    {
        Toggle(UIPanelType.Pet);
    }
}
