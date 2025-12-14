using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("슬롯 내부 컴포넌트")]
    public Image itemIcon;
    public TextMeshProUGUI amountText;
    public Image RoundLine;

    [Header("반응 설정")]
    public float pressScale = 0.9f;
    public float dragScale = 1.2f;

    private bool consumedByPopup = false;


    public int SlotIndex { get; private set; }
    private InventoryPopup inventory;
    private ScrollRect scrollRect;
    private float originalAlpha;

    // ★ 아이템 고유의 스케일(Sx, Sy)을 기억하는 변수
    private Vector3 currentBaseScale = Vector3.one;

    // 상태 변수들
    private bool isPointerDown = false;
    private bool isLongPressed = false;
    private bool isItemDragging = false;
    private float pressTimer = 0f;
    private const float LongPressDuration = .7f;

    private void Awake()
    {
        scrollRect = GetComponentInParent<ScrollRect>();
    }

    public void Init(int index, InventoryPopup inven)
    {
        SlotIndex = index;
        inventory = inven;
    }

    private void Update()
    {
        // 꾹 누르기 체크
        if (isPointerDown && !isLongPressed)
        {
            pressTimer += Time.deltaTime;
            // 기억해둔 스케일 기준으로 0.9배 작아짐
            Vector3 targetScale = currentBaseScale * pressScale;
            itemIcon.transform.localScale = Vector3.Lerp(itemIcon.transform.localScale, targetScale, Time.deltaTime * 10f);

            if (pressTimer >= LongPressDuration && itemIcon.sprite != null)
            {
                OnLongPressComplete();
            }
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (consumedByPopup)
        {
            consumedByPopup = false; // 다음 입력을 위해 리셋
            return;
        }        // 아이템 없으면 무시
        if (itemIcon.sprite == null) return;
        // 드래그였으면 클릭 취소
        if (isItemDragging) return;
        // 롱프레스였으면 클릭 취소
        if (isLongPressed) return;
        // 👉 여기서 ClickPopup 띄우기
        inventory.ShowItemClickPopup(this);
    }
    private void OnLongPressComplete()
    {
        isLongPressed = true;
        inventory.HideItemClickPopup();
        // 1초 뒤 기억해둔 스케일 기준으로 1.2배 커짐
        itemIcon.transform.localScale = currentBaseScale * dragScale;

#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate(); 
#endif
    }

    // ★★★ [핵심 변경] sx, sy 값을 받아서 기억합니다. (기본값 1f) ★★★
    public void SetSlot(Sprite sprite, int amount, bool isEquipValue, float sx = 1f, float sy = 1f)
    {
        // 1. 스케일 기억
        currentBaseScale = new Vector3(sx, sy, 1f);

        // 2. 아이콘 설정 및 크기 적용
        itemIcon.sprite = sprite;
        itemIcon.transform.localScale = currentBaseScale; // 바로 적용

        // 3. 기타 설정
        Color c = itemIcon.color;
        c.a = 1f;
        itemIcon.color = c;
        itemIcon.gameObject.SetActive(true);

        RoundLine.gameObject.SetActive(isEquipValue);

        if (amount > 1)
        {
            amountText.text = amount.ToString();
            amountText.gameObject.SetActive(true);
        }
        else
        {
            amountText.text = "";
            amountText.gameObject.SetActive(false);
        }
    }

    public void ClearSlot()
    {
        itemIcon.sprite = null;
        // 비울 때는 1,1,1로 초기화하거나 원하는 대로 설정
        currentBaseScale = Vector3.one;
        itemIcon.transform.localScale = Vector3.one;

        Color c = itemIcon.color;
        c.a = 0f;
        itemIcon.color = c;
        RoundLine.gameObject.SetActive(false);
        amountText.gameObject.SetActive(false);
    }

    // ... (OnPointerDown, OnPointerUp 등은 위 로직에 맞춰 복구만 하면 되므로 생략) ...
    public void OnPointerDown(PointerEventData eventData)
    {
        consumedByPopup = inventory.HideItemClickPopup();
        if (itemIcon.sprite == null) return;
        isPointerDown = true;
        isLongPressed = false;
        isItemDragging = false;
        pressTimer = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        pressTimer = 0f;
        // 드래그 중이 아니면 원래 크기로 복구
        if (!isItemDragging && itemIcon.sprite != null)
        {
            itemIcon.transform.localScale = currentBaseScale;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLongPressed)
        {
            if (itemIcon.sprite == null) return;

            isItemDragging = true;
            originalAlpha = itemIcon.color.a;

            // 슬롯 자신은 커진 상태 유지
            itemIcon.transform.localScale = currentBaseScale * dragScale;

            Sprite sprite = itemIcon.sprite;
            int count = amountText.gameObject.activeSelf ? int.Parse(amountText.text) : 1;

            // ★★★ [핵심] 기억해둔 sx, sy 값을 인벤토리에 전달 ★★★
            inventory.StartDrag(this, sprite, count, currentBaseScale);

            Color c = itemIcon.color;
            c.a = 0.4f;
            itemIcon.color = c;
        }
        else // 스크롤
        {
            isItemDragging = false;
            isPointerDown = false;

            if (itemIcon.sprite != null)
                itemIcon.transform.localScale = currentBaseScale; // 복구

            if (scrollRect != null) scrollRect.OnBeginDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isItemDragging) inventory.Drag(eventData);
        else if (scrollRect != null) scrollRect.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isItemDragging)
        {
            if (itemIcon.sprite != null)
            {
                Color c = itemIcon.color;
                c.a = originalAlpha;
                itemIcon.color = c;
                itemIcon.transform.localScale = currentBaseScale; // 원래 크기로 복구
            }
            inventory.EndDrag(this, eventData);
        }
        else
        {
            if (scrollRect != null) scrollRect.OnEndDrag(eventData);
        }

        isPointerDown = false;
        isLongPressed = false;
        isItemDragging = false;
        pressTimer = 0f;
    }
}