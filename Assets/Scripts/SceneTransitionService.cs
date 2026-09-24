using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SceneTransitionService : MonoBehaviour
{
    private const float FadeToBlackDuration = 0.18f;
    private const float FadeFromBlackDuration = 0.22f;
    private static SceneTransitionService instance;
    private CanvasGroup fadeGroup;
    private bool transitioning;

    public static bool IsTransitioning => instance != null && instance.transitioning;
    public static float FadeAlpha => instance == null || instance.fadeGroup == null ? 0f : instance.fadeGroup.alpha;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject service = new GameObject("Scene Transition Service");
        instance = service.AddComponent<SceneTransitionService>();
        DontDestroyOnLoad(service);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateOverlay();
    }

    private void Start() => StartCoroutine(FadeInitialScene());

    public static void LoadScene(string sceneName)
    {
        Bootstrap();
        sceneName = NormalizeSceneName(sceneName);
        if (string.IsNullOrWhiteSpace(sceneName) || instance.transitioning) return;
        instance.StartCoroutine(instance.Transition(sceneName));
    }

    public static void ReloadActiveScene() => LoadScene(SceneManager.GetActiveScene().name);

    private IEnumerator FadeInitialScene()
    {
        transitioning = true;
        yield return Fade(0f, FadeFromBlackDuration);
        fadeGroup.blocksRaycasts = false;
        transitioning = false;
    }

    private IEnumerator Transition(string sceneName)
    {
        transitioning = true;
        fadeGroup.blocksRaycasts = true;
        yield return Fade(1f, FadeToBlackDuration);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null)
        {
            Debug.LogError("Unable to start scene transition to '" + sceneName + "'.");
            yield return Fade(0f, FadeFromBlackDuration);
            fadeGroup.blocksRaycasts = false;
            transitioning = false;
            yield break;
        }
        while (!load.isDone) yield return null;
        yield return null;
        yield return Fade(0f, FadeFromBlackDuration);
        fadeGroup.blocksRaycasts = false;
        transitioning = false;
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = fadeGroup.alpha;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        fadeGroup.alpha = target;
    }

    private void CreateOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        gameObject.AddComponent<GraphicRaycaster>();
        fadeGroup = gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = true;
        fadeGroup.interactable = false;

        GameObject imageObject = new GameObject("Black Fade", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
    }

    private static string NormalizeSceneName(string sceneName)
    {
        if (sceneName == "Preparation") return GameProgress.PreparationSceneName;
        if (sceneName == "GameplayLevel1") return "Gameplay_Level1";
        if (sceneName == "GameplayLevel2") return "Gameplay_Level2";
        if (sceneName == "GameplayLevel3") return "Gameplay_Level3";
        return sceneName;
    }
}
