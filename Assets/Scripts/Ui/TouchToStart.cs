using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class TouchToStart : MonoBehaviour
{
    [Header("Scene")]
    public string lobbySceneName = "Lobby";

    void Update()
    {
        // Mouse
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return;

            LoadLobby();
        }

        // Touch
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                return;

            LoadLobby();
        }
    }

    void LoadLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogWarning("Lobby scene name is empty!");
            return;
        }

        SceneTransitionService.LoadScene(lobbySceneName);
    }
}