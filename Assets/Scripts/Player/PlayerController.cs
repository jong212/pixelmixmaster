using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BACKND;
using SimpleInputNamespace;
using LitJson;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Combat Settings")]
    [SyncVar] public float attackRange = 1.5f; // ★ SyncVar 추가
    public int attackDamage = 10;    // 공격력

    [Header("Camera Settings")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -10);
    [SerializeField] private float cameraSmoothing = 0.125f;

    [Header("Meteor System")]
    [SerializeField] private float meteorSpawnInterval = 0.5f;

    // 스네이크 히스토리
    [Header("Snake Trail Settings")]
    public List<Vector3> positionHistory = new List<Vector3>();
    public float recordInterval = 0.02f;
    private float recordTimer = 0f;

    // 입력 및 구성 요소
    public Vector2 movement;
    public Rigidbody2D rb;
    public Joystick joystick;
    private bool isFacingRight = true;
    private Camera mainCamera;
    private Coroutine meteorCoroutine;
    public SpriteRenderer parts_weapon;
    public SpriteRenderer parts_helmet_front;
    public SpriteRenderer parts_helmet_back;
    public SpriteRenderer parts_cloth;
    public SpriteRenderer parts_cloth_left;
    public SpriteRenderer parts_cloth_right;
    public SpriteRenderer parts_pant_left;
    public SpriteRenderer parts_pant_right;
 
    private NetworkAnimator networkAnim;
    private PlayerObj playerObj;
    [SyncVar] public string CharacterName;

    [Header("Sprite")]
    [SyncVar(hook = nameof(ChangeWeapon))]
    public string weaponName = "Default";

    [SyncVar(hook = nameof(ChangeHelment))]
    public string helmetName = "Default";

    [SyncVar(hook = nameof(ChangeCloth))]
    public string ClothName = "Default";

    [SyncVar(hook = nameof(ChangePant))]
    public string PantName = "Default";

    [SyncVar(hook = nameof(OnAnimStateChanged))]
    private PlayerState _netState = PlayerState.IDLE;
    // ★ 추가: 공격 애니메이션 인덱스도 SyncVar로
    private SpriteRenderer spriteRenderer;

    [Header("Auto Combat Settings")]
    public bool enableAutoCombat = true;

    private float autoDetectRadius = 3f;   // 이 거리 안에 있으면 조이스틱으로 움직여도 계속 같은 몬스터 타겟 유지
    private float autoChaseRadius = 6f;   // 기존 타겟 유지 최대 거리 (넘으면 타겟 버림)

    public float autoAttackInterval = 0.7f; // 몇초에 한 번 공격할지
    public float autoScanInterval = 0.2f;   // 타겟 탐색 주기(성능용)
    public float inputDeadZone = 0.15f;     // 조이스틱 데드존

    public float attackStateLockTime = 0.35f; // ATTACK 애니 락 시간(서버)

    public LayerMask monsterLayer; // (선택) 몬스터 레이어 지정하면 더 정확/빠름

    private Vector2 _inputMove;
    private Vector2 _autoMove;

    private GameObject _currentTarget;
    private float _nextAutoAttackTime;
    private float _nextScanTime;
    private Coroutine _attackLockCo;

    // =================================================================
    // ★ 1. 서버 접속 시: 출석부에 내 이름 적기
    // =================================================================
    public override void OnStartServer()
    {
        base.OnStartServer(); // 필수!
        // 게임 매니저(서버 관리자)가 있으면 나를 등록
        RootManager.Instance.GameNetworkManager.RegisterPlayer(this);
    }

    // =================================================================
    // ★ 2. 서버 접속 종료 시: 출석부에서 이름 지우기
    // =================================================================
    public override void OnStopServer()
    {
        RootManager.Instance.GameNetworkManager.UnregisterPlayer(this);
        base.OnStopServer(); // 필수!
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        networkAnim = GetComponent<NetworkAnimator>();
        playerObj = GetComponent<PlayerObj>();
        //spriteRenderer = GetComponent<SpriteRenderer>(); // ✅ SpriteRenderer 연결
    }


    private void ChangeWeapon(string oldName, string newName) 
    {
        ChangeEquip("Weapon", oldName, newName);
        
        // ★ 무기 변경 시 해당 무기의 AnimIdx 가져오기
        UpdateWeaponAnimIdx(newName);
    }   
    private void ChangeHelment(string oldName, string newName) => ChangeEquip("Helmet", oldName, newName);
    private void ChangeCloth(string oldName, string newName) => ChangeEquip("Cloth", oldName, newName);
    private void ChangePant(string oldName, string newName) => ChangeEquip("Pant", oldName, newName);
    private void ChangeEquip(    string partsName,    string oldName,    string newName)
    {
        // ⭐ 장착 안 한 상태
        if (string.IsNullOrEmpty(newName) || newName == "Default")
        {
            ClearPart(partsName);
            return;
        }

        // false == Front, 
        // true == Back
        Sprite sprite = null;
        Sprite leftSprite = null;
        Sprite rightSprite = null;

        // 1️⃣ Helmet: Front / Back 처리
        bool helmetBackOrFrontValue = false;

        if (partsName == "Helmet")
        {
            if (newName.Contains("_Front"))
            {
                newName = newName.Replace("_Front", "");
            }
            else if (newName.Contains("_Back"))
            {
                newName = newName.Replace("_Back", "");
                helmetBackOrFrontValue = true;
            }
        }

        // 2️⃣ 파츠별 스프라이트 로딩 전략
        if (partsName == "Pant")
        {
            // ❗ Pant는 기본 sprite 없음
            leftSprite = RootManager.Instance.AddressableCDD.GetSprite(newName + "_Left");
            rightSprite = RootManager.Instance.AddressableCDD.GetSprite(newName + "_Right");

            if (leftSprite == null && rightSprite == null)
            {
                Debug.LogWarning($"Pant 스프라이트를 찾을 수 없습니다: {newName}");
                return;
            }
        }
        else
        {
            // Weapon / Helmet / Cloth
            sprite = RootManager.Instance.AddressableCDD.GetSprite(newName);

            if (sprite == null)
            {
                Debug.LogWarning($"스프라이트를 찾을 수 없습니다: {newName}");
                return;
            }

            // Cloth는 Left / Right 추가
            if (partsName == "Cloth")
            {
                leftSprite = RootManager.Instance.AddressableCDD.GetSprite(newName + "_Left");
                rightSprite = RootManager.Instance.AddressableCDD.GetSprite(newName + "_Right");
            }
        }

        switch (partsName)
        {
            case "Weapon":
                parts_weapon.sprite = sprite;
                break;

            case "Helmet":
                if (!helmetBackOrFrontValue)
                {
                    parts_helmet_front.sprite = sprite;
                    parts_helmet_back.sprite = null;
                }
                else
                {
                    parts_helmet_back.sprite = sprite;
                    parts_helmet_front.sprite = null;
                }
                break;

            case "Cloth":
                parts_cloth.sprite = sprite;
                parts_cloth_left.sprite = leftSprite;
                parts_cloth_right.sprite = rightSprite;
                break;

            case "Pant":
                parts_pant_left.sprite = leftSprite;
                parts_pant_right.sprite = rightSprite;
                break;
        }

    }
    private void ClearPart(string partsName)
    {
        switch (partsName)
        {
            case "Weapon":
                parts_weapon.sprite = null;
                break;

            case "Helmet":
                parts_helmet_front.sprite = null;
                parts_helmet_back.sprite = null;
                break;

            case "Cloth":
                parts_cloth.sprite = null;
                parts_cloth_left.sprite = null;
                parts_cloth_right.sprite = null;
                break;

            case "Pant":
                parts_pant_left.sprite = null;
                parts_pant_right.sprite = null;
                break;
        }
    }
    public override void OnStartLocalPlayer()
    {
        mainCamera = Camera.main;
        GameNetworkManager networkManager = NetworkManager.Instance as GameNetworkManager;
        if (networkManager != null)
        {
            networkManager.JoystickPrefab.SetActive(true);
            joystick = networkManager.JoystickPrefab.GetComponent<Joystick>();
        }

        UpdateCameraPosition();
        meteorCoroutine = StartCoroutine(AutoSpawnMeteors());
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (joystick == null) return; // 조이스틱 세팅 안됐을 때 null 방지

        // ✅ 조이스틱 입력
        _inputMove = new Vector2(joystick.xAxis.value, joystick.yAxis.value);
        bool hasInput = _inputMove.sqrMagnitude > (inputDeadZone * inputDeadZone);

        // ✅ 입력 있으면 수동 이동 우선
        if (hasInput)
        {
            _autoMove = Vector2.zero;
            movement = _inputMove;
        }
        else
        {
            // ✅ 멈췄을 때 자동전투
            if (enableAutoCombat)
            {
                AutoCombatUpdate();
                movement = _autoMove;
            }
            else
            {
                _autoMove = Vector2.zero;
                movement = Vector2.zero;
            }
        }

        // 좌우 방향 회전
        if (movement.x > 0.1f && !isFacingRight)
        {
            isFacingRight = true;
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (movement.x < -0.1f && isFacingRight)
        {
            isFacingRight = false;
            transform.rotation = Quaternion.Euler(0, 180, 0);
        }

        // 애니메이션 상태 업데이트
        HandleAnimationState();
    }
    // ★ 무기의 AnimIdx와 ARange 업데이트
    private void UpdateWeaponAnimIdx(string weaponName)
    {
        Debug.Log("Anim" + weaponName);
        if (playerObj == null) return;

        int animIdx = 0; // 기본값

        if (!string.IsNullOrEmpty(weaponName) && weaponName != "Default")
        {
            var weaponInfo = RootManager.Instance.ChartManager.InvenInfoList
                .Find(x => x.Name == weaponName && x.Type == "Weapon");

            if (weaponInfo != null)
            {
                animIdx = weaponInfo.AnimIdx;
                
                // ★ 차트에서 ARange 가져와서 attackRange에 설정
                attackRange = weaponInfo.ARange;
                
                Debug.Log($"무기 변경: {weaponName}, 공격 애니메이션 인덱스: {animIdx}, 공격 거리: {attackRange}");
            }
            else
            {
                attackRange = 1.5f; // 기본값
                Debug.LogWarning($"무기 정보를 찾을 수 없음: {weaponName}");
            }
        }
        else
        {
            attackRange = 1.5f; // Default 무기
        }

        // ★ PlayerObj의 ATTACK 애니메이션 인덱스 설정
        playerObj.SetStateAnimationIndex(PlayerState.ATTACK, animIdx);
    }

    private void AutoCombatUpdate()
    {
        // 1️⃣ 타겟 유효성
        if (!IsTargetValid(_currentTarget))
            _currentTarget = null;

        // 2️⃣ 타겟이 있으면 → 노란색 범위로 유지 체크
        if (_currentTarget != null)
        {
            float dist = Vector2.Distance(transform.position, _currentTarget.transform.position);

            // 🟡 노란색(기존 타겟 유지 범위)
            if (dist > autoDetectRadius)
            {
                _currentTarget = null; // 전투 종료
            }
        }

        // 3️⃣ 타겟 없을 때만 → 파란색 범위로 탐색
        if (_currentTarget == null)
        {
            _currentTarget = AcquireNearestTarget(autoChaseRadius);

            if (_currentTarget == null)
            {
                _autoMove = Vector2.zero;
                return;
            }
        }

        // 4️⃣ 거리 판단
        float d = Vector2.Distance(transform.position, _currentTarget.transform.position);

        if (d > attackRange)
        {
            _autoMove = (_currentTarget.transform.position - transform.position).normalized;
        }
        else
        {
            _autoMove = Vector2.zero;

            if (Time.time >= _nextAutoAttackTime)
            {
                _nextAutoAttackTime = Time.time + autoAttackInterval;
                CmdAutoAttackMonster(_currentTarget);
            }
        }
    }

    private bool IsTargetValid(GameObject target)
    {
        if (target == null) return false;
        if (!target.CompareTag("Monster")) return false;

        Monster m = target.GetComponent<Monster>();
        if (m == null) return false;
        if (!m.alive) return false;

        return true;
    }

    private GameObject AcquireNearestTarget(float radius)
    {
        Collider2D[] hits;

        if (monsterLayer.value != 0)
            hits = Physics2D.OverlapCircleAll(transform.position, radius, monsterLayer);
        else
            hits = Physics2D.OverlapCircleAll(transform.position, radius);

        GameObject best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (col == null) continue;
            if (!col.CompareTag("Monster")) continue;

            GameObject go = col.gameObject;

            Monster m = go.GetComponent<Monster>();
            if (m == null || !m.alive) continue;

            float d = Vector2.Distance(transform.position, go.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = go;
            }
        }

        return best;
    }
    // ★ 상태 판단 및 서버 전송 로직
    private void HandleAnimationState()
    {
        // 공격 중일 때는 이동 상태로 덮어쓰지 않음 (공격 모션 끝날 때까지 대기)
        if (_netState == PlayerState.ATTACK) return;

        PlayerState targetState = PlayerState.IDLE;

        if (movement.sqrMagnitude > 0.01f)
        {
           // Debug.Log("test123");
            targetState = PlayerState.MOVE;
        }
        else
        {
            targetState = PlayerState.IDLE;
        }

        // 현재 서버 상태와 다를 때만 요청 (네트워크 최적화)
        if (_netState != targetState)
        {
            CmdChangeState(targetState);
        }
    }
    // ★ 3. 서버에 상태 변경 요청 (Command)
    [Command]
    private void CmdChangeState(PlayerState newState)
    {
        _netState = newState; // 서버가 값을 바꾸면 -> Hook 발동 -> 모든 클라 애니메이션 변경
    }
    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        if (movement.sqrMagnitude > 0.01f)
        {
            rb.MovePosition(rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime);
        }
        else
        {
            rb.velocity = Vector2.zero;
        }
    }
  
    // ===================================================================================
    // 스네이크: 플레이어 이동 히스토리 기록 (펫들이 따라갈 경로)
    // ===================================================================================
    private void RecordPositionHistory()
    {
        recordTimer += Time.deltaTime;

        if (recordTimer >= recordInterval)
        {
            positionHistory.Insert(0, transform.position);
            recordTimer = 0f;
        }

        if (positionHistory.Count > 3000)
        {
            positionHistory.RemoveRange(2000, positionHistory.Count - 2000);
        }
    }

    // ===================================================================================
    // 카메라
    // ===================================================================================
    private void LateUpdate()
    {
        if (!isLocalPlayer) return;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        if (mainCamera == null) return;
        Vector3 targetPos = transform.position + cameraOffset;
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPos, cameraSmoothing);
    }

    // ===================================================================================
    // 메테오
    // ===================================================================================
    [Command]
    private void CmdSpawnMeteor()
    {
        GameObject meteorPrefab = NetworkManager.Instance.spawnPrefabs.Find(p => p.name == "Meteor");
        if (meteorPrefab == null) return;

        Vector3 spawnPos = transform.position + new Vector3(-2, 2, 0);
        GameObject meteorObj = Instantiate(meteorPrefab, spawnPos, Quaternion.identity);

        Meteor m = meteorObj.GetComponent<Meteor>();
        if (m != null) m.Initialize(spawnPos);

        NetworkServer.Spawn(meteorObj);
    }

    private IEnumerator AutoSpawnMeteors()
    {
        while (true)
        {
            CmdSpawnMeteor();
            yield return new WaitForSeconds(meteorSpawnInterval);
        }
    }
    // Hook 함수: 실제 애니메이션 재생 담당
    private void OnAnimStateChanged(PlayerState oldState, PlayerState newState)
    {
        if (playerObj != null)
        {
            // SPUM에게 애니메이션 재생 명령
            playerObj._currentState = newState;
            playerObj.PlayStateAnimation(newState);
        }
    }
   
    public void ApplyAllEquipment(JsonData equipInventory)
    {
        // 1️⃣ 현재 장착된 타입 추적
        HashSet<string> equippedTypes = new HashSet<string>();

        if (equipInventory != null)
        {
            foreach (string key in equipInventory.Keys)
            {
                JsonData data = equipInventory[key];
                if (data == null) continue;
                if (!data.Keys.Contains("itemId")) continue;
                if (!data.Keys.Contains("isEquip")) continue;
                if (!bool.Parse(data["isEquip"].ToString())) continue;

                int itemId = int.Parse(data["itemId"].ToString());
                InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList
                    .Find(x => x.ItemId == itemId);

                if (info == null) continue;
                if (!InventoryLogic.Instance.IsEquipType(info.Type)) continue;

                equippedTypes.Add(info.Type);

                switch (info.Type)
                {
                    case "Weapon":
                        weaponName = info.Name;
                        break;
                    case "Helmet":
                        helmetName = info.Name;
                        break;
                    case "Cloth":
                        ClothName = info.Name;
                        break;
                    case "Pant":
                        PantName = info.Name;
                        break;
                }
            }
        }

        // 2️⃣ 한 번도 안 나온 타입 = 미착용 → Default
        if (!equippedTypes.Contains("Weapon"))
            weaponName = "Default";

        if (!equippedTypes.Contains("Helmet"))
            helmetName = "Default";

        if (!equippedTypes.Contains("Cloth"))
            ClothName = "Default";

        if (!equippedTypes.Contains("Pant"))
            PantName = "Default";
    }
    // =================================================================
    // (추가) 자동 공격 서버 처리
    // =================================================================
    [Command]
    private void CmdAutoAttackMonster(GameObject targetMonster)
    {
        if (targetMonster == null) return;
        if (!targetMonster.CompareTag("Monster")) return;

        Monster monsterScript = targetMonster.GetComponent<Monster>();
        if (monsterScript == null || !monsterScript.alive) return;

        // 서버에서 거리 검증 (약간 여유)
        float dist = Vector2.Distance(transform.position, targetMonster.transform.position);
        if (dist > attackRange + 0.25f) return;

        // ★ 공격 전에 몬스터 방향으로 회전
        Vector3 dirToMonster = targetMonster.transform.position - transform.position;
        if (dirToMonster.x > 0 && !isFacingRight)
        {
            // 몬스터가 오른쪽에 있는데 왼쪽 보고 있으면 → 오른쪽으로 회전
            isFacingRight = true;
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (dirToMonster.x < 0 && isFacingRight)
        {
            // 몬스터가 왼쪽에 있는데 오른쪽 보고 있으면 → 왼쪽으로 회전
            isFacingRight = false;
            transform.rotation = Quaternion.Euler(0, 180, 0);
        }

        // ATTACK 상태로 잠깐 고정
        _netState = PlayerState.ATTACK;

        // 데미지
        monsterScript.TakeDamage(attackDamage, this.gameObject);

        // ATTACK -> IDLE 복귀
        if (_attackLockCo != null)
            StopCoroutine(_attackLockCo);

        _attackLockCo = StartCoroutine(ServerAttackStateLock());
    }

    [Server]
    private IEnumerator ServerAttackStateLock()
    {
        yield return new WaitForSeconds(attackStateLockTime);

        if (_netState == PlayerState.ATTACK)
            _netState = PlayerState.IDLE;
    }
    private void OnDrawGizmosSelected()
    {
        // 🔴 공격
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 🟡 기존 타겟 유지
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, autoDetectRadius);

        // 🔵 타겟 없을 때 탐색
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, autoChaseRadius);

        if (_currentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
        }
    }



}
