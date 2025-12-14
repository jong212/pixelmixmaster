using LitJson;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class InventoryPopup : BasePanel
{
    [Header("슬롯 부모 오브젝트 (Content)")]
    public Transform slotParent; // 인벤 하단 부모
    public Transform setslotParent; // 인베탑 장착 파트 부모

    public Transform clickPopup;
    private ItemSlotUI[] _slots;

    public DragIcon dragIcon;

    private ItemSlotUI draggedSlot;
    private Sprite draggedSprite;
    private int draggedCount;

    // 문자열 키값 오타 방지용 상수
    private const string KEY_ITEM_ID = "itemId";
    private const string KEY_IS_EQUIP = "isEquip";
    private const string KEY_COUNT = "count";

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
    // ===============   인벤토리 UI 갱신      ===================
    // ========================================================
    public override void Refresh()
    {
        base.Refresh();
        RefreshEquipSlots_BySlotName(); // ★ 부위 기준 장착 슬롯

        JsonData myInven = SetDataManager.Instance.equipInventory;
        if (myInven == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            // 1. 일단 슬롯 초기화 (데이터 없으면 빈 상태 유지)
            _slots[i].ClearSlot();

            string key = i.ToString();

            // 2. 데이터 유효성 검사 (Early Exit 패턴 적용)
            if (!myInven.Keys.Contains(key)) continue;

            JsonData itemData = myInven[key];
            if (itemData == null || !itemData.Keys.Contains(KEY_ITEM_ID)) continue;

            // 3. 안전하게 데이터 파싱 (함수로 분리하여 깔끔하게 처리)
            int itemId = SafeParseInt(itemData, KEY_ITEM_ID);
            int count = SafeParseInt(itemData, KEY_COUNT, 1); // 기본값 1
            bool isEquipValue = SafeParseBool(itemData, KEY_IS_EQUIP);

            // 4. 차트 정보 가져오기
            InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList.Find(x => x.ItemId == itemId);
            if (info == null) continue;

            // 5. 스프라이트 이름 처리 (Pant 예외 처리)
            string spriteName = info.Name;
            if (info.Type == "Pant") spriteName += "_Left";

            // 6. UI 반영
            if (RootManager.Instance.AddressableCDD.SpriteCache.TryGetValue(spriteName, out Sprite sprite))
            {
                // 스케일 안전 처리
                float sx = info.Sx != 0 ? info.Sx : 1f;
                float sy = info.Sy != 0 ? info.Sy : 1f;

                // ★ SetSlot에 스프라이트와 함께 스케일 값도 같이 넘겨줍니다.
                _slots[i].SetSlot(sprite, count, isEquipValue, sx, sy);

                // SetNativeSize가 필요하다면 여기서 호출하거나, SetSlot 내부에서 처리
                _slots[i].itemIcon.SetNativeSize();


                _slots[i].itemIcon.transform.localScale = new Vector3(sx, sy, 1f);
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

            // 처리 대상 부위가 아니면 패스
            if (slotName != "Weapon" && slotName != "Helmet" && slotName != "Cloth" && slotName != "Pant")
                continue;

            Transform emptyObj = slot.GetChild(0); // Icon_0 (빈 아이콘)
            Transform equipObj = slot.GetChild(1); // Icon_1 (장착 아이콘)
            UnityEngine.UI.Image equipImage = equipObj.GetComponent<UnityEngine.UI.Image>();

            if (equipImage == null) continue;

            bool hasEquipped = false;
            string spriteName = null;
            float scalexValue = 1f;
            float scaleyValue = 1f;

            // 인벤토리 전체를 순회하며 해당 부위에 장착된 아이템 찾기
            foreach (string key in inven.Keys)
            {
                JsonData itemData = inven[key];

                // 데이터 검증 및 장착 여부 확인
                if (itemData == null || !itemData.Keys.Contains(KEY_IS_EQUIP)) continue;

                // 안전한 bool 파싱 사용
                if (!SafeParseBool(itemData, KEY_IS_EQUIP)) continue;

                int itemId = SafeParseInt(itemData, KEY_ITEM_ID);
                InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList.Find(x => x.ItemId == itemId);

                if (info == null) continue;

                // 부위 일치 확인
                if (info.Type == slotName)
                {
                    spriteName = info.Name;
                    scalexValue = info.Sx != 0 ? info.Sx : 1f;
                    scaleyValue = info.Sy != 0 ? info.Sy : 1f;

                    if (slotName == "Pant") spriteName += "_Left";

                    hasEquipped = true;
                    break; // 찾았으면 루프 종료
                }
            }

            // UI 갱신 로직
            if (hasEquipped && RootManager.Instance.AddressableCDD.SpriteCache.TryGetValue(spriteName, out Sprite sprite))
            {
                // 장착 상태
                emptyObj.gameObject.SetActive(false);
                equipObj.gameObject.SetActive(true);

                equipImage.sprite = sprite;
                equipImage.SetNativeSize();
                equipImage.transform.localScale = new Vector3(scalexValue, scaleyValue, 1f);
            }
            else
            {
                // 미장착 상태
                emptyObj.gameObject.SetActive(true);
                equipObj.gameObject.SetActive(false);

                equipImage.sprite = null;
                equipImage.transform.localScale = Vector3.one;
            }
        }
    }

    // ========================================================
    // ===============     HELPER METHODS     =================
    // ========================================================

    // 안전한 Int 파싱 (키가 없거나 에러나면 defaultValue 반환)
    private int SafeParseInt(JsonData data, string key, int defaultValue = 0)
    {
        if (!data.Keys.Contains(key) || data[key] == null) return defaultValue;
        if (int.TryParse(data[key].ToString(), out int result)) return result;
        return defaultValue;
    }

    // 안전한 Bool 파싱 (True/False 문자열 및 1/0 숫자 모두 대응)
    private bool SafeParseBool(JsonData data, string key)
    {
        if (!data.Keys.Contains(key) || data[key] == null) return false;

        string strVal = data[key].ToString();

        // 1. "True", "False" 문자열 시도
        if (bool.TryParse(strVal, out bool result)) return result;

        // 2. "1", "0" 숫자 시도
        if (int.TryParse(strVal, out int intVal)) return intVal == 1;

        return false;
    }

    // ========================================================
    // ===============      DRAG LOGIC        =================
    // ========================================================

    // StartDrag, Drag는 기존 로직이 깔끔하여 그대로 유지합니다.
    public void StartDrag(ItemSlotUI slot, Sprite sprite, int count, Vector3 scale)
    {
        if (sprite == null) return;

        draggedSlot = slot;
        draggedSprite = sprite;
        draggedCount = count;

        dragIcon.gameObject.SetActive(true);
        dragIcon.SetSprite(sprite);

        dragIcon.icon.SetNativeSize();
        dragIcon.icon.transform.localScale = scale;
    }

    public void Drag(PointerEventData eventData)
    {
        dragIcon.Follow(eventData.position);
    }

    public void EndDrag(ItemSlotUI endSlot, PointerEventData eventData)
    {
        dragIcon.Hide();

        if (draggedSlot == null) return;

        ItemSlotUI hoveredSlot = GetSlotUnderMouse(eventData);

        // 드래그 실패 (허공에 드랍)
        if (hoveredSlot == null)
        {
            Refresh(); // 원래대로 복구
            draggedSlot = null;
            return;
        }

        // 슬롯 교체 실행
        SwapOrMove(draggedSlot, hoveredSlot);

        draggedSlot = null;
        Refresh();
        Save();
    }

    private ItemSlotUI GetSlotUnderMouse(PointerEventData eventData)
    {
        foreach (var slot in _slots)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(slot.GetComponent<RectTransform>(), eventData.position))
                return slot;
        }
        return null;
    }

    private void SwapOrMove(ItemSlotUI from, ItemSlotUI to)
    {
        // 인덱스가 같으면(자기 자신에게 드랍) 무시
        if (from.SlotIndex == to.SlotIndex) return;

        JsonData inven = SetDataManager.Instance.equipInventory;
        string fromKey = from.SlotIndex.ToString();
        string toKey = to.SlotIndex.ToString();

        // 키가 존재하는지 확인하여 안전하게 교체
        bool hasFrom = inven.Keys.Contains(fromKey);
        bool hasTo = inven.Keys.Contains(toKey);

        // 둘 다 데이터가 있을 때만 교체하거나, 없는 쪽으로 이동시키는 로직 등
        // 현재 구조상 단순히 키값을 교체하는 것이므로 아래 로직 유지하되 null 체크만 주의

        // LitJson 특성상 없는 키를 접근하면 null일 수 있으므로 임시 변수에 담을 때 주의
        JsonData temp = hasFrom ? inven[fromKey] : null;

        if (hasTo) inven[fromKey] = inven[toKey];
        else if (hasFrom) inven[fromKey] = null; // to가 없었으니 from도 비움 (사실상 remove)

        if (temp != null) inven[toKey] = temp;
        else inven[toKey] = null;
    }
    public void ShowItemClickPopup(ItemSlotUI slot)
    {
        // 슬롯 클릭
        if (HideItemClickPopup())
        {
            return;
        }
        clickPopup.gameObject.SetActive(true);
        SetPopupPosition(slot);

        // 아이템 정보 세팅
        //SetItemInfo(slot);
    }
    private void SetPopupPosition(ItemSlotUI slot)
    {
        RectTransform slotRt = slot.GetComponent<RectTransform>();
        RectTransform popupRt = clickPopup.GetComponent<RectTransform>();

        Vector3 basePos = slotRt.position;
        Vector3 offset = new Vector3(150f, 0f, 0f);

        // 오른쪽 벗어나면 왼쪽으로
        if (basePos.x + offset.x + popupRt.rect.width > Screen.width)
            offset.x = -150f;

        popupRt.position = basePos + offset;
    }
    public bool HideItemClickPopup()
    {
        if (!clickPopup.gameObject.activeSelf)
            return false;   // 닫을 게 없었음

        clickPopup.gameObject.SetActive(false);
        return true;        // 닫았음
    }


    private void Save()
    {
        SetDataManager.Instance.UpdateEquipment(SetDataManager.Instance.equipInventory);
    }
}