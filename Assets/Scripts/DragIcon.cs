using UnityEngine;
using UnityEngine.UI;

public class DragIcon : MonoBehaviour
{
    public Image icon;

    public void SetSprite(Sprite sprite)
    {
        icon.sprite = sprite;
        icon.color = Color.white;
    }

    public void Follow(Vector2 pos)
    {
        transform.position = pos;
    }

    public void Hide()
    {
        icon.color = new Color(1, 1, 1, 0);  // Åõ¸í
        gameObject.SetActive(false);      // ¿ÏÀü ¼û±è
    }
}
