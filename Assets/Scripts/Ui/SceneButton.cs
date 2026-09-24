using UnityEngine;

public class SceneButton : MonoBehaviour
{
    [Header("Scene Settings")]
    public string sceneName;

    public void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("Scene name is empty!");
            return;
        }

        SceneTransitionService.LoadScene(sceneName);
    }

    public void ExitApplication()
    {
        Application.Quit();
    }
}