using UnityEngine;
using UnityEngine.Video;

// Generated on the temporary Web build scene; original scene clips stay editable.
[DefaultExecutionOrder(-1000)]
public sealed class RedSceneWebVideoSource : MonoBehaviour
{
    public VideoPlayer player;
    public string relativeFile;
    public bool autoPlay;
    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        player.clip = null;
        player.source = VideoSource.Url;
        player.url = Application.streamingAssetsPath.TrimEnd('/') + "/" + relativeFile;
#endif
    }
    private void OnEnable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (autoPlay) player.Play();
#endif
    }
}
