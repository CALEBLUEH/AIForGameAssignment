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
            ApplyLamZHTemplate();
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
        ApplyLamZHTemplate();
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
            director.squadButtons.Length != 4 || director.timerText == null ||
            director.enemyText == null || director.energySegments == null ||
            director.energySegments.Length != 10 || director.pauseButton == null ||
            director.speedButton == null || director.autoButton == null)
            throw new System.Exception("Canvas HUD is incomplete.");
        int players = 0, enemies = 0;
        foreach (var unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (!NavMesh.SamplePosition(unit.transform.position, out _, 2f, NavMesh.AllAreas))
                throw new System.Exception(unit.name + " is not on the baked NavMesh.");
            if (unit.team == CombatUnit.CombatTeam.Player) players++; else enemies++;
        }
        if (players != 4 || enemies != 3) throw new System.Exception("Unexpected initial unit count.");
        Debug.Log("AIFG scene validation passed: LamZH HUD, 4 players, 3 enemies, NavMesh paths, no NavMeshAgent.");
    }

    [MenuItem("AIFG/Apply LamZH UI Template")]
    public static void ApplyLamZHTemplate()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameplayAI.unity");
        var director = Object.FindFirstObjectByType<BattleDirector>();
        if (director == null) throw new System.Exception("BattleDirector is missing.");
        var oldCanvas = GameObject.Find("Battle Canvas");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);
        var oldEventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (oldEventSystem != null) Object.DestroyImmediate(oldEventSystem.gameObject);

        var canvasObject = new GameObject("Battle Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Color navy = new Color(0.035f, 0.095f, 0.17f, 0.94f);
        Color blue = new Color(0.08f, 0.28f, 0.48f, 0.96f);
        Color cyan = new Color(0.10f, 0.72f, 0.92f, 1f);

        // Pre-battle selection keeps the same workflow with LamZH's blue tactical styling.
        director.setupPanel = Panel(canvasObject.transform, "Setup Panel", new Vector2(680, 560),
            new Vector2(0.5f, 0.5f), navy);
        AddAccent(director.setupPanel.transform, new Vector2(680, 8), new Vector2(0, 276), cyan);
        director.setupText = Label(director.setupPanel.transform, "Setup Instructions", font,
            new Vector2(610, 96), new Vector2(0, 215), 24, TextAnchor.MiddleCenter);
        director.setupButtons = new Button[4];
        director.squadButtons = new Button[4];
        for (int i = 0; i < 4; i++)
            director.setupButtons[i] = StyledButton(director.setupPanel.transform, "Squad Slot " + (i + 1),
                font, new Vector2(540, 56), new Vector2(0, 120 - i * 62), blue, 19);
        director.leaderButton = StyledButton(director.setupPanel.transform, "Leader", font,
            new Vector2(540, 50), new Vector2(0, -145), new Color(0.12f, 0.42f, 0.62f, 1f), 19);
        director.startButton = StyledButton(director.setupPanel.transform, "START ASSAULT", font,
            new Vector2(540, 62), new Vector2(0, -220), new Color(0.98f, 0.67f, 0.05f, 1f), 22);

        // LamZH gameplay layout: level top-left, enemy/timer/pause top-right,
        // cost segments and action cards on the lower-right.
        director.battlePanel = Panel(canvasObject.transform, "Battle HUD", new Vector2(1600, 900),
            new Vector2(0.5f, 0.5f), Color.clear);
        var level = Panel(director.battlePanel.transform, "Level Badge", new Vector2(230, 62),
            new Vector2(0.5f, 0.5f), navy);
        level.GetComponent<RectTransform>().anchoredPosition = new Vector2(-670, 395);
        Label(level.transform, "Level Text", font, new Vector2(210, 55), Vector2.zero, 24,
            TextAnchor.MiddleCenter).text = "ASSAULT 1-2";

        var enemyPanel = Panel(director.battlePanel.transform, "Enemy Counter", new Vector2(150, 58),
            new Vector2(0.5f, 0.5f), navy);
        enemyPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(485, 395);
        Icon(enemyPanel.transform, "Enemy Icon", Sprite("Assets/UI/Enemy Icon.png"),
            new Vector2(48, 48), new Vector2(-45, 0));
        director.enemyText = Label(enemyPanel.transform, "Enemy Left", font, new Vector2(75, 50),
            new Vector2(30, 0), 25, TextAnchor.MiddleCenter);

        var timerPanel = Panel(director.battlePanel.transform, "Timer Panel", new Vector2(165, 58),
            new Vector2(0.5f, 0.5f), navy);
        timerPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(645, 395);
        Icon(timerPanel.transform, "Timer Icon", Sprite("Assets/UI/Timer Icon.png"),
            new Vector2(45, 45), new Vector2(-52, 0));
        director.timerText = Label(timerPanel.transform, "Timer", font, new Vector2(100, 50),
            new Vector2(25, 0), 25, TextAnchor.MiddleCenter);
        director.pauseButton = SpriteButton(director.battlePanel.transform, "Pause Button",
            Sprite("Assets/UI/Pause Button.png"), new Vector2(72, 72), new Vector2(752, 392), font, "");

        director.statusText = Label(director.battlePanel.transform, "Stage Status", font,
            new Vector2(700, 40), new Vector2(-420, 350), 19, TextAnchor.MiddleLeft);
        var info = Panel(director.battlePanel.transform, "Selected Unit Panel", new Vector2(650, 84),
            new Vector2(0.5f, 0.5f), navy);
        info.GetComponent<RectTransform>().anchoredPosition = new Vector2(-455, -390);
        director.squadText = Label(info.transform, "Unit Status", font, new Vector2(610, 70),
            Vector2.zero, 19, TextAnchor.MiddleLeft);

        for (int i = 0; i < 4; i++)
        {
            director.squadButtons[i] = StyledButton(director.battlePanel.transform,
                "Unit Card " + (i + 1), font, new Vector2(145, 90),
                new Vector2(110 + i * 152, -300), i == 2
                    ? new Color(0.18f, 0.48f, 0.62f, 0.98f)
                    : new Color(0.10f, 0.25f, 0.40f, 0.98f), 18);
        }
        director.moveButton = StyledButton(director.battlePanel.transform, "Move Skill", font,
            new Vector2(145, 62), new Vector2(110, -200), blue, 17);
        director.moveButton.GetComponentInChildren<Text>().text = "MOVE";
        director.skillButton = StyledButton(director.battlePanel.transform, "Character Skill", font,
            new Vector2(145, 62), new Vector2(262, -200), new Color(0.18f, 0.48f, 0.62f, 1f), 16);
        director.skillButton.GetComponentInChildren<Text>().text = "SKILL";
        director.healButton = StyledButton(director.battlePanel.transform, "Support Heal", font,
            new Vector2(145, 62), new Vector2(414, -200), new Color(0.15f, 0.55f, 0.42f, 1f), 16);
        director.healButton.GetComponentInChildren<Text>().text = "HEAL 25";
        director.coverButton = StyledButton(director.battlePanel.transform, "Support Cover", font,
            new Vector2(145, 62), new Vector2(566, -200), new Color(0.62f, 0.40f, 0.12f, 1f), 16);
        director.coverButton.GetComponentInChildren<Text>().text = "COVER 35";

        var costBadge = Panel(director.battlePanel.transform, "Cost Badge", new Vector2(82, 82),
            new Vector2(0.5f, 0.5f), navy);
        costBadge.GetComponent<RectTransform>().anchoredPosition = new Vector2(75, -410);
        Label(costBadge.transform, "Cost Caption", font, new Vector2(75, 24), new Vector2(0, 22),
            14, TextAnchor.MiddleCenter).text = "COST";
        director.energyText = Label(costBadge.transform, "Energy Count", font, new Vector2(75, 46),
            new Vector2(0, -10), 28, TextAnchor.MiddleCenter);
        director.energySegments = new Image[10];
        for (int i = 0; i < director.energySegments.Length; i++)
        {
            var segment = Panel(director.battlePanel.transform, "Energy Segment " + (i + 1),
                new Vector2(48, 22), new Vector2(0.5f, 0.5f),
                new Color(0.12f, 0.78f, 0.96f, 1f));
            segment.GetComponent<RectTransform>().anchoredPosition = new Vector2(140 + i * 52, -417);
            director.energySegments[i] = segment.GetComponent<Image>();
        }
        director.speedButton = SpriteButton(director.battlePanel.transform, "Speed Button",
            Sprite("Assets/UI/SpeedUpButton.png"), new Vector2(150, 76), new Vector2(695, -285), font, "1x");
        director.autoButton = SpriteButton(director.battlePanel.transform, "Auto Button",
            Sprite("Assets/UI/Auto Button.png"), new Vector2(150, 76), new Vector2(695, -380), font, "");

        director.resultPanel = Panel(canvasObject.transform, "Result Panel", new Vector2(620, 320),
            new Vector2(0.5f, 0.5f), navy);
        AddAccent(director.resultPanel.transform, new Vector2(620, 8), new Vector2(0, 156), cyan);
        director.resultText = Label(director.resultPanel.transform, "Result", font,
            new Vector2(560, 165), new Vector2(0, 42), 34, TextAnchor.MiddleCenter);
        director.restartButton = StyledButton(director.resultPanel.transform, "Restart", font,
            new Vector2(450, 60), new Vector2(0, -105), new Color(0.98f, 0.67f, 0.05f, 1f), 21);
        director.restartButton.GetComponentInChildren<Text>().text = "RESTART";
        director.battlePanel.SetActive(false);
        director.resultPanel.SetActive(false);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Applied LamZH UI template to GameplayAI.");
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

    private static Button StyledButton(Transform parent, string name, Font font, Vector2 size,
        Vector2 position, Color color, int fontSize)
    {
        var button = Button(parent, name, font, size, position);
        button.GetComponent<Image>().color = color;
        var label = button.GetComponentInChildren<Text>();
        label.fontSize = fontSize;
        label.text = name;
        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        button.colors = colors;
        return button;
    }

    private static Button SpriteButton(Transform parent, string name, Sprite sprite, Vector2 size,
        Vector2 position, Font font, string caption)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        if (!string.IsNullOrEmpty(caption))
        {
            var label = Label(obj.transform, "State", font, size, Vector2.zero, 17, TextAnchor.MiddleCenter);
            label.text = caption;
            label.color = new Color(0.05f, 0.12f, 0.20f, 1f);
        }
        return obj.GetComponent<Button>();
    }

    private static void Icon(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void AddAccent(Transform parent, Vector2 size, Vector2 position, Color color)
    {
        var accent = Panel(parent, "Accent", size, new Vector2(0.5f, 0.5f), color);
        accent.GetComponent<RectTransform>().anchoredPosition = position;
        accent.GetComponent<Image>().raycastTarget = false;
    }

    private static Sprite Sprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.Exception("Missing LamZH UI sprite: " + path);
        return sprite;
    }
}
