using UnityEngine;
using BACKND;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Tilemaps;
using static GameNetworkManager;
using System.Linq;
using System;
using Random = UnityEngine.Random;

// ======================
// 몬스터 데이터 청사진
// ======================
[System.Serializable]
public class SettingMonsterData
{
    public int monster_id;
    public string name;
    public string type;
    public int level;
    public int health;
    public int damage;
    public int defense;
    public string mapcode;   // ★ 이 맵에 속한 타입인지 구분
    public string position; // 예: { "x": 345.5, "y": 64.0, "z": 780.2 }
}

public struct CreateCharacterMessage : NetworkMessage
{
    public string CharacterName;
}

public class GameNetworkManager : NetworkManager
{
    [Header("Server Data")]
    private RemoteConfig _remoteConfig;
    private BACKND.DataTable _dataTable;
    private Dictionary<string, int> _mapinfo = new Dictionary<string, int>();

    [Header("Parents")]
    public Transform Mapparent;
    public Transform Monsterparent;

    public List<Transform> SvMapSpawnList = new List<Transform>();
    private Dictionary<string, SettingMonsterData> _monsterConfigs;

    [Header("Game UI")]
    public GameObject JoystickPrefab;


    public int MonsterMaxCount = 100;
    public float MonsterSpawnDelay = 2f;         // 몬스터 재생성 딜레이
    public float MinDistanceFromPlayers = 25f;   // 플레이어로부터 최소 거리
    public float SpawnRadius = 50f;              // 몬스터 스폰 반경 (0,0에서부터의 거리)
    public float RespawnDelay = 3f;

    private List<GameObject> activeMonsters = new List<GameObject>();
    private bool isServerRunning = false;
    private Coroutine monsterManagerCoroutine;
    private Bounds groundBounds;

    public override void Awake()
    {
        // 화면 비율 설정
        SetCameraRect();
        base.Awake();
    }
    private void Start()
    {
        Init();
    }
    private void Init()
    {
        _dataTable = GetComponent<BACKND.DataTable>();
        _remoteConfig = NetworkManager.Instance.RemoteConfig; 
    }
    // ======================
    //  실행 주체 : 서버
    //  Awake 개념
    //  클라접속시 서버한테 메시지 보낼 수 있도록 미리 받을 준비 하기 위해 핸들러 등록
    // ======================    
    public override void OnStartServer()
    {
        Debug.Log("1_서버 실행");
        base.OnStartServer();

        // 데디 서버 입장에서는 국민(클라)과 소통하기 위해 국민청원 같은 사이트를 만드는? 개념으로 데이터 전달받기위해 아래와 같이 핸들러를 미리 등록
        NetworkServer.RegisterHandler<CreateCharacterMessage>(OnCreateCharacterMessage);

        _mapinfo = _remoteConfig.GetValue<Dictionary<string, int>>("mapinfo");

        // 1. map1:30 키쌍으로 된 RemoteConfig의 mapinfo json 데이터를 가져와서 GameDataManager의 네트워크 변수에 Add함 
        // 2. 클라를 위한 것이며 클라는 해당 리스트의 map1 map2 이런 값을 보고 어드레서블에서 맵을 세팅함
        // 3. 서버도 맵 세팅함 
        SetMap();

        // 서버가 데이터테이블 로드 하고 몬스터 정보 캐싱만 해둠
        InitServerSetData();    
        
        CalculateGroundBounds();
        Debug.Log("???");

        // 이거 코루틴보다 밑에가있으면 안 됨 while그냥 종료되어버림 ㅋㅋ
        isServerRunning = true;

        // 서버 시작 시 몬스터 관리 코루틴 시작
        monsterManagerCoroutine = StartCoroutine(MonsterManagerRoutine());               
    }

    [Server]
    private void SetMap()
    {
        GameObject prefab = spawnPrefabs.Find(p => p.name == "GameDataManager");
        GameObject go = Instantiate(prefab);
        NetworkServer.Spawn(go);
        var dataInstance = go.GetComponent<GameDataManager>();

        foreach (var kvp in _mapinfo)
        {
            string mapName = kvp.Key;

            Debug.Log($"1_2서버 실행 : GameDataManager 에 {mapName} 맵 변수 추가완료 ");
            dataInstance.mapList.Add(mapName);
        }
        dataInstance.ServerMapSet();
    }

    private void InitServerSetData()
    {
        // 딕셔너리 초기화
        _monsterConfigs = new Dictionary<string, SettingMonsterData>(64);

        // DataTable 확보(없으면 붙이기)
        _dataTable = _dataTable ?? GetComponent<BACKND.DataTable>()
                     ?? gameObject.AddComponent<BACKND.DataTable>();

        // 테이블 로드
        var rows = _dataTable.From<SettingMonsterData>().ToList();
        if (rows == null || rows.Count == 0)
        {
            Debug.LogError("❌ 몬스터 데이터 테이블이 비어있습니다.");
            return;
        }

        // 캐싱: key = monster_id.ToString()
        foreach (var row in rows)
        {
            string key = row.monster_id.ToString();
            _monsterConfigs[key] = row; // 존재하면 덮어쓰기(최신값 유지)
        }

        // 로그
        Debug.Log($"✅ 몬스터 설정 {_monsterConfigs.Count}개 캐싱 완료");
        foreach (var kv in _monsterConfigs)
        {
            var data = kv.Value;
            Debug.Log($"[몬스터 ID:{kv.Key}] name:{data.name}, type:{data.type}, level:{data.level}, hp:{data.health}, dmg:{data.damage}, def:{data.defense}");
        }
    }
     
    /// <summary> 
    /// 실행주체 : 서버
    /// 서버가 메시지를 받아들이고 > 데이터 세팅 후 최종적으로 > AddPlayerForConnection을 통해 Spawn함
    /// </summary>
    private void OnCreateCharacterMessage(NetworkConnectionToClient conn, CreateCharacterMessage message)
    {
        Transform startPosition = GetStartPosition();
        Vector3 spawnPosition = startPosition != null ? startPosition.position : Vector3.zero;
        Quaternion spawnRotation = startPosition != null ? startPosition.rotation : Quaternion.identity;

        Sprite prefabs = RootManager.Instance.AddressableCDD.GetSprite("Sword_2");

        if (prefabs != null)
        {
            GameObject players = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            var playerController = players.GetComponent<PlayerController>();
            playerController.equippedSpriteName = prefabs.name;
            playerController.CharacterName = message.CharacterName;

            NetworkServer.AddPlayerForConnection(conn, players);
        }
        else
        {
            Debug.LogError("❌ Addressables 프리팹을 찾을 수 없습니다: Pikachu");
        }
        //GameObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);
    }
    /// <summary> 
    /// 실행 주체 - 클라
    /// 클라이언트 → 서버로 메시지 전송
    /// 데이터 세팅한거 서버 한테 보내기
    /// TO DO 여기서 데이터 세팅하기 
    /// </summary>
    /// 
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log(" 클라 실행");
        CreateCharacterMessage createCharacterMessage = new CreateCharacterMessage
        {
            CharacterName = "Player1",
        };

        NetworkClient.Send(createCharacterMessage);
    }



    // 초기 몬스터 스폰
    [Server] 
    private void SpawnInitialMonsters()
    {

        if (_monsterConfigs == null || _monsterConfigs.Count == 0)
        {
            Debug.LogWarning("SpawnInitialMonsters: 몬스터 설정이 비어있습니다.");
            return;
        }

        // 맵 개수 만큼 돔 
        foreach (var kv in _mapinfo)
        {
            string mapKey = kv.Key;   // 예: "map1"
            int target = kv.Value;    // 예: 30

            // 맵이 실제 존재하지 않으면 continue
            if (SvMapSpawnList.All(t => t.name != mapKey))
            {
                Debug.LogWarning($"❌ {mapKey} 맵이 실제 SvMapSpawnList에 없음. 스폰 생략.");
                continue;
            }

            if (target <= 0) continue;

            // 데이터 테이블에서 가져온 모든 몬스터 중 map1에 해당하는 몬스터만 걸러서 리스트에 넣음
            var typesForMap = _monsterConfigs.Values
                .Where(d => d.mapcode == mapKey)
                .ToList();

            if (typesForMap.Count == 0)
            {
                Debug.LogWarning($"{mapKey}: 스폰 가능한 몬스터 타입이 없습니다.");
                continue;
            }

            // 스폰 개수 만큼 돔
            // 맵 이름 첫 번쨰 매게 변수 mapKey로 전달 
            // 두 번쨰 typeData는 몬스터 데이터임
            for (int i = 0; i < target; i++)
            {
                var typeData = typesForMap[i % typesForMap.Count];
                SpawnMonsterForMap(mapKey, typeData); // mapKey 전달 (string으로 바꿈)
            }
        }
    }
    
    [Server] //몬스터 관리 코루틴 - 몬스터 수를 계속 확인하고 부족하면 스폰
    private IEnumerator MonsterManagerRoutine()
    {
        // 초기 스폰(맵별 라운드로빈 이미 구현됨)
        SpawnInitialMonsters();
        while (isServerRunning)
        {
            Debug.Log("1");
            // 0) 진짜로 파괴(null)된 참조만 정리 (숨김/죽음 상태는 그대로 유지)
            for (int i = activeMonsters.Count - 1; i >= 0; --i)
            {
                if (activeMonsters[i] == null)
                    activeMonsters.RemoveAt(i);
            }
            Debug.Log(activeMonsters.Count);
            // 1) alive == false 인 몬스터만 보고 리스폰 스케줄/실행
            for (int i = 0; i < activeMonsters.Count; i++)
            {
                var go = activeMonsters[i];
                if (go == null) continue;

                var m = go.GetComponent<Monster>();
                if (m == null) continue;

                // 살아있으면 스킵
                if (m.alive) continue;

                Debug.Log(m.nextRespawnTime);
                // 예약 없으면 예약시간 설정
                if (m.nextRespawnTime <= 0f)
                {
                    m.nextRespawnTime = Time.time + RespawnDelay;
                    continue;
                }

                // 예약 시간이 지났으면 리스폰
                if (Time.time >= m.nextRespawnTime)
                {
                    // 위치 재설정
                    go.transform.position = GetValidSpawnPosition();

                    // 상태 초기화 (ResetForRespawn() 있으면 그거 쓰고, 없으면 아래 기본값)
                    // m.ResetForRespawn();
                    m.currentHealth = m.maxHealth;
                    var col = go.GetComponent<Collider2D>();
                    if (col) col.enabled = true;
                    m.nextRespawnTime = 0f;

                    // 보이기 ON (SyncVar라면 클라에 자동 반영)
                    m.alive = true;

                    // 폭주 방지(선택)
                    yield return null;
                }
            }

            // 2) 다음 틱까지 대기
            yield return new WaitForSeconds(MonsterSpawnDelay);
        }
    }

    // 실제 몬스터 스폰
    // 맵 마다 스폰포인트가 여러개 있는데 이 중 랜덤으로 몬스터 배치
    // 몬스터는 #Monster 하위의 맵별 오브젝트 자식으로 배치 되도록 함 (하이어라키 직관성)
    [Server]
    private GameObject SpawnMonsterForMap(string mapKey, SettingMonsterData data)
    {
        if (data == null) return null;

        GameObject prefab = spawnPrefabs.Find(p => p.name == data.name);
        if (prefab == null)
        {
            Debug.LogWarning($"프리팹 없음: {data.name}");
            return null;
        }

        // parent 만들기
        Transform parent = GetMonsterParent(mapKey);

        // 여기서 맵 자식 중 랜덤으로 위치 설정
        Vector3 pos = GetRandomSpawnPointFromMap(mapKey);  

        GameObject go = Instantiate(prefab, pos, Quaternion.identity, parent); // ★ 부모 적용됨


        var m = go.GetComponent<Monster>();
        if (m != null)
        {
            m.zoneId = mapKey;
            m.archetypeId = data.monster_id;
            m.SetGroundBounds(groundBounds);
        }

        NetworkServer.Spawn(go);
        activeMonsters.Add(go);
        return go;
    }
    private Transform GetMonsterParent(string mapKey)
    {
        // 1) Monsterparent 밑에서 같은 이름의 자식 찾기
        Transform mapRoot = Monsterparent.Find(mapKey);

        // 2) 없으면 새로 생성
        if (mapRoot == null)
        {
            GameObject go = new GameObject(mapKey);
            go.transform.SetParent(Monsterparent, false);
            go.transform.localPosition = Vector3.zero;
            mapRoot = go.transform;
        }

        return mapRoot;
    }
    private Vector3 GetRandomSpawnPointFromMap(string mapKey)
    {
        Transform mapRoot = SvMapSpawnList.FirstOrDefault(t => t.name == mapKey);
        if (mapRoot == null || mapRoot.childCount == 0)
        {
            Debug.LogWarning($"[스폰 위치 없음] {mapKey} 맵에 자식 오브젝트가 없습니다. 기본 위치 반환.");
            return new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0);  // 무조건 (0,0) 말고 랜덤
        }

        int index = Random.Range(0, mapRoot.childCount);
        return mapRoot.GetChild(index).position;
    }
    [Server] // 유효한 스폰 위치 찾기 (그라운드 크기 기반)
    private Vector3 GetValidSpawnPosition()
    {
        // 최대 시도 횟수 설정
        int maxAttempts = 30;
        
        // 스폰 중심점 (맵의 중앙)
        Vector3 spawnCenter = groundBounds.center;
        
        // 그라운드 크기와 SpawnRadius 중 작은 값 사용
        float mapWidth = groundBounds.size.x;
        float mapHeight = groundBounds.size.y;
        float effectiveRadius = Mathf.Min(SpawnRadius, Mathf.Min(mapWidth, mapHeight) / 2);
        
        for (int i = 0; i < maxAttempts; i++)
        {
            // 원 내부에 랜덤하게 위치 생성 (균등 분포를 위해 sqrt 사용)
            float angle = Random.Range(0f, Mathf.PI * 2);
            float distance = Mathf.Sqrt(Random.Range(0f, 1f)) * effectiveRadius;
            
            float posX = spawnCenter.x + Mathf.Cos(angle) * distance;
            float posY = spawnCenter.y + Mathf.Sin(angle) * distance;
            
            // 그라운드 경계 내로 제한
            float borderMargin = 1.0f;
            posX = Mathf.Clamp(posX, groundBounds.min.x + borderMargin, groundBounds.max.x - borderMargin);
            posY = Mathf.Clamp(posY, groundBounds.min.y + borderMargin, groundBounds.max.y - borderMargin);
            
            Vector3 position = new Vector3(posX, posY, 0);
            
            // 플레이어가 있는 경우에만 플레이어와의 거리 체크
            bool isTooCloseToPlayer = false;
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            
            if (players.Length > 0)
            {
                foreach (var player in players)
                {
                    if (Vector3.Distance(position, player.transform.position) < MinDistanceFromPlayers)
                    {
                        isTooCloseToPlayer = true;
                        break;
                    }
                }
            }
            
            // 다른 몬스터와의 거리도 확인
            bool isTooCloseToMonster = false;
            float minMonsterDistance = 2.0f; // 몬스터 간 최소 거리
            
            foreach (var monster in activeMonsters)
            {
                if (monster != null && Vector3.Distance(position, monster.transform.position) < minMonsterDistance)
                {
                    isTooCloseToMonster = true;
                    break;
                }
            }
            
            // 유효한 위치라면 반환 (플레이어가 없으면 플레이어 거리 체크 무시)
            if ((!isTooCloseToPlayer || players.Length == 0) && !isTooCloseToMonster)
            {
                return position;
            }
        }
        
        // 최대 시도 횟수를 초과하면 그냥 랜덤 위치 반환
        float fallbackAngle = Random.Range(0f, Mathf.PI * 2);
        float fallbackDistance = Mathf.Sqrt(Random.Range(0f, 1f)) * effectiveRadius;
        
        float fallbackX = spawnCenter.x + Mathf.Cos(fallbackAngle) * fallbackDistance;
        float fallbackY = spawnCenter.y + Mathf.Sin(fallbackAngle) * fallbackDistance;
        
        // 그라운드 경계 내로 제한
        float fbBorderMargin = 1.0f;
        fallbackX = Mathf.Clamp(fallbackX, groundBounds.min.x + fbBorderMargin, groundBounds.max.x - fbBorderMargin);
        fallbackY = Mathf.Clamp(fallbackY, groundBounds.min.y + fbBorderMargin, groundBounds.max.y - fbBorderMargin);
        
        return new Vector3(fallbackX, fallbackY, 0);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        isServerRunning = false;

        // 서버 중지 시 코루틴 중단
        if (monsterManagerCoroutine != null)
        {
            StopCoroutine(monsterManagerCoroutine);
            monsterManagerCoroutine = null;
        }

        // 모든 몬스터 제거
        ClearAllMonsters();
    }
    
    [Server] // 모든 몬스터 제거
    private void ClearAllMonsters()
    {
        foreach (var monster in activeMonsters)
        {
            if (monster != null)
            {
                NetworkServer.Destroy(monster);
            }
        }
        
        activeMonsters.Clear();
    }

    private void CalculateGroundBounds()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            // Grid에서 모든 Tilemap 찾기
            Tilemap[] tilemaps = ground.GetComponentsInChildren<Tilemap>();
            if (tilemaps != null && tilemaps.Length > 0)
            {
                // 모든 타일맵의 경계를 합쳐서 전체 경계 계산
                Bounds combinedBounds = new Bounds();
                bool firstBound = true;

                foreach (Tilemap tilemap in tilemaps)
                {
                    if (tilemap.GetComponent<TilemapRenderer>() != null)
                    {
                        // 타일맵의 사용된 영역 계산
                        tilemap.CompressBounds();

                        // 타일맵의 실제 사용된 영역의 경계 가져오기
                        if (firstBound)
                        {
                            // 로컬 좌표의 경계를 월드 좌표로 변환
                            Vector3 min = tilemap.transform.TransformPoint(tilemap.cellBounds.min);
                            Vector3 max = tilemap.transform.TransformPoint(tilemap.cellBounds.max);
                            combinedBounds = new Bounds();
                            combinedBounds.SetMinMax(min, max);
                            firstBound = false;
                        }
                        else
                        {
                            // 로컬 좌표의 경계를 월드 좌표로 변환
                            Vector3 min = tilemap.transform.TransformPoint(tilemap.cellBounds.min);
                            Vector3 max = tilemap.transform.TransformPoint(tilemap.cellBounds.max);
                            Bounds tileBounds = new Bounds();
                            tileBounds.SetMinMax(min, max);

                            combinedBounds.Encapsulate(tileBounds);
                        }
                    }
                }

                groundBounds = combinedBounds;
            }
        }

        // 그라운드가 없거나 계산 실패 시 기본 경계 설정
        if (groundBounds.size.x <= 0 || groundBounds.size.y <= 0)
        {
            groundBounds = new Bounds(Vector3.zero, new Vector3(SpawnRadius * 2, SpawnRadius * 2, 0));
        }
    }

    private void SetCameraRect()
    {
        Camera mainCamera = Camera.main;

        // 세로 모드에서는 9:16 비율 유지
        float targetAspect = 9.0f / 16.0f;
        float windowAspect = (float)Screen.width / (float)Screen.height;
        float scaleWidth = windowAspect / targetAspect;

        if (scaleWidth < 1.0f)
        {
            Rect rect = mainCamera.rect;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
            mainCamera.rect = rect;
        }
        else
        {
            float scaleHeight = 1.0f / scaleWidth;
            Rect rect = mainCamera.rect;
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
            mainCamera.rect = rect;
        }
    }
    

    public SettingMonsterData GetMonsterSetting(string monsterId)
    {
        if (_monsterConfigs.TryGetValue(monsterId, out var setting))
        {
            return setting;
        }

        Debug.LogWarning($"몬스터 ID {monsterId} 설정 없음");
        return null;
    }
}