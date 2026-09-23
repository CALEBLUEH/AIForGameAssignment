using UnityEngine;
using UnityEngine.UI;

public class LogoFadeIn : MonoBehaviour
{
    [Header("Logo")]
    public Image logo;

    [Header("Fade Settings")]
    public float delay = 3f;
    public float fadeDuration = 1f;

    private float timer = 0f;
    private bool fading = false;

    void Start()
    {
        // 一开始完全透明
        Color color = logo.color;
        color.a = 0f;
        logo.color = color;
    }

    void Update()
    {
        // 等待
        if (!fading)
        {
            timer += Time.deltaTime;

            if (timer >= delay)
            {
                fading = true;
            }

            return;
        }

        // 慢慢变成完全不透明
        Color logoColor = logo.color;
        logoColor.a += Time.deltaTime / fadeDuration;
        logoColor.a = Mathf.Clamp01(logoColor.a);
        logo.color = logoColor;
    }
}