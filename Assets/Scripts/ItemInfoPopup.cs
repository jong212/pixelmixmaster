using LitJson;
using System.Linq;
using TMPro;
using UnityEngine;
public enum CommonResult
{
    Success,

    // 데이터 무결성
    ItemNotFound,
    ItemMoved,

    // 조건 실패
    NotEquipItem,
    LevelTooLow,

    // 슬롯 / 장착 관련
    SlotAlreadyOccupied
}

public class ItemInfoPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private GameObject equipButton; // 초록 버튼

    [SerializeField] private InventoryPopup inventoryPopup;

    private ItemPopupContext context;

    public void Open(ItemPopupContext ctx)
    {
        context = ctx;
        Refresh();
        gameObject.SetActive(true);
    }

    private void Refresh()
    {
        JsonData inven = RootManager.Instance.SetDataManager.equipInventory;
        if (inven == null)
        {
            Close();
            return;
        }

        string key = context.slotIndex.ToString();
        if (!inven.Keys.Contains(key))
        {
            Close();
            return;
        }

        JsonData itemData = inven[key];
        int itemId = int.Parse(itemData["itemId"].ToString());

        InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList
            .Find(x => x.ItemId == itemId);
        if (info == null)
        {
            Close();
            return;
        }
        // =========================
        // ⭐ 여기부터 UI 분기
        // =========================

        bool isEquipItem = InventoryLogic.Instance.IsEquipType(info.Type);
            Debug.Log(isEquipItem);
        bool isEquipped =
            itemData.Keys.Contains("isEquip") &&
            bool.Parse(itemData["isEquip"].ToString());

        if (isEquipItem)
        {
            equipButton.SetActive(true);

            if (isEquipped)
            {
                actionButtonText.text = "Unset";
            }
            else
            {
                actionButtonText.text = "Set";
            }
        }
        else
        {
            equipButton.SetActive(true);
            actionButtonText.text = "Use";
        }
        // TODO: UI 세팅
        // nameText.text = info.Name;
    }

    public void OnClickAction()
    {
        JsonData inven = RootManager.Instance.SetDataManager.equipInventory;
        if (inven == null) return;

        string key = context.slotIndex.ToString();
        if (!inven.Keys.Contains(key)) return;

        JsonData itemData = inven[key];

        int currentItemId = int.Parse(itemData["itemId"].ToString());
        InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList
            .Find(x => x.ItemId == currentItemId);

        if (info == null) return;

        // =========================
        // ⭐ 여기서 행동 분기
        // =========================

        CommonResult result;

        bool isEquipItem = InventoryLogic.Instance.IsEquipType(info.Type);
        bool isEquipped =
            itemData.Keys.Contains("isEquip") &&
            bool.Parse(itemData["isEquip"].ToString());

        if (isEquipItem)
        {
            result = isEquipped
                ? InventoryLogic.Instance.TryUnequip(context)
                : InventoryLogic.Instance.TryEquip(context);
            var activePlayer = RootManager.Instance.GameNetworkManager.ActivePlayers;
            if(activePlayer != null)
            {
                PlayerController localP = activePlayer.FirstOrDefault(p => p.isLocalPlayer);
                if(localP != null)
                {
                    localP.ApplyAllEquipment(RootManager.Instance.SetDataManager.equipInventory);
                }
            }
            HandleResult(result);
        }
        else
        {
            // 소비 아이템
            Debug.Log("아이템 사용");
            // TODO: UseItem(context)
            Close();
        }
    }

    private void HandleResult(CommonResult result)
    {
        switch (result)
        {
            case CommonResult.Success:
                inventoryPopup.Refresh();   // ⭐ 핵심
                Close();
                break;

            case CommonResult.ItemMoved:
            case CommonResult.ItemNotFound:
                inventoryPopup.Refresh();   // ⭐ 핵심
                Refresh();
                break;

            case CommonResult.NotEquipItem:
                Debug.Log("장착할 수 없는 아이템입니다.");
                break;

            case CommonResult.SlotAlreadyOccupied:
                Debug.Log("이미 해당 부위에 장비가 있습니다.");
                break;
        }
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }
}
public class InventoryLogic
{
    public static InventoryLogic Instance = new InventoryLogic();
    private InventoryLogic() { }

    // ======================================================
    // 공통 검증 + 데이터 추출
    // ======================================================
    private bool CommonIdxCheckLogic(
        ItemPopupContext ctx,
        out JsonData itemData,
        out InvenInfo info,
        out CommonResult failResult
    )
    {
        itemData = null;
        info = null;
        failResult = CommonResult.Success;

        JsonData inven = RootManager.Instance.SetDataManager.equipInventory;
        if (inven == null)
        {
            failResult = CommonResult.ItemNotFound;
            return false;
        }

        string key = ctx.slotIndex.ToString();
        if (!inven.Keys.Contains(key))
        {
            failResult = CommonResult.ItemMoved;
            return false;
        }

        itemData = inven[key];
        if (itemData == null || !itemData.Keys.Contains("itemId"))
        {
            failResult = CommonResult.ItemMoved;
            return false;
        }

        int currentItemId = int.Parse(itemData["itemId"].ToString());
        if (currentItemId != ctx.itemId)
        {
            failResult = CommonResult.ItemMoved;
            return false;
        }

        info = RootManager.Instance.ChartManager.InvenInfoList
            .Find(x => x.ItemId == currentItemId);

        if (info == null)
        {
            failResult = CommonResult.ItemNotFound;
            return false;
        }

        return true;
    }

    // ======================================================
    // 장비 장착
    // ======================================================
    public CommonResult TryEquip(ItemPopupContext ctx)
    {
        if (!CommonIdxCheckLogic(ctx, out JsonData itemData, out InvenInfo info, out CommonResult fail))
            return fail;

        if (!IsEquipType(info.Type))
            return CommonResult.NotEquipItem;

        if (IsAlreadyEquipped(info.Type))
            return CommonResult.SlotAlreadyOccupied;

        itemData["isEquip"] = true;
        RootManager.Instance.SetDataManager.UpdateEquipment(
            RootManager.Instance.SetDataManager.equipInventory
        );

        return CommonResult.Success;
    }

    // ======================================================
    // 장비 해제 (확장 예시)
    // ======================================================
    public CommonResult TryUnequip(ItemPopupContext ctx)
    {
        if (!CommonIdxCheckLogic(ctx, out JsonData itemData, out InvenInfo info, out CommonResult fail))
            return fail;

        if (!itemData.Keys.Contains("isEquip") ||
            !bool.Parse(itemData["isEquip"].ToString()))
            return CommonResult.NotEquipItem;

        itemData["isEquip"] = false;
        RootManager.Instance.SetDataManager.UpdateEquipment(
            RootManager.Instance.SetDataManager.equipInventory
        );

        return CommonResult.Success;
    }

    // ======================================================
    // 내부 헬퍼
    // ======================================================
    public bool IsEquipType(string type)
    {
        return type == "Weapon"
            || type == "Helmet"
            || type == "Cloth"
            || type == "Pant";
    }

    private bool IsAlreadyEquipped(string equipType)
    {
        JsonData inven = RootManager.Instance.SetDataManager.equipInventory;
        if (inven == null)
            return false;

        foreach (string key in inven.Keys)
        {
            JsonData data = inven[key];
            if (data == null) continue;
            if (!data.Keys.Contains("isEquip")) continue;
            if (!bool.Parse(data["isEquip"].ToString())) continue;

            int id = int.Parse(data["itemId"].ToString());
            InvenInfo info = RootManager.Instance.ChartManager.InvenInfoList
                .Find(x => x.ItemId == id);

            if (info != null && info.Type == equipType)
                return true;
        }

        return false;
    }
}
