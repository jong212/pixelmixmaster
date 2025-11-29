using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BACKND;
using SimpleInputNamespace;
using UnityEngine.Tilemaps;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Combat Settings")]
    public float attackRange = 1.5f; // 공격 범위 (반경)
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
    private Vector2 movement;
    private Rigidbody2D rb;
    public Joystick joystick;
    private bool isFacingRight = true;
    private Camera mainCamera;
    private Coroutine meteorCoroutine;
    public SpriteRenderer parts_sward;
    private NetworkAnimator networkAnim;
    private PlayerObj playerObj;
    [SyncVar] public string CharacterName;

    [Header("Sprite")]
    [SyncVar(hook = nameof(OnSpriteChanged))]
    public string equippedSpriteName = "Default";
    [SyncVar(hook = nameof(OnAnimStateChanged))]
    private PlayerState _netState = PlayerState.IDLE;
    private SpriteRenderer spriteRenderer;
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
  
    private void OnSpriteChanged(string oldName, string newName)
    {
        Sprite test = RootManager.Instance.AddressableCDD.GetSprite(newName);
        if (test  != null)
        {
            Debug.Log(newName);
            parts_sward.sprite = test;
        }
        else
        {
            Debug.LogWarning($"스프라이트를 찾을 수 없습니다: {newName}");
        }
    }

  
    public override void OnStartLocalPlayer()
    {
        mainCamera = Camera.main;
        //RootManager.Instance.SetDataManager.InitializeOnServerSetData(this);
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

        // **조이스틱 입력**
        movement = new Vector2(joystick.xAxis.value, joystick.yAxis.value);

        // B. 공격 테스트 (스페이스바)
        // ※ 모바일이라면 UI 버튼 OnClick 이벤트에 PerformAttack()을 연결하세요.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PerformAttack();
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
        // 4. 애니메이션 상태 업데이트 (핵심 로직)
        HandleAnimationState();
        // 🔥 위치 히스토리 기록
        RecordPositionHistory();
    }
    // ★ 상태 판단 및 서버 전송 로직
    private void HandleAnimationState()
    {
        // 공격 중일 때는 이동 상태로 덮어쓰지 않음 (공격 모션 끝날 때까지 대기)
        if (_netState == PlayerState.ATTACK) return;

        PlayerState targetState = PlayerState.IDLE;

        if (movement.sqrMagnitude > 0.01f)
        {
            Debug.Log("test123");
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
    // =================================================================

    // [Client] 공격 버튼을 누르면 실행되는 함수
    public void PerformAttack()
    {
        // 1. 내 주변(attackRange)에 있는 콜라이더 탐색
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);

        foreach (var hit in hits)
        {
            // 2. 몬스터인지 태그로 확인 (반드시 몬스터 프리팹 Tag를 'Monster'로 설정하세요)
            if (hit.CompareTag("Monster"))
            {
                // 3. 서버에 타격 요청
                CmdAttackMonster(hit.gameObject);

                // (선택) 한 번에 한 마리만 때리기 (광역기면 break 삭제)
                break;
            }
        }
    }

    // [Server] 클라이언트의 요청을 받아 실제 데미지를 주는 함수
    [Command]
    private void CmdAttackMonster(GameObject targetMonster)
    {
        if (targetMonster == null) return;

        // 몬스터 스크립트 가져오기
        Monster monsterScript = targetMonster.GetComponent<Monster>();

        if (monsterScript != null && monsterScript.alive)
        {
            // 몬스터에게 데미지를 주고, 공격자(나, this.gameObject)를 알려줌 -> 어그로 시작
            monsterScript.TakeDamage(attackDamage, this.gameObject);
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
}
