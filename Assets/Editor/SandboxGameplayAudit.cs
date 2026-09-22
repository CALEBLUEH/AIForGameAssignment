using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SandboxGameplayAudit
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/GameplayAI.unity",
        "Assets/Scenes/GameplayLevel2.unity",
        "Assets/Scenes/GameplayLevel3.unity",
        "Assets/Scenes/Sandbox_Gameplay.unity"
    };

    public static void Report()
    {
        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            BattleDirector director = Find<BattleDirector>(scene);
            string missing = director == null ? "BattleDirector" : string.Join(",", typeof(BattleDirector)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && field.GetValue(director) == null)
                .Select(field => field.Name));
            Debug.Log($"GAMEPLAY_AUDIT scene={scene.name} cover={FindAll<CoverPoint>(scene).Length} blockers={FindAll<BlockingObstacle>(scene).Length} destructible={FindAll<DestructibleCover>(scene).Length} buttons={FindAll<UnityEngine.UI.Button>(scene).Length} sliders={FindAll<UnityEngine.UI.Slider>(scene).Length} missingDirectorRefs=[{missing}]");

            if (scene.name == "Sandbox_Gameplay" && director != null && director.battlePanel != null)
            {
                Transform current = director.battlePanel.transform;
                while (current != null)
                {
                    Debug.Log($"SANDBOX_BATTLE_UI_CHAIN object={current.name} activeSelf={current.gameObject.activeSelf} activeInHierarchy={current.gameObject.activeInHierarchy}");
                    current = current.parent;
                }
            }

            if (scene.name != "Sandbox_Gameplay") continue;
            GameObject obstacleRoot = FindObject(scene, "Obstacle");
            if (obstacleRoot == null) continue;
            foreach (Collider collider in obstacleRoot.GetComponentsInChildren<Collider>(true))
                Debug.Log($"SANDBOX_OBSTACLE path={PathOf(collider.transform, obstacleRoot.transform)} active={collider.gameObject.activeInHierarchy} boundsCenter={collider.bounds.center} boundsSize={collider.bounds.size}");
        }
        EditorApplication.Exit(0);
    }

    private static string PathOf(Transform current, Transform root)
    {
        string value = current.name;
        while (current.parent != null && current != root)
        {
            current = current.parent;
            value = current.name + "/" + value;
        }
        return value;
    }

    private static T Find<T>(Scene scene) where T : Component => FindAll<T>(scene).FirstOrDefault();
    private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static GameObject FindObject(Scene scene, string objectName) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .FirstOrDefault(item => item.name == objectName)?.gameObject;
}
