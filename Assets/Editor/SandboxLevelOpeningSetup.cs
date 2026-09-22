using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SandboxLevelOpeningSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";

    [MenuItem("Tools/AI For Game/Configure Sandbox Level Opening")]
    public static void Configure()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BattleDirector director = FindSceneComponent<BattleDirector>(scene);
        Camera gameplayCamera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
        Transform coordinateRoot = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "SquadCoordinate");
        Camera openingCamera = coordinateRoot != null
            ? coordinateRoot.GetComponentsInChildren<Camera>(true).FirstOrDefault(camera => camera != gameplayCamera)
            : null;
        if (director == null || gameplayCamera == null || coordinateRoot == null || openingCamera == null)
            throw new InvalidOperationException("Sandbox needs BattleDirector, Main Camera, SquadCoordinate and Opening Camera before setup.");

        AutoCombatAI[] authoredSquad = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<AutoCombatAI>(true))
            .Where(ai => ai.GetComponent<CombatUnit>()?.team == CombatUnit.CombatTeam.Player && ai.gameObject.activeSelf)
            .OrderBy(ai => ai.rosterOrder)
            .Take(4)
            .ToArray();
        if (authoredSquad.Length != 4)
            throw new InvalidOperationException("Exactly four currently visible player characters are required to author the opening slots.");

        Transform[] markers = new Transform[4];
        for (int i = 0; i < markers.Length; i++)
        {
            string markerName = "Squad Spawn " + (i + 1);
            Transform marker = coordinateRoot.Find(markerName);
            if (marker == null)
            {
                marker = new GameObject(markerName).transform;
                marker.SetParent(coordinateRoot, false);
            }
            marker.SetPositionAndRotation(authoredSquad[i].transform.position, authoredSquad[i].transform.rotation);
            marker.localScale = Vector3.one;
            markers[i] = marker;
            EditorUtility.SetDirty(marker);
        }

        LevelOpeningSequence sequence = coordinateRoot.GetComponent<LevelOpeningSequence>();
        if (sequence == null) sequence = coordinateRoot.gameObject.AddComponent<LevelOpeningSequence>();
        sequence.Configure(openingCamera, gameplayCamera, markers, 2f, 10f);
        director.SetOpeningSequence(sequence);
        openingCamera.enabled = false;
        AudioListener openingListener = openingCamera.GetComponent<AudioListener>();
        if (openingListener != null) openingListener.enabled = false;
        gameplayCamera.enabled = true;
        AudioListener gameplayListener = gameplayCamera.GetComponent<AudioListener>();
        if (gameplayListener != null) gameplayListener.enabled = true;

        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(openingCamera);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + ScenePath);
        Debug.Log("SANDBOX_LEVEL_OPENING_CONFIGURED four reusable squad markers and the authored opening camera are under SquadCoordinate.");
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
}
