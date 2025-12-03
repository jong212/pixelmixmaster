using BackEnd;
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
        SetIngameUI();
    }
    private void SetIngameUI()
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
}
