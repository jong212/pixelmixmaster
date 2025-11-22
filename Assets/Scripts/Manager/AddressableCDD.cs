using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using BACKND;
public class AddressableCDD : MonoBehaviour
{
    public bool IsReady { get; private set; }


    [Header("UI Components")]
    [SerializeField] private Button _downloadButton;
    public Button _nextScene;
    public TMP_Text _statusText;
    [SerializeField] private Slider _progressBar;

    [Header("Addressables Labels")]
    [SerializeField] private string _prefabLabel = "Prefabs";
    [SerializeField] private string _spriteLabel = "Sprites";

    public Transform FilePatchObj;
    public Transform NoticeParent;
    public Transform NoticeTextObj;

    public Image _bg;
    //  캐시용 딕셔너리
    private Dictionary<string, GameObject> _prefabCache = new();
    private Dictionary<string, Sprite> _spriteCache = new();
    public IReadOnlyDictionary<string, GameObject> PrefabCache => _prefabCache;
    public IReadOnlyDictionary<string, Sprite> SpriteCache => _spriteCache;
    public void Initialize()
    {

#if UNITY_SERVER
            StartCoroutine(ServerLogic());
#else
        // 클라이언트 전용 코드
        FilePatchObj.gameObject.SetActive(true);
        _progressBar.gameObject.SetActive(false);
        _downloadButton.gameObject.SetActive(false);
        StartCoroutine(CheckCatalogUpdate());
#endif



    }
#if UNITY_SERVER
    private IEnumerator ServerLogic()
    {
        yield return StartCoroutine(LoadSprites());
    }
#endif
    private IEnumerator CheckCatalogUpdate()
    {
        _statusText.text = "카탈로그 확인중..";

        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        yield return checkHandle;

        if (checkHandle.Status == AsyncOperationStatus.Succeeded &&
            checkHandle.Result != null &&
            checkHandle.Result.Count > 0)
        {
            Debug.Log("📌 새로운 카탈로그 있음 → 업데이트 중");
            var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result);
            yield return updateHandle;

            Debug.Log("✅ 카탈로그 업데이트 완료");
        }
        else
        {
            Debug.Log("📌 카탈로그 최신 상태");
        }

        // ✅ 카탈로그 업데이트 끝나고 나서 다운로드 체크 진행
        StartCoroutine(CheckAllDownloadStatus());
    }

    //  프리팹 + 스프라이트 모두 다운로드 상태 체크
    private IEnumerator CheckAllDownloadStatus()
    {
        _statusText.text = "리소스 파일 체크중..";

        long totalSize = 0;


        // Sprite 라벨 체크
        var spriteSizeHandle = Addressables.GetDownloadSizeAsync(_spriteLabel);
        yield return spriteSizeHandle;
        if (spriteSizeHandle.Status == AsyncOperationStatus.Succeeded)
            totalSize += spriteSizeHandle.Result;

        Addressables.Release(spriteSizeHandle);

        // 다운로드 필요 여부 판단
        yield return new WaitForSeconds(0.3f);
        if (totalSize > 0)
        {
            _statusText.text = $"업데이트 필요 {totalSize / (1024f * 1024f):F2} MB";
            _downloadButton.gameObject.SetActive(true);
            _downloadButton.onClick.AddListener(InitiateDownload);
        }
        else
        {
            _statusText.text = "Complete Resource..";
            StartCoroutine(LoadAllAssetsToCache());
        }
    }

    private void InitiateDownload()
    {
        _statusText.text = "다운로드 중입니다...";
        _downloadButton.gameObject.SetActive(false);
        _progressBar.gameObject.SetActive(true);
        StartCoroutine(DownloadAllAssets());
    }

    // ?? Prefab + Sprite 라벨 각각 다운로드
    private IEnumerator DownloadAllAssets()
    {
        var spriteDownload = Addressables.DownloadDependenciesAsync(_spriteLabel);

        while (!spriteDownload.IsDone)
        {
            var s = spriteDownload.GetDownloadStatus();

            long total = s.TotalBytes;
            long downloaded = s.DownloadedBytes;

            _progressBar.value = total > 0 ? (float)downloaded / total : 1f;
            yield return null;
        }


        bool success = spriteDownload.Status == AsyncOperationStatus.Succeeded;

        Addressables.Release(spriteDownload);

        if (success)
        {
            _statusText.text = "리소스 다운로드 완료";
            _progressBar.value = 1f;
            StartCoroutine(LoadAllAssetsToCache(1f));
        }
        else
        {
            _statusText.text = "Download failed!";
            _progressBar.gameObject.SetActive(false);
        }
    }



    private IEnumerator LoadPrefabs()
    {
        // Prefab 라벨에 포함된 모든 에셋 주소(Location) 가져오기
        var locationsHandle = Addressables.LoadResourceLocationsAsync(_prefabLabel, typeof(GameObject));
        yield return locationsHandle;

        if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("❌ Failed to load prefab locations!");
            yield break;
        }

        // 각 주소(Location)마다 에셋 직접 로드
        foreach (var loc in locationsHandle.Result)
        {
            var loadHandle = Addressables.LoadAssetAsync<GameObject>(loc);
            yield return loadHandle;

            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                var prefab = loadHandle.Result;
                if (!_prefabCache.ContainsKey(loc.PrimaryKey))
                {
                    _prefabCache[loc.PrimaryKey] = prefab;
                    Debug.Log($"✅ Cached Prefab Asset: {loc.PrimaryKey}");
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ Failed to load prefab from {loc.PrimaryKey}");
            }
        }

        // 여기서는 Release 금지 (Asset 자체이므로 유지)
    }


    private IEnumerator LoadSprites()
    {
        var handle = Addressables.LoadAssetsAsync<Sprite>(_spriteLabel, (sprite) =>
        {
            Debug.Log($"[로드된 스프라이트 이름] => {sprite.name}");  // ✅ 여기가 중요!

            if (!_spriteCache.ContainsKey(sprite.name))
            {
                _spriteCache.Add(sprite.name, sprite);
                Debug.Log($"Cached Sprite: {sprite.name}");
            }
        });
        yield return handle;
    }

    // ?? Prefab + Sprite 로드 후 캐싱
    private IEnumerator LoadAllAssetsToCache(float delay = 0f)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _statusText.text = "Loading Sprites...";
        yield return LoadSprites();

        _statusText.text = "All assets cached!";
        yield return new WaitForSeconds(0.5f);
        IsReady = true;

        yield break;
    }

    // ?? 캐시 접근용 함수
    public GameObject GetPrefab(string name)
    {
        _prefabCache.TryGetValue(name, out var prefab);
        return prefab;
    }
    public void LogAllCachedSprites()
    {
        foreach (var kvp in _spriteCache)
        {
            Debug.Log($"[Cache Key] {kvp.Key} → Sprite Name: {kvp.Value.name}");
        }
    }
    public Sprite GetSprite(string name)
    {
        Debug.Log(name);
        LogAllCachedSprites();
        _spriteCache.TryGetValue(name, out var sprite);
        Debug.Log(sprite);
        return sprite;
    }
    private void Update()
    {
        /* // PrefabCache 상태 확인
         foreach (var kvp in _prefabCache)
         {
             if (kvp.Value == null)
             {
                 Debug.LogWarning($"❌ Destroyed prefab detected: {kvp.Key}");
             }
             else
             {
                 Debug.Log($"✅ Alive prefab: {kvp.Key}");
             }
         }

         // SpriteCache 상태 확인 (참고용)
         foreach (var kvp in _spriteCache)
         {
             if (kvp.Value == null)
             {
                 Debug.LogWarning($"❌ Destroyed sprite detected: {kvp.Key}");
             }
         }
 */
    }
}

