using UnityEngine;
using UnityEngine.SceneManagement;

public class TouchToStart : MonoBehaviour
{
    [Header("Scene")]
    public string lobbySceneName = "Lobby";

    void Update()
    {
        // Mouse click
        if (Input.GetMouseButtonDown(0))
        {
            LoadLobby();
        }

        // Touch
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            LoadLobby();
        }
    }

    void LoadLobby()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}