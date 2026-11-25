using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[BuildCallbackVersion(1)]
public sealed class RedWebVideoBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.WebGL) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var player in root.GetComponentsInChildren<SkillCinematicPlayer>(true))
            {
                player.PrepareWebBuild();
                Debug.Log("Prepared streamed Red Dissertation skill videos: " + scene.path);
            }
            foreach (var video in root.GetComponentsInChildren<VideoPlayer>(true))
            {
                if (video.clip == null) continue;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(video.clip));
                if (string.IsNullOrEmpty(guid)) throw new System.Exception("Scene video has no asset GUID.");
                var source = video.GetComponent<RedSceneWebVideoSource>();
                if (source == null) source = video.gameObject.AddComponent<RedSceneWebVideoSource>();
                source.player = video;
                source.relativeFile = "RedDissertation/scene-" + guid + ".mp4";
                source.autoPlay = video.playOnAwake;
                video.playOnAwake = false;
                video.clip = null;
                video.source = VideoSource.Url;
                video.url = "StreamingAssets/" + source.relativeFile;
                Debug.Log("Prepared streamed scene video: " + scene.path);
            }
        }
    }
}
