using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterSkillPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.CharacterSkillSmoke.Running";
    private const string PhaseKey = "AIForGame.CharacterSkillSmoke.Phase";
    private const string SelectionKey = "AIForGame.CharacterSkillSmoke.Selection";
    private static double checkAt;
    private static CombatUnit coneTarget;
    private static float coneHealthBefore;
    private static CombatUnit mikaTarget, healedAlly;
    private static float mikaHealthBefore, injuredHealth, yuukaHealth, yuukaShieldBeforeDamage;
    private static AutoCombatAI mika, ayane, yuuka, momoi, hina;
    private static readonly Dictionary<AutoCombatAI, Vector3> CastStarts = new Dictionary<AutoCombatAI, Vector3>();

    static CharacterSkillPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Character Skill Play Mode Smoke")]
    public static void Run()
    {
        SessionState.SetString(SelectionKey, string.Join(",", GameProgress.GetSelectedCharacters()));
        GameProgress.SetSelectedCharacters(new[] { "Yuuka", "Ayane", "Mika", "Momoi" });
        EditorSceneManager.OpenScene(ScenePath);
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
            SessionState.EraseString(SelectionKey);
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
            RestoreSelection();
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void Begin(BattleDirector director)
    {
        AutoCombatAI[] all = Object.FindObjectsByType<AutoCombatAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        AutoCombatAI[] players = all.Where(ai => ai.GetComponent<CombatUnit>().team == CombatUnit.CombatTeam.Player).ToArray();
        AutoCombatAI[] enemies = all.Where(ai => ai.gameObject.activeInHierarchy && ai.GetComponent<CombatUnit>().team == CombatUnit.CombatTeam.Enemy).ToArray();
        if (players.Length < 5 || enemies.Length < 2) throw new InvalidOperationException("Expected five player characters and at least two enemies.");
        foreach (AutoCombatAI ai in all) ai.enabled = false;
        foreach (AutoCombatAI playerAI in players)
        {
            playerAI.characterSkillCost = 1f;
            playerAI.characterSkillCooldown = 0f;
            playerAI.skillWindup = 0.1f;
            playerAI.skillRecovery = 0.1f;
            playerAI.skillProjectileSpeed = 200f;
            playerAI.skillDropDuration = 0.15f;
        }

        SkillCardUI characterCard = Object.FindObjectsByType<SkillCardUI>(FindObjectsSortMode.None)
            .FirstOrDefault(card => card.gameObject.activeInHierarchy &&
                players.Any(ai => string.Equals(ai.name, card.DisplayName, StringComparison.OrdinalIgnoreCase)));
        if (characterCard == null) throw new InvalidOperationException("No visible character card was available for cancel testing.");
        characterCard.Button.onClick.Invoke();
        if (!Mathf.Approximately(Time.timeScale, 0.2f)) throw new InvalidOperationException("Selecting a skill did not slow time to 20%.");
        SkillTargetingOverlayUI overlay = Resources.FindObjectsOfTypeAll<SkillTargetingOverlayUI>().FirstOrDefault();
        if (overlay == null || !overlay.gameObject.activeSelf) throw new InvalidOperationException("Skill dimmer/prompt overlay did not open.");
        characterCard.Button.onClick.Invoke();
        if (!Mathf.Approximately(Time.timeScale, 1f) || overlay.gameObject.activeSelf)
            throw new InvalidOperationException("Clicking the same card did not cancel targeting without casting.");
        if (!Mathf.Approximately(SkillCinematicPlayer.ResolvePlaybackSpeed(director, 0.2f), director.BattleSpeed))
            throw new InvalidOperationException("Skill video speed still inherited the 20% targeting slowdown.");
        director.speedButton.onClick.Invoke();
        if (!Mathf.Approximately(director.BattleSpeed, 2f) ||
            !Mathf.Approximately(SkillCinematicPlayer.ResolvePlaybackSpeed(director, 0.2f), 2f))
            throw new InvalidOperationException("Skill video did not follow the 2x battle speed setting.");
        director.speedButton.onClick.Invoke();

        mika = Role(players, AutoCombatAI.CombatRole.MikaSingleTarget);
        ayane = Role(players, AutoCombatAI.CombatRole.AyaneHealer);
        yuuka = Role(players, AutoCombatAI.CombatRole.YuukaTank);
        momoi = Role(players, AutoCombatAI.CombatRole.MomoiLowCostAOE);
        hina = Role(players, AutoCombatAI.CombatRole.HinaHighCostAOE);
        foreach (AutoCombatAI caster in new[] { mika, ayane, yuuka, momoi, hina }) CastStarts[caster] = caster.transform.position;
        CombatUnit enemy = enemies[0].Unit;
        enemy.ConfigureSpawn(5000f, enemy.attackPower, enemy.defense, false);
        enemies[1].Unit.ConfigureSpawn(5000f, enemies[1].Unit.attackPower, enemies[1].Unit.defense, false);
        mika.skillRange = 500f;
        mikaTarget = enemy;
        mikaHealthBefore = enemy.CurrentHealth;
        if (!mika.TryCharacterSkillAt(enemy.transform.position, enemy) || enemy.CurrentHealth != mikaHealthBefore)
            throw new InvalidOperationException("Mika did not enter a delayed cast cleanly.");

        healedAlly = momoi.Unit;
        healedAlly.TakeDamage(30f, enemy.transform.position);
        injuredHealth = healedAlly.CurrentHealth;
        ayane.skillRange = 500f;
        ayane.aoeRadius = 8f;
        if (!ayane.TryCharacterSkillAt(healedAlly.transform.position) || healedAlly.CurrentHealth != injuredHealth)
            throw new InvalidOperationException("Ayane did not wait before applying her heal.");

        yuukaHealth = yuuka.Unit.CurrentHealth;
        if (!yuuka.TryCharacterSkillAt(yuuka.transform.position) || yuuka.Unit.ShieldPoints > 0f)
            throw new InvalidOperationException("Yuuka did not wait before applying her shield.");

        coneTarget = enemies[1].Unit;
        coneHealthBefore = coneTarget.CurrentHealth;
        momoi.skillRange = hina.skillRange = 500f;
        momoi.coneAngle = hina.coneAngle = 170f;
        momoi.skillPowerMultiplier = hina.skillPowerMultiplier = 0.6f;
        momoi.damageTickDuration = hina.damageTickDuration = 0.3f;
        Vector3 conePoint = coneTarget.transform.position;
        if (!momoi.TryCharacterSkillAt(conePoint) || !hina.TryCharacterSkillAt(conePoint))
            throw new InvalidOperationException("Momoi/Hina cone skills did not start.");
        foreach (AutoCombatAI caster in new[] { mika, ayane, yuuka, momoi, hina })
            if (!caster.IsCastingSkill) throw new InvalidOperationException(caster.name + " was not locked in its cast state.");
        checkAt = EditorApplication.timeSinceStartup + 1.2d;
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void Finish()
    {
        if (mikaTarget == null || mikaTarget.CurrentHealth >= mikaHealthBefore)
            throw new InvalidOperationException("Mika's delayed projectile did not damage its target.");
        if (healedAlly == null || healedAlly.CurrentHealth <= injuredHealth)
            throw new InvalidOperationException("Ayane's dropped med kit did not heal its area.");
        if (yuuka == null || yuuka.Unit.ShieldPoints <= 0f)
            throw new InvalidOperationException("Yuuka's delayed shield was not applied.");
        if (yuuka.GetComponentInChildren<SkillShieldVisual>(true) == null)
            throw new InvalidOperationException("Yuuka's visible shield sphere was not created.");
        yuukaShieldBeforeDamage = yuuka.Unit.ShieldPoints;
        yuuka.Unit.TakeDamage(20f, mikaTarget.transform.position);
        if (yuuka.Unit.CurrentHealth < yuukaHealth || yuuka.Unit.ShieldPoints >= yuukaShieldBeforeDamage)
            throw new InvalidOperationException("Yuuka's shield did not absorb incoming damage.");
        if (coneTarget == null || coneTarget.CurrentHealth >= coneHealthBefore)
            throw new InvalidOperationException("Momoi/Hina multi-tick cone damage did not affect an enemy.");
        foreach (KeyValuePair<AutoCombatAI, Vector3> cast in CastStarts)
            if (cast.Key == null || Vector3.Distance(cast.Key.transform.position, cast.Value) > 0.02f || cast.Key.IsCastingSkill)
                throw new InvalidOperationException((cast.Key == null ? "A caster" : cast.Key.name) + " moved during or remained locked after casting.");
        Debug.Log("CHARACTER_SKILL_PLAYMODE_OK targeting slow/dim/cancel, delayed stationary casts, Mika projectile, Ayane med-kit heal, Yuuka shield, and Momoi/Hina barrages passed.");
        RestoreSelection();
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }

    private static AutoCombatAI Role(IEnumerable<AutoCombatAI> players, AutoCombatAI.CombatRole role)
    {
        AutoCombatAI result = players.FirstOrDefault(ai => ai.role == role);
        if (result == null) throw new InvalidOperationException("Missing player role " + role);
        if (!result.gameObject.activeSelf) result.gameObject.SetActive(true);
        return result;
    }

    private static void RestoreSelection()
    {
        string selection = SessionState.GetString(SelectionKey, string.Empty);
        if (!string.IsNullOrEmpty(selection)) GameProgress.SetSelectedCharacters(selection.Split(','));
    }
}
