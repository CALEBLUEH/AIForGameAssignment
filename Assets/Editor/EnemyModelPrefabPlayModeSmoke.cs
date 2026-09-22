using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyModelPrefabPlayModeSmoke
{
    private const string RunningKey = "AIForGame.EnemyModelPrefabSmoke.Running";
    private const string PhaseKey = "AIForGame.EnemyModelPrefabSmoke.Phase";
    private static readonly string[] PrefabPaths =
    {
        "Assets/Prefabs/Enemies/Enemy_Robot.prefab",
        "Assets/Prefabs/Enemies/Enemy_ToramaruTank.prefab",
        "Assets/Prefabs/Enemies/Enemy_HelmetGangVehicle.prefab",
        "Assets/Prefabs/Enemies/Enemy_Sensei.prefab",
        "Assets/Prefabs/Enemies/Boss_SaibaMomoi.prefab",
        "Assets/Prefabs/Enemies/Boss_KisakiBall.prefab",
        "Assets/Prefabs/Enemies/Boss_KoyukiPrism.prefab"
    };

    private static readonly Dictionary<EnemyAttackRecoil, Vector3> RestPositions = new();
    private static readonly Dictionary<EnemyAttackRecoil, Quaternion> RestRotations = new();
    private static AutoCombatAI idleProbe;
    private static double checkAt;

    static EnemyModelPrefabPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Enemy Model Prefab Play Mode Smoke")]
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetInt(PhaseKey, 0) != 99) return;
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        try
        {
            int phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 0) BeginIdleCheck();
            else if (phase == 1 && EditorApplication.timeSinceStartup >= checkAt) BeginPrefabCheck();
            else if (phase == 2 && EditorApplication.timeSinceStartup >= checkAt) BeginRecoilCheck();
            else if (phase == 3 && EditorApplication.timeSinceStartup >= checkAt) CheckRecoilKick();
            else if (phase == 4 && EditorApplication.timeSinceStartup >= checkAt) Finish();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void BeginIdleCheck()
    {
        GameObject probe = new GameObject("No Enemy Idle Probe");
        probe.AddComponent<CapsuleCollider>();
        CombatUnit unit = probe.AddComponent<CombatUnit>();
        unit.team = CombatUnit.CombatTeam.Player;
        unit.SetApplyCharacterDataOnAwake(false);
        idleProbe = probe.AddComponent<AutoCombatAI>();
        checkAt = EditorApplication.timeSinceStartup + 0.1d;
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void BeginPrefabCheck()
    {
        if (idleProbe == null || idleProbe.CurrentState != "Waiting")
            throw new InvalidOperationException("A character with no enemy did not enter the idle Waiting state.");
        Object.Destroy(idleProbe.gameObject);

        RestPositions.Clear();
        RestRotations.Clear();
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[i]);
            if (prefab == null) throw new InvalidOperationException("Missing prefab " + PrefabPaths[i]);
            GameObject instance = Object.Instantiate(prefab, new Vector3(i * 4f, 0f, 0f), Quaternion.identity);
            AutoCombatAI ai = instance.GetComponent<AutoCombatAI>();
            CombatUnit unit = instance.GetComponent<CombatUnit>();
            EnemyAttackRecoil recoil = instance.GetComponent<EnemyAttackRecoil>();
            if (ai == null || unit == null || recoil == null || recoil.VisualRoot == null)
                throw new InvalidOperationException(instance.name + " lost its runtime combat or recoil wiring.");
            ai.enabled = false;
            bool expectedBoss = instance.name.StartsWith("Boss_", StringComparison.Ordinal);
            if (unit.team != CombatUnit.CombatTeam.Enemy || unit.isBoss != expectedBoss)
                throw new InvalidOperationException(instance.name + " has incorrect runtime enemy/boss identity.");
            if (instance.name.StartsWith("Enemy_Sensei", StringComparison.Ordinal) &&
                instance.GetComponentInChildren<Animator>(true) != null)
                throw new InvalidOperationException("Sensei should remain a static T-pose model without an Animator.");
            RestPositions[recoil] = recoil.VisualRoot.localPosition;
            RestRotations[recoil] = recoil.VisualRoot.localRotation;
        }

        checkAt = EditorApplication.timeSinceStartup + 0.1d;
        SessionState.SetInt(PhaseKey, 2);
    }

    private static void BeginRecoilCheck()
    {
        foreach (EnemyAttackRecoil recoil in RestPositions.Keys) recoil.PlayRecoil();
        checkAt = EditorApplication.timeSinceStartup + 0.06d;
        SessionState.SetInt(PhaseKey, 3);
    }

    private static void CheckRecoilKick()
    {
        if (RestPositions.Keys.Any(recoil => recoil == null || recoil.VisualRoot == null ||
            (Vector3.Distance(recoil.VisualRoot.localPosition, RestPositions[recoil]) < 0.001f &&
             Quaternion.Angle(recoil.VisualRoot.localRotation, RestRotations[recoil]) < 0.1f)))
            throw new InvalidOperationException("At least one enemy model did not visibly recoil during attack animation.");
        checkAt = EditorApplication.timeSinceStartup + 0.2d;
        SessionState.SetInt(PhaseKey, 4);
    }

    private static void Finish()
    {
        if (RestPositions.Keys.Any(recoil => recoil == null || recoil.VisualRoot == null ||
            Vector3.Distance(recoil.VisualRoot.localPosition, RestPositions[recoil]) > 0.001f ||
            Quaternion.Angle(recoil.VisualRoot.localRotation, RestRotations[recoil]) > 0.1f))
            throw new InvalidOperationException("At least one enemy model did not return to its authored pose after recoil.");
        Debug.Log("ENEMY_MODEL_PREFAB_PLAYMODE_OK seven model prefabs, boss identity, Sensei T-pose, no-enemy idle state and attack recoil passed.");
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }
}
