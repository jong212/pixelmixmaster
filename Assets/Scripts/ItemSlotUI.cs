using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("슬롯 내부 컴포넌트 연결")]
    public Image itemIcon;
    public TextMeshProUGUI amountText;

    public int SlotIndex { get; private set; }
    private InventoryPopup inventory;

    private float originalAlpha;  // ★ 원래 알파 저장용

    public void Init(int index, InventoryPopup inven)
    {
        SlotIndex = index;
        inventory = inven;
    }

    public void SetSlot(Sprite sprite, int amount)
    {
        itemIcon.sprite = sprite;

        Color c = itemIcon.color;
        c.a = 1f;
        itemIcon.color = c;

        itemIcon.gameObject.SetActive(true);

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

        Color c = itemIcon.color;
        c.a = 0f; // ★ 빈 슬롯은 투명
        itemIcon.color = c;

        amountText.gameObject.SetActive(false);
    }

    // ========================================================
    // DRAG EVENT
    // ========================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemIcon.sprite == null)
            return;  // ★ 빈 슬롯 드래그 금지!

        // ★ 원래 알파값 저장
        originalAlpha = itemIcon.color.a;

        // 현재 스프라이트, 수량 가져오기
        Sprite sprite = itemIcon.sprite;
        int count = amountText.gameObject.activeSelf ? int.Parse(amountText.text) : 1;

        inventory.StartDrag(this, sprite, count);

        // 드래그 중 반투명 처리
        Color c = itemIcon.color;
        c.a = 0.4f;
        itemIcon.color = c;
    }

    public void OnDrag(PointerEventData eventData)
    {
        inventory.Drag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (itemIcon.sprite != null)
        {
            // ★ 빈 슬롯이 아니라면 원래 알파 복구
            Color c = itemIcon.color;
            c.a = originalAlpha;
            itemIcon.color = c;
        }

        inventory.EndDrag(this, eventData);
    }
}
