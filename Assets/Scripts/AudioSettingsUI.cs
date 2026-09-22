using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    private const string VolumeKey = "AIFG.MasterVolume";
    private const string MuteKey = "AIFG.Muted";
    public Slider volumeSlider;
    public Text volumeText;
    public Button muteButton;
    public Text muteText;
    private bool muted;

    private void Awake()
    {
        float volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
        Apply(volume);
    }

    public static void ApplySavedVolume()
    {
        float volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        bool savedMuted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
        AudioListener.volume = savedMuted ? 0f : volume;
    }

    private void SetVolume(float volume)
    {
        PlayerPrefs.SetFloat(VolumeKey, volume);
        if (volume > 0f && muted)
        {
            muted = false;
            PlayerPrefs.SetInt(MuteKey, 0);
        }
        Apply(volume);
    }

    private void ToggleMute()
    {
        muted = !muted;
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        Apply(volumeSlider == null ? 0.8f : volumeSlider.value);
    }

    private void Apply(float volume)
    {
        AudioListener.volume = muted ? 0f : volume;
        if (volumeText != null) volumeText.text = "MASTER VOLUME  " + Mathf.RoundToInt(volume * 100f) + "%";
        if (muteText != null) muteText.text = muted ? "UNMUTE" : "MUTE";
        PlayerPrefs.Save();
    }
}
