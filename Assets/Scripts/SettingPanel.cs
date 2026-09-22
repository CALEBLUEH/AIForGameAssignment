using UnityEngine;

public class SettingPanel : MonoBehaviour
{
    [Header("Setting Panel")]
    public GameObject settingPanel;

    public void OpenSetting()
    {
        settingPanel.SetActive(true);
    }

    public void CloseSetting()
    {
        settingPanel.SetActive(false);
    }

    public void ToggleSetting()
    {
        settingPanel.SetActive(!settingPanel.activeSelf);
    }
}