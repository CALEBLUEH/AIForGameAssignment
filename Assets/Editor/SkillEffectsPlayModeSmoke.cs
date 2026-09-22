using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SkillEffectsPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.SkillEffectsSmoke.Running";
    private const string PhaseKey = "AIForGame.SkillEffectsSmoke.Phase";
    private static double checkAt;
    private static CombatUnit ally, enemy;
    private static float attackBefore, enemyHealthBefore;
    private static Vector3 coverPoint;

    static SkillEffectsPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Skill Effects Play Mode Smoke")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        checkAt = 0d;
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
            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (director == null || !director.IsPlaying) return;
            int phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 0) Begin(director);
            else if (phase == 1 && EditorApplication.timeSinceStartup >= checkAt) Finish();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void Begin(BattleDirector director)
    {
        AutoCombatAI[] navigation = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None);
        foreach (AutoCombatAI ai in navigation) ai.enabled = false;
        ally = navigation.Select(ai => ai.Unit).First(unit => unit.team == CombatUnit.CombatTeam.Player && !unit.IsDead);
        enemy = navigation.Select(ai => ai.Unit).First(unit => unit.team == CombatUnit.CombatTeam.Enemy && !unit.IsDead);
        enemy.ConfigureSpawn(1000f, enemy.attackPower, 0f, false);

        Type directorType = typeof(BattleDirector);
        Type kindType = directorType.GetNestedType("QueuedSkillKind", BindingFlags.NonPublic);
        FieldInfo queueField = directorType.GetField("skillQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        IEnumerable queue = (IEnumerable)queueField.GetValue(director);
        string[] kinds = queue.Cast<object>().Select(entry => entry.GetType()
            .GetField("kind", BindingFlags.Instance | BindingFlags.Public).GetValue(entry).ToString()).ToArray();
        foreach (string required in new[] { "Heal", "Buff", "Airstrike", "Cover" })
            if (!kinds.Contains(required)) throw new InvalidOperationException(required + " is missing from the randomized FIFO queue.");

        director.universalCapacity = 1000f;
        SetPrivate(directorType, director, "basicBuffDuration", 0.25f);
        SetPrivate(directorType, director, "airstrikeDropDuration", 0.12f);
        SetPrivate(directorType, director, "coverDropDuration", 0.12f);
        SetPrivate(directorType, director, "skillDropHeight", 3f);
        directorType.GetField("universalPoints", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, 1000f);
        MethodInfo cast = directorType.GetMethod("TryResolveBasicSkill", BindingFlags.Instance | BindingFlags.NonPublic);

        ally.TakeDamage(30f, enemy.transform.position);
        float injured = ally.CurrentHealth;
        Invoke(cast, director, kindType, "Heal", ally.transform.position);
        if (ally.CurrentHealth <= injured) throw new InvalidOperationException("Basic Heal did not restore the selected ally.");

        attackBefore = ally.AttackPower;
        Invoke(cast, director, kindType, "Buff", ally.transform.position);
        if (ally.AttackPower < attackBefore * 1.39f) throw new InvalidOperationException("Basic Buff did not add 40% attack.");

        enemyHealthBefore = enemy.CurrentHealth;
        Invoke(cast, director, kindType, "Airstrike", enemy.transform.position);
        coverPoint = ally.transform.position + Vector3.right * 2.5f;
        Invoke(cast, director, kindType, "Cover", coverPoint);

        SkillTargetingOverlayUI overlay = Resources.FindObjectsOfTypeAll<SkillTargetingOverlayUI>().FirstOrDefault();
        if (overlay == null) throw new InvalidOperationException("Missing targeting overlay.");
        overlay.Show("TEST", "TEST");
        overlay.SetHighlightHoles(new[] { ally }, Camera.main);
        UnityEngine.UI.Image dimmer = overlay.GetComponentsInChildren<UnityEngine.UI.Image>(true)
            .FirstOrDefault(image => image.name == "Targeting Dimmer");
        if (dimmer == null || dimmer.material == null || dimmer.material.shader.name != "AIFG/Targeting Dimmer Cutout")
            throw new InvalidOperationException("The undimmed target cutout shader was not active.");
        overlay.Hide();

        checkAt = EditorApplication.timeSinceStartup + 0.65d;
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void Finish()
    {
        if (enemy == null || enemy.CurrentHealth >= enemyHealthBefore)
            throw new InvalidOperationException("Airstrike missile did not damage its landing area.");
        if (ally == null || ally.AttackPower > attackBefore * 1.01f)
            throw new InvalidOperationException("Basic Buff did not expire after its configured duration.");
        GameObject cover = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == "Placed Cover" && Vector3.Distance(item.transform.position, coverPoint) < 1f);
        if (cover == null || cover.GetComponent<NavMeshObstacle>() == null ||
            cover.GetComponent<TacticalCoverObstacle>() == null || cover.GetComponentsInChildren<CoverPoint>().Length != 3)
            throw new InvalidOperationException("Dropped cover was not configured as a carved tactical obstacle with three cover points.");
        Debug.Log("SKILL_EFFECTS_PLAYMODE_OK basic Heal/Buff/Airstrike/Cover, FIFO inclusion, timed buff expiry, tactical dropped cover, and undimmed target cutout passed.");
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }

    private static void Invoke(MethodInfo method, BattleDirector director, Type kindType, string kind, Vector3 point)
    {
        object[] arguments = { Enum.Parse(kindType, kind), point, Vector3.zero };
        bool result = (bool)method.Invoke(director, arguments);
        if (!result) throw new InvalidOperationException("Basic " + kind + " was rejected.");
    }

    private static void SetPrivate(Type type, object target, string field, float value)
    {
        FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new MissingFieldException(type.Name, field);
        info.SetValue(target, value);
    }
}
