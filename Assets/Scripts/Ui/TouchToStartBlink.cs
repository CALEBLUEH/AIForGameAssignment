using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TouchToStartBlink : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text text;
    public Image panel;

    [Header("Blink Settings")]
    public float alphaSpeed = 50f;

    private float alpha = 50f;
    private bool fadingIn = true;

    void Start()
    {
        SetAlpha(text, alpha);
        SetAlpha(panel, alpha);
    }

    void Update()
    {
        if (fadingIn)
        {
            alpha += alphaSpeed * Time.deltaTime;

            if (alpha >= 200f)
            {
                alpha = 200f;
                fadingIn = false;
            }
        }
        else
        {
            alpha -= alphaSpeed * Time.deltaTime;

            if (alpha <= 50f)
            {
                alpha = 50f;
                fadingIn = true;
            }
        }

        SetAlpha(text, alpha);
        SetAlpha(panel, alpha);
    }

    void SetAlpha(Graphic graphic, float alphaValue)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alphaValue / 255f;
        graphic.color = color;
    }
}