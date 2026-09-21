using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BuildPlayableScene
{
    private static int smokeStep;
    private static double smokeWaitStarted;
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
            director.speedButton == null || director.autoButton == null ||
            director.manaSlider == null || director.bossHealthSlider == null ||
            director.targetingFeedback == null)
            throw new System.Exception("Canvas HUD is incomplete.");
        if (GameObject.Find("Linear Stage Environment") == null ||
            Object.FindObjectsByType<CoverPoint>(FindObjectsSortMode.None).Length < 6 ||
            Camera.main == null || Camera.main.GetComponent<TacticalCameraFollow>() == null)
            throw new System.Exception("Linear stage environment is incomplete.");
        int players = 0, enemies = 0;
        foreach (var unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (!NavMesh.SamplePosition(unit.transform.position, out _, 2f, NavMesh.AllAreas))
                throw new System.Exception(unit.name + " is not on the baked NavMesh.");
            if (unit.team == CombatUnit.CombatTeam.Player) players++; else enemies++;
        }
        if (players != 4 || enemies != 3) throw new System.Exception("Unexpected initial unit count.");
        if (!NavMesh.SamplePosition(new Vector3(-10f, 0f, 0f), out NavMeshHit roadStart, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(new Vector3(57f, 0f, 0f), out NavMeshHit bossSection, 2f, NavMesh.AllAreas))
            throw new System.Exception("Road endpoints are not on the NavMesh.");
        var sectionPath = new NavMeshPath();
        if (!NavMesh.CalculatePath(roadStart.position, bossSection.position, NavMesh.AllAreas, sectionPath) ||
            sectionPath.status != NavMeshPathStatus.PathComplete)
            throw new System.Exception("The three road sections are not connected.");
        if (NavMesh.SamplePosition(new Vector3(25f, 0f, 14f), out _, 1f, NavMesh.AllAreas))
            throw new System.Exception("Roadside area is unexpectedly walkable.");
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
        var oldFeedback = GameObject.Find("Skill Targeting Feedback");
        if (oldFeedback != null) Object.DestroyImmediate(oldFeedback);

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

        director.bossHealthPanel = Panel(director.battlePanel.transform, "Boss Health Panel",
            new Vector2(560, 82), new Vector2(0.5f, 0.5f), navy);
        director.bossHealthPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 388);
        director.bossNameText = Label(director.bossHealthPanel.transform, "Boss Name", font,
            new Vector2(520, 30), new Vector2(0, 21), 19, TextAnchor.MiddleCenter);
        director.bossHealthSlider = CreateSlider(director.bossHealthPanel.transform, "Boss HP",
            new Vector2(510, 24), new Vector2(0, -18), new Color(0.88f, 0.12f, 0.18f, 1f));
        director.bossHealthPanel.SetActive(false);

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
        AddCooldownOverlay(director.moveButton.transform, font,
            out director.moveCooldownOverlay, out director.moveCooldownText);
        AddCooldownOverlay(director.skillButton.transform, font,
            out director.skillCooldownOverlay, out director.skillCooldownText);

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
        director.manaSlider = CreateSlider(director.battlePanel.transform, "Mana Slider",
            new Vector2(515, 18), new Vector2(390, -382), cyan);
        director.manaSlider.minValue = 0f;
        director.manaSlider.maxValue = director.universalCapacity;
        director.manaSlider.value = director.universalCapacity;
        director.speedButton = SpriteButton(director.battlePanel.transform, "Speed Button",
            Sprite("Assets/UI/SpeedUpButton.png"), new Vector2(150, 76), new Vector2(695, -285), font, "1x");
        director.autoButton = SpriteButton(director.battlePanel.transform, "Auto Button",
            Sprite("Assets/UI/Auto Button.png"), new Vector2(150, 76), new Vector2(695, -380), font, "");
        director.targetingFeedback = CreateTargetingFeedback(director.transform);

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

    [MenuItem("AIFG/Build Linear Three Section Stage")]
    public static void BuildLinearStage()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameplayAI.unity");
        GameObject old = GameObject.Find("Linear Stage Environment");
        if (old != null) Object.DestroyImmediate(old);
        GameObject plane = GameObject.Find("Plane");
        if (plane != null) Object.DestroyImmediate(plane);
        var root = new GameObject("Linear Stage Environment");
        Material road = GetOrCreateMaterial("Assets/Materials/LinearRoad.mat", new Color(0.12f, 0.15f, 0.20f));
        Material edge = GetOrCreateMaterial("Assets/Materials/RoadEdge.mat", new Color(0.22f, 0.30f, 0.36f));
        Material cover = GetOrCreateMaterial("Assets/Materials/Cover.mat", new Color(0.12f, 0.48f, 0.64f));
        Material building = GetOrCreateMaterial("Assets/Materials/Building.mat", new Color(0.17f, 0.20f, 0.27f));
        Material marker = GetOrCreateMaterial("Assets/Materials/SectionMarker.mat", new Color(0.95f, 0.68f, 0.08f));

        CreateBlock(root.transform, "Walkable Road", new Vector3(25f, -0.5f, 0f),
            new Vector3(90f, 1f, 18f), road, 9);
        CreateBlock(root.transform, "North Boundary", new Vector3(25f, 1.5f, 10f),
            new Vector3(90f, 4f, 2f), edge, 8);
        CreateBlock(root.transform, "South Boundary", new Vector3(25f, 1.5f, -10f),
            new Vector3(90f, 4f, 2f), edge, 8);
        for (int i = 0; i < 9; i++)
        {
            float x = -14f + i * 11f;
            CreateBlock(root.transform, "North Building " + i, new Vector3(x, 3f, 14f),
                new Vector3(8f, 6f + (i % 3) * 2f, 6f), building, 8);
            CreateBlock(root.transform, "South Building " + i, new Vector3(x, 3f, -14f),
                new Vector3(8f, 6f + ((i + 1) % 3) * 2f, 6f), building, 8);
        }
        CreateBlock(root.transform, "Section 2 Line", new Vector3(18f, 0.02f, 0f),
            new Vector3(0.35f, 0.04f, 18f), marker, 9);
        CreateBlock(root.transform, "Boss Section Line", new Vector3(46f, 0.02f, 0f),
            new Vector3(0.35f, 0.04f, 18f), marker, 9);
        float[] coverX = { 2f, 10f, 23f, 31f, 43f, 51f };
        for (int i = 0; i < coverX.Length; i++)
        {
            float z = i % 2 == 0 ? -2.8f : 2.8f;
            GameObject block = CreateBlock(root.transform, "Cover Barrier " + (i + 1),
                new Vector3(coverX[i], 0.6f, z), new Vector3(0.9f, 1.2f, 4.2f), cover, 8);
            var point = new GameObject("Cover Point " + (i + 1));
            point.transform.SetParent(root.transform);
            point.transform.position = block.transform.position + new Vector3(-1.25f, -0.55f, 0f);
            point.AddComponent<CoverPoint>();
        }

        string[] players = { "Aegis", "Ranger", "Medic", "Vanguard" };
        for (int i = 0; i < players.Length; i++)
        {
            GameObject player = GameObject.Find(players[i]);
            if (player != null) player.transform.position = new Vector3(-10f - (i % 2) * 2f, 1f,
                (i < 2 ? -2.5f : 2.5f) + (i % 2) * 1.5f);
        }
        for (int i = 1; i <= 3; i++)
        {
            GameObject enemy = GameObject.Find("Raider " + i);
            if (enemy != null) enemy.transform.position = new Vector3(6f + i * 2.2f, 1f,
                (i - 2) * 3f);
        }
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(-18f, 25f, -20f);
            camera.fieldOfView = 52f;
            var follow = camera.GetComponent<TacticalCameraFollow>() ?? camera.gameObject.AddComponent<TacticalCameraFollow>();
            follow.offset = new Vector3(-8f, 25f, -20f);
            follow.lookAhead = new Vector3(8f, 0f, 0f);
        }
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null) throw new System.Exception("NavMeshSurface is missing.");
        surface.layerMask = (1 << 8) | (1 << 9);
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        const string navMeshPath = "Assets/Scenes/GameplayAI/NavMesh-LinearRoad.asset";
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(navMeshPath) != null)
            AssetDatabase.DeleteAsset(navMeshPath);
        if (surface.navMeshData != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(surface.navMeshData)))
        {
            AssetDatabase.CreateAsset(surface.navMeshData, navMeshPath);
            AssetDatabase.SaveAssets();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Built linear road with wave-one, wave-two and boss sections.");
    }

    public static void SmokeThreeSectionStage()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayAI.unity");
        smokeStep = 0;
        EditorApplication.playModeStateChanged += SmokePlayModeChanged;
        EditorApplication.EnterPlaymode();
    }

    private static void SmokePlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        smokeWaitStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update += SmokeStageTick;
    }

    private static void SmokeStageTick()
    {
        try
        {
            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (director == null) return;
            double waited = EditorApplication.timeSinceStartup - smokeWaitStarted;
            if (smokeStep == 0 && waited > 0.5)
            {
                director.startButton.onClick.Invoke();
                if (!director.IsPlaying || !director.battlePanel.activeSelf)
                    throw new System.Exception("Battle did not start from the Canvas button.");
                director.skillButton.onClick.Invoke();
                AutoCombatAI aegis = GameObject.Find("Aegis").GetComponent<AutoCombatAI>();
                if (!director.ResolveTargetAt(aegis.transform.position))
                    throw new System.Exception("Character skill targeting did not resolve.");
                if (aegis.CharacterCooldownRemaining <= 0f)
                    throw new System.Exception("Character skill cooldown did not start.");
                DestroyAllEnemies();
                smokeStep = 1;
                smokeWaitStarted = EditorApplication.timeSinceStartup;
            }
            else if (smokeStep == 1 && waited > 3.2)
            {
                int enemies = CountRuntimeEnemies(out bool hasBoss);
                if (enemies != director.waveTwoCount || hasBoss)
                    throw new System.Exception("Wave two did not spawn in the second section.");
                DestroyAllEnemies();
                smokeStep = 2;
                smokeWaitStarted = EditorApplication.timeSinceStartup;
            }
            else if (smokeStep == 2 && waited > 3.2)
            {
                int enemies = CountRuntimeEnemies(out bool hasBoss);
                if (enemies != 1 || !hasBoss)
                    throw new System.Exception("Boss did not spawn after wave two.");
                if (!director.bossHealthPanel.activeSelf || director.bossHealthSlider.value <= 0f)
                    throw new System.Exception("Boss health UI did not activate.");
                if (director.manaSlider == null || CoverPoint.All.Count < 6)
                    throw new System.Exception("Mana or cover gameplay objects are missing.");
                Debug.Log("AIFG play-mode smoke passed: skill cooldown, wave 1, wave 2, boss, mana and cover.");
                EditorApplication.update -= SmokeStageTick;
                EditorApplication.Exit(0);
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.update -= SmokeStageTick;
            EditorApplication.Exit(1);
        }
    }

    private static void DestroyAllEnemies()
    {
        foreach (CombatUnit unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (unit.team == CombatUnit.CombatTeam.Enemy) unit.TakeDamage(100000f);
    }

    private static int CountRuntimeEnemies(out bool hasBoss)
    {
        int count = 0;
        hasBoss = false;
        foreach (CombatUnit unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (unit.team != CombatUnit.CombatTeam.Enemy || unit.IsDead) continue;
            count++;
            hasBoss |= unit.IsBoss;
        }
        return count;
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

    private static Slider CreateSlider(Transform parent, string name, Vector2 size, Vector2 position, Color fillColor)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = size;
        rootRect.anchoredPosition = position;
        var background = Panel(root.transform, "Background", size, new Vector2(0.5f, 0.5f),
            new Color(0.07f, 0.10f, 0.16f, 0.95f));
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3, 3);
        fillAreaRect.offsetMax = new Vector2(-3, -3);
        var fill = Panel(fillArea.transform, "Fill", Vector2.zero, new Vector2(0.5f, 0.5f), fillColor);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        var slider = root.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.targetGraphic = background.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = false;
        return slider;
    }

    private static void AddCooldownOverlay(Transform parent, Font font, out Image overlay, out Text timer)
    {
        var rect = parent.GetComponent<RectTransform>();
        var cover = Panel(parent, "Cooldown Overlay", rect.sizeDelta, new Vector2(0.5f, 0.5f),
            new Color(0.18f, 0.20f, 0.24f, 0.82f));
        cover.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        overlay = cover.GetComponent<Image>();
        overlay.raycastTarget = false;
        timer = Label(parent, "Cooldown Timer", font, rect.sizeDelta, Vector2.zero, 29, TextAnchor.MiddleCenter);
        timer.fontStyle = FontStyle.Bold;
        timer.raycastTarget = false;
        cover.SetActive(false);
    }

    private static SkillTargetingFeedback CreateTargetingFeedback(Transform parent)
    {
        var root = new GameObject("Skill Targeting Feedback");
        root.transform.SetParent(parent);
        var feedback = root.AddComponent<SkillTargetingFeedback>();
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Transparent Skill Range";
        disc.transform.SetParent(root.transform);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        Material rangeMaterial = GetOrCreateMaterial("Assets/Materials/SkillRange.mat",
            new Color(0.08f, 0.75f, 1f, 0.22f), true);
        feedback.rangeRenderer = disc.GetComponent<MeshRenderer>();
        feedback.rangeRenderer.sharedMaterial = rangeMaterial;
        var arrow = new GameObject("Movement Arrow", typeof(LineRenderer));
        arrow.transform.SetParent(root.transform);
        feedback.arrowRenderer = arrow.GetComponent<LineRenderer>();
        feedback.arrowRenderer.useWorldSpace = true;
        feedback.arrowRenderer.widthMultiplier = 0.18f;
        feedback.arrowRenderer.numCapVertices = 5;
        feedback.arrowRenderer.sharedMaterial = GetOrCreateMaterial("Assets/Materials/MovementArrow.mat",
            new Color(1f, 0.72f, 0.08f, 0.95f), true);
        var particles = new GameObject("Skill Feedback Particles", typeof(ParticleSystem));
        particles.transform.SetParent(root.transform);
        feedback.feedbackParticles = particles.GetComponent<ParticleSystem>();
        var main = feedback.feedbackParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.7f;
        main.startLifetime = 0.55f;
        main.startSpeed = 4.5f;
        main.startSize = 0.28f;
        main.maxParticles = 80;
        var emission = feedback.feedbackParticles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });
        var shape = feedback.feedbackParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.7f;
        return feedback;
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale,
        Material material, int layer)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent);
        block.transform.position = position;
        block.transform.localScale = scale;
        block.layer = layer;
        block.GetComponent<MeshRenderer>().sharedMaterial = material;
        return block;
    }

    private static Material GetOrCreateMaterial(string path, Color color, bool transparent = false)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find(transparent ? "Sprites/Default" : "Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }
}
