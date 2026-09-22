using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SandboxTacticsPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.SandboxTacticsSmoke.Running";
    private const string PhaseKey = "AIForGame.SandboxTacticsSmoke.Phase";
    private const string OriginalSelectionKey = "AIForGame.SandboxTacticsSmoke.Selection";
    private static Vector3 momoiStart;
    private static double checkAt;

    static SandboxTacticsPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Sandbox Tactics Play Mode Smoke")]
    public static void Run()
    {
        SessionState.SetString(OriginalSelectionKey, string.Join(",", GameProgress.GetSelectedCharacters()));
        GameProgress.SetSelectedCharacters(new[] { "Yuuka", "Ayane", "Mika", "Momoi" });
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
            RestoreSelection();
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseString(OriginalSelectionKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        try
        {
            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (director == null || !director.IsPlaying) return;
            int phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 0) BeginValidation(director);
            else if (phase == 1 && EditorApplication.timeSinceStartup >= checkAt) FinishValidation();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreSelection();
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void BeginValidation(BattleDirector director)
    {
        AutoCombatAI[] allAI = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None);
        AutoCombatAI[] players = allAI.Where(ai => ai.Unit.team == CombatUnit.CombatTeam.Player && ai.gameObject.activeInHierarchy).ToArray();
        AutoCombatAI[] enemies = allAI.Where(ai => ai.Unit.team == CombatUnit.CombatTeam.Enemy && ai.gameObject.activeInHierarchy).ToArray();
        AutoCombatAI momoi = players.FirstOrDefault(ai => ai.name == "Momoi");
        AutoCombatAI yuuka = players.FirstOrDefault(ai => ai.name == "Yuuka");
        if (players.Length != 4 || enemies.Length == 0 || momoi == null || yuuka == null)
            throw new InvalidOperationException("Expected four selected players including Momoi/Yuuka and an active enemy wave.");
        if (players.Any(ai => ai.separationDistance < 4f || ai.separationStrength < 0.99f))
            throw new InvalidOperationException("Player separation was not widened.");
        if (enemies.Any(ai => ai.separationDistance < 1.9f || ai.separationStrength < 0.99f))
            throw new InvalidOperationException("Enemy separation was not widened.");

        EnemyFormationMember[] formationMembers = enemies.Select(enemy => enemy.GetComponent<EnemyFormationMember>()).ToArray();
        if (formationMembers.Any(member => member == null))
            throw new InvalidOperationException("Every spawned enemy must belong to its wave formation.");
        int distinctDestinations = formationMembers.Skip(1)
            .Select(member => member.TryGetDestination(players[0].transform.position, out Vector3 value) ? value : Vector3.positiveInfinity)
            .Distinct().Count();
        if (formationMembers.Length > 1 && distinctDestinations < formationMembers.Length - 1)
            throw new InvalidOperationException("Enemy formation followers did not receive distinct slots.");

        TacticalCoverObstacle[] covers = Object.FindObjectsByType<TacticalCoverObstacle>(FindObjectsSortMode.None);
        CoverPoint[] points = Object.FindObjectsByType<CoverPoint>(FindObjectsSortMode.None);
        if (covers.Length < 2 || points.Length < 4 || covers.Any(cover => cover.GetComponent<DestructibleCover>() == null ||
            cover.Capacity != 1 || cover.GetComponentInChildren<CoverHealthHUD>(true) == null))
            throw new InvalidOperationException("Every gameplay obstacle needs capacity-one cover, universal health and a health HUD.");
        TacticalCoverObstacle testObstacle = covers.FirstOrDefault(cover => cover.GetComponentsInChildren<CoverPoint>(true).Length >= 2);
        CoverPoint[] testPoints = testObstacle == null ? Array.Empty<CoverPoint>() :
            testObstacle.GetComponentsInChildren<CoverPoint>(true);
        CoverPoint testPoint = testPoints.FirstOrDefault(point => point.CanBeUsedBy(yuuka.Unit) && point.CanBeUsedBy(enemies[0].Unit));
        if (testPoint == null || !testPoint.TryReserve(yuuka.Unit))
            throw new InvalidOperationException("A player could not reserve an available obstacle.");
        CoverPoint secondPoint = testPoints.FirstOrDefault(point => point != testPoint);
        if (secondPoint != null && secondPoint.TryReserve(enemies[0].Unit))
            throw new InvalidOperationException("Two units reserved different points on the same capacity-one obstacle.");
        testPoint.Release(yuuka.Unit);
        if (!testPoint.TryReserve(enemies[0].Unit))
            throw new InvalidOperationException("An enemy could not reserve the same available obstacle.");
        testPoint.Abandon(enemies[0].Unit);
        if (testPoint.CanBeUsedBy(enemies[0].Unit) || !testPoint.CanBeUsedBy(yuuka.Unit))
            throw new InvalidOperationException("Obstacle abandonment must be shared within one team without blocking the other team.");

        string[] missingUI =
        {
            director.battlePanel == null ? "battlePanel" : !director.battlePanel.activeInHierarchy ? "battlePanelInactive" : null,
            director.moveButton == null ? "moveButton" : null,
            director.skillButton == null ? "skillButton" : null,
            director.healButton == null ? "healButton" : null,
            director.coverButton == null ? "coverButton" : null,
            director.pauseButton == null ? "pauseButton" : null,
            director.speedButton == null ? "speedButton" : null,
            director.autoButton == null ? "autoButton" : null,
            director.manaSlider == null ? "manaSlider" : null,
            director.targetingFeedback == null ? "targetingFeedback" : null
        };
        missingUI = missingUI.Where(value => value != null).ToArray();
        if (missingUI.Length > 0)
            throw new InvalidOperationException("Gameplay skill or battle UI incomplete: " + string.Join(",", missingUI));

        if (director.AutoEnabled) director.autoButton.onClick.Invoke();
        momoiStart = momoi.transform.position;
        // Auto may have used a skill in the first frame. Disable it, then allow the longest
        // configured cooldown to expire before testing the actual UI button callbacks.
        checkAt = EditorApplication.timeSinceStartup + 11d;
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void FinishValidation()
    {
        AutoCombatAI[] players = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None)
            .Where(ai => ai.Unit.team == CombatUnit.CombatTeam.Player && ai.gameObject.activeInHierarchy)
            .OrderBy(ai => ai.rosterOrder).ToArray();
        AutoCombatAI momoi = players.FirstOrDefault(ai => ai.name == "Momoi");
        AutoCombatAI yuuka = players.FirstOrDefault(ai => ai.name == "Yuuka");
        BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
        if (momoi == null || yuuka == null || director == null)
            throw new InvalidOperationException("The selected squad or BattleDirector disappeared during validation.");
        float moved = Vector3.Distance(momoiStart, momoi.transform.position);
        if (moved < 1f || momoi.CurrentState == "No path")
            throw new InvalidOperationException($"Momoi still failed navigation: moved={moved:F3}, state={momoi.CurrentState}.");

        int yuukaSlot = Array.IndexOf(players, yuuka);
        director.squadButtons[yuukaSlot].onClick.Invoke();
        float movementBefore = yuuka.Unit.MovementPoints;
        if (!TryFindMovementDestination(yuuka.transform.position, yuuka.movementRange, out Vector3 moveDestination))
            throw new InvalidOperationException("No reachable nearby NavMesh point was available for the movement-skill UI test.");
        director.moveButton.onClick.Invoke();
        if (!director.ResolveTargetAt(moveDestination) || yuuka.Unit.MovementPoints >= movementBefore)
            throw new InvalidOperationException($"The movement-skill UI did not activate Yuuka's movement skill; cooldown={yuuka.MovementCooldownRemaining:F2}, points={yuuka.Unit.MovementPoints:F1}.");

        float manaBefore = director.UniversalPoints;
        bool skillUsed;
        if (director.UsesSkillQueue)
        {
            // The dedicated queue smoke validates card click/rotation. This broader tactics
            // smoke invokes Yuuka directly because her randomized card may not be visible.
            skillUsed = yuuka.TryCharacterSkill(null);
        }
        else
        {
            director.skillButton.onClick.Invoke();
            skillUsed = director.ResolveTargetAt(yuuka.transform.position);
        }
        if (!skillUsed || director.UniversalPoints >= manaBefore)
            throw new InvalidOperationException($"The character-skill UI did not activate Yuuka's defensive skill; cooldown={yuuka.CharacterCooldownRemaining:F2}, mana={director.UniversalPoints:F1}.");

        Debug.Log($"SANDBOX_TACTICS_PLAYMODE_OK Momoi moved={moved:F3}; all obstacles have capacity-one shared reservation, health/HUD and team-shared abandonment; enemy formation slots, wider separation, and movement/character-skill UI passed.");
        RestoreSelection();
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }

    private static bool TryFindMovementDestination(Vector3 origin, float range, out Vector3 destination)
    {
        Vector3[] directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 5f, NavMesh.AllAreas))
        {
            destination = default;
            return false;
        }
        foreach (Vector3 direction in directions)
        {
            if (!NavMesh.SamplePosition(origin + direction * Mathf.Min(3f, range * 0.6f),
                    out NavMeshHit end, 3f, NavMesh.AllAreas)) continue;
            var path = new NavMeshPath();
            if (Vector3.Distance(start.position, end.position) > 1f &&
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                destination = end.position;
                return true;
            }
        }
        destination = default;
        return false;
    }

    private static void RestoreSelection()
    {
        string selection = SessionState.GetString(OriginalSelectionKey, string.Empty);
        if (!string.IsNullOrEmpty(selection)) GameProgress.SetSelectedCharacters(selection.Split(','));
    }
}
