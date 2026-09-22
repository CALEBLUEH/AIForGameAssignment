using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SandboxRosterPlayModeSmoke
{
    private const string PreparationScenePath = "Assets/Scenes/SandBox_Preparation.unity";
    private const string RunningKey = "AIForGame.SandboxRosterSmoke.Running";
    private const string PhaseKey = "AIForGame.SandboxRosterSmoke.Phase";
    private const string SuccessKey = "AIForGame.SandboxRosterSmoke.Success";
    private const string OriginalSelectionKey = "AIForGame.SandboxRosterSmoke.OriginalSelection";
    private static double earliestCheck;
    private static double deadline;

    static SandboxRosterPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Sandbox Roster Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before starting the sandbox-roster smoke test.");

        SessionState.SetString(OriginalSelectionKey, string.Join(",", GameProgress.GetSelectedCharacters()));
        EditorSceneManager.OpenScene(PreparationScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetBool(SuccessKey, false);
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
            bool success = SessionState.GetBool(SuccessKey, false);
            CleanupSession();
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            return;
        }

        try
        {
            if (deadline <= 0d)
            {
                deadline = EditorApplication.timeSinceStartup + 20d;
                earliestCheck = EditorApplication.timeSinceStartup + 1d;
            }
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Sandbox roster smoke test timed out in phase " + phase + ".");
            if (EditorApplication.timeSinceStartup < earliestCheck) return;

            if (phase == 0) ValidatePreparationAndEnterBattle();
            else if (phase == 1) ValidateGameplay();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreSelection();
            Finish(false);
        }
    }

    private static void ValidatePreparationAndEnterBattle()
    {
        if (SceneManager.GetActiveScene().name != "SandBox_Preparation") return;
        PreparationMenuController controller = Object.FindFirstObjectByType<PreparationMenuController>();
        PreparationCharacterDisplay[] displays = Object.FindObjectsByType<PreparationCharacterDisplay>(FindObjectsSortMode.None)
            .OrderBy(display => display.RosterIndex).ToArray();
        if (controller == null || displays.Length != 5)
            throw new InvalidOperationException("Preparation scene did not start with five wired displays.");
        if (displays.Any(display => display.CharacterAnimator == null || display.ModelPivot == null))
            throw new InvalidOperationException("A preparation character is missing its animator or rotation pivot.");
        if (displays.Any(display => !display.CharacterAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle")))
            throw new InvalidOperationException("Every preparation character must loop the Idle state.");

        Quaternion before = displays[0].ModelPivot.localRotation;
        displays[0].RotateModel(12f);
        if (Quaternion.Angle(before, displays[0].ModelPivot.localRotation) < 5f)
            throw new InvalidOperationException("Preparation drag rotation did not rotate the model pivot.");

        int selectedIndex = Array.FindIndex(displays, display => display.IsSelected);
        int unselectedIndex = Array.FindIndex(displays, display => !display.IsSelected);
        if (selectedIndex < 0 || unselectedIndex < 0)
            throw new InvalidOperationException("The preparation roster must begin with four selected and one unselected character.");
        controller.ToggleCharacterFromPodium(selectedIndex);
        controller.ToggleCharacterFromPodium(unselectedIndex);
        if (displays[selectedIndex].IsSelected || !displays[unselectedIndex].IsSelected || !controller.battleButton.interactable)
            throw new InvalidOperationException("Podium selection did not update selection state and Battle availability.");

        SessionState.SetInt(PhaseKey, 1);
        earliestCheck = EditorApplication.timeSinceStartup + 1.5d;
        deadline = EditorApplication.timeSinceStartup + 20d;
        controller.battleButton.onClick.Invoke();
    }

    private static void ValidateGameplay()
    {
        if (SceneManager.GetActiveScene().name != "Sandbox_Gameplay") return;
        BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
        if (director == null || !director.IsPlaying) return;

        CombatUnit[] players = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CombatUnit>(true))
            .Where(unit => unit.team == CombatUnit.CombatTeam.Player)
            .ToArray();
        if (players.Length != 5 || players.Count(unit => unit.gameObject.activeInHierarchy) != 4)
            throw new InvalidOperationException("Gameplay must contain five roster slots with exactly four selected units active.");

        foreach (CombatUnit player in players)
        {
            bool shouldBeActive = GameProgress.IsCharacterSelected(player.name);
            if (player.gameObject.activeInHierarchy != shouldBeActive)
                throw new InvalidOperationException(player.name + " does not match the preparation selection.");
            if (player.GetComponent<CombatAnimationDriver>() == null)
                throw new InvalidOperationException(player.name + " is missing CombatAnimationDriver.");
            MeshRenderer capsule = player.GetComponent<MeshRenderer>();
            if (capsule != null && capsule.enabled)
                throw new InvalidOperationException(player.name + " still displays its capsule mesh.");
            Transform model = player.transform.Find("Character Model (" + player.name + ")");
            if (model == null || model.GetComponentInChildren<Animator>(true) == null)
                throw new InvalidOperationException(player.name + " is missing its animated character model.");
        }

        RestoreSelection();
        Debug.Log("SANDBOX_ROSTER_PLAYMODE_OK: five idle preparation models rotate, podium selection swaps one member, Battle loads gameplay, and exactly four selected animated models deploy.");
        Finish(true);
    }

    private static void RestoreSelection()
    {
        string original = SessionState.GetString(OriginalSelectionKey, string.Empty);
        if (!string.IsNullOrEmpty(original)) GameProgress.SetSelectedCharacters(original.Split(','));
    }

    private static void Finish(bool success)
    {
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }

    private static void CleanupSession()
    {
        SessionState.EraseBool(RunningKey);
        SessionState.EraseInt(PhaseKey);
        SessionState.EraseBool(SuccessKey);
        SessionState.EraseString(OriginalSelectionKey);
    }
}
