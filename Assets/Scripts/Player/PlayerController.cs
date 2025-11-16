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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

        // **조이스틱 입력**
        movement = new Vector2(joystick.xAxis.value, joystick.yAxis.value);

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

        // 🔥 위치 히스토리 기록
        RecordPositionHistory();
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
}
