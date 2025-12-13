using UnityEngine;

// 이 스크립트를 상속받아 InventoryPanel, PetPanel 등을 만듭니다.
public class BasePanel : MonoBehaviour
{
    // 패널의 타입을 인스펙터에서 설정
    public UI.UIPanelType panelType;

    // 열릴 때 실행
    public virtual void Open()
    {
        gameObject.SetActive(true);
        Refresh();
        // 여기에 "아이템 로드", "새로고침" 등의 로직을 오버라이드 해서 넣으면 됩니다.
    }

    // 닫힐 때 실행
    public virtual void Close()
    {
        gameObject.SetActive(false);
    }
    public virtual void Refresh()
    {
        // 기본은 아무것도 안 함 (설정창 같은 건 데이터 갱신이 필요 없을 수도 있으니까)
        Debug.Log($"{panelType} 패널 새로고침 됨");
    }
}