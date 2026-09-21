using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BattleDirector : MonoBehaviour
{
    public static BattleDirector Instance { get; private set; }
    [Header("Canvas references")]
    public GameObject setupPanel, battlePanel, resultPanel;
    public Text setupText, statusText, squadText, resultText, timerText, enemyText, energyText;
    public Image[] energySegments;
    public Slider manaSlider, bossHealthSlider;
    public GameObject bossHealthPanel;
    public Text bossNameText, moveCooldownText, skillCooldownText;
    public Image moveCooldownOverlay, skillCooldownOverlay;
    public SkillTargetingFeedback targetingFeedback;
    public Button[] setupButtons, squadButtons;
    public Button leaderButton, startButton, moveButton, skillButton, healButton, coverButton, restartButton;
    public Button pauseButton, speedButton, autoButton;
    [Header("Stage")]
    public float universalCapacity = 100f, universalRegeneration = 8f;
    public float healCost = 25f, coverCost = 35f;
    public int waveTwoCount = 4;
    public float timedEnemyDelay = 18f;
    [SerializeField] private float universalPoints;
    [SerializeField] private bool isPlaying;
    private readonly List<AutoCombatAI> squad = new List<AutoCombatAI>();
    private readonly List<CombatUnit> initialEnemies = new List<CombatUnit>();
    private bool[] selected;
    private int leaderIndex, selectedIndex, wave = 1;
    private float elapsed;
    private bool finished;
    private bool paused;
    private int speedLevel = 1;
    [SerializeField] private bool autoEnabled = true;
    private GameObject enemyTemplate;
    private CombatUnit boss;
    private float nextPhaseTime;
    private enum StagePhase { WaveOne, MovingToWaveTwo, WaveTwo, MovingToBoss, Boss }
    private StagePhase phase = StagePhase.WaveOne;
    private enum TargetMode { None, Move, Skill }
    private TargetMode targetMode;
    public bool IsPlaying => isPlaying;
    public float UniversalPoints => universalPoints;
    public bool AutoEnabled => autoEnabled;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        universalPoints = universalCapacity;
        foreach (var ai in FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None))
        {
            var combatUnit = ai.GetComponent<CombatUnit>();
            if (combatUnit.team == CombatUnit.CombatTeam.Player) squad.Add(ai);
            else initialEnemies.Add(combatUnit);
        }
        squad.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        selected = new bool[squad.Count];
        for (int i = 0; i < selected.Length; i++) selected[i] = true;
        if (initialEnemies.Count > 0)
        {
            enemyTemplate = Instantiate(initialEnemies[0].gameObject, transform);
            enemyTemplate.name = "Enemy Spawn Template";
            enemyTemplate.SetActive(false);
        }
        BindButtons();
        setupPanel.SetActive(true);
        battlePanel.SetActive(false);
        resultPanel.SetActive(false);
        RefreshSetup();
    }

    private void BindButtons()
    {
        for (int i = 0; i < setupButtons.Length; i++)
        {
            int index = i;
            setupButtons[i].onClick.AddListener(() => ToggleMember(index));
            squadButtons[i].onClick.AddListener(() => SelectMember(index));
        }
        leaderButton.onClick.AddListener(CycleLeader);
        startButton.onClick.AddListener(StartBattle);
        moveButton.onClick.AddListener(() => BeginTargeting(TargetMode.Move));
        skillButton.onClick.AddListener(UseCharacterSkill);
        healButton.onClick.AddListener(UseHeal);
        coverButton.onClick.AddListener(UseCover);
        if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
        if (speedButton != null) speedButton.onClick.AddListener(CycleSpeed);
        if (autoButton != null) autoButton.onClick.AddListener(ToggleAuto);
        restartButton.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex));
    }

    private void ToggleMember(int index)
    {
        if (index >= selected.Length) return;
        int count = 0;
        foreach (bool value in selected) if (value) count++;
        if (selected[index] && count <= 1) return;
        selected[index] = !selected[index];
        if (!selected[leaderIndex]) leaderIndex = System.Array.FindIndex(selected, x => x);
        RefreshSetup();
    }

    private void CycleLeader()
    {
        for (int offset = 1; offset <= selected.Length; offset++)
        {
            int index = (leaderIndex + offset) % selected.Length;
            if (!selected[index]) continue;
            leaderIndex = index;
            break;
        }
        RefreshSetup();
    }

    private void RefreshSetup()
    {
        setupText.text = "ASSAULT  •  Pick up to four units and a leader\n" +
            "The squad attacks automatically. Use skills to change the fight.";
        leaderButton.GetComponentInChildren<Text>().text = "Leader: " + squad[leaderIndex].name;
        for (int i = 0; i < setupButtons.Length; i++)
        {
            setupButtons[i].gameObject.SetActive(i < squad.Count);
            if (i >= squad.Count) continue;
            setupButtons[i].GetComponentInChildren<Text>().text =
                (selected[i] ? "[IN] " : "[OUT] ") + squad[i].name;
        }
    }

    private void StartBattle()
    {
        for (int i = 0; i < squad.Count; i++)
        {
            if (!selected[i]) { squad[i].gameObject.SetActive(false); continue; }
            var member = squad[i].GetComponent<SquadMember>();
            if (i == leaderIndex)
            {
                if (member != null) member.leader = null;
            }
            else if (member != null) member.leader = squad[leaderIndex].transform;
        }
        selectedIndex = leaderIndex;
        phase = StagePhase.WaveOne;
        wave = 1;
        isPlaying = true;
        setupPanel.SetActive(false);
        battlePanel.SetActive(true);
        RefreshBattle();
    }

    private void Update()
    {
        if (!isPlaying) return;
        elapsed += Time.deltaTime;
        universalPoints = Mathf.Min(universalCapacity, universalPoints + universalRegeneration * Time.deltaTime);
        int enemies = CountAlive(CombatUnit.CombatTeam.Enemy);
        int allies = CountAlive(CombatUnit.CombatTeam.Player);
        if (allies == 0) { Finish(false); return; }
        if (phase == StagePhase.WaveOne && enemies == 0)
        {
            phase = StagePhase.MovingToWaveTwo;
            nextPhaseTime = Time.time + 2f;
        }
        else if (phase == StagePhase.MovingToWaveTwo && Time.time >= nextPhaseTime)
        {
            wave = 2;
            for (int i = 0; i < waveTwoCount; i++)
                SpawnEnemy(new Vector3(25f + i * 2.2f, 1f, (i % 2 == 0 ? -3f : 3f)));
            phase = StagePhase.WaveTwo;
        }
        else if (phase == StagePhase.WaveTwo && enemies == 0)
        {
            phase = StagePhase.MovingToBoss;
            nextPhaseTime = Time.time + 2f;
        }
        else if (phase == StagePhase.MovingToBoss && Time.time >= nextPhaseTime)
        {
            wave = 3;
            SpawnBoss();
            phase = StagePhase.Boss;
        }
        else if (phase == StagePhase.Boss && (boss == null || boss.IsDead))
        {
            Finish(true);
            return;
        }
        UpdateTargetPointer();
        if (Input.GetMouseButtonDown(0) && targetMode != TargetMode.None &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            HandleWorldClick();
        if (targetMode != TargetMode.None && Input.GetKeyDown(KeyCode.Escape)) ExitTargeting();
        RefreshBattle();
    }

    private int CountAlive(CombatUnit.CombatTeam team)
    {
        int count = 0;
        foreach (var unit in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (!unit.IsDead && unit.team == team) count++;
        return count;
    }

    private void SpawnEnemy(Vector3 position)
    {
        if (enemyTemplate == null) return;
        var clone = Instantiate(enemyTemplate, position, Quaternion.identity);
        clone.name = "Wave " + wave + " Enemy";
        clone.transform.SetParent(null);
        clone.SetActive(true);
    }

    private void SpawnBoss()
    {
        if (enemyTemplate == null) return;
        GameObject clone = Instantiate(enemyTemplate, new Vector3(57f, 1f, 0f), Quaternion.identity);
        clone.name = "Section Boss";
        clone.transform.SetParent(null);
        clone.transform.localScale = Vector3.one * 2.2f;
        clone.SetActive(true);
        boss = clone.GetComponent<CombatUnit>();
        boss.ConfigureSpawn(650f, 42f, 14f, true);
        boss.attackRange = 6.5f;
        boss.attackSpeed = 0.7f;
    }

    private void SelectMember(int index)
    {
        if (index < squad.Count && squad[index] != null && squad[index].gameObject.activeInHierarchy)
        { selectedIndex = index; ExitTargeting(); }
    }

    private AutoCombatAI Selected => selectedIndex < squad.Count ? squad[selectedIndex] : null;

    private void UseCharacterSkill()
    {
        if (Selected == null || Selected.Unit.IsDead) return;
        BeginTargeting(TargetMode.Skill);
    }

    private void UseHeal()
    {
        ExitTargeting();
        if (Selected == null || Selected.Unit.IsDead || !TrySpendUniversal(healCost)) return;
        Selected.Unit.Heal(35f);
        if (targetingFeedback != null)
            targetingFeedback.PlayFeedback(Selected.transform.position, new Color(0.2f, 1f, 0.55f));
    }

    private void UseCover()
    {
        ExitTargeting();
        if (Selected == null || Selected.Unit.IsDead || !TrySpendUniversal(coverCost)) return;
        Vector3 forward = Selected.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.right;
        Vector3 position = Selected.transform.position + forward.normalized * 2f;
        position.y = 0f;
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Placed Cover";
        block.transform.position = position + Vector3.up * 0.9f;
        block.transform.rotation = Quaternion.LookRotation(forward);
        block.transform.localScale = new Vector3(2.5f, 1.8f, 0.7f);
        block.layer = 8;
        var obstacle = block.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        if (targetingFeedback != null)
            targetingFeedback.PlayFeedback(position, new Color(0.25f, 0.75f, 1f));
    }

    private void HandleWorldClick()
    {
        if (Selected == null || Selected.Unit.IsDead) { ExitTargeting(); return; }
        if (Camera.main == null) { ExitTargeting(); return; }
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!TryGetGroundPoint(ray, out Vector3 pointer)) { ExitTargeting(); return; }
        ResolveTargetAt(pointer);
    }

    public bool ResolveTargetAt(Vector3 pointer)
    {
        if (targetMode == TargetMode.None || Selected == null || Selected.Unit.IsDead)
        {
            ExitTargeting();
            return false;
        }
        bool used = false;
        Vector3 feedbackPosition = pointer;
        if (targetMode == TargetMode.Move) used = Selected.TryMovementSkill(pointer);
        else if (targetMode == TargetMode.Skill)
        {
            CombatUnit target = Selected.characterSkillKind == AutoCombatAI.CharacterSkillKind.PowerUp
                ? null : FindSkillTarget(pointer);
            used = Selected.TryCharacterSkill(target);
            if (target != null) feedbackPosition = target.transform.position;
        }
        if (used && targetingFeedback != null)
            targetingFeedback.PlayFeedback(feedbackPosition, new Color(1f, 0.65f, 0.1f));
        ExitTargeting();
        return used;
    }

    private CombatUnit FindSkillTarget(Vector3 pointer)
    {
        CombatUnit best = null;
        float bestPointerDistance = float.PositiveInfinity;
        bool wantsAlly = Selected.characterSkillKind == AutoCombatAI.CharacterSkillKind.Heal;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate.IsDead || (candidate.team == Selected.Unit.team) != wantsAlly ||
                FlatDistance(Selected.transform.position, candidate.transform.position) > Selected.skillRange) continue;
            float pointerDistance = FlatDistance(pointer, candidate.transform.position);
            if (pointerDistance < bestPointerDistance)
            {
                best = candidate;
                bestPointerDistance = pointerDistance;
            }
        }
        return best;
    }

    private static bool TryGetGroundPoint(Ray ray, out Vector3 point)
    {
        var ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }
        point = default;
        return false;
    }

    private void BeginTargeting(TargetMode mode)
    {
        if (Selected == null || Selected.Unit.IsDead) return;
        if (mode == TargetMode.Move && Selected.MovementCooldownRemaining > 0f) return;
        if (mode == TargetMode.Skill && Selected.CharacterCooldownRemaining > 0f) return;
        targetMode = mode;
        Time.timeScale = 0.12f;
        if (targetingFeedback == null) return;
        if (mode == TargetMode.Move) targetingFeedback.ShowArrow(Selected.transform, Selected.movementRange);
        else
        {
            bool ranged = Selected.characterSkillKind != AutoCombatAI.CharacterSkillKind.PowerUp;
            targetingFeedback.ShowRange(Selected.transform, ranged ? Selected.skillRange : 2.2f, ranged);
        }
    }

    private void UpdateTargetPointer()
    {
        if (targetMode == TargetMode.None || targetingFeedback == null || Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (TryGetGroundPoint(ray, out Vector3 pointer)) targetingFeedback.SetPointer(pointer);
    }

    private void ExitTargeting()
    {
        targetMode = TargetMode.None;
        if (targetingFeedback != null) targetingFeedback.Hide();
        Time.timeScale = paused ? 0f : speedLevel;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public bool TrySpendUniversal(float amount)
    {
        if (universalPoints < amount) return false;
        universalPoints -= amount;
        return true;
    }
    public void RefundUniversal(float amount) =>
        universalPoints = Mathf.Min(universalCapacity, universalPoints + amount);

    private void RefreshBattle()
    {
        int enemies = CountAlive(CombatUnit.CombatTeam.Enemy);
        string section = phase == StagePhase.MovingToWaveTwo ? "ADVANCING TO SECTION 2" :
            phase == StagePhase.MovingToBoss ? "ADVANCING TO BOSS" :
            phase == StagePhase.Boss ? "BOSS SECTION" : "WAVE " + wave;
        statusText.text = "ASSAULT  •  " + section + "  •  Enemies " + enemies +
            "  •  Mana " + Mathf.FloorToInt(universalPoints) + "/" + universalCapacity;
        if (timerText != null) timerText.text = Mathf.FloorToInt(elapsed / 60f).ToString("00") + ":" +
            Mathf.FloorToInt(elapsed % 60f).ToString("00");
        if (enemyText != null) enemyText.text = enemies.ToString();
        if (energyText != null) energyText.text = Mathf.FloorToInt(universalPoints).ToString();
        if (manaSlider != null)
        {
            manaSlider.maxValue = universalCapacity;
            manaSlider.value = universalPoints;
        }
        if (energySegments != null)
        {
            float filled = universalPoints / Mathf.Max(1f, universalCapacity) * energySegments.Length;
            for (int i = 0; i < energySegments.Length; i++)
                energySegments[i].color = i < filled
                    ? new Color(0.12f, 0.78f, 0.96f, 1f)
                    : new Color(0.14f, 0.19f, 0.29f, 0.9f);
        }
        if (Selected != null)
        {
            var unit = Selected.Unit;
            squadText.text = Selected.name + "  HP " + Mathf.CeilToInt(unit.CurrentHealth) + "/" + unit.maxHealth +
                "  Move " + Mathf.FloorToInt(unit.MovementPoints) + "/" + unit.skillPointCapacity +
                "  •  " + Selected.CurrentState +
                (targetMode == TargetMode.None ? "" : "  •  " +
                    (targetMode == TargetMode.Move ? "Click a move position" :
                    Selected.characterSkillKind == AutoCombatAI.CharacterSkillKind.PowerUp ? "Click to activate" : "Click near a target"));
            RefreshCooldown(moveCooldownOverlay, moveCooldownText, moveButton,
                Selected.MovementCooldownRemaining, unit.MovementPoints >= Selected.movementCost);
            RefreshCooldown(skillCooldownOverlay, skillCooldownText, skillButton,
                Selected.CharacterCooldownRemaining, universalPoints >= Selected.characterSkillCost);
        }
        bool showBoss = boss != null && !boss.IsDead && phase == StagePhase.Boss;
        if (bossHealthPanel != null) bossHealthPanel.SetActive(showBoss);
        if (showBoss && bossHealthSlider != null)
        {
            bossHealthSlider.maxValue = boss.maxHealth;
            bossHealthSlider.value = boss.CurrentHealth;
            if (bossNameText != null) bossNameText.text = boss.name;
        }
        for (int i = 0; i < squadButtons.Length && i < squad.Count; i++)
            squadButtons[i].GetComponentInChildren<Text>().text =
                (i == selectedIndex ? "▶ " : "") + squad[i].name;
    }

    private static void RefreshCooldown(Image overlay, Text timer, Button button, float remaining, bool affordable)
    {
        bool cooling = remaining > 0.01f;
        if (overlay != null) overlay.gameObject.SetActive(cooling);
        if (timer != null) timer.text = cooling ? Mathf.CeilToInt(remaining).ToString() : "";
        if (button != null) button.interactable = !cooling && affordable;
    }

    private void TogglePause()
    {
        if (targetMode != TargetMode.None) ExitTargeting();
        paused = !paused;
        Time.timeScale = paused ? 0f : speedLevel;
        if (pauseButton != null) pauseButton.GetComponent<Image>().color =
            paused ? new Color(0.55f, 0.72f, 0.82f, 1f) : Color.white;
        SetButtonCaption(pauseButton, paused ? "▶" : "");
    }

    private void CycleSpeed()
    {
        speedLevel = speedLevel == 1 ? 2 : 1;
        if (!paused) Time.timeScale = speedLevel;
        SetButtonCaption(speedButton, speedLevel + "x");
    }

    private void ToggleAuto()
    {
        autoEnabled = !autoEnabled;
        if (autoButton != null) autoButton.GetComponent<Image>().color =
            autoEnabled ? Color.white : new Color(0.45f, 0.48f, 0.52f, 1f);
        SetButtonCaption(autoButton, autoEnabled ? "AUTO" : "MANUAL");
    }

    private static void SetButtonCaption(Button button, string caption)
    {
        if (button == null) return;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null) label.text = caption;
    }

    private void Finish(bool won)
    {
        if (finished) return;
        finished = true;
        isPlaying = false;
        ExitTargeting();
        Time.timeScale = 1f;
        resultText.text = won ? "VICTORY\nThe assault is complete." : "DEFEAT\nYour squad has fallen.";
        battlePanel.SetActive(false);
        resultPanel.SetActive(true);
    }
}
