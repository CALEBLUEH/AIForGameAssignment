using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AutoBattleFlowPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.AutoBattleFlowSmoke.Running";
    private const string PhaseKey = "AIForGame.AutoBattleFlowSmoke.Phase";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static double earliestCheck;
    private static double deadline;
    private static CombatUnit lowestHealthAlly;
    private static CombatUnit highestDamageAlly;
    private static float valueBefore;

    static AutoBattleFlowPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Auto Battle And Flow Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before starting the auto-battle smoke test.");
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        earliestCheck = deadline = 0d;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        int phase = SessionState.GetInt(PhaseKey, 0);
        if (!EditorApplication.isPlaying)
        {
            if (phase != 99) return;
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        try
        {
            if (deadline <= 0d) deadline = EditorApplication.timeSinceStartup + 35d;
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Auto-battle flow smoke timed out in phase " + phase + ".");
            if (EditorApplication.timeSinceStartup < earliestCheck) return;

            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (director == null || (phase < 4 && !director.IsPlaying)) return;

            if (phase == 0)
            {
                FreezeBackgroundAutoDecisions(director);
                CombatUnit[] allies = Units(CombatUnit.CombatTeam.Player);
                CombatUnit enemy = Units(CombatUnit.CombatTeam.Enemy).FirstOrDefault();
                if (allies.Length < 2 || enemy == null)
                    throw new InvalidOperationException("The scene needs two allies and one enemy for auto-decision validation.");

                SetField(allies[0], "currentHealth", allies[0].maxHealth * 0.68f);
                SetField(allies[1], "currentHealth", allies[1].maxHealth * 0.22f);
                lowestHealthAlly = allies[1];
                highestDamageAlly = allies.OrderByDescending(unit => unit.AttackPower).First();
                if (Invoke<CombatUnit>(director, "FindLowestHealthAlly", true) != lowestHealthAlly)
                    throw new InvalidOperationException("Auto heal did not select the ally with the lowest HP ratio.");
                if (Invoke<CombatUnit>(director, "FindHighestDamageAlly") != highestDamageAlly)
                    throw new InvalidOperationException("Auto buff did not select the highest-damage ally.");
                ValidateMovementDestination(allies[0].GetComponent<AutoCombatAI>(), enemy);

                valueBefore = lowestHealthAlly.CurrentHealth;
                UseBasicSkill(director, "Heal");
                if (lowestHealthAlly.CurrentHealth <= valueBefore)
                    throw new InvalidOperationException("Automatic Heal did not restore the lowest-health ally.");
                Advance(1, 0.65d);
            }
            else if (phase == 1)
            {
                valueBefore = highestDamageAlly.AttackPower;
                UseBasicSkill(director, "Buff");
                if (highestDamageAlly.AttackPower <= valueBefore)
                    throw new InvalidOperationException("Automatic Buff did not increase the highest-damage ally's attack.");
                Advance(2, 0.65d);
            }
            else if (phase == 2)
            {
                UseBasicSkill(director, "Cover");
                Advance(3, 0.65d);
            }
            else if (phase == 3)
            {
                UseBasicSkill(director, "Airstrike");
                SetField(director, "resultDelay", 0.1f);
                Invoke<object>(director, "Finish", false);
                if (!director.IsResultPending || director.resultPanel.activeSelf)
                    throw new InvalidOperationException("Defeat result did not respect its configured delay.");
                Advance(4, 0.65d);
            }
            else if (phase == 4)
            {
                CanvasGroup group = director.resultPanel.GetComponent<CanvasGroup>();
                Text[] text = director.resultPanel.GetComponentsInChildren<Text>(true);
                if (!director.resultPanel.activeInHierarchy || group == null || group.alpha < 0.99f)
                    throw new InvalidOperationException("Post-battle dim panel did not fade in.");
                if (!text.Any(label => label.text == "DEFEAT") ||
                    !text.Any(label => label.text == "CONFIRM") || !text.Any(label => label.text == "RESTART"))
                    throw new InvalidOperationException("Post-battle title or Confirm/Restart controls are missing.");
                IEnumerator victoryPreview = Invoke<IEnumerator>(director, "ShowResultAfterDelay", true, 3);
                director.StartCoroutine(victoryPreview);
                Advance(5, 0.65d);
            }
            else if (phase == 5)
            {
                Text[] text = director.resultPanel.GetComponentsInChildren<Text>(true);
                if (!director.resultPanel.activeInHierarchy || !text.Any(label => label.text == "VICTORY"))
                    throw new InvalidOperationException("Victory result presentation did not appear.");
                Debug.Log("AUTO_BATTLE_FLOW_PLAYMODE_OK lowest-HP heal, highest-damage buff, airstrike, cover, max-range dash destination, delayed Victory/Defeat dim, Confirm, and Restart passed.");
                Finish();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void FreezeBackgroundAutoDecisions(BattleDirector director)
    {
        SetField(director, "nextAutoBasicSkillDecision", Time.time + 1000f);
        foreach (AutoCombatAI ai in Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None))
        {
            SetField(ai, "nextAutoDecision", Time.time + 1000f);
            ai.enabled = false;
        }
    }

    private static void ValidateMovementDestination(AutoCombatAI ai, CombatUnit enemy)
    {
        if (ai == null) throw new InvalidOperationException("Player is missing AutoCombatAI.");
        Vector3 original = enemy.transform.position;
        Vector3 forward = ai.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        float startDistance = ai.Unit.AttackRange * 3f + ai.movementRange;
        enemy.transform.position = ai.NavMeshWorldPosition + forward.normalized * startDistance;
        object[] arguments = { enemy, Vector3.zero };
        MethodInfo method = typeof(AutoCombatAI).GetMethod("TryGetAutoMovementDestination", PrivateInstance);
        bool found = method != null && (bool)method.Invoke(ai, arguments);
        Vector3 destination = (Vector3)arguments[1];
        enemy.transform.position = original;
        if (!found) throw new InvalidOperationException("Full movement gauge did not produce an automatic dash destination.");
        float travel = FlatDistance(ai.NavMeshWorldPosition, destination);
        float remaining = startDistance - travel;
        float desiredRemaining = ai.Unit.AttackRange * ai.autoMovementAttackRangeFraction;
        if (travel > ai.movementRange + 0.05f || Mathf.Abs(remaining - desiredRemaining) > 0.25f && travel < ai.movementRange - 0.05f)
            throw new InvalidOperationException("Automatic dash did not stop near maximum attack range.");
    }

    private static void UseBasicSkill(BattleDirector director, string kindName)
    {
        FreezeBackgroundAutoDecisions(director);
        SetField(director, "universalPoints", 100f);
        IList queue = (IList)GetField(director, "skillQueue");
        object desired = queue.Cast<object>().FirstOrDefault(entry =>
            GetField(entry, "kind").ToString() == kindName);
        if (desired == null) throw new InvalidOperationException(kindName + " is missing from the skill queue.");
        queue.Remove(desired);
        queue.Insert(0, desired);
        float energyBefore = director.UniversalPoints;
        bool used = Invoke<bool>(director, "TryAutoUseQueuedBasicSkill");
        if (!used || director.UniversalPoints >= energyBefore)
            throw new InvalidOperationException("Automatic " + kindName + " did not cast or spend energy.");
    }

    private static CombatUnit[] Units(CombatUnit.CombatTeam team) =>
        Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
            .Where(unit => unit.team == team && !unit.IsDead && unit.gameObject.activeInHierarchy).ToArray();

    private static void Advance(int phase, double delay)
    {
        SessionState.SetInt(PhaseKey, phase);
        earliestCheck = EditorApplication.timeSinceStartup + delay;
    }

    private static void Finish()
    {
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }

    private static T Invoke<T>(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, PrivateInstance);
        if (method == null) throw new MissingMethodException(target.GetType().Name, methodName);
        object result = method.Invoke(target, arguments);
        return result == null ? default : (T)result;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance | BindingFlags.Public);
        if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance | BindingFlags.Public);
        if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
