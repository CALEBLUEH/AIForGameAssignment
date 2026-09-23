using System;
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

    private Image[] achievementStars;

    public void Refresh(int level, bool unlocked, int stars)
    {
        GameProgress.LevelStar flags = GameProgress.LevelStar.None;
        if (stars >= 1) flags |= GameProgress.LevelStar.Completed;
        if (stars >= 2) flags |= GameProgress.LevelStar.NoCharacterDefeated;
        if (stars >= 3) flags |= GameProgress.LevelStar.UnderTwoMinutes;
        RefreshAchievements(level, unlocked, flags);
    }

    public void RefreshAchievements(int level, bool unlocked, GameProgress.LevelStar flags)
    {
        if (levelImage != null) levelImage.sprite = levelSprite;
        if (imagePlaceholder != null) imagePlaceholder.SetActive(levelSprite == null);
        if (levelNameText != null) levelNameText.text = "LEVEL " + level + "  •  " +
            (level == 1 ? "CITY OUTSKIRTS" : level == 2 ? "OLD CATHEDRAL" : "FINAL DISTRICT");
        bool complete = (flags & GameProgress.LevelStar.Completed) != 0;
        bool noDefeats = (flags & GameProgress.LevelStar.NoCharacterDefeated) != 0;
        bool underTwoMinutes = (flags & GameProgress.LevelStar.UnderTwoMinutes) != 0;
        if (starsText != null) starsText.text = (complete ? "★" : "☆") + " " +
            (noDefeats ? "★" : "☆") + " " + (underTwoMinutes ? "★" : "☆");
        RefreshStarImages(complete, noDefeats, underTwoMinutes);
        if (stateText != null) stateText.text = unlocked ? "ENTER" : "LOCKED";
        if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
        if (selectButton != null) selectButton.interactable = unlocked;
    }

    private void RefreshStarImages(bool complete, bool noDefeats, bool underTwoMinutes)
    {
        if (achievementStars == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            achievementStars = Array.FindAll(images, image => image != null &&
                image.gameObject.name.StartsWith("StarUi", StringComparison.Ordinal));
            Array.Sort(achievementStars, (left, right) =>
                left.rectTransform.anchoredPosition.x.CompareTo(right.rectTransform.anchoredPosition.x));
        }

        LevelStarSpriteSet sprites = LevelStarSpriteSet.Load();
        if (sprites == null) return;
        bool[] earned = { complete, noDefeats, underTwoMinutes };
        for (int i = 0; i < achievementStars.Length && i < earned.Length; i++)
            achievementStars[i].sprite = earned[i] ? sprites.earnedStar : sprites.hiddenStar;
    }
}
