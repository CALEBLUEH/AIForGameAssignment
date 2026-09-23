using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BattleDirector : MonoBehaviour
{
    public static BattleDirector Instance { get; private set; }
    [Header("Canvas references")]
    public GameObject setupPanel, battlePanel, resultPanel, pausePanel;
    public Text setupText, statusText, squadText, resultText, timerText, enemyText, energyText;
    public Image[] energySegments;
    public Slider manaSlider, bossHealthSlider;
    public GameObject bossHealthPanel;
    public Text bossNameText, moveCooldownText, skillCooldownText;
    public Image moveCooldownOverlay, skillCooldownOverlay;
    public SkillTargetingFeedback targetingFeedback;
    [Header("Skill targeting presentation")]
    [SerializeField] private SkillTargetingOverlayUI targetingOverlay;
    [SerializeField] private Material targetOutlineMaterial;
    [SerializeField] private LayerMask targetingGroundMask = ~0;
    [Range(0.05f, 1f)] [SerializeField] private float skillTargetingTimeScale = 0.2f;
    public Button[] setupButtons, squadButtons;
    public Button leaderButton, startButton, moveButton, skillButton, healButton, coverButton, restartButton;
    [SerializeField] private Button confirmButton;
    public Button pauseButton, speedButton, autoButton, resumeButton, backButton;
    [Header("Three-card skill queue")]
    [Tooltip("Exactly three visible cards. When assigned, these replace the legacy Character/Heal/Cover button bindings.")]
    [SerializeField] private SkillCardUI[] skillCards;
    [SerializeField] private Sprite yuukaSkillIcon, ayaneSkillIcon, mikaSkillIcon, momoiSkillIcon, hinaSkillIcon;
    [SerializeField] private Sprite healSkillIcon, buffSkillIcon, airstrikeSkillIcon, coverSkillIcon;
    [Header("Basic skill effects")]
    [SerializeField] private GameObject airstrikeMissilePrefab;
    [SerializeField] private GameObject coverObstaclePrefab;
    [Min(0f)] [SerializeField] private float basicHealAmount = 35f;
    [Range(0f, 3f)] [SerializeField] private float basicBuffAttackMultiplier = 0.4f;
    [Min(0.1f)] [SerializeField] private float basicBuffDuration = 10f;
    [Min(0f)] [SerializeField] private float airstrikeDamage = 75f;
    [Min(0.25f)] [SerializeField] private float airstrikeRadius = 3.5f;
    [Min(0.05f)] [SerializeField] private float airstrikeParticleRadius = 2.5f;
    [Min(0.05f)] [SerializeField] private float airstrikeParticleSize = 0.9f;
    [Min(0.1f)] [SerializeField] private float basicTargetSnapRadius = 1.8f;
    [Min(1f)] [SerializeField] private float skillDropHeight = 10f;
    [Min(0.1f)] [SerializeField] private float airstrikeDropDuration = 0.65f;
    [Min(0.1f)] [SerializeField] private float coverDropDuration = 0.75f;
    [Min(0.25f)] [SerializeField] private float coverIndicatorRadius = 1.5f;
    [Min(1f)] [SerializeField] private float placedCoverHealth = 150f;
    [Header("Stage")]
    [Range(1, 3)] public int levelNumber = 1;
    public float universalCapacity = 100f, universalRegeneration = 8f;
    public float healCost = 25f, buffCost = 30f, airstrikeCost = 45f, coverCost = 35f;
    public int waveTwoCount = 4;
    public float timedEnemyDelay = 18f;
    public bool startImmediately;
    public string preparationSceneName = "Preparation";
    [Header("Enemy waves")]
    [Tooltip("When assigned, marker-based waves replace the legacy hardcoded enemy coordinates and boss sequence.")]
    [SerializeField] private EnemyWaveSpawner enemyWaveSpawner;
    [Header("Level opening")]
    [Tooltip("Optional reusable two-second squad introduction. Battle AI and waves remain stopped until it completes.")]
    [SerializeField] private LevelOpeningSequence openingSequence;
    [Header("Post battle presentation")]
    [SerializeField] private CanvasGroup resultCanvasGroup;
    [SerializeField] private Text resultDetailsText;
    [Min(0f)] [SerializeField] private float resultDelay = 2f;
    [Min(0.05f)] [SerializeField] private float resultFadeDuration = 0.35f;
    [SerializeField] private string levelSelectionSceneName = "LevelSelection";
    [SerializeField] private Color victoryTitleColor = new Color(1f, 0.78f, 0.08f, 1f);
    [SerializeField] private Color defeatTitleColor = new Color(0.92f, 0.22f, 0.22f, 1f);
    [Header("Boss configuration")]
    public string bossName = "Boss 1";

    public enum BossStage
    {
        Stage1,
        Stage2,
        Stage3
    }

    [Header("Boss Stage Data")]
    [Tooltip("Choose which of your three BossData assets this stage should use.")]
    public BossStage bossStage = BossStage.Stage1;

    [Tooltip("Assign BossData for Stage 1.")]
    public CharacterData bossDataStage1;

    [Tooltip("Assign BossData 1 for Stage 2.")]
    public CharacterData bossDataStage2;

    [Tooltip("Assign BossData 2 for Stage 3.")]
    public CharacterData bossDataStage3;

    [Header("Legacy Boss Stats - Compatibility Only")]
    [Tooltip("Kept so older Editor scripts such as BuildPlayableScene.cs still compile. The spawned boss uses CharacterData from the boss prefab instead.")]
    public float bossHealth = 520f;
    public float bossAttack = 34f;
    public float bossDefense = 10f;

    public StatusEffectSpec[] bossSelfEffects;
    public StatusEffectSpec[] bossTargetEffects;
    public StatusEffectSpec[] bossAllyEffects;
    public float bossAbilityCooldown = 8f;
    [SerializeField] private float universalPoints;
    [SerializeField] private bool isPlaying;
    private readonly List<AutoCombatAI> squad = new List<AutoCombatAI>();
    private readonly List<CombatUnit> initialEnemies = new List<CombatUnit>();
    private readonly List<GameObject> enemyTemplates = new List<GameObject>();
    private readonly List<int> deployedIndices = new List<int>();
    private readonly HashSet<int> deployedUnitIds = new HashSet<int>();
    private bool[] selected;
    private int leaderIndex, selectedIndex, wave = 1, enemySpawnIndex, playerDeaths;
    private float elapsed;
    private bool finished;
    private bool paused;
    private bool openingInProgress;
    private int speedLevel = 1;
    [SerializeField] private bool autoEnabled = true;
    [Min(0.1f)] [SerializeField] private float autoBasicSkillDecisionInterval = 0.35f;
    private float nextAutoBasicSkillDecision;
    private CombatUnit boss;
    private float nextPhaseTime;
    private enum StagePhase { WaveOne, MovingToWaveTwo, WaveTwo, MovingToBoss, Boss }
    private StagePhase phase = StagePhase.WaveOne;
    private enum TargetMode { None, Move, Skill }
    private TargetMode targetMode;
    private int targetingStartedFrame = -1;
    [Header("Movement skill input")]
    [Min(0.1f)] [SerializeField] private float movementDragCancelRadius = 0.8f;
    [Min(8f)] [SerializeField] private float movementScreenPickRadius = 70f;
    private bool movementDragActive;
    private AutoCombatAI movementDragOwner;
    private SkillTargetHighlighter targetHighlighter;
    public bool IsPlaying => isPlaying;
    public float UniversalPoints => universalPoints;
    public bool AutoEnabled => autoEnabled;
    public float BattleSpeed => Mathf.Max(0.1f, speedLevel);
    public bool IsResultPending => finished && resultPanel != null && !resultPanel.activeSelf;

    public void SetEnemyWaveSpawner(EnemyWaveSpawner spawner) => enemyWaveSpawner = spawner;

    [Header("Selected Attack Radius")]
    [SerializeField] private Material attackRadiusMaterial;
    [SerializeField] private float attackRadiusRingWidth = 0.18f;
    [SerializeField] private float attackRadiusHeightOffset = 0.08f;
    private GameObject selectedAttackRadiusObject;
    private GroundRadiusVisual selectedAttackRadiusVisual;

    private enum QueuedSkillKind { Character, Heal, Buff, Airstrike, Cover }
    private sealed class QueuedSkill
    {
        public QueuedSkillKind kind;
        public AutoCombatAI owner;
    }
    private readonly List<QueuedSkill> skillQueue = new List<QueuedSkill>();
    private QueuedSkill activeQueuedSkill;
    private bool skillQueueTransitioning;
    public bool UsesSkillQueue => skillCards != null && skillCards.Length == 3 &&
        skillCards[0] != null && skillCards[1] != null && skillCards[2] != null;

    private void Awake()
    {
        Instance = this;
        targetHighlighter = new SkillTargetHighlighter(targetOutlineMaterial);
        Time.timeScale = 1f;
        AudioSettingsUI.ApplySavedVolume();
        CombatUnit.UnitDied += OnUnitDied;
        universalPoints = universalCapacity;
        foreach (var ai in FindObjectsByType<AutoCombatAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var combatUnit = ai.GetComponent<CombatUnit>();
            if (combatUnit.team == CombatUnit.CombatTeam.Player) squad.Add(ai);
            else initialEnemies.Add(combatUnit);
        }
        squad.Sort((a, b) => a.rosterOrder.CompareTo(b.rosterOrder));
        selected = new bool[squad.Count];
        for (int i = 0; i < selected.Length; i++)
            selected[i] = startImmediately ? GameProgress.IsCharacterSelected(squad[i].name) : i < 4;
        int selectedCount = 0;
        foreach (bool value in selected) if (value) selectedCount++;
        if (selectedCount != Mathf.Min(4, squad.Count))
            for (int i = 0; i < selected.Length; i++) selected[i] = i < 4;
        leaderIndex = System.Array.FindIndex(selected, value => value);
        if (leaderIndex < 0) leaderIndex = 0;
        foreach (CombatUnit initialEnemy in initialEnemies)
        {
            if (enemyWaveSpawner != null) break;
            GameObject template = Instantiate(initialEnemy.gameObject, transform);
            template.name = initialEnemy.name + " Spawn Template";
            template.SetActive(false);
            enemyTemplates.Add(template);
        }
        BindButtons();
        setupPanel.SetActive(true);
        battlePanel.SetActive(false);
        resultPanel.SetActive(false);
        if (resultCanvasGroup != null) resultCanvasGroup.alpha = 0f;
        if (pausePanel != null) pausePanel.SetActive(false);
        RefreshSetup();
    }

    private void Start()
    {
        if (startImmediately) StartBattle();
    }

    private void OnDestroy()
    {
        targetHighlighter?.Clear();
        CombatUnit.UnitDied -= OnUnitDied;
        if (Instance == this) Instance = null;
    }

    private void BindButtons()
    {
        for (int i = 0; i < setupButtons.Length; i++)
        {
            int index = i;
            setupButtons[i].onClick.AddListener(() => ToggleMember(index));
            squadButtons[i].onClick.AddListener(() => SelectDeployedSlot(index));
        }
        leaderButton.onClick.AddListener(CycleLeader);
        startButton.onClick.AddListener(StartBattle);
        moveButton.onClick.AddListener(() => BeginTargeting(TargetMode.Move));
        if (UsesSkillQueue)
        {
            for (int i = 0; i < skillCards.Length; i++)
            {
                int slot = i;
                skillCards[i].Button.onClick.AddListener(() => UseQueuedSkill(slot));
            }
        }
        else
        {
            skillButton.onClick.AddListener(UseCharacterSkill);
            healButton.onClick.AddListener(UseHeal);
            coverButton.onClick.AddListener(UseCover);
        }
        if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton != null) resumeButton.onClick.AddListener(TogglePause);
        if (backButton != null) backButton.onClick.AddListener(ReturnToPreparation);
        if (speedButton != null) speedButton.onClick.AddListener(CycleSpeed);
        if (autoButton != null) autoButton.onClick.AddListener(ToggleAuto);
        restartButton.onClick.AddListener(RestartBattle);
        if (confirmButton != null) confirmButton.onClick.AddListener(ReturnToLevelSelection);
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
        if (isPlaying || openingInProgress) return;
        PrepareSquadDeployment();
        setupPanel.SetActive(false);
        battlePanel.SetActive(false);
        if (openingSequence != null && openingSequence.isActiveAndEnabled)
        {
            openingInProgress = true;
            List<AutoCombatAI> deployedSquad = new List<AutoCombatAI>(deployedIndices.Count);
            foreach (int index in deployedIndices) deployedSquad.Add(squad[index]);
            openingSequence.Play(deployedSquad, ActivateBattle);
            return;
        }
        ActivateBattle();
    }

    private void PrepareSquadDeployment()
    {
        deployedIndices.Clear();
        deployedUnitIds.Clear();
        playerDeaths = 0;
        if (leaderIndex >= selected.Length || !selected[leaderIndex])
            leaderIndex = System.Array.FindIndex(selected, value => value);
        for (int i = 0; i < squad.Count; i++)
        {
            if (!selected[i]) { squad[i].gameObject.SetActive(false); continue; }
            squad[i].gameObject.SetActive(true);
            deployedIndices.Add(i);
            CombatUnit deployedUnit = squad[i].GetComponent<CombatUnit>();
            if (deployedUnit != null) deployedUnitIds.Add(deployedUnit.GetInstanceID());
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
    }

    private void ActivateBattle()
    {
        openingInProgress = false;
        isPlaying = true;
        setupPanel.SetActive(false);
        battlePanel.SetActive(true);
        InitializeSkillQueue();
        if (enemyWaveSpawner != null) enemyWaveSpawner.BeginBattle();
        RefreshBattle();
    }

    public void SetOpeningSequence(LevelOpeningSequence sequence) => openingSequence = sequence;

    private void OnUnitDied(CombatUnit deadUnit)
    {
        if (deadUnit != null && deadUnit.team == CombatUnit.CombatTeam.Player &&
            deployedUnitIds.Contains(deadUnit.GetInstanceID()))
        {
            playerDeaths++;
            skillQueue.RemoveAll(entry => entry.owner != null && entry.owner.Unit == deadUnit);
            RefreshSkillQueue();
        }
    }

    private void Update()
    {
        if (!isPlaying) return;
        elapsed += Time.deltaTime;
        universalPoints = Mathf.Min(universalCapacity, universalPoints + universalRegeneration * Time.deltaTime);
        int enemies = CountAlive(CombatUnit.CombatTeam.Enemy);
        int allies = CountAlive(CombatUnit.CombatTeam.Player);
        if (autoEnabled && UsesSkillQueue && enemies > 0 && Time.time >= nextAutoBasicSkillDecision)
        {
            nextAutoBasicSkillDecision = Time.time + autoBasicSkillDecisionInterval;
            TryAutoUseQueuedBasicSkill();
        }
        if (allies == 0) { Finish(false); return; }
        if (enemyWaveSpawner != null)
        {
            wave = Mathf.Max(1, enemyWaveSpawner.CurrentWave);
            if (enemyWaveSpawner.FinishAfterLastWave && enemyWaveSpawner.IsComplete && enemies == 0)
            {
                Finish(true);
                return;
            }
        }
        else if (phase == StagePhase.WaveOne && enemies == 0)
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
        if (targetMode == TargetMode.None && Input.GetMouseButtonDown(0) && !IsPointerOverInteractiveUI())
            TryBeginMovementDrag();
        UpdateTargetPointer();
        if (targetMode == TargetMode.Move && movementDragActive && Input.GetMouseButtonUp(0))
            FinishMovementDrag();
        else if (targetMode == TargetMode.Move && !movementDragActive &&
            Input.GetMouseButtonDown(0) && !IsPointerOverInteractiveUI())
            HandleWorldRelease();
        else if (targetMode == TargetMode.Skill && Input.GetMouseButtonUp(0) &&
            Time.frameCount > targetingStartedFrame && !IsPointerOverInteractiveUI())
            HandleWorldRelease();
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
        if (enemyTemplates.Count == 0) return;
        GameObject template = enemyTemplates[enemySpawnIndex++ % enemyTemplates.Count];
        var clone = Instantiate(template, position, Quaternion.identity);
        clone.name = "Wave " + wave + " Enemy";
        clone.transform.SetParent(null);
        clone.SetActive(true);
    }

    private CharacterData GetBossDataForCurrentStage()
    {
        switch (bossStage)
        {
            case BossStage.Stage1:
                return bossDataStage1;
            case BossStage.Stage2:
                return bossDataStage2;
            case BossStage.Stage3:
                return bossDataStage3;
            default:
                return bossDataStage1;
        }
    }

    private void SpawnBoss()
    {
        if (enemyTemplates.Count == 0) return;
        GameObject clone = Instantiate(enemyTemplates[0], new Vector3(57f, 1f, 0f), Quaternion.identity);
        clone.name = bossName;
        clone.transform.SetParent(null);
        clone.transform.localScale = Vector3.one * 2.2f;
        clone.SetActive(true);
        boss = clone.GetComponent<CombatUnit>();
        if (boss == null)
        {
            Debug.LogError("The spawned boss prefab needs a CombatUnit component.");
            Destroy(clone);
            return;
        }

        // The boss is generated at runtime. Pick the CharacterData for this stage
        // and apply it directly to the newly generated CombatUnit.
        CharacterData selectedBossData = GetBossDataForCurrentStage();

        if (selectedBossData != null)
        {
            boss.ConfigureFromCharacterData(selectedBossData, true);
        }
        else
        {
            Debug.LogWarning($"No CharacterData assigned for {bossStage}. Using legacy fallback boss stats.");
            boss.ConfigureSpawn(bossHealth, bossAttack, bossDefense, true);
        }

        boss.isElite = true;
        clone.GetComponent<AutoCombatAI>().ConfigureEnemyAbilities(
            bossSelfEffects, bossTargetEffects, bossAllyEffects, bossAbilityCooldown);
    }

    private void SelectDeployedSlot(int slot)
    {
        if (slot >= 0 && slot < deployedIndices.Count) SelectMember(deployedIndices[slot]);
    }

    private void SelectMember(int index)
    {
        if (IsUsableSquadMember(index))
        {
            selectedIndex = index;
            ExitTargeting();
            RefreshSelectedAttackRadius();
        }
    }

    private void RefreshSelectedAttackRadius()
    {
        AutoCombatAI selectedMember = Selected;

        if (selectedMember == null || selectedMember.Unit == null || selectedMember.Unit.IsDead)
        {
            if (selectedAttackRadiusObject != null)
                selectedAttackRadiusObject.SetActive(false);
            return;
        }

        if (selectedAttackRadiusObject == null)
        {
            selectedAttackRadiusObject = new GameObject("Selected Attack Radius");
            selectedAttackRadiusVisual = selectedAttackRadiusObject.AddComponent<GroundRadiusVisual>();

            MeshRenderer renderer = selectedAttackRadiusObject.GetComponent<MeshRenderer>();
            if (attackRadiusMaterial != null)
                renderer.sharedMaterial = attackRadiusMaterial;

            selectedAttackRadiusVisual.radiusSource = GroundRadiusVisual.RadiusSource.AttackRange;
            selectedAttackRadiusVisual.ringWidth = attackRadiusRingWidth;
            selectedAttackRadiusVisual.heightOffset = attackRadiusHeightOffset;
        }

        selectedAttackRadiusObject.SetActive(true);
        selectedAttackRadiusObject.transform.SetParent(selectedMember.transform, false);
        selectedAttackRadiusObject.transform.localPosition = Vector3.zero;
        selectedAttackRadiusObject.transform.localRotation = Quaternion.identity;
        selectedAttackRadiusObject.transform.localScale = Vector3.one;

        selectedAttackRadiusVisual.combatUnit = selectedMember.Unit;
        selectedAttackRadiusVisual.radiusSource = GroundRadiusVisual.RadiusSource.AttackRange;
        selectedAttackRadiusVisual.ringWidth = attackRadiusRingWidth;
        selectedAttackRadiusVisual.heightOffset = attackRadiusHeightOffset;
        selectedAttackRadiusVisual.GenerateRing();
    }

    private AutoCombatAI Selected
    {
        get
        {
            if (IsUsableSquadMember(selectedIndex)) return squad[selectedIndex];
            for (int i = 0; i < squad.Count; i++)
            {
                if (!IsUsableSquadMember(i)) continue;
                selectedIndex = i;
                return squad[i];
            }
            return null;
        }
    }

    private bool IsUsableSquadMember(int index)
    {
        if (index < 0 || index >= squad.Count) return false;
        AutoCombatAI member = squad[index];
        return member != null && member.gameObject.activeInHierarchy &&
            member.Unit != null && !member.Unit.IsDead;
    }

    private static bool IsPointerOverInteractiveUI()
    {
        if (EventSystem.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        foreach (RaycastResult result in results)
            if (result.gameObject.GetComponentInParent<Selectable>() != null) return true;
        return false;
    }

    private void UseCharacterSkill()
    {
        if (Selected == null || Selected.Unit.IsDead) return;
        BeginTargeting(TargetMode.Skill);
    }

    private void InitializeSkillQueue()
    {
        if (!UsesSkillQueue) return;
        skillQueue.Clear();
        foreach (int index in deployedIndices)
        {
            if (!IsUsableSquadMember(index)) continue;
            skillQueue.Add(new QueuedSkill { kind = QueuedSkillKind.Character, owner = squad[index] });
        }
        skillQueue.Add(new QueuedSkill { kind = QueuedSkillKind.Heal });
        skillQueue.Add(new QueuedSkill { kind = QueuedSkillKind.Buff });
        skillQueue.Add(new QueuedSkill { kind = QueuedSkillKind.Airstrike });
        skillQueue.Add(new QueuedSkill { kind = QueuedSkillKind.Cover });

        for (int i = skillQueue.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            QueuedSkill temporary = skillQueue[i];
            skillQueue[i] = skillQueue[swapIndex];
            skillQueue[swapIndex] = temporary;
        }
        skillQueueTransitioning = false;
        RefreshSkillQueue();
    }

    private void UseQueuedSkill(int slot)
    {
        if (!UsesSkillQueue || skillQueueTransitioning || slot < 0 || slot >= 3 || slot >= skillQueue.Count) return;
        QueuedSkill entry = skillQueue[slot];
        if (targetMode == TargetMode.Skill && activeQueuedSkill == entry)
        {
            ExitTargeting();
            return;
        }
        if (targetMode != TargetMode.None) ExitTargeting();
        switch (entry.kind)
        {
            case QueuedSkillKind.Character:
                if (entry.owner == null || entry.owner.Unit == null || entry.owner.Unit.IsDead) return;
                int ownerIndex = squad.IndexOf(entry.owner);
                if (ownerIndex < 0) return;
                SelectMember(ownerIndex);
                activeQueuedSkill = entry;
                BeginTargeting(TargetMode.Skill);
                break;
            case QueuedSkillKind.Heal:
            case QueuedSkillKind.Buff:
            case QueuedSkillKind.Airstrike:
            case QueuedSkillKind.Cover:
                activeQueuedSkill = entry;
                BeginTargeting(TargetMode.Skill);
                break;
        }
    }

    public bool CanUseQueuedCharacterSkill(AutoCombatAI owner)
    {
        if (!UsesSkillQueue) return true;
        if (skillQueueTransitioning || owner == null) return false;
        int visibleCount = Mathf.Min(3, skillQueue.Count);
        for (int i = 0; i < visibleCount; i++)
            if (skillQueue[i].kind == QueuedSkillKind.Character && skillQueue[i].owner == owner) return true;
        return false;
    }

    public void NotifyCharacterSkillUsed(AutoCombatAI owner)
    {
        if (!UsesSkillQueue || owner == null) return;
        QueuedSkill entry = skillQueue.Find(candidate =>
            candidate.kind == QueuedSkillKind.Character && candidate.owner == owner);
        if (entry != null) ConsumeQueuedSkill(entry);
    }

    private void ConsumeQueuedSkill(QueuedSkill entry)
    {
        if (entry == null || skillQueueTransitioning) return;
        int index = skillQueue.IndexOf(entry);
        if (index < 0) return;
        skillQueueTransitioning = true;
        System.Action rotate = () =>
        {
            if (skillQueue.Remove(entry)) skillQueue.Add(entry);
            skillQueueTransitioning = false;
            RefreshSkillQueue();
        };
        if (index < 3 && index < skillCards.Length) skillCards[index].PlayUsed(rotate);
        else rotate();
    }

    private void RefreshSkillQueue()
    {
        if (!UsesSkillQueue || skillQueueTransitioning) return;
        for (int i = 0; i < skillCards.Length; i++)
        {
            if (i >= skillQueue.Count)
            {
                skillCards[i].gameObject.SetActive(false);
                continue;
            }
            skillCards[i].gameObject.SetActive(true);
            QueuedSkill entry = skillQueue[i];
            bool usable = IsQueuedSkillUsable(entry);
            skillCards[i].Bind(IconFor(entry), NameFor(entry), CostFor(entry), usable);
        }
    }

    private bool IsQueuedSkillUsable(QueuedSkill entry)
    {
        if (!isPlaying || entry == null) return false;
        if (entry.kind == QueuedSkillKind.Heal) return Selected != null && universalPoints >= healCost;
        if (entry.kind == QueuedSkillKind.Buff) return Selected != null && universalPoints >= buffCost;
        if (entry.kind == QueuedSkillKind.Airstrike) return Selected != null && universalPoints >= airstrikeCost;
        if (entry.kind == QueuedSkillKind.Cover) return Selected != null && universalPoints >= coverCost;
        return entry.owner != null && entry.owner.Unit != null && !entry.owner.Unit.IsDead &&
            entry.owner.CharacterCooldownRemaining <= 0f && universalPoints >= entry.owner.characterSkillCost;
    }

    private float CostFor(QueuedSkill entry)
    {
        if (entry.kind == QueuedSkillKind.Heal) return healCost;
        if (entry.kind == QueuedSkillKind.Buff) return buffCost;
        if (entry.kind == QueuedSkillKind.Airstrike) return airstrikeCost;
        if (entry.kind == QueuedSkillKind.Cover) return coverCost;
        return entry.owner == null ? 0f : entry.owner.characterSkillCost;
    }

    private static string NameFor(QueuedSkill entry)
    {
        if (entry.kind == QueuedSkillKind.Heal) return "HEAL";
        if (entry.kind == QueuedSkillKind.Buff) return "BUFF";
        if (entry.kind == QueuedSkillKind.Airstrike) return "AIRSTRIKE";
        if (entry.kind == QueuedSkillKind.Cover) return "COVER";
        return entry.owner == null ? "SKILL" : entry.owner.name.ToUpperInvariant();
    }

    private Sprite IconFor(QueuedSkill entry)
    {
        if (entry.kind == QueuedSkillKind.Heal) return healSkillIcon;
        if (entry.kind == QueuedSkillKind.Buff) return buffSkillIcon;
        if (entry.kind == QueuedSkillKind.Airstrike) return airstrikeSkillIcon;
        if (entry.kind == QueuedSkillKind.Cover) return coverSkillIcon;
        if (entry.owner == null) return null;
        switch (entry.owner.role)
        {
            case AutoCombatAI.CombatRole.YuukaTank: return yuukaSkillIcon;
            case AutoCombatAI.CombatRole.AyaneHealer: return ayaneSkillIcon;
            case AutoCombatAI.CombatRole.MikaSingleTarget: return mikaSkillIcon;
            case AutoCombatAI.CombatRole.MomoiLowCostAOE: return momoiSkillIcon;
            case AutoCombatAI.CombatRole.HinaHighCostAOE: return hinaSkillIcon;
            default: return null;
        }
    }

    private void UseHeal()
    {
        TryUseHeal();
    }

    private bool TryUseHeal()
    {
        ExitTargeting();
        if (Selected == null || Selected.Unit.IsDead || !TrySpendUniversal(healCost)) return false;
        Selected.Unit.Heal(35f);
        if (targetingFeedback != null)
            targetingFeedback.PlayFeedback(Selected.transform.position, new Color(0.2f, 1f, 0.55f));
        return true;
    }

    private void UseCover()
    {
        TryUseCover();
    }

    private bool TryUseCover()
    {
        ExitTargeting();
        if (Selected == null || Selected.Unit.IsDead || !TrySpendUniversal(coverCost)) return false;

        Vector3 forward = Selected.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.right;
        forward.Normalize();

        Vector3 position = Selected.transform.position + forward * 2f;
        position.y = 0f;

        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Placed Cover";
        block.transform.position = position + Vector3.up * 0.9f;
        block.transform.rotation = Quaternion.LookRotation(forward);
        block.transform.localScale = new Vector3(2.5f, 1.8f, 0.7f);
        block.layer = 8;

        var obstacle = block.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;

        // The skill-created wall has its own HP. Damage prevented by its protection
        // is transferred to this object until it breaks.
        block.AddComponent<DestructibleCover>();
        TacticalCoverObstacle coverGroup = block.AddComponent<TacticalCoverObstacle>();

        // IMPORTANT: the old skill only spawned a NavMesh obstacle.
        // AutoCombatAI therefore saw it only as something to walk around.
        // Add real CoverPoints on the PLAYER side of the wall so ranged units can use it.
        Vector3 right = block.transform.right;
        Vector3 playerSide = position - forward * 0.85f;

        for (int i = -1; i <= 1; i++)
        {
            GameObject pointObject = new GameObject("Placed Cover Point " + (i + 2));
            pointObject.transform.SetParent(block.transform, true);

            Vector3 pointPosition = playerSide + right * (i * 0.75f);
            pointPosition.y = Selected.transform.position.y;
            pointObject.transform.position = pointPosition;

            CoverPoint point = pointObject.AddComponent<CoverPoint>();
            point.Configure(block.GetComponent<Collider>(), coverGroup, 0.35f, 0.45f);
        }

        if (targetingFeedback != null)
            targetingFeedback.PlayFeedback(position, new Color(0.25f, 0.75f, 1f));
        return true;
    }

    private void HandleWorldRelease()
    {
        if (Selected == null || Selected.Unit.IsDead) { ExitTargeting(); return; }
        if (Camera.main == null) { ExitTargeting(); return; }
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!TryGetGroundPoint(ray, out Vector3 pointer)) return;
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
            if (activeQueuedSkill != null && activeQueuedSkill.kind != QueuedSkillKind.Character)
            {
                QueuedSkill completedSkill = activeQueuedSkill;
                used = TryResolveBasicSkill(completedSkill.kind, pointer, out feedbackPosition);
                if (used) ConsumeQueuedSkill(completedSkill);
            }
            else
            {
                CombatUnit target = Selected.role == AutoCombatAI.CombatRole.MikaSingleTarget
                    ? FindSkillTarget(pointer) : null;
                used = Selected.TryCharacterSkillAt(pointer, target);
                if (target != null) feedbackPosition = target.transform.position;
            }
        }
        if (used && targetingFeedback != null)
            targetingFeedback.PlayFeedback(feedbackPosition, new Color(1f, 0.65f, 0.1f));
        if (used || targetMode == TargetMode.Move) ExitTargeting();
        return used;
    }

    private bool TryAutoUseQueuedBasicSkill()
    {
        if (!isPlaying || !autoEnabled || !UsesSkillQueue || skillQueueTransitioning ||
            targetMode != TargetMode.None) return false;

        int visibleCount = Mathf.Min(3, skillQueue.Count);
        // Healing is the only skill that waits for a condition. If it is visible and
        // an ally is injured, it gets first priority and selects the lowest HP ratio.
        for (int i = 0; i < visibleCount; i++)
        {
            QueuedSkill heal = skillQueue[i];
            if (heal.kind != QueuedSkillKind.Heal || universalPoints < healCost) continue;
            CombatUnit ally = FindLowestHealthAlly(true);
            if (ally != null && TryResolveBasicSkill(heal.kind, ally.transform.position, out _, null))
            {
                ConsumeQueuedSkill(heal);
                return true;
            }
        }

        for (int i = 0; i < visibleCount; i++)
        {
            QueuedSkill entry = skillQueue[i];
            if (entry.kind == QueuedSkillKind.Character || entry.kind == QueuedSkillKind.Heal ||
                universalPoints < CostFor(entry)) continue;
            if (!TryAutoUseBasicEntry(entry)) continue;
            ConsumeQueuedSkill(entry);
            return true;
        }
        return false;
    }

    private bool TryAutoUseBasicEntry(QueuedSkill entry)
    {
        if (entry.kind == QueuedSkillKind.Buff)
        {
            CombatUnit damageDealer = FindHighestDamageAlly();
            return damageDealer != null && TryResolveBasicSkill(entry.kind,
                damageDealer.transform.position, out _, null);
        }
        if (entry.kind == QueuedSkillKind.Airstrike)
        {
            CombatUnit target = FindBestAirstrikeTarget();
            return target != null && TryResolveBasicSkill(entry.kind, target.transform.position, out _, null);
        }
        if (entry.kind == QueuedSkillKind.Cover)
        {
            CombatUnit protectedAlly = FindLowestHealthAlly(false) ?? FindHighestDamageAlly();
            CombatUnit enemy = FindNearestEnemy(protectedAlly == null ? Vector3.zero : protectedAlly.transform.position);
            if (protectedAlly == null || enemy == null) return false;
            Vector3 direction = enemy.transform.position - protectedAlly.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = protectedAlly.transform.forward;
            Vector3 placement = protectedAlly.transform.position + direction.normalized * 2f;
            AutoCombatAI context = protectedAlly.GetComponent<AutoCombatAI>();
            return TryResolveBasicSkill(entry.kind, placement, out _, context);
        }
        return false;
    }

    private CombatUnit FindLowestHealthAlly(bool requireInjured)
    {
        CombatUnit best = null;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate.IsDead || candidate.team != CombatUnit.CombatTeam.Player ||
                (requireInjured && candidate.HealthRatio >= 0.999f)) continue;
            if (best == null || candidate.HealthRatio < best.HealthRatio ||
                (Mathf.Approximately(candidate.HealthRatio, best.HealthRatio) && candidate.CurrentHealth < best.CurrentHealth))
                best = candidate;
        }
        return best;
    }

    private CombatUnit FindHighestDamageAlly()
    {
        CombatUnit best = null;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (!candidate.IsDead && candidate.team == CombatUnit.CombatTeam.Player &&
                (best == null || candidate.AttackPower > best.AttackPower)) best = candidate;
        return best;
    }

    private CombatUnit FindBestAirstrikeTarget()
    {
        CombatUnit best = null;
        int bestCount = -1;
        CombatUnit[] units = FindObjectsByType<CombatUnit>(FindObjectsSortMode.None);
        foreach (CombatUnit candidate in units)
        {
            if (candidate.IsDead || candidate.team != CombatUnit.CombatTeam.Enemy) continue;
            int count = 0;
            foreach (CombatUnit other in units)
                if (!other.IsDead && other.team == CombatUnit.CombatTeam.Enemy &&
                    FlatDistance(candidate.transform.position, other.transform.position) <= airstrikeRadius) count++;
            if (count > bestCount) { bestCount = count; best = candidate; }
        }
        return best;
    }

    private static CombatUnit FindNearestEnemy(Vector3 origin)
    {
        CombatUnit best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate.IsDead || candidate.team != CombatUnit.CombatTeam.Enemy) continue;
            float distance = FlatDistance(origin, candidate.transform.position);
            if (distance < bestDistance) { bestDistance = distance; best = candidate; }
        }
        return best;
    }

    private void TryBeginMovementDrag()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            CombatUnit candidate = hit.collider.GetComponentInParent<CombatUnit>();
            if (candidate == null || candidate.IsDead || candidate.team != CombatUnit.CombatTeam.Player ||
                !candidate.IsMovementGaugeFull) continue;
            AutoCombatAI ai = candidate.GetComponent<AutoCombatAI>();
            int index = squad.IndexOf(ai);
            if (index < 0 || !IsUsableSquadMember(index) || ai.IsDashing || ai.MovementCooldownRemaining > 0f)
                continue;
            SelectMember(index);
            movementDragOwner = ai;
            movementDragActive = true;
            BeginTargeting(TargetMode.Move);
            return;
        }

        // A unit pressed against cover can be completely hidden from the physics ray.
        // Fall back to its visible screen-space bounds so the obstacle never traps input.
        AutoCombatAI screenCandidate = FindMovementDragCandidateFromScreen(Input.mousePosition);
        if (screenCandidate == null) return;
        int selected = squad.IndexOf(screenCandidate);
        SelectMember(selected);
        movementDragOwner = screenCandidate;
        movementDragActive = true;
        BeginTargeting(TargetMode.Move);
    }

    private AutoCombatAI FindMovementDragCandidateFromScreen(Vector2 screenPoint)
    {
        if (Camera.main == null) return null;
        AutoCombatAI screenCandidate = null;
        float bestPixels = movementScreenPickRadius;
        foreach (AutoCombatAI ai in squad)
        {
            if (ai == null || ai.Unit == null || ai.Unit.IsDead ||
                ai.Unit.team != CombatUnit.CombatTeam.Player || !ai.Unit.IsMovementGaugeFull ||
                ai.IsDashing || ai.MovementCooldownRemaining > 0f) continue;
            int index = squad.IndexOf(ai);
            if (index < 0 || !IsUsableSquadMember(index)) continue;
            Collider body = ai.GetComponent<Collider>() ?? ai.GetComponentInChildren<Collider>(true);
            Vector3 world = body == null ? ai.transform.position + Vector3.up : body.bounds.center;
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            if (screen.z <= 0f) continue;
            float pixels = Vector2.Distance(screenPoint, new Vector2(screen.x, screen.y));
            if (pixels >= bestPixels) continue;
            bestPixels = pixels;
            screenCandidate = ai;
        }
        return screenCandidate;
    }

    private void FinishMovementDrag()
    {
        if (!movementDragActive || movementDragOwner == null || targetingFeedback == null)
        {
            ExitTargeting();
            return;
        }
        Vector3 pointer = targetingFeedback.Pointer;
        if (FlatDistance(movementDragOwner.NavMeshWorldPosition, pointer) <= movementDragCancelRadius)
        {
            ExitTargeting();
            return;
        }
        ResolveTargetAt(pointer);
    }

    private bool TryResolveBasicSkill(QueuedSkillKind kind, Vector3 pointer, out Vector3 feedbackPosition,
        AutoCombatAI context = null)
    {
        feedbackPosition = pointer;
        if (kind == QueuedSkillKind.Heal || kind == QueuedSkillKind.Buff)
        {
            CombatUnit ally = FindAllyTarget(pointer);
            if (ally == null) return false;
            float cost = kind == QueuedSkillKind.Heal ? healCost : buffCost;
            if (!TrySpendUniversal(cost)) return false;
            feedbackPosition = ally.transform.position;
            if (kind == QueuedSkillKind.Heal)
            {
                ally.Heal(basicHealAmount);
                SkillVfx.SpawnBurst(ally.transform.position + Vector3.up * 0.75f,
                    new Color(0.2f, 1f, 0.45f), 0.22f, 20, 0.65f);
            }
            else
            {
                ally.AddTimedModifier(CombatUnit.Stat.Attack, 0f, basicBuffAttackMultiplier, basicBuffDuration);
                SkillVfx.SpawnAura(ally.transform, new Color(1f, 0.82f, 0.12f), basicBuffDuration, 0.7f);
            }
            return true;
        }

        if (kind == QueuedSkillKind.Airstrike)
        {
            if (!TrySpendUniversal(airstrikeCost)) return false;
            Vector3 landingPoint = pointer;
            SkillVfx.DropModel(airstrikeMissilePrefab, landingPoint,
                Vector3.up * skillDropHeight - Vector3.right * 4f,
                airstrikeDropDuration, 1f, true, landed =>
                {
                    SkillVfx.SpawnBurst(landingPoint + Vector3.up * 0.25f,
                        new Color(1f, 0.42f, 0.12f), airstrikeParticleSize, 55, 1.1f,
                        airstrikeParticleRadius);
                    HashSet<DestructibleCover> hitCovers =
                        DestructibleCover.DamageInRadius(landingPoint, airstrikeRadius, airstrikeDamage);
                    foreach (CombatUnit enemy in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                        if (!enemy.IsDead && enemy.team == CombatUnit.CombatTeam.Enemy &&
                            FlatDistance(landingPoint, enemy.transform.position) <= airstrikeRadius)
                        {
                            DestructibleCover enemyCover = enemy.ActiveCoverPoint == null
                                ? null : enemy.ActiveCoverPoint.Destructible;
                            if (enemyCover != null && hitCovers.Contains(enemyCover)) continue;
                            enemy.TakeDamage(airstrikeDamage, landingPoint);
                        }
                });
            return true;
        }

        if (kind == QueuedSkillKind.Cover)
        {
            if (!TrySpendUniversal(coverCost)) return false;
            AutoCombatAI coverOwner = context != null ? context : Selected;
            if (coverOwner == null) { RefundUniversal(coverCost); return false; }
            Vector3 facing = coverOwner.transform.position - pointer;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(facing.normalized);
            SkillVfx.DropModel(coverObstaclePrefab, pointer, Vector3.up * skillDropHeight,
                coverDropDuration, 1f, false, landed => ConfigurePlacedCover(landed, pointer, rotation));
            return true;
        }
        return false;
    }

    private CombatUnit FindAllyTarget(Vector3 pointer)
    {
        CombatUnit best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate.IsDead || candidate.team != CombatUnit.CombatTeam.Player) continue;
            float distance = FlatDistance(pointer, candidate.transform.position);
            if (distance >= bestDistance) continue;
            best = candidate;
            bestDistance = distance;
        }
        return bestDistance <= basicTargetSnapRadius ? best : null;
    }

    private void ConfigurePlacedCover(GameObject cover, Vector3 position, Quaternion rotation)
    {
        if (cover == null) return;
        cover.name = "Placed Cover";
        cover.transform.SetPositionAndRotation(position, rotation);
        cover.layer = 8;
        foreach (Transform child in cover.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;

        Collider physicalCollider = cover.GetComponentInChildren<Collider>(true);
        if (physicalCollider == null)
        {
            Bounds bounds = CalculateRendererBounds(cover);
            BoxCollider box = cover.AddComponent<BoxCollider>();
            box.center = cover.transform.InverseTransformPoint(bounds.center);
            Vector3 scale = cover.transform.lossyScale;
            box.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            physicalCollider = box;
        }
        foreach (Collider collider in cover.GetComponentsInChildren<Collider>(true)) collider.enabled = true;

        NavMeshObstacle navObstacle = cover.GetComponent<NavMeshObstacle>();
        if (navObstacle == null) navObstacle = cover.AddComponent<NavMeshObstacle>();
        navObstacle.carving = true;
        DestructibleCover destructible = cover.GetComponent<DestructibleCover>();
        if (destructible == null) destructible = cover.AddComponent<DestructibleCover>();
        destructible.Configure(placedCoverHealth);
        destructible.enabled = true;
        TacticalCoverObstacle group = cover.GetComponent<TacticalCoverObstacle>();
        if (group == null) group = cover.AddComponent<TacticalCoverObstacle>();
        group.enabled = true;

        CoverPoint[] authoredPoints = cover.GetComponentsInChildren<CoverPoint>(true);
        if (authoredPoints.Length > 0)
        {
            foreach (CoverPoint point in authoredPoints)
                point.Configure(physicalCollider, group, 1f, 0.45f);
        }
        else
        {
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject pointObject = new GameObject(side < 0 ? "Cover Point A" : "Cover Point B");
                pointObject.transform.SetParent(cover.transform, true);
                pointObject.transform.position = position + rotation * Vector3.forward * (side * 0.85f);
                CoverPoint point = pointObject.AddComponent<CoverPoint>();
                point.Configure(physicalCollider, group, 1f, 0.45f);
            }
        }
        CoverHealthHUD hud = cover.GetComponentInChildren<CoverHealthHUD>(true);
        if (hud != null)
        {
            hud.gameObject.SetActive(true);
            hud.enabled = true;
        }
        SkillVfx.SpawnBurst(position + Vector3.up * 0.2f, new Color(0.3f, 0.75f, 1f), 0.3f, 28, 0.75f);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, new Vector3(2.5f, 1.8f, 0.7f));
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private CombatUnit FindSkillTarget(Vector3 pointer)
    {
        CombatUnit best = null;
        float bestPointerDistance = float.PositiveInfinity;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate.IsDead || candidate.team == Selected.Unit.team ||
                FlatDistance(Selected.transform.position, candidate.transform.position) > Selected.skillRange) continue;
            float pointerDistance = FlatDistance(pointer, candidate.transform.position);
            if (pointerDistance < bestPointerDistance)
            {
                best = candidate;
                bestPointerDistance = pointerDistance;
            }
        }
        return bestPointerDistance <= Selected.targetSnapRadius ? best : null;
    }

    private bool TryGetGroundPoint(Ray ray, out Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, targetingGroundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
            {
                point = navHit.position;
                return true;
            }
        }
        var fallback = new Plane(Vector3.up, Vector3.zero);
        if (fallback.Raycast(ray, out float distance) &&
            NavMesh.SamplePosition(ray.GetPoint(distance), out NavMeshHit fallbackHit, 10f, NavMesh.AllAreas))
        {
            point = fallbackHit.position;
            return true;
        }
        point = default;
        return false;
    }

    private void BeginTargeting(TargetMode mode)
    {
        if (Selected == null || Selected.Unit.IsDead) return;
        if (mode == TargetMode.Move && (Selected.MovementCooldownRemaining > 0f ||
            Selected.IsDashing || !Selected.Unit.IsMovementGaugeFull)) return;
        bool targetsBasicSkill = mode == TargetMode.Skill && activeQueuedSkill != null &&
            activeQueuedSkill.kind != QueuedSkillKind.Character;
        if (mode == TargetMode.Skill && !targetsBasicSkill && Selected.CharacterCooldownRemaining > 0f) return;
        if (targetsBasicSkill && !IsQueuedSkillUsable(activeQueuedSkill)) return;
        targetMode = mode;
        targetingStartedFrame = Time.frameCount;
        Time.timeScale = skillTargetingTimeScale;
        if (mode == TargetMode.Skill && targetingOverlay != null)
        {
            if (targetsBasicSkill) targetingOverlay.Show(NameFor(activeQueuedSkill), BasicSkillDescription(activeQueuedSkill.kind));
            else targetingOverlay.Show(Selected.name.ToUpperInvariant() + " SKILL", Selected.SkillDescription);
        }
        if (targetingFeedback == null) return;
        if (mode == TargetMode.Move) targetingFeedback.ShowArrow(Selected.transform, Selected.movementRange);
        else if (!targetsBasicSkill) targetingFeedback.ShowCharacterSkill(Selected);
        else
        {
            float indicatorRadius = activeQueuedSkill.kind == QueuedSkillKind.Airstrike ? airstrikeRadius :
                activeQueuedSkill.kind == QueuedSkillKind.Cover ? coverIndicatorRadius : basicTargetSnapRadius * 0.55f;
            targetingFeedback.ShowWorldCircle(Selected.NavMeshWorldPosition, indicatorRadius);
        }
    }

    private static string BasicSkillDescription(QueuedSkillKind kind)
    {
        if (kind == QueuedSkillKind.Heal) return "Drag onto an ally to restore health.";
        if (kind == QueuedSkillKind.Buff) return "Drag onto an ally to increase attack by 40% for 10 seconds.";
        if (kind == QueuedSkillKind.Airstrike) return "Choose an area for a damaging missile strike.";
        return "Choose an area to drop a usable cover obstacle.";
    }

    private void UpdateTargetPointer()
    {
        if (targetMode == TargetMode.None || targetingFeedback == null || Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (TryGetGroundPoint(ray, out Vector3 pointer)) targetingFeedback.SetPointer(pointer);
        if (targetMode == TargetMode.Skill) RefreshSkillTargetHighlights();
    }

    private void ExitTargeting()
    {
        targetMode = TargetMode.None;
        movementDragActive = false;
        movementDragOwner = null;
        activeQueuedSkill = null;
        if (targetingFeedback != null) targetingFeedback.Hide();
        if (targetingOverlay != null) targetingOverlay.Hide();
        targetHighlighter?.Clear();
        Time.timeScale = paused ? 0f : speedLevel;
    }

    private void RefreshSkillTargetHighlights()
    {
        if (Selected == null || targetingFeedback == null || targetHighlighter == null) return;
        var affected = new List<CombatUnit>();
        Vector3 pointer = targetingFeedback.Pointer;
        if (activeQueuedSkill != null && activeQueuedSkill.kind != QueuedSkillKind.Character)
        {
            if (activeQueuedSkill.kind == QueuedSkillKind.Heal || activeQueuedSkill.kind == QueuedSkillKind.Buff)
            {
                CombatUnit ally = FindAllyTarget(pointer);
                if (ally != null) affected.Add(ally);
            }
            else if (activeQueuedSkill.kind == QueuedSkillKind.Airstrike)
            {
                foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                    if (!candidate.IsDead && candidate.team == CombatUnit.CombatTeam.Enemy &&
                        FlatDistance(pointer, candidate.transform.position) <= airstrikeRadius)
                        affected.Add(candidate);
            }
            targetHighlighter.Set(affected);
            if (targetingOverlay != null) targetingOverlay.SetHighlightHoles(affected, Camera.main);
            return;
        }
        switch (Selected.role)
        {
            case AutoCombatAI.CombatRole.YuukaTank:
                affected.Add(Selected.Unit);
                break;
            case AutoCombatAI.CombatRole.MikaSingleTarget:
                CombatUnit target = FindSkillTarget(pointer);
                if (target != null) affected.Add(target);
                break;
            case AutoCombatAI.CombatRole.AyaneHealer:
                foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                    if (!candidate.IsDead && candidate.team == Selected.Unit.team &&
                        FlatDistance(pointer, candidate.transform.position) <= Selected.aoeRadius)
                        affected.Add(candidate);
                break;
            case AutoCombatAI.CombatRole.MomoiLowCostAOE:
            case AutoCombatAI.CombatRole.HinaHighCostAOE:
                Vector3 origin = Selected.NavMeshWorldPosition;
                Vector3 direction = pointer - origin;
                foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                    if (!candidate.IsDead && candidate.team != Selected.Unit.team &&
                        Selected.IsInsideCone(origin, direction, candidate.transform.position))
                        affected.Add(candidate);
                break;
        }
        targetHighlighter.Set(affected);
        if (targetingOverlay != null) targetingOverlay.SetHighlightHoles(affected, Camera.main);
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
        string section = enemyWaveSpawner != null
            ? enemyWaveSpawner.IsTransitioning ? "ENEMY REINFORCEMENTS INCOMING" : "WAVE " + Mathf.Max(1, enemyWaveSpawner.CurrentWave)
            : phase == StagePhase.MovingToWaveTwo ? "ADVANCING TO SECTION 2" :
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
        RefreshSelectedAttackRadius();

        AutoCombatAI selectedMember = Selected;
        if (selectedMember != null)
        {
            var unit = selectedMember.Unit;
            squadText.text = selectedMember.name + "  HP " + Mathf.CeilToInt(unit.CurrentHealth) + "/" + unit.maxHealth +
                "  Move " + Mathf.FloorToInt(unit.MovementPoints) + "/" + unit.skillPointCapacity +
                "  •  " + selectedMember.CurrentState +
                (targetMode == TargetMode.None ? "" : "  •  " +
                    (targetMode == TargetMode.Move ? "Drag to a dash position" :
                    selectedMember.characterSkillKind == AutoCombatAI.CharacterSkillKind.PowerUp ? "Click to activate" : "Click near a target"));
            RefreshCooldown(moveCooldownOverlay, moveCooldownText, moveButton,
                selectedMember.MovementCooldownRemaining, unit.IsMovementGaugeFull);
            if (!UsesSkillQueue)
                RefreshCooldown(skillCooldownOverlay, skillCooldownText, skillButton,
                    selectedMember.CharacterCooldownRemaining, universalPoints >= selectedMember.characterSkillCost);
        }
        RefreshSkillQueue();
        bool showBoss = boss != null && !boss.IsDead && phase == StagePhase.Boss;
        if (bossHealthPanel != null) bossHealthPanel.SetActive(showBoss);
        if (showBoss && bossHealthSlider != null)
        {
            bossHealthSlider.maxValue = boss.maxHealth;
            bossHealthSlider.value = boss.CurrentHealth;
            if (bossNameText != null) bossNameText.text = boss.name;
        }
        for (int i = 0; i < squadButtons.Length; i++)
        {
            AutoCombatAI member = i < deployedIndices.Count ? squad[deployedIndices[i]] : null;
            bool alive = member != null && member.Unit != null && !member.Unit.IsDead;
            squadButtons[i].interactable = alive;
            bool isSelected = alive && deployedIndices[i] == selectedIndex;
            squadButtons[i].GetComponentInChildren<Text>().text = (isSelected ? "▶ " : "") +
                (alive ? member.name : i < deployedIndices.Count ? "DOWN" : "EMPTY");
        }
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
        if (pausePanel != null) pausePanel.SetActive(paused);
        if (pauseButton != null) pauseButton.GetComponent<Image>().color =
            paused ? new Color(0.55f, 0.72f, 0.82f, 1f) : Color.white;
        SetButtonCaption(pauseButton, paused ? "▶" : "");
    }

    private void ReturnToPreparation()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(preparationSceneName);
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
        int stars = playerDeaths == 0 ? 3 : playerDeaths == 1 ? 2 : 1;
        if (won) GameProgress.CompleteLevel(levelNumber, stars);
        StartCoroutine(ShowResultAfterDelay(won, stars));
    }

    private IEnumerator ShowResultAfterDelay(bool won, int stars)
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, resultDelay));
        if (resultText != null)
        {
            resultText.text = won ? "VICTORY" : "DEFEAT";
            resultText.color = won ? victoryTitleColor : defeatTitleColor;
        }
        if (resultDetailsText != null)
            resultDetailsText.text = won
                ? new string('★', stars) + new string('☆', 3 - stars) + "\n" + playerDeaths +
                  " deployed character" + (playerDeaths == 1 ? "" : "s") + " lost"
                : "Your squad has fallen.";
        if (resultPanel == null) yield break;
        resultPanel.SetActive(true);
        if (resultCanvasGroup == null) yield break;
        resultCanvasGroup.alpha = 0f;
        float duration = Mathf.Max(0.05f, resultFadeDuration);
        for (float elapsedFade = 0f; elapsedFade < duration; elapsedFade += Time.unscaledDeltaTime)
        {
            resultCanvasGroup.alpha = Mathf.Clamp01(elapsedFade / duration);
            yield return null;
        }
        resultCanvasGroup.alpha = 1f;
    }

    private void RestartBattle()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void ReturnToLevelSelection()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrWhiteSpace(levelSelectionSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(levelSelectionSceneName);
    }
}
