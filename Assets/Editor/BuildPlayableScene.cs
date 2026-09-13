using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BuildPlayableScene
{
    [MenuItem("AIFG/Build Playable Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameplayAI.unity");
        if (GameObject.Find("Battle Canvas") != null)
        {
            Validate();
            return;
        }
        foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            Object.DestroyImmediate(agent);

        var leader = GameObject.Find("Leader");
        var first = GameObject.Find("Member");
        var second = GameObject.Find("Member (1)");
        var enemies = new[] { GameObject.Find("E1"), GameObject.Find("E2"), GameObject.Find("E3") };
        leader.name = "Aegis";
        first.name = "Ranger";
        second.name = "Medic";
        leader.transform.position = new Vector3(-5f, 1f, 0f);
        first.transform.position = new Vector3(-7f, 1f, 2f);
        second.transform.position = new Vector3(-7f, 1f, -2f);
        var fourth = Object.Instantiate(first, new Vector3(-9f, 1f, 0f), Quaternion.identity);
        fourth.name = "Vanguard";
        fourth.transform.SetParent(first.transform.parent);
        var players = new[] { leader, first, second, fourth };
        if (leader.GetComponent<SquadMember>() == null) leader.AddComponent<SquadMember>();
        for (int i = 0; i < players.Length; i++)
        {
            var ai = players[i].GetComponent<AutoCombatAI>();
            ai.sightBlockers = 1 << 8;
            ai.movementKind = (AutoCombatAI.MovementKind)(i % 3);
            ai.characterSkillKind = (AutoCombatAI.CharacterSkillKind)(i % 3);
            var member = players[i].GetComponent<SquadMember>();
            if (member != null) member.leader = leader.transform;
        }
        second.GetComponent<AutoCombatAI>().characterSkillKind = AutoCombatAI.CharacterSkillKind.Heal;
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].name = "Raider " + (i + 1);
            enemies[i].transform.position = new Vector3(8f + 2f * i, 1f, (i - 1) * 3f);
            enemies[i].GetComponent<AutoCombatAI>().sightBlockers = 1 << 8;
        }

        var manager = GameObject.Find("GameManager");
        var director = manager.GetComponent<BattleDirector>() ?? manager.AddComponent<BattleDirector>();
        var canvasObject = new GameObject("Battle Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        director.setupPanel = Panel(canvasObject.transform, "Setup Panel", new Vector2(620, 510),
            new Vector2(0.5f, 0.5f), new Color(0.05f, 0.09f, 0.15f, 0.94f));
        director.setupText = Label(director.setupPanel.transform, "Setup Instructions", font,
            new Vector2(570, 90), new Vector2(0, 190), 23, TextAnchor.MiddleCenter);
        director.setupButtons = new Button[4];
        director.squadButtons = new Button[4];
        for (int i = 0; i < 4; i++)
            director.setupButtons[i] = Button(director.setupPanel.transform, "Slot " + (i + 1),
                font, new Vector2(500, 53), new Vector2(0, 108 - i * 57));
        director.leaderButton = Button(director.setupPanel.transform, "Leader Button", font,
            new Vector2(500, 48), new Vector2(0, -133));
        director.startButton = Button(director.setupPanel.transform, "Start Button", font,
            new Vector2(500, 58), new Vector2(0, -199));
        director.startButton.GetComponentInChildren<Text>().text = "START ASSAULT";

        director.battlePanel = Panel(canvasObject.transform, "Battle HUD", new Vector2(1540, 185),
            new Vector2(0.5f, 0f), new Color(0.05f, 0.09f, 0.15f, 0.88f));
        var battleRect = director.battlePanel.GetComponent<RectTransform>();
        battleRect.anchorMin = new Vector2(0.5f, 0f);
        battleRect.anchorMax = new Vector2(0.5f, 0f);
        battleRect.anchoredPosition = new Vector2(0, 103);
        director.statusText = Label(director.battlePanel.transform, "Stage Status", font,
            new Vector2(1460, 34), new Vector2(0, 73), 22, TextAnchor.MiddleLeft);
        director.squadText = Label(director.battlePanel.transform, "Unit Status", font,
            new Vector2(1460, 32), new Vector2(0, 34), 20, TextAnchor.MiddleLeft);
        for (int i = 0; i < 4; i++)
        {
            var battleButton = Button(director.battlePanel.transform, "Select Unit " + (i + 1),
                font, new Vector2(212, 45), new Vector2(-628 + i * 220, -25));
            director.squadButtons[i] = battleButton;
        }
        director.moveButton = Button(director.battlePanel.transform, "Move Skill", font,
            new Vector2(195, 45), new Vector2(320, -25));
        director.moveButton.GetComponentInChildren<Text>().text = "MOVE → CLICK";
        director.skillButton = Button(director.battlePanel.transform, "Character Skill", font,
            new Vector2(195, 45), new Vector2(525, -25));
        director.skillButton.GetComponentInChildren<Text>().text = "CHARACTER SKILL";
        director.healButton = Button(director.battlePanel.transform, "Support Heal", font,
            new Vector2(195, 45), new Vector2(320, -75));
        director.healButton.GetComponentInChildren<Text>().text = "HEAL 25";
        director.coverButton = Button(director.battlePanel.transform, "Support Cover", font,
            new Vector2(195, 45), new Vector2(525, -75));
        director.coverButton.GetComponentInChildren<Text>().text = "COVER 35 → CLICK";
        Label(director.battlePanel.transform, "Help", font, new Vector2(800, 34),
            new Vector2(-310, -75), 17, TextAnchor.MiddleLeft).text =
            "Select unit • Move and cover: click ground • Burst and heal skill: click target";

        director.resultPanel = Panel(canvasObject.transform, "Result Panel", new Vector2(600, 300),
            new Vector2(0.5f, 0.5f), new Color(0.05f, 0.09f, 0.15f, 0.94f));
        director.resultText = Label(director.resultPanel.transform, "Result", font,
            new Vector2(540, 150), new Vector2(0, 45), 32, TextAnchor.MiddleCenter);
        director.restartButton = Button(director.resultPanel.transform, "Restart", font,
            new Vector2(420, 58), new Vector2(0, -95));
        director.restartButton.GetComponentInChildren<Text>().text = "RESTART";
        director.battlePanel.SetActive(false);
        director.resultPanel.SetActive(false);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
        Debug.Log("AIFG playable scene built without NavMeshAgent.");
    }

    [MenuItem("AIFG/Validate Playable Scene")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayAI.unity");
        if (Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Length != 0)
            throw new System.Exception("NavMeshAgent remains in the scene.");
        var director = Object.FindFirstObjectByType<BattleDirector>();
        if (director == null || director.setupPanel == null || director.battlePanel == null ||
            director.resultPanel == null || director.setupButtons.Length != 4 ||
            director.squadButtons.Length != 4)
            throw new System.Exception("Canvas HUD is incomplete.");
        int players = 0, enemies = 0;
        foreach (var unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (!NavMesh.SamplePosition(unit.transform.position, out _, 2f, NavMesh.AllAreas))
                throw new System.Exception(unit.name + " is not on the baked NavMesh.");
            if (unit.team == CombatUnit.CombatTeam.Player) players++; else enemies++;
        }
        if (players != 4 || enemies != 3) throw new System.Exception("Unexpected initial unit count.");
        Debug.Log("AIFG scene validation passed: 4 players, 3 enemies, Canvas HUD, NavMesh paths, no NavMeshAgent.");
    }


    private static GameObject Panel(Transform parent, string name, Vector2 size, Vector2 anchor, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        obj.GetComponent<Image>().color = color;
        return obj;
    }
    private static Text Label(Transform parent, string name, Font font, Vector2 size, Vector2 position,
        int fontSize, TextAnchor alignment)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var label = obj.GetComponent<Text>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        return label;
    }
    private static Button Button(Transform parent, string name, Font font, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        obj.GetComponent<Image>().color = new Color(0.16f, 0.36f, 0.48f, 1f);
        var label = Label(obj.transform, "Text", font, size - new Vector2(8, 4), Vector2.zero,
            19, TextAnchor.MiddleCenter);
        label.text = name;
        return obj.GetComponent<Button>();
    }
}
