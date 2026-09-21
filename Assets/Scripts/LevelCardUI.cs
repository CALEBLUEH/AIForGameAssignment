using UnityEngine;
using UnityEngine.UI;

public class LevelCardUI : MonoBehaviour
{
    [Header("Assign the final level artwork here")]
    public Sprite levelSprite;
    public Image levelImage;
    public GameObject imagePlaceholder;
    public Text levelNameText;
    public Text starsText;
    public Text stateText;
    public GameObject lockOverlay;
    public Button selectButton;

    public void Refresh(int level, bool unlocked, int stars)
    {
        if (levelImage != null) levelImage.sprite = levelSprite;
        if (imagePlaceholder != null) imagePlaceholder.SetActive(levelSprite == null);
        if (levelNameText != null) levelNameText.text = "LEVEL " + level + "  •  " +
            (level == 1 ? "CITY OUTSKIRTS" : level == 2 ? "OLD CATHEDRAL" : "FINAL DISTRICT");
        if (starsText != null) starsText.text = stars == 0 ? "☆ ☆ ☆" :
            (stars >= 1 ? "★" : "☆") + " " + (stars >= 2 ? "★" : "☆") + " " + (stars >= 3 ? "★" : "☆");
        if (stateText != null) stateText.text = unlocked ? "ENTER" : "LOCKED";
        if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
        if (selectButton != null) selectButton.interactable = unlocked;
    }
}
