using UnityEngine;
using UnityEngine.UI;

public class WorldUnitHUD : MonoBehaviour
{
    public CombatUnit unit;
    public Slider healthSlider;
    public Image healthFill;
    public Text unitName;
    public Text[] damageLabels;
    public Color playerColor = new Color(0.12f, 0.78f, 1f, 1f);
    public Color enemyColor = new Color(1f, 0.22f, 0.18f, 1f);
    private float[] labelLives;
    private Vector2[] labelOrigins;
    private int nextLabel;

    private void Awake()
    {
        Bind(unit != null ? unit : GetComponentInParent<CombatUnit>());
        labelLives = new float[damageLabels == null ? 0 : damageLabels.Length];
        labelOrigins = new Vector2[labelLives.Length];
        for (int i = 0; i < labelLives.Length; i++)
        {
            labelOrigins[i] = damageLabels[i].rectTransform.anchoredPosition;
            damageLabels[i].gameObject.SetActive(false);
        }
    }

    public void Bind(CombatUnit target)
    {
        unit = target;
        if (unit == null) return;
        if (unitName != null) unitName.text = unit.name;
        if (healthFill != null) healthFill.color =
            unit.team == CombatUnit.CombatTeam.Player ? playerColor : enemyColor;
        RefreshHealth();
    }

    private void LateUpdate()
    {
        if (unit == null) return;
        Camera camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
        RefreshHealth();
        for (int i = 0; i < labelLives.Length; i++)
        {
            if (labelLives[i] <= 0f) continue;
            labelLives[i] -= Time.unscaledDeltaTime;
            float progress = 1f - Mathf.Clamp01(labelLives[i] / 1.05f);
            damageLabels[i].rectTransform.anchoredPosition = labelOrigins[i] + Vector2.up * (16f + progress * 70f);
            Color color = damageLabels[i].color;
            color.a = 1f - progress;
            damageLabels[i].color = color;
            if (labelLives[i] <= 0f) damageLabels[i].gameObject.SetActive(false);
        }
    }

    private void RefreshHealth()
    {
        if (healthSlider == null || unit == null) return;
        healthSlider.maxValue = Mathf.Max(1f, unit.maxHealth);
        healthSlider.value = Mathf.Clamp(unit.CurrentHealth, 0f, unit.maxHealth);
    }

    public void ShowDamage(float amount)
    {
        ShowNumber("-" + Mathf.CeilToInt(amount), new Color(1f, 0.82f, 0.18f, 1f));
    }

    public void ShowHeal(float amount)
    {
        ShowNumber("+" + Mathf.CeilToInt(amount), new Color(0.25f, 1f, 0.48f, 1f));
    }

    private void ShowNumber(string value, Color color)
    {
        if (damageLabels == null || damageLabels.Length == 0) return;
        int index = nextLabel++ % damageLabels.Length;
        Text label = damageLabels[index];
        label.text = value;
        label.color = color;
        label.rectTransform.anchoredPosition = labelOrigins[index];
        label.gameObject.SetActive(true);
        labelLives[index] = 1.05f;
    }
}
