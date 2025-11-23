using UnityEngine;
using BACKND;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Tilemaps;
using static GameNetworkManager;
using System.Linq;

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
    public int mapcode;   // ★ 이 맵에 속한 타입인지 구분
    public string position; // 예: { "x": 345.5, "y": 64.0, "z": 780.2 }
}

public struct CreateCharacterMessage : NetworkMessage
{
    public string CharacterName;
}

public class GameNetworkManager : NetworkManager
{  
    [Header("Game UI")]
    public GameObject JoystickPrefab;

    [Header("Monster Settings")]
    private BACKND.DataTable _dataTable;
    private Dictionary<string, SettingMonsterData> _monsterConfigs;

    [Header("Monster Spawn")]
    // 맵별 목표 수(예: 1번=30, 2번=30)
    private Dictionary<int, int> SpwnCount = new Dictionary<int, int>
    {
        { 1, 1 }, { 2, 1 }
    };
     
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
        _dataTable = GetComponent<BACKND.DataTable>();
        // 화면 비율 설정
        SetCameraRect();
        base.Awake();
    }
    // ======================
    //  실행 주체 : 서버
    //  Awake 개념
    //  클라접속시 서버한테 메시지 보낼 수 있도록 미리 받을 준비 하기 위해 핸들러 등록
    // ======================    
    public override void OnStartServer()
    {
        Debug.Log("서버 실행");
        base.OnStartServer();

        InitServerSetData();                                                             // 몬스터 데이터 캐싱
        NetworkServer.RegisterHandler<CreateCharacterMessage>(OnCreateCharacterMessage); // 데디 서버 입장에서는 국민(클라)과 소통하기 위해 국민청원 같은 사이트를 만드는? 개념으로 데이터 전달받기위해 아래와 같이 핸들러를 미리 등록
        CalculateGroundBounds();
        Debug.Log("???");
        // 그라운드 경계 계산
        isServerRunning = true;                                                          // 이거 코루틴보다 밑에가있으면 안 됨 while그냥 종료되어버림 ㅋㅋ
        monsterManagerCoroutine = StartCoroutine(MonsterManagerRoutine());               // 서버 시작 시 몬스터 관리 코루틴 시작
    }
  
    // 몬스터 데이터 캐싱: DataTable → Dictionary<string, SettingMonsterData>
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



 
    [Server] // 초기 몬스터 스폰
    private void SpawnInitialMonsters()
    {
        if (_monsterConfigs == null || _monsterConfigs.Count == 0)
        {
            Debug.LogWarning("SpawnInitialMonsters: 몬스터 설정이 비어있습니다.");
            return;
        }
        Debug.Log(SpwnCount);
        foreach (var kv in SpwnCount)
        {
            int mapId = kv.Key;
            int target = kv.Value;
            if (target <= 0) continue;

            // 1) 이 맵에 속한 타입들만 임시 리스트로 모으기
            var typesForMap = new List<SettingMonsterData>();
            foreach (var d in _monsterConfigs.Values)
            {
                if (d.mapcode == mapId)
                    typesForMap.Add(d);
            }

            if (typesForMap.Count == 0)
            {
                Debug.LogWarning($"map {mapId}: 스폰 가능한 타입이 없습니다.");
                continue;
            }

            // 2) 라운드로빈으로 target 개수만큼 스폰
            for (int i = 0; i < target; i++)
            {
                var typeData = typesForMap[i % typesForMap.Count];
                SpawnMonsterForMap(mapId, typeData);
            }
        }
    }
    
    [Server] //몬스터 관리 코루틴 - 몬스터 수를 계속 확인하고 부족하면 스폰
    private IEnumerator MonsterManagerRoutine()
    {
        // 초기 스폰(맵별 라운드로빈 이미 구현됨)
        SpawnInitialMonsters();
        Debug.Log("서버 스폰 테스트");
        Debug.Log(isServerRunning);
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

    [Server]
    private GameObject SpawnMonsterForMap(int mapId, SettingMonsterData data)
    {
        if (data == null) return null;

        // 프리팹 이름은 data.name과 동일하다고 가정
        GameObject prefab = spawnPrefabs.Find(p => p.name == data.name);
        if (prefab == null)
        {
            Debug.LogWarning($"프리팹 없음: {data.name}");
            return null;
        }

        Vector3 pos = GetValidSpawnPosition();
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);

        var m = go.GetComponent<Monster>();
        if (m != null)
        {
            m.zoneId = mapId;                // 맵 식별자 주입(관리 편의)
            m.archetypeId = data.monster_id; // 타입 식별자(원하면 사용)
            m.SetGroundBounds(groundBounds);

            // 원하면 여기서 스탯도 초기화 가능:
            // m.maxHealth = data.health; m.currentHealth = data.health; ...
        }

        NetworkServer.Spawn(go);
        activeMonsters.Add(go);
        return go;
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
}
/*private void InitServerSetData()
  {
      // [ 몬스터 세팅 ]
      var config = NetworkManager.Instance.RemoteConfig;

      // Remote Config에서 monsterConfigs 키의 전체 JSON을 로드
      _monsterConfigs = config.GetValue<Dictionary<string, SettingMonsterData>>("MonsterData");

      if (_monsterConfigs == null || _monsterConfigs.Count == 0)
      {
          Debug.LogError("몬스터 설정을 불러오지 못했습니다.");
          return;
      }

      Debug.Log($"몬스터 설정 {_monsterConfigs.Count}개 로드됨");
      foreach (var kvp in _monsterConfigs)
      {
          var id = kvp.Key;
          var data = kvp.Value;

          Debug.Log($"[몬스터 ID: {id}]");
          Debug.Log($"  이름: {data.name}");
          Debug.Log($"  레벨: {data.level}");
          Debug.Log($"  HP: {data.hp}");
          Debug.Log($"  공격력: {data.attack}");
          Debug.Log($"  이동속도: {data.moveSpeed}");
          Debug.Log($"  리스폰 시간: {data.respawnTime}");
      }
      MonsterMaxCount = config.GetValue<int>("MonsterMaxCount");
      MonsterSpawnDelay = config.GetValue<float>("MonsterSpawnDelay");
      MinDistanceFromPlayers = config.GetValue<float>("MinDistanceFromPlayers");
      SpawnRadius = config.GetValue<int>("SpawnRadius");
  }

  public SettingMonsterData GetMonsterSetting(string monsterId)
  {
      if (_monsterConfigs.TryGetValue(monsterId, out var setting))
      {
          return setting;
      }

      Debug.LogWarning($"몬스터 ID {monsterId} 설정 없음");
      return null;
  }*/