using LitJson;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryPopup : BasePanel
{
    [Header("슬롯 부모 오브젝트 (Content)")]
    public Transform slotParent;
    public Transform setslotParent;//장착 파트 부모
    private ItemSlotUI[] _slots;

    public DragIcon dragIcon;

    private ItemSlotUI draggedSlot;
    private Sprite draggedSprite;
    private int draggedCount;

    private void Awake()
    {
        _slots = slotParent.GetComponentsInChildren<ItemSlotUI>(true);

        // 슬롯 초기화
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].Init(i, this);
    }

    public override void Open()
    {
        base.Open();
        Refresh();
    }

    // ========================================================
    // ===============   인벤토리 UI 갱신     ===================
    // ========================================================
    public override void Refresh()
    {
        base.Refresh();
        RefreshEquipSlots_BySlotName();       // ★ 부위 기준 장착 슬롯
        JsonData myInven = SetDataManager.Instance.equipInventory;
        if (myInven == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            string key = i.ToString();

            if (myInven.Keys.Contains(key))
            {
                JsonData itemData = myInven[key];

                if (itemData != null && itemData.Keys.Contains("itemId"))
                {
                    int itemId = int.Parse(itemData["itemId"].ToString());
                    int count = itemData.Keys.Contains("count")
                        ? int.Parse(itemData["count"].ToString())
                        : 1;

                    // CSV 데이터에서 아이템 정보 찾기
                    InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList
                        .Find(x => x.ItemId == itemId);

                    if (info == null)
                    {
                        _slots[i].ClearSlot();
                        continue;
                    }

                    string spriteName = info.Name;
                    if(info.Type == "Pant")
                    {
                        spriteName = spriteName + "_Left";
                    }
                    if (RootManager.Instance.AddressableCDD.SpriteCache
                        .TryGetValue(spriteName, out Sprite sprite))
                    {
                        _slots[i].SetSlot(sprite, count);
                        _slots[i].itemIcon.SetNativeSize();
                        _slots[i].itemIcon.transform.localScale = new Vector3(info.Sx, info.Sy, 1f);
                    }
                    else
                    {
                        _slots[i].ClearSlot();
                    }
                }
                else
                {
                    _slots[i].ClearSlot();
                }
            }
            else
            {
                _slots[i].ClearSlot();
            }
        }
    }
    private void RefreshEquipSlots_BySlotName()
    {
        JsonData inven = SetDataManager.Instance.equipInventory;
        if (inven == null) return;

        foreach (Transform slot in setslotParent)
        {
            string slotName = slot.name; // "Weapon", "Helmet", "Cloth", "Pant"

            // 처리 대상 부위만
            if (
                slotName != "Weapon" &&
                slotName != "Helmet" &&
                slotName != "Cloth" &&
                slotName != "Pant"
            )
                continue;

            // ★ 구조 고정: 0 = 미장착, 1 = 장착
            Transform emptyObj = slot.GetChild(0); // Icon_0
            Transform equipObj = slot.GetChild(1); // Icon_1

            Image equipImage = equipObj.GetComponent<Image>();
            if (equipImage == null) continue;

            bool hasEquipped = false;
            string spriteName = null;
            float scalexValue = default;
            float scaleyValue = default;

            // equipInventory 순회 → 해당 부위 착용 아이템 찾기
            foreach (string key in inven.Keys)
            {
                JsonData itemData = inven[key];
                if (itemData == null) continue;

                if (!itemData.Keys.Contains("isEquip") || !(bool)itemData["isEquip"])
                    continue;

                int itemId = int.Parse(itemData["itemId"].ToString());

                InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList
                    .Find(x => x.ItemId == itemId);

                if (info == null) continue;

                // 부위 일치
                if (info.Type != slotName)
                    continue;

                spriteName = info.Name;
                scalexValue = info.Sx;
                scaleyValue = info.Sy;

                // Pant는 Left 기준
                if (slotName == "Pant")
                    spriteName += "_Left";

                hasEquipped = true;
                break; // 부위당 하나만
            }

            // ===============================
            // UI 반영
            // ===============================
            if (!hasEquipped)
            {
                // ❌ 장비 없음
                emptyObj.gameObject.SetActive(true);

                equipImage.sprite = null;
                equipObj.gameObject.SetActive(false);
            }
            else
            {
                // ✅ 장비 있음
                if (RootManager.Instance.AddressableCDD.SpriteCache
                    .TryGetValue(spriteName, out Sprite sprite))
                {
                    equipImage.sprite = sprite;
                    
                    equipImage.SetNativeSize();
                    equipImage.transform.localScale = new Vector3(scalexValue, scaleyValue, 1f);

                    emptyObj.gameObject.SetActive(false);
                    equipObj.gameObject.SetActive(true);
                }
                else
                {
                    // 스프라이트 못 찾으면 안전하게 미장착 처리
                    emptyObj.gameObject.SetActive(true);
                    equipImage.sprite = null;
                    equipImage.transform.localScale = new Vector3(1f,1f,1f);
                    equipObj.gameObject.SetActive(false);
                }
            }
        }
    }

    // ========================================================
    // ===============     DRAG START     =======================
    // ========================================================
    public void StartDrag(ItemSlotUI slot, Sprite sprite, int count)
    {
        if (sprite == null)
            return;

        draggedSlot = slot;
        draggedSprite = sprite;
        draggedCount = count;

        dragIcon.gameObject.SetActive(true);   // ★ 여기서 활성화!
        dragIcon.SetSprite(sprite);
    }


    // ========================================================
    // ===============     DRAG MOVE     ========================
    // ========================================================
    public void Drag(PointerEventData eventData)
    {
        dragIcon.Follow(eventData.position);
    }

    // ========================================================
    // ===============     DRAG END       =======================
    // ========================================================
    public void EndDrag(ItemSlotUI endSlot, PointerEventData eventData)
    {
        // 유령 아이콘 숨기기
        dragIcon.Hide();

        if (draggedSlot == null)
            return;

        // 마우스 아래에 어떤 슬롯이 있는지 확인
        ItemSlotUI hoveredSlot = GetSlotUnderMouse(eventData);
        if (hoveredSlot == null)
        {
            // 드래그 실패 → 원복
            Refresh();
            draggedSlot = null;
            return;
        }

        // 슬롯 교체 or 이동
        SwapOrMove(draggedSlot, hoveredSlot);

        draggedSlot = null;

        Refresh();   // UI 갱신
        Save();      // 데이터 저장
    }

    // ========================================================
    // ===============  슬롯 판정 (마우스 위치) =================
    // ========================================================
    private ItemSlotUI GetSlotUnderMouse(PointerEventData eventData)
    {
        foreach (var slot in _slots)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                slot.GetComponent<RectTransform>(), eventData.position))
            {
                return slot;
            }
        }
        return null;
    }

    // ========================================================
    // ===============  슬롯 교체 / 이동 로직 ===================
    // ========================================================
    private void SwapOrMove(ItemSlotUI from, ItemSlotUI to)
    {
        JsonData inven = SetDataManager.Instance.equipInventory;

        string fromKey = from.SlotIndex.ToString();
        string toKey = to.SlotIndex.ToString();

        JsonData temp = inven[fromKey];
        inven[fromKey] = inven[toKey];
        inven[toKey] = temp;
    }

    // ========================================================
    // ===============  저장 호출 ===============================
    // ========================================================
    private void Save()
    {
        SetDataManager.Instance.UpdateEquipment(
            SetDataManager.Instance.equipInventory);
    }
}
