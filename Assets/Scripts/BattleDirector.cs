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
    public Text setupText, statusText, squadText, resultText;
    public Button[] setupButtons, squadButtons;
    public Button leaderButton, startButton, moveButton, skillButton, healButton, coverButton, restartButton;
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
    private bool timedSpawned, secondWaveSpawned, finished;
    private GameObject enemyTemplate;
    private enum TargetMode { None, Move, Skill, Cover }
    private TargetMode targetMode;
    public bool IsPlaying => isPlaying;
    public float UniversalPoints => universalPoints;

    private void Awake()
    {
        Instance = this;
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
        moveButton.onClick.AddListener(() => targetMode = TargetMode.Move);
        skillButton.onClick.AddListener(UseCharacterSkill);
        healButton.onClick.AddListener(UseHeal);
        coverButton.onClick.AddListener(() => targetMode = TargetMode.Cover);
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
        if (!timedSpawned && elapsed >= timedEnemyDelay)
        { timedSpawned = true; SpawnEnemy(new Vector3(18f, 1f, 18f)); }
        int enemies = CountAlive(CombatUnit.CombatTeam.Enemy);
        int allies = CountAlive(CombatUnit.CombatTeam.Player);
        if (allies == 0) { Finish(false); return; }
        if (!secondWaveSpawned && enemies == 0)
        {
            secondWaveSpawned = true;
            wave = 2;
            for (int i = 0; i < waveTwoCount; i++)
                SpawnEnemy(new Vector3(13f + i * 2f, 1f, 16f + (i % 2) * 2f));
        }
        else if (secondWaveSpawned && enemies == 0 && timedSpawned) { Finish(true); return; }
        if (Input.GetMouseButtonDown(0) && targetMode != TargetMode.None &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            HandleWorldClick();
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

    private void SelectMember(int index)
    {
        if (index < squad.Count && squad[index] != null && squad[index].gameObject.activeInHierarchy)
        { selectedIndex = index; targetMode = TargetMode.None; }
    }

    private AutoCombatAI Selected => selectedIndex < squad.Count ? squad[selectedIndex] : null;

    private void UseCharacterSkill()
    {
        if (Selected == null || Selected.Unit.IsDead) return;
        if (Selected.characterSkillKind == AutoCombatAI.CharacterSkillKind.PowerUp)
            Selected.TryCharacterSkill(null);
        else targetMode = TargetMode.Skill;
    }

    private void UseHeal()
    {
        if (Selected == null || Selected.Unit.IsDead || !TrySpendUniversal(healCost)) return;
        Selected.Unit.Heal(35f);
    }

    private void HandleWorldClick()
    {
        if (Selected == null || Selected.Unit.IsDead) { targetMode = TargetMode.None; return; }
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;
        if (targetMode == TargetMode.Move) Selected.TryMovementSkill(hit.point);
        else if (targetMode == TargetMode.Skill)
        {
            var target = hit.collider.GetComponentInParent<CombatUnit>();
            Selected.TryCharacterSkill(target);
        }
        else if (targetMode == TargetMode.Cover && TrySpendUniversal(coverCost))
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Placed Cover";
            block.transform.position = hit.point + Vector3.up * 0.9f;
            block.transform.localScale = new Vector3(2.5f, 1.8f, 0.7f);
            block.layer = 8;
            var obstacle = block.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
        }
        targetMode = TargetMode.None;
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
        statusText.text = "ASSAULT  •  Wave " + wave + "  •  Enemies " + CountAlive(CombatUnit.CombatTeam.Enemy) +
            "  •  Universal " + Mathf.FloorToInt(universalPoints) + "/" + universalCapacity;
        if (Selected != null)
        {
            var unit = Selected.Unit;
            squadText.text = Selected.name + "  HP " + Mathf.CeilToInt(unit.CurrentHealth) + "/" + unit.maxHealth +
                "  Move " + Mathf.FloorToInt(unit.MovementPoints) + "/" + unit.skillPointCapacity +
                "  •  " + Selected.CurrentState +
                (targetMode == TargetMode.None ? "" : "  •  Click a " + (targetMode == TargetMode.Move || targetMode == TargetMode.Cover ? "position" : "target"));
        }
        for (int i = 0; i < squadButtons.Length && i < squad.Count; i++)
            squadButtons[i].GetComponentInChildren<Text>().text =
                (i == selectedIndex ? "▶ " : "") + squad[i].name;
    }

    private void Finish(bool won)
    {
        if (finished) return;
        finished = true;
        isPlaying = false;
        resultText.text = won ? "VICTORY\nThe assault is complete." : "DEFEAT\nYour squad has fallen.";
        battlePanel.SetActive(false);
        resultPanel.SetActive(true);
    }
}
