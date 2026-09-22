using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CoverHealthHUD : MonoBehaviour
{
    [SerializeField] private DestructibleCover cover;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fill;

    public void Configure(DestructibleCover target, Slider slider, Image fillImage)
    {
        Unsubscribe();
        cover = target;
        healthSlider = slider;
        fill = fillImage;
        Subscribe();
        Refresh();
    }

    private void Awake()
    {
        if (cover == null) cover = GetComponentInParent<DestructibleCover>();
        if (healthSlider == null) healthSlider = GetComponentInChildren<Slider>(true);
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable() => Unsubscribe();

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
    }

    private void Subscribe()
    {
        if (cover == null) return;
        cover.HealthChanged -= OnHealthChanged;
        cover.HealthChanged += OnHealthChanged;
    }

    private void Unsubscribe()
    {
        if (cover != null) cover.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(DestructibleCover _) => Refresh();

    private void Refresh()
    {
        if (cover == null || healthSlider == null) return;
        healthSlider.maxValue = Mathf.Max(1f, cover.MaxHealth);
        healthSlider.value = Mathf.Clamp(cover.CurrentHealth, 0f, cover.MaxHealth);
        if (fill != null) fill.color = new Color(0.25f, 0.78f, 1f, 1f);
    }
}
