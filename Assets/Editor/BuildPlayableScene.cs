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
            director.targetingFeedback == null ||
            director.battlePanel.GetComponent<Image>().raycastTarget)
            throw new System.Exception("Canvas HUD is incomplete.");
        if (GameObject.Find("Linear Stage Environment") == null ||
            Object.FindObjectsByType<CoverPoint>(FindObjectsSortMode.None).Length < 6 ||
            Object.FindObjectsByType<BlockingObstacle>(FindObjectsSortMode.None).Length < 10 ||
            Camera.main == null || Camera.main.GetComponent<TacticalCameraFollow>() == null)
            throw new System.Exception("Linear stage environment is incomplete.");
        int players = 0, enemies = 0;
        foreach (var unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (!NavMesh.SamplePosition(unit.transform.position, out _, 2f, NavMesh.AllAreas))
                throw new System.Exception(unit.name + " is not on the baked NavMesh.");
            var worldHUD = unit.GetComponentInChildren<WorldUnitHUD>(true);
            if (worldHUD == null || worldHUD.healthSlider == null || worldHUD.damageLabels == null ||
                worldHUD.damageLabels.Length < 4 || worldHUD.statusIconCatalog == null ||
                worldHUD.statusIcons == null || worldHUD.statusIcons.Length != 8)
                throw new System.Exception(unit.name + " is missing its world health and damage UI.");
            if (unit.team == CombatUnit.CombatTeam.Player) players++; else enemies++;
        }
        if (players != 5 || enemies != 3) throw new System.Exception("Unexpected initial unit count.");
        if (!NavMesh.SamplePosition(new Vector3(-10f, 0f, 0f), out NavMeshHit roadStart, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(new Vector3(57f, 0f, 0f), out NavMeshHit bossSection, 2f, NavMesh.AllAreas))
            throw new System.Exception("Road endpoints are not on the NavMesh.");
        var sectionPath = new NavMeshPath();
        if (!NavMesh.CalculatePath(roadStart.position, bossSection.position, NavMesh.AllAreas, sectionPath) ||
            sectionPath.status != NavMeshPathStatus.PathComplete)
            throw new System.Exception("The three road sections are not connected.");
        if (NavMesh.SamplePosition(new Vector3(25f, 0f, 14f), out _, 1f, NavMesh.AllAreas))
            throw new System.Exception("Roadside area is unexpectedly walkable.");
        var statTestObject = new GameObject("Status Calculation Validation");
        CombatUnit statTest = statTestObject.AddComponent<CombatUnit>();
        statTest.attackPower = 20f; statTest.defense = 10f;
        statTest.ApplyStatus(Effect(StatusEffectType.Strength, 5f, 5f));
        statTest.ApplyStatus(Effect(StatusEffectType.Rage, 0.2f, 5f));
        statTest.ApplyStatus(Effect(StatusEffectType.Hardening, 2f, 5f));
        statTest.ApplyStatus(Effect(StatusEffectType.Fortified, 0.25f, 5f));
        if (!Mathf.Approximately(statTest.AttackPower, 30f) || !Mathf.Approximately(statTest.Defense, 15f))
            throw new System.Exception("Flat-then-percentage status calculation is incorrect.");
        Object.DestroyImmediate(statTestObject);
        if (!director.startImmediately || director.pausePanel == null || director.resumeButton == null ||
            director.backButton == null || director.pausePanel.GetComponentInChildren<AudioSettingsUI>(true) == null)
            throw new System.Exception("Gameplay preparation flow or pause audio panel is incomplete.");
        var buildScenes = EditorBuildSettings.scenes;
        if (buildScenes.Length < 5 || buildScenes[0].path != "Assets/Scenes/LevelSelection.unity" ||
            buildScenes[1].path != "Assets/Scenes/Preparation.unity" ||
            buildScenes[2].path != "Assets/Scenes/GameplayAI.unity" ||
            buildScenes[3].path != "Assets/Scenes/GameplayLevel2.unity" ||
            buildScenes[4].path != "Assets/Scenes/GameplayLevel3.unity")
            throw new System.Exception("Campaign build scene order is incorrect.");
        EditorSceneManager.OpenScene("Assets/Scenes/Preparation.unity");
        var preparation = Object.FindFirstObjectByType<PreparationMenuController>();
        if (preparation == null || preparation.battleButton == null || preparation.settingsPanel == null ||
            preparation.settingsPanel.GetComponent<AudioSettingsUI>() == null ||
            preparation.characterButtons == null || preparation.characterButtons.Length != 5 ||
            Object.FindFirstObjectByType<PreparationCameraController>() == null ||
            GameObject.Find("Ready Characters") == null ||
            GameObject.Find("Ready Characters").transform.childCount != 5)
            throw new System.Exception("Preparation scene is incomplete.");
        EditorSceneManager.OpenScene("Assets/Scenes/LevelSelection.unity");
        LevelSelectionController levelSelection = Object.FindFirstObjectByType<LevelSelectionController>();
        if (levelSelection == null || levelSelection.levelCards == null || levelSelection.levelCards.Length != 3)
            throw new System.Exception("Level Selection scene is incomplete.");
        for (int level = 2; level <= 3; level++)
        {
            EditorSceneManager.OpenScene(GameProgress.BattleSceneForLevel(level) == "GameplayLevel2"
                ? "Assets/Scenes/GameplayLevel2.unity" : "Assets/Scenes/GameplayLevel3.unity");
            BattleDirector stageDirector = Object.FindFirstObjectByType<BattleDirector>();
            if (stageDirector == null || stageDirector.levelNumber != level ||
                Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None).Length != 8)
                throw new System.Exception("Battle level " + level + " is incomplete.");
        }
        Debug.Log("AIFG campaign validation passed: 3 levels, 5-character roster, statuses, Canvas icons, cover/blockers, NavMesh and no NavMeshAgent.");
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
        director.battlePanel.GetComponent<Image>().raycastTarget = false;
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

        director.pausePanel = Panel(canvasObject.transform, "Pause Panel", new Vector2(620, 500),
            new Vector2(0.5f, 0.5f), navy);
        AddAccent(director.pausePanel.transform, new Vector2(620, 8), new Vector2(0, 246), cyan);
        Label(director.pausePanel.transform, "Pause Title", font, new Vector2(560, 55),
            new Vector2(0, 205), 32, TextAnchor.MiddleCenter).text = "PAUSED";
        CreateAudioSettings(director.pausePanel.transform, font, Vector2.zero, out _);
        director.resumeButton = StyledButton(director.pausePanel.transform, "RESUME", font,
            new Vector2(250, 58), new Vector2(-140, -205), new Color(0.10f, 0.62f, 0.82f, 1f), 20);
        director.backButton = StyledButton(director.pausePanel.transform, "BACK TO SQUAD", font,
            new Vector2(250, 58), new Vector2(140, -205), new Color(0.65f, 0.25f, 0.22f, 1f), 18);

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
        director.pausePanel.SetActive(false);
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

    [MenuItem("AIFG/Build Preparation Flow and Unit HUDs")]
    public static void BuildPresentationFlow()
    {
        ApplyLamZHTemplate();
        var gameplay = EditorSceneManager.OpenScene("Assets/Scenes/GameplayAI.unity");
        var director = Object.FindFirstObjectByType<BattleDirector>();
        if (director == null) throw new System.Exception("BattleDirector is missing.");
        director.startImmediately = true;
        AttachWorldUnitHUDs();
        EditorSceneManager.MarkSceneDirty(gameplay);
        EditorSceneManager.SaveScene(gameplay);
        BuildPreparationScene();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Preparation.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameplayAI.unity", true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("Built preparation scene, gameplay pause audio panel, and world unit HUDs.");
    }

    [MenuItem("AIFG/Build Three-Level Campaign Expansion")]
    public static void BuildCampaignExpansion()
    {
        ConfigureBaseRosterAndStage();
        const string levelTwoPath = "Assets/Scenes/GameplayLevel2.unity";
        const string levelThreePath = "Assets/Scenes/GameplayLevel3.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(levelTwoPath) == null)
            AssetDatabase.CopyAsset("Assets/Scenes/GameplayAI.unity", levelTwoPath);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(levelThreePath) == null)
            AssetDatabase.CopyAsset("Assets/Scenes/GameplayAI.unity", levelThreePath);
        ConfigureStageScene(levelTwoPath, 2);
        ConfigureStageScene(levelThreePath, 3);
        UpgradePreparationScene();
        BuildLevelSelectionScene();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/LevelSelection.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Preparation.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameplayAI.unity", true),
            new EditorBuildSettingsScene(levelTwoPath, true),
            new EditorBuildSettingsScene(levelThreePath, true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("Built three-level selection, five-character roster, shared status UI, and stage configurations.");
    }

    private static void ConfigureBaseRosterAndStage()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameplayAI.unity");
        RenameIfPresent("Aegis", "Yuuka");
        RenameIfPresent("Medic", "Ayane");
        RenameIfPresent("Ranger", "Mika");
        RenameIfPresent("Vanguard", "Momoi");
        GameObject momoi = GameObject.Find("Momoi");
        if (GameObject.Find("Hina") == null && momoi != null)
        {
            GameObject hina = Object.Instantiate(momoi, momoi.transform.parent);
            hina.name = "Hina";
            hina.transform.position = new Vector3(-14f, 1f, 0f);
        }
        ConfigureCharacter("Yuuka", AutoCombatAI.CombatRole.YuukaTank, 0,
            AutoCombatAI.CharacterSkillKind.Defensive, 160f, 18f, 12f, 35f, 10f, 1f, 5f,
            Effect(StatusEffectType.Hardening, 7f, 8f), Effect(StatusEffectType.Fortified, 0.2f, 6f));
        ConfigureCharacter("Ayane", AutoCombatAI.CombatRole.AyaneHealer, 1,
            AutoCombatAI.CharacterSkillKind.Heal, 105f, 15f, 5f, 35f, 8f, 1f, 7f,
            Effect(StatusEffectType.Strength, 8f, 8f), default);
        ConfigureCharacter("Mika", AutoCombatAI.CombatRole.MikaSingleTarget, 2,
            AutoCombatAI.CharacterSkillKind.Burst, 120f, 34f, 6f, 45f, 11f, 3.2f, 7f,
            Effect(StatusEffectType.Penetration, 6f, 8f), default);
        ConfigureCharacter("Momoi", AutoCombatAI.CombatRole.MomoiLowCostAOE, 3,
            AutoCombatAI.CharacterSkillKind.LowCostAOE, 105f, 22f, 5f, 25f, 7f, 1.3f, 8f,
            Effect(StatusEffectType.Weak, 7f, 7f), default);
        ConfigureCharacter("Hina", AutoCombatAI.CombatRole.HinaHighCostAOE, 4,
            AutoCombatAI.CharacterSkillKind.HighCostAOE, 115f, 30f, 5f, 65f, 14f, 2f, 9f,
            Effect(StatusEffectType.Piercing, 0.25f, 9f), default);
        Vector3[] positions =
        {
            new Vector3(-10f, 1f, -3.5f), new Vector3(-12f, 1f, -1.7f),
            new Vector3(-10f, 1f, 0f), new Vector3(-12f, 1f, 1.7f), new Vector3(-10f, 1f, 3.5f)
        };
        string[] names = { "Yuuka", "Ayane", "Mika", "Momoi", "Hina" };
        for (int i = 0; i < names.Length; i++) if (GameObject.Find(names[i]) != null) GameObject.Find(names[i]).transform.position = positions[i];
        AddBlockingObstacleMarkers();
        ConfigureStageValues(1);
        AttachWorldUnitHUDs();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void RenameIfPresent(string oldName, string newName)
    {
        GameObject current = GameObject.Find(newName);
        if (current != null) return;
        GameObject old = GameObject.Find(oldName);
        if (old != null) old.name = newName;
    }

    private static void ConfigureCharacter(string name, AutoCombatAI.CombatRole role, int order,
        AutoCombatAI.CharacterSkillKind skill, float health, float attack, float defense, float cost,
        float cooldown, float multiplier, float range, StatusEffectSpec primary, StatusEffectSpec secondary)
    {
        GameObject character = GameObject.Find(name);
        if (character == null) throw new System.Exception("Missing character " + name);
        CombatUnit unit = character.GetComponent<CombatUnit>();
        AutoCombatAI ai = character.GetComponent<AutoCombatAI>();
        unit.team = CombatUnit.CombatTeam.Player;
        unit.maxHealth = health; unit.attackPower = attack; unit.defense = defense;
        ai.role = role; ai.rosterOrder = order; ai.characterSkillKind = skill;
        ai.characterSkillCost = cost; ai.characterSkillCooldown = cooldown;
        ai.skillPowerMultiplier = multiplier; ai.skillRange = range;
        ai.primaryEffect = primary; ai.secondaryEffect = secondary;
        ai.aoeRadius = role == AutoCombatAI.CombatRole.HinaHighCostAOE ? 5.5f :
            role == AutoCombatAI.CombatRole.MomoiLowCostAOE ? 4.5f : 4f;
        ai.skillHealAmount = 45f;
        ai.healingThreshold = 0.72f; ai.defensiveHealthThreshold = 0.58f;
        ai.aoeMinimumEnemyCount = 2; ai.expensiveAoeMinimumEnemyCount = 3;
        ai.separationDistance = 1.35f; ai.separationStrength = 0.7f; ai.coverDetectionRange = 12f;
    }

    private static StatusEffectSpec Effect(StatusEffectType type, float amount, float duration) =>
        new StatusEffectSpec { type = type, amount = amount, duration = duration };

    private static void AddBlockingObstacleMarkers()
    {
        GameObject environment = GameObject.Find("Linear Stage Environment");
        if (environment == null) return;
        foreach (Collider collider in environment.GetComponentsInChildren<Collider>(true))
        {
            string obstacleName = collider.gameObject.name;
            bool blocking = obstacleName.Contains("Boundary") || obstacleName.Contains("Building");
            if (blocking && collider.GetComponent<BlockingObstacle>() == null)
                collider.gameObject.AddComponent<BlockingObstacle>();
        }
    }

    private static void ConfigureStageScene(string path, int level)
    {
        var scene = EditorSceneManager.OpenScene(path);
        ConfigureStageValues(level);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureStageValues(int level)
    {
        BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
        director.levelNumber = level;
        director.startImmediately = true;
        director.bossName = "Boss " + level;
        director.bossHealth = 580f + level * 120f;
        director.bossAttack = 36f + level * 7f;
        director.bossDefense = 11f + level * 3f;
        director.bossAbilityCooldown = level == 3 ? 7f : 9f;
        if (level == 1)
        {
            director.bossSelfEffects = new[] { Effect(StatusEffectType.Hardening, 8f, 8f) };
            director.bossTargetEffects = new[] { Effect(StatusEffectType.Weak, 8f, 7f) };
            director.bossAllyEffects = new StatusEffectSpec[0];
        }
        else if (level == 2)
        {
            director.bossSelfEffects = new[] { Effect(StatusEffectType.Fortified, 0.25f, 8f) };
            director.bossTargetEffects = new[] { Effect(StatusEffectType.Penetration, 7f, 8f) };
            director.bossAllyEffects = new[] { Effect(StatusEffectType.Hardening, 6f, 7f) };
        }
        else
        {
            director.bossSelfEffects = new[] { Effect(StatusEffectType.Rage, 0.28f, 7f), Effect(StatusEffectType.Fortified, 0.25f, 7f) };
            director.bossTargetEffects = new[] { Effect(StatusEffectType.Dull, 0.22f, 7f), Effect(StatusEffectType.Piercing, 0.25f, 7f) };
            director.bossAllyEffects = new StatusEffectSpec[0];
        }
        int enemyIndex = 0;
        foreach (AutoCombatAI ai in Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None))
        {
            CombatUnit unit = ai.GetComponent<CombatUnit>();
            if (unit.team != CombatUnit.CombatTeam.Enemy) continue;
            ai.role = AutoCombatAI.CombatRole.Enemy;
            unit.isElite = enemyIndex == 2;
            unit.maxHealth = 90f + level * 25f + (unit.isElite ? 45f : 0f);
            unit.attackPower = 18f + level * 5f;
            if (level == 1)
            {
                ai.selfAbilityEffects = enemyIndex % 2 == 0 ? new[] { Effect(StatusEffectType.Strength, 6f, 7f) } : new StatusEffectSpec[0];
                ai.targetAbilityEffects = enemyIndex % 2 == 1 ? new[] { Effect(StatusEffectType.Weak, 5f, 6f) } : new StatusEffectSpec[0];
            }
            else if (level == 2)
            {
                ai.selfAbilityEffects = new[] { Effect(StatusEffectType.Hardening, 6f, 7f) };
                ai.targetAbilityEffects = new[] { Effect(StatusEffectType.Penetration, 5f, 7f) };
            }
            else
            {
                ai.selfAbilityEffects = new[] { Effect(StatusEffectType.Rage, 0.18f, 7f) };
                ai.targetAbilityEffects = enemyIndex % 2 == 0
                    ? new[] { Effect(StatusEffectType.Dull, 0.16f, 7f) }
                    : new[] { Effect(StatusEffectType.Piercing, 0.18f, 7f) };
            }
            ai.allyAbilityEffects = new StatusEffectSpec[0];
            ai.enemyAbilityCooldown = 9f - level * 0.5f;
            enemyIndex++;
        }
    }

    private static void UpgradePreparationScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Preparation.unity");
        PreparationMenuController controller = Object.FindFirstObjectByType<PreparationMenuController>();
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (controller == null || canvas == null) throw new System.Exception("Existing Preparation scene is incomplete.");
        RenameIfPresent("Aegis Ready Display", "Yuuka Ready Display");
        RenameIfPresent("Medic Ready Display", "Ayane Ready Display");
        RenameIfPresent("Ranger Ready Display", "Mika Ready Display");
        RenameIfPresent("Vanguard Ready Display", "Momoi Ready Display");
        Transform readyRoot = GameObject.Find("Ready Characters").transform;
        if (GameObject.Find("Hina Ready Display") == null)
        {
            Transform source = readyRoot.Find("Momoi Ready Display");
            GameObject hina = Object.Instantiate(source.gameObject, readyRoot);
            hina.name = "Hina Ready Display";
        }
        string[] names = { "Yuuka", "Ayane", "Mika", "Momoi", "Hina" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform display = readyRoot.Find(names[i] + " Ready Display");
            display.position = new Vector3(-6.4f + i * 3.2f, 0f, 0f);
        }
        Transform oldSelection = canvas.transform.Find("Roster Selection");
        if (oldSelection != null) Object.DestroyImmediate(oldSelection.gameObject);
        var oldCards = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in canvas.transform)
            if (child.name.Contains("Ready Card")) oldCards.Add(child.gameObject);
        foreach (GameObject oldCard in oldCards) Object.DestroyImmediate(oldCard);
        var selectionRoot = new GameObject("Roster Selection", typeof(RectTransform));
        selectionRoot.transform.SetParent(canvas.transform, false);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        controller.characterNames = names;
        controller.characterButtons = new Button[5];
        controller.selectionText = Label(selectionRoot.transform, "Selection Count", font,
            new Vector2(650, 32), new Vector2(0, -224), 18, TextAnchor.MiddleCenter);
        for (int i = 0; i < names.Length; i++)
            controller.characterButtons[i] = StyledButton(selectionRoot.transform, names[i] + " Selector", font,
                new Vector2(175, 52), new Vector2(-380 + i * 190, -275),
                new Color(0.10f, 0.36f, 0.58f, 1f), 17);
        controller.backButton = StyledButton(selectionRoot.transform, "BACK TO LEVELS", font,
            new Vector2(220, 52), new Vector2(-665, 385), new Color(0.12f, 0.28f, 0.45f, 1f), 17);
        controller.titleText = GameObject.Find("Ready Title")?.GetComponent<Text>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildLevelSelectionScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Color pale = new Color(0.82f, 0.93f, 0.98f, 1f);
        Color navy = new Color(0.06f, 0.20f, 0.34f, 0.96f);
        Color blue = new Color(0.10f, 0.58f, 0.82f, 1f);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.GetComponent<Camera>().backgroundColor = pale;
        var canvasObject = new GameObject("Level Selection Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        Panel(canvasObject.transform, "Background", new Vector2(1600, 900), new Vector2(0.5f, 0.5f), pale)
            .GetComponent<Image>().raycastTarget = false;
        var header = Panel(canvasObject.transform, "Header", new Vector2(1600, 72), new Vector2(0.5f, 0.5f),
            new Color(0.96f, 0.98f, 1f, 1f));
        header.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 414);
        Label(header.transform, "Title", font, new Vector2(520, 60), new Vector2(-500, 0), 30,
            TextAnchor.MiddleLeft).text = "‹   SELECT EPISODE";
        var chapter = Panel(canvasObject.transform, "Chapter Panel", new Vector2(500, 690),
            new Vector2(0.5f, 0.5f), new Color(0.68f, 0.86f, 0.94f, 0.75f));
        chapter.GetComponent<RectTransform>().anchoredPosition = new Vector2(-520, -20);
        Label(chapter.transform, "Chapter", font, new Vector2(420, 90), new Vector2(0, 190), 34,
            TextAnchor.MiddleLeft).text = "CHAPTER 1\nTACTICAL ASSAULT";
        Label(chapter.transform, "Description", font, new Vector2(420, 220), new Vector2(0, 35), 20,
            TextAnchor.UpperLeft).text = "Deploy four students, advance through each combat section, and defeat the stage boss.\n\nClear stages to unlock the next episode.";
        var listHeader = Panel(canvasObject.transform, "Episode List Header", new Vector2(720, 58),
            new Vector2(0.5f, 0.5f), navy);
        listHeader.GetComponent<RectTransform>().anchoredPosition = new Vector2(350, 320);
        Label(listHeader.transform, "Text", font, new Vector2(660, 50), Vector2.zero, 23,
            TextAnchor.MiddleLeft).text = "EPISODE LIST";
        var controllerObject = new GameObject("Level Selection Director", typeof(LevelSelectionController));
        LevelSelectionController controller = controllerObject.GetComponent<LevelSelectionController>();
        controller.levelCards = new LevelCardUI[3];
        for (int i = 0; i < 3; i++)
        {
            var card = Panel(canvasObject.transform, "Level " + (i + 1) + " Card", new Vector2(780, 175),
                new Vector2(0.5f, 0.5f), new Color(0.95f, 0.98f, 1f, 0.98f));
            card.GetComponent<RectTransform>().anchoredPosition = new Vector2(380, 185 - i * 190);
            LevelCardUI cardUI = card.AddComponent<LevelCardUI>();
            var imageSlot = Panel(card.transform, "Assignable Level Image", new Vector2(220, 135),
                new Vector2(0.5f, 0.5f), new Color(0.58f, 0.78f, 0.88f, 1f));
            imageSlot.GetComponent<RectTransform>().anchoredPosition = new Vector2(-260, 0);
            cardUI.levelImage = imageSlot.GetComponent<Image>();
            Text placeholder = Label(imageSlot.transform, "Placeholder", font, new Vector2(190, 70), Vector2.zero, 16,
                TextAnchor.MiddleCenter);
            placeholder.text = "ASSIGN LEVEL IMAGE\nIN INSPECTOR";
            cardUI.imagePlaceholder = placeholder.gameObject;
            cardUI.levelNameText = Label(card.transform, "Level Name", font, new Vector2(420, 48),
                new Vector2(100, 48), 23, TextAnchor.MiddleLeft);
            cardUI.starsText = Label(card.transform, "Stars", font, new Vector2(260, 46),
                new Vector2(20, -18), 27, TextAnchor.MiddleLeft);
            cardUI.selectButton = StyledButton(card.transform, "ENTER", font, new Vector2(190, 58),
                new Vector2(240, -48), blue, 20);
            cardUI.stateText = cardUI.selectButton.GetComponentInChildren<Text>();
            cardUI.lockOverlay = Panel(card.transform, "Lock Overlay", new Vector2(780, 175),
                new Vector2(0.5f, 0.5f), new Color(0.25f, 0.31f, 0.36f, 0.72f));
            Label(cardUI.lockOverlay.transform, "Lock", font, new Vector2(300, 70), new Vector2(215, 0),
                24, TextAnchor.MiddleCenter).text = "🔒  LOCKED";
            controller.levelCards[i] = cardUI;
        }
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LevelSelection.unity");
    }

    private static void AttachWorldUnitHUDs()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        const string catalogPath = "Assets/StatusIconCatalog.asset";
        StatusIconCatalog catalog = AssetDatabase.LoadAssetAtPath<StatusIconCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<StatusIconCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        foreach (CombatUnit unit in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            Transform old = unit.transform.Find("World Unit HUD");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("World Unit HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(WorldUnitHUD));
            root.transform.SetParent(unit.transform, false);
            root.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            root.transform.localScale = Vector3.one * 0.01f;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 125f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 15f;
            var hud = root.GetComponent<WorldUnitHUD>();
            hud.unit = unit;
            hud.statusIconCatalog = catalog;
            hud.unitName = Label(root.transform, "Unit Name", font, new Vector2(210, 30),
                new Vector2(0, 48), 22, TextAnchor.MiddleCenter);
            hud.unitName.fontStyle = FontStyle.Bold;
            hud.healthSlider = CreateSlider(root.transform, "Health", new Vector2(190, 18),
                new Vector2(0, -8), unit.team == CombatUnit.CombatTeam.Player
                    ? new Color(0.12f, 0.78f, 1f, 1f) : new Color(1f, 0.22f, 0.18f, 1f));
            hud.healthFill = hud.healthSlider.fillRect.GetComponent<Image>();
            var statusRoot = new GameObject("Status Icons", typeof(RectTransform));
            statusRoot.transform.SetParent(root.transform, false);
            hud.statusContainer = statusRoot.GetComponent<RectTransform>();
            hud.statusContainer.sizeDelta = new Vector2(220, 26);
            hud.statusContainer.anchoredPosition = new Vector2(0, 17);
            hud.statusIcons = new Image[8];
            for (int i = 0; i < hud.statusIcons.Length; i++)
            {
                var icon = new GameObject("Status Icon " + (i + 1), typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                icon.transform.SetParent(statusRoot.transform, false);
                Image image = icon.GetComponent<Image>();
                image.raycastTarget = false;
                image.gameObject.SetActive(false);
                hud.statusIcons[i] = image;
            }
            hud.damageLabels = new Text[5];
            for (int i = 0; i < hud.damageLabels.Length; i++)
            {
                Text damage = Label(root.transform, "Damage Number " + (i + 1), font,
                    new Vector2(180, 46), new Vector2((i - 2) * 8f, 8f), 30, TextAnchor.MiddleCenter);
                damage.fontStyle = FontStyle.Bold;
                damage.raycastTarget = false;
                damage.gameObject.SetActive(false);
                hud.damageLabels[i] = damage;
            }
        }
    }

    private static void BuildPreparationScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.42f, 0.48f, 0.58f, 1f);
        Material floorMaterial = GetOrCreateMaterial("Assets/Materials/PreparationFloor.mat",
            new Color(0.045f, 0.09f, 0.15f));
        Material podiumMaterial = GetOrCreateMaterial("Assets/Materials/PreparationPodium.mat",
            new Color(0.08f, 0.58f, 0.78f));
        Material[] characterMaterials =
        {
            GetOrCreateMaterial("Assets/Materials/PreparationAegis.mat", new Color(0.15f, 0.65f, 0.95f)),
            GetOrCreateMaterial("Assets/Materials/PreparationRanger.mat", new Color(0.24f, 0.82f, 0.62f)),
            GetOrCreateMaterial("Assets/Materials/PreparationMedic.mat", new Color(0.92f, 0.42f, 0.62f)),
            GetOrCreateMaterial("Assets/Materials/PreparationVanguard.mat", new Color(0.95f, 0.66f, 0.18f))
        };
        CreateBlock(null, "Preparation Floor", new Vector3(0f, -0.3f, 1f),
            new Vector3(28f, 0.6f, 18f), floorMaterial, 0);
        CreateBlock(null, "Backdrop", new Vector3(0f, 5f, 6f),
            new Vector3(28f, 10f, 0.6f), floorMaterial, 0);
        var readyRoot = new GameObject("Ready Characters");
        string[] names = { "Aegis", "Ranger", "Medic", "Vanguard" };
        for (int i = 0; i < names.Length; i++)
        {
            var character = new GameObject(names[i] + " Ready Display");
            character.transform.SetParent(readyRoot.transform);
            character.transform.position = new Vector3(-4.8f + i * 3.2f, 0f, 0f);
            GameObject podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            podium.name = "Podium";
            podium.transform.SetParent(character.transform, false);
            podium.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            podium.transform.localScale = new Vector3(1.25f, 0.18f, 1.25f);
            podium.GetComponent<MeshRenderer>().sharedMaterial = podiumMaterial;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Character Body";
            body.transform.SetParent(character.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            body.transform.localScale = new Vector3(0.82f, 1.22f, 0.82f);
            body.GetComponent<MeshRenderer>().sharedMaterial = characterMaterials[i];
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Tactical Visor";
            visor.transform.SetParent(character.transform, false);
            visor.transform.localPosition = new Vector3(0f, 2.28f, -0.62f);
            visor.transform.localScale = new Vector3(0.72f, 0.18f, 0.12f);
            visor.GetComponent<MeshRenderer>().sharedMaterial = podiumMaterial;
        }
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener),
            typeof(PreparationCameraController));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.055f, 0.10f, 1f);
        camera.fieldOfView = 48f;
        var lightObject = new GameObject("Directional Light", typeof(Light));
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Color navy = new Color(0.035f, 0.095f, 0.17f, 0.94f);
        Color cyan = new Color(0.10f, 0.72f, 0.92f, 1f);
        var canvasObject = new GameObject("Preparation Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1600, 900);
        canvasScaler.matchWidthOrHeight = 0.5f;
        Label(canvasObject.transform, "Ready Title", font, new Vector2(900, 70),
            new Vector2(0, 370), 42, TextAnchor.MiddleCenter).text = "SQUAD READY";
        Label(canvasObject.transform, "Camera Help", font, new Vector2(700, 35),
            new Vector2(0, 325), 18, TextAnchor.MiddleCenter).text =
            "Right-drag to inspect the squad  •  Scroll to zoom";
        for (int i = 0; i < names.Length; i++)
        {
            var card = Panel(canvasObject.transform, names[i] + " Ready Card", new Vector2(210, 48),
                new Vector2(0.5f, 0.5f), navy);
            card.GetComponent<RectTransform>().anchoredPosition = new Vector2(-360 + i * 240, -285);
            Label(card.transform, "Name", font, new Vector2(195, 42), Vector2.zero, 18,
                TextAnchor.MiddleCenter).text = "READY  •  " + names[i].ToUpperInvariant();
        }
        var controllerObject = new GameObject("Preparation Director", typeof(PreparationMenuController));
        var controller = controllerObject.GetComponent<PreparationMenuController>();
        controller.battleButton = StyledButton(canvasObject.transform, "BATTLE", font,
            new Vector2(420, 72), new Vector2(0, -375), new Color(0.98f, 0.67f, 0.05f, 1f), 27);
        controller.settingsButton = StyledButton(canvasObject.transform, "SETTINGS", font,
            new Vector2(210, 58), Vector2.zero, new Color(0.10f, 0.36f, 0.58f, 1f), 19);
        var settingsRect = controller.settingsButton.GetComponent<RectTransform>();
        settingsRect.anchorMin = settingsRect.anchorMax = new Vector2(1f, 0f);
        settingsRect.anchoredPosition = new Vector2(-125, 45);
        controller.settingsPanel = CreateAudioSettings(canvasObject.transform, font, Vector2.zero, out _);
        var audioRect = controller.settingsPanel.GetComponent<RectTransform>();
        audioRect.anchorMin = audioRect.anchorMax = new Vector2(1f, 0f);
        audioRect.anchoredPosition = new Vector2(-290, 235);
        controller.closeSettingsButton = StyledButton(controller.settingsPanel.transform, "CLOSE", font,
            new Vector2(230, 52), new Vector2(0, -150), new Color(0.16f, 0.46f, 0.66f, 1f), 18);
        controller.settingsPanel.SetActive(false);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Preparation.unity");
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
                if (!director.IsPlaying) director.startButton.onClick.Invoke();
                if (!director.IsPlaying || !director.battlePanel.activeSelf)
                    throw new System.Exception("Battle did not start from the Canvas button.");
                CombatUnit aegisUnit = GameObject.Find("Aegis").GetComponent<CombatUnit>();
                CombatUnit enemyUnit = null;
                foreach (CombatUnit candidate in Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                    if (candidate.team == CombatUnit.CombatTeam.Enemy) { enemyUnit = candidate; break; }
                aegisUnit.TakeDamage(30f);
                enemyUnit.TakeDamage(30f);
                if (!HasVisibleDamageNumber(aegisUnit) || !HasVisibleDamageNumber(enemyUnit))
                    throw new System.Exception("Player or enemy floating damage number did not activate.");
                director.pauseButton.onClick.Invoke();
                if (!director.pausePanel.activeSelf || Time.timeScale != 0f)
                    throw new System.Exception("Gameplay pause panel did not pause the battle.");
                director.resumeButton.onClick.Invoke();
                if (director.pausePanel.activeSelf || Time.timeScale <= 0f)
                    throw new System.Exception("Gameplay pause panel did not resume the battle.");
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

    private static bool HasVisibleDamageNumber(CombatUnit unit)
    {
        WorldUnitHUD hud = unit.GetComponentInChildren<WorldUnitHUD>(true);
        if (hud == null || hud.damageLabels == null) return false;
        foreach (Text label in hud.damageLabels)
            if (label != null && label.gameObject.activeSelf) return true;
        return false;
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

    private static GameObject CreateAudioSettings(Transform parent, Font font, Vector2 position,
        out AudioSettingsUI audioSettings)
    {
        var panel = Panel(parent, "Audio Settings Panel", new Vector2(540, 350),
            new Vector2(0.5f, 0.5f), new Color(0.045f, 0.12f, 0.21f, 0.98f));
        panel.GetComponent<RectTransform>().anchoredPosition = position;
        AddAccent(panel.transform, new Vector2(540, 7), new Vector2(0, 171),
            new Color(0.10f, 0.72f, 0.92f, 1f));
        Label(panel.transform, "Audio Title", font, new Vector2(480, 50), new Vector2(0, 125),
            27, TextAnchor.MiddleCenter).text = "AUDIO SETTINGS";
        Text volumeText = Label(panel.transform, "Volume Value", font, new Vector2(450, 34),
            new Vector2(0, 78), 18, TextAnchor.MiddleCenter);
        Slider slider = CreateSlider(panel.transform, "Master Volume", new Vector2(430, 28),
            new Vector2(0, 35), new Color(0.10f, 0.78f, 0.96f, 1f));
        slider.interactable = true;
        Button mute = StyledButton(panel.transform, "MUTE", font, new Vector2(230, 54),
            new Vector2(0, -48), new Color(0.18f, 0.42f, 0.62f, 1f), 19);
        audioSettings = panel.AddComponent<AudioSettingsUI>();
        audioSettings.volumeSlider = slider;
        audioSettings.volumeText = volumeText;
        audioSettings.muteButton = mute;
        audioSettings.muteText = mute.GetComponentInChildren<Text>();
        return panel;
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
