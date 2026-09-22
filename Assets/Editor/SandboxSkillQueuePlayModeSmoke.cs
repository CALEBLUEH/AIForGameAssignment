using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SandboxSkillQueuePlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.SkillQueueSmoke.Running";
    private const string PhaseKey = "AIForGame.SkillQueueSmoke.Phase";
    private const string SuccessKey = "AIForGame.SkillQueueSmoke.Success";
    private static readonly Dictionary<int, Vector3> EnemyStarts = new Dictionary<int, Vector3>();
    private static double deadline;
    private static double earliestCheck;
    private static string[] initialCards;
    private static float universalBefore;
    private static AutoCombatAI movementUnit;
    private static Vector3 movementStart;

    static SandboxSkillQueuePlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Sandbox Skill Queue Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before starting the skill-queue smoke test.");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetBool(SuccessKey, false);
        EnemyStarts.Clear();
        initialCards = null;
        deadline = 0d;
        earliestCheck = 0d;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        int phase = SessionState.GetInt(PhaseKey, 0);
        if (!EditorApplication.isPlaying)
        {
            if (phase != 99) return;
            bool success = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseBool(SuccessKey);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            return;
        }

        try
        {
            if (deadline <= 0d)
            {
                deadline = EditorApplication.timeSinceStartup + 25d;
                earliestCheck = EditorApplication.timeSinceStartup + 1d;
            }
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Sandbox skill-queue smoke timed out in phase " + phase + ".");
            if (EditorApplication.timeSinceStartup < earliestCheck) return;

            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (director == null || !director.IsPlaying) return;
            SkillCardUI[] cards = Object.FindObjectsByType<SkillCardUI>(FindObjectsSortMode.None)
                .OrderBy(card => card.transform.GetSiblingIndex()).ToArray();

            if (phase == 0)
            {
                if (!director.UsesSkillQueue || cards.Length != 3)
                    throw new InvalidOperationException("The battle does not have exactly three configured skill cards.");
                foreach (SkillCardUI card in cards)
                    if (!card.gameObject.activeInHierarchy || card.DisplayedIcon == null || string.IsNullOrWhiteSpace(card.DisplayedCost))
                        throw new InvalidOperationException("A visible skill card is missing its icon or cost.");
                foreach (CombatUnit player in ActiveUnits(CombatUnit.CombatTeam.Player))
                    if (player.AppliesCharacterDataOnAwake)
                        throw new InvalidOperationException(player.name + " still overwrites Inspector stats from CharacterData in Awake.");
                if (director.AutoEnabled) director.autoButton.onClick.Invoke();
                foreach (CombatUnit enemy in ActiveUnits(CombatUnit.CombatTeam.Enemy))
                    EnemyStarts[enemy.GetInstanceID()] = enemy.transform.position;
                SessionState.SetInt(PhaseKey, 1);
                earliestCheck = EditorApplication.timeSinceStartup + 5d;
            }
            else if (phase == 1)
            {
                CombatUnit[] enemies = ActiveUnits(CombatUnit.CombatTeam.Enemy);
                int movedEnemies = enemies.Count(enemy => EnemyStarts.TryGetValue(enemy.GetInstanceID(), out Vector3 start) &&
                    FlatDistance(start, enemy.transform.position) > 1f);
                if (enemies.Length == 0 || movedEnemies < Mathf.CeilToInt(enemies.Length * 0.5f))
                {
                    string states = string.Join(", ", enemies.Select(enemy => enemy.name + "=" +
                        enemy.GetComponent<AutoCombatAI>().CurrentState + "/" +
                        (EnemyStarts.TryGetValue(enemy.GetInstanceID(), out Vector3 start) ? FlatDistance(start, enemy.transform.position).ToString("0.00") : "new")));
                    throw new InvalidOperationException("Enemy formation did not advance as a moving group. " + states);
                }

                initialCards = cards.Select(card => card.DisplayName).ToArray();
                universalBefore = director.UniversalPoints;
                UseFirstCard(director, cards[0]);
                SessionState.SetInt(PhaseKey, 2);
                earliestCheck = EditorApplication.timeSinceStartup + 0.55d;
            }
            else if (phase == 2)
            {
                cards = Object.FindObjectsByType<SkillCardUI>(FindObjectsSortMode.None)
                    .OrderBy(card => card.transform.GetSiblingIndex()).ToArray();
                string[] after = cards.Select(card => card.DisplayName).ToArray();
                if (after.Length != 3 || after[0] != initialCards[1] || after[1] != initialCards[2])
                    throw new InvalidOperationException("Used skill did not rotate from the front to the FIFO queue tail.");
                if (director.UniversalPoints >= universalBefore)
                    throw new InvalidOperationException("Using the first skill card did not spend universal skill points.");

                movementUnit = ActiveUnits(CombatUnit.CombatTeam.Player)
                    .Select(unit => unit.GetComponent<AutoCombatAI>()).Where(ai => ai != null)
                    .OrderBy(ai => ai.MovementCooldownRemaining).First();
                movementStart = movementUnit.transform.position;
                SessionState.SetInt(PhaseKey, 3);
                earliestCheck = EditorApplication.timeSinceStartup + Math.Max(0.1d, movementUnit.MovementCooldownRemaining + 0.1d);
            }
            else if (phase == 3)
            {
                if (!TryReachableOffset(movementUnit.transform.position, out Vector3 destination))
                    throw new InvalidOperationException("No nearby NavMesh destination was available for movement-skill validation.");
                if (!movementUnit.TryMovementSkill(destination))
                    throw new InvalidOperationException("Movement skill rejected a nearby reachable NavMesh destination.");
                SessionState.SetInt(PhaseKey, 4);
                earliestCheck = EditorApplication.timeSinceStartup + 0.8d;
            }
            else if (phase == 4)
            {
                if (movementUnit == null || FlatDistance(movementStart, movementUnit.transform.position) < 0.5f)
                    throw new InvalidOperationException("Movement skill succeeded but did not move its character.");
                Debug.Log("SANDBOX_SKILL_QUEUE_PLAYMODE_OK Three FIFO icon cards, cost spending, fade rotation, Inspector-authored stats, moving enemy formation, and movement skill passed.");
                SessionState.SetBool(SuccessKey, true);
                SessionState.SetInt(PhaseKey, 99);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(SuccessKey, false);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
        }
    }

    private static void UseFirstCard(BattleDirector director, SkillCardUI card)
    {
        string displayName = card.DisplayName;
        card.Button.onClick.Invoke();
        if (displayName == "HEAL" || displayName == "BUFF")
        {
            CombatUnit ally = ActiveUnits(CombatUnit.CombatTeam.Player).First();
            if (displayName == "HEAL") ally.TakeDamage(30f, ally.transform.position + Vector3.right);
            if (!director.ResolveTargetAt(ally.transform.position))
                throw new InvalidOperationException(displayName + " basic skill did not resolve on an ally.");
            return;
        }
        if (displayName == "AIRSTRIKE")
        {
            CombatUnit enemy = ActiveUnits(CombatUnit.CombatTeam.Enemy).FirstOrDefault();
            if (enemy == null || !director.ResolveTargetAt(enemy.transform.position))
                throw new InvalidOperationException("AIRSTRIKE basic skill did not resolve on the battlefield.");
            return;
        }
        if (displayName == "COVER")
        {
            CombatUnit ally = ActiveUnits(CombatUnit.CombatTeam.Player).First();
            if (!director.ResolveTargetAt(ally.transform.position + Vector3.right * 2f))
                throw new InvalidOperationException("COVER basic skill did not resolve on the battlefield.");
            return;
        }

        AutoCombatAI owner = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None)
            .FirstOrDefault(ai => ai.Unit.team == CombatUnit.CombatTeam.Player &&
                string.Equals(ai.name, displayName, StringComparison.OrdinalIgnoreCase));
        if (owner == null) throw new InvalidOperationException("Could not resolve skill-card owner " + displayName + ".");

        CombatUnit target;
        if (owner.characterSkillKind == AutoCombatAI.CharacterSkillKind.Heal)
        {
            target = owner.Unit;
            target.TakeDamage(30f, target.transform.position + Vector3.right);
        }
        else if (owner.characterSkillKind == AutoCombatAI.CharacterSkillKind.PowerUp ||
                 owner.characterSkillKind == AutoCombatAI.CharacterSkillKind.Defensive)
            target = owner.Unit;
        else
        {
            target = ActiveUnits(CombatUnit.CombatTeam.Enemy).FirstOrDefault();
            if (target == null) throw new InvalidOperationException("No enemy was available for character skill validation.");
            // Runtime-only test placement isolates card activation from map distance.
            // The formation movement check has already completed before this point.
            target.transform.position = owner.transform.position + owner.transform.forward *
                Mathf.Min(2f, owner.skillRange * 0.5f);
        }
        if (!director.ResolveTargetAt(target.transform.position))
            throw new InvalidOperationException(displayName + " skill card did not resolve successfully.");
    }

    private static bool TryReachableOffset(Vector3 startPosition, out Vector3 destination)
    {
        if (!NavMesh.SamplePosition(startPosition, out NavMeshHit start, 5f, NavMesh.AllAreas))
        {
            destination = default;
            return false;
        }
        Vector3[] directions =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1f, 0f, 1f).normalized, new Vector3(-1f, 0f, 1f).normalized,
            new Vector3(1f, 0f, -1f).normalized, new Vector3(-1f, 0f, -1f).normalized
        };
        foreach (float radius in new[] { 2f, 4f, 6f })
        foreach (Vector3 direction in directions)
        {
            if (!NavMesh.SamplePosition(start.position + direction * radius, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2 &&
                FlatDistance(start.position, hit.position) > 0.75f)
            {
                destination = hit.position;
                return true;
            }
        }
        destination = default;
        return false;
    }

    private static CombatUnit[] ActiveUnits(CombatUnit.CombatTeam team) =>
        Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
            .Where(unit => unit.team == team && !unit.IsDead && unit.gameObject.activeInHierarchy).ToArray();

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
