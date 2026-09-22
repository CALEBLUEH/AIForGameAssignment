using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CharacterStateSandboxValidator
{
    private const string ScenePath = "Assets/Scenes/Sandbox_CharacterStates.unity";
    private const string ActiveKey = "AIFG.CharacterPreview.ValidationActive";
    private const string StageKey = "AIFG.CharacterPreview.ValidationStage";
    private const string TimeKey = "AIFG.CharacterPreview.ValidationTime";
    private const string CharacterIndexKey = "AIFG.CharacterPreview.ValidationCharacterIndex";
    private static readonly string HinaIdleScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Hina-Idle.png");
    private static readonly string HinaRunScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Hina-Run.png");
    private static readonly string HinaAttackScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Hina-Attack.png");
    private static readonly string MomoiAttackScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Momoi-Attack.png");
    private static readonly string MikaAttackScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Mika-Attack.png");
    private static readonly string AyaneAttackScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Ayane-Attack.png");
    private static readonly string YuukaAttackScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Yuuka-Attack.png");
    private static readonly string YuukaWeapon1ScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Yuuka-Weapon1.png");
    private static readonly string YuukaWeapon2ScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Yuuka-Weapon2.png");
    private static readonly string[] FaceScreenshotPaths =
    {
        Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Hina-Face.png"),
        Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Momoi-Face.png"),
        Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Mika-Face.png"),
        Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Ayane-Face.png"),
        Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Yuuka-Face.png")
    };
    private static readonly string MomoiRetreatScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Momoi-Retreat.png");
    private static readonly string MomoiCoverScreenshotPath = Path.Combine(Path.GetTempPath(), "AIForGameAssignment-CharacterSandbox-Momoi-Cover.png");

    static CharacterStateSandboxValidator()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
    }

    [MenuItem("AIFG/Character Preview/Validate Sandbox Scene")]
    public static void ValidateInteractive()
    {
        ValidateSceneStructure();
        Debug.Log("CHARACTER_SANDBOX_STATIC_VALIDATION_COMPLETE");
    }

    public static void ValidateFromCommandLine()
    {
        try
        {
            ValidateSceneStructure();
            foreach (string screenshot in ScreenshotPaths)
                if (File.Exists(screenshot)) File.Delete(screenshot);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(StageKey, 0);
            SessionState.SetInt(CharacterIndexKey, 2);
            SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    public static void CaptureFinalDiagnostics()
    {
        ValidateSceneStructure();
        CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
        Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (preview == null || camera == null)
            throw new InvalidOperationException("The sandbox preview controller or camera is missing.");

        SerializedObject serialized = new SerializedObject(preview);
        SerializedProperty characters = serialized.FindProperty("characters");
        AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/CharacterPreview/Attack.anim");
        if (attack == null) throw new InvalidOperationException("Attack animation is missing for diagnostic rendering.");

        AnimationMode.StartAnimationMode();
        try
        {
            for (int index = 0; index < characters.arraySize; index++)
            {
                for (int rootIndex = 0; rootIndex < characters.arraySize; rootIndex++)
                {
                    GameObject root = (GameObject)characters.GetArrayElementAtIndex(rootIndex)
                        .FindPropertyRelative("root").objectReferenceValue;
                    root.SetActive(rootIndex == index);
                }

                Animator animator = (Animator)characters.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("animator").objectReferenceValue;
                AnimationMode.SampleAnimationClip(animator.gameObject, attack, 0.20f);
                SaveCameraRender(camera, index == 0 ? HinaAttackScreenshotPath :
                    index == 1 ? MomoiAttackScreenshotPath : GetAdditionalAttackScreenshotPath(index));
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) throw new InvalidOperationException("Character at index " + index + " has no head bone.");
                SaveFaceRender(camera, head.position, FaceScreenshotPaths[index]);
            }

            MeshRenderer[] yuukaWeapons = GetSelectedWeapons(preview, 4);
            yuukaWeapons[0].enabled = true;
            yuukaWeapons[1].enabled = false;
            SaveCameraRender(camera, YuukaWeapon1ScreenshotPath);
            yuukaWeapons[0].enabled = false;
            yuukaWeapons[1].enabled = true;
            SaveCameraRender(camera, YuukaWeapon2ScreenshotPath);
            foreach (MeshRenderer weapon in yuukaWeapons) weapon.enabled = true;
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        Debug.Log("CHARACTER_SANDBOX_DIAGNOSTIC_RENDER_COMPLETE");
    }

    private static void SaveCameraRender(Camera camera, string path)
    {
        const int width = 769;
        const int height = 433;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static void SaveFaceRender(Camera camera, Vector3 headPosition, string path)
    {
        Vector3 previousPosition = camera.transform.position;
        Quaternion previousRotation = camera.transform.rotation;
        float previousFieldOfView = camera.fieldOfView;
        try
        {
            Vector3 viewDirection = (previousPosition - headPosition).normalized;
            camera.transform.position = headPosition + viewDirection * 0.65f;
            camera.transform.LookAt(headPosition);
            camera.fieldOfView = 32f;
            SaveCameraRender(camera, path);
        }
        finally
        {
            camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
            camera.fieldOfView = previousFieldOfView;
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.update -= Tick;
            return;
        }

        try
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0 && EditorApplication.isPlaying)
            {
                SessionState.SetInt(StageKey, 1);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 1 && EditorApplication.isPlaying && Elapsed() > 0.75d)
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                if (preview == null) throw new InvalidOperationException("Preview controller was not present in Play Mode.");
                SerializedObject serialized = new SerializedObject(preview);
                SerializedProperty characters = serialized.FindProperty("characters");
                SerializedProperty characterButtons = serialized.FindProperty("characterButtons");
                for (int characterIndex = 0; characterIndex < characters.arraySize; characterIndex++)
                {
                    Button characterButton = (Button)characterButtons.GetArrayElementAtIndex(characterIndex).objectReferenceValue;
                    characterButton.onClick.Invoke();
                    SerializedProperty character = characters.GetArrayElementAtIndex(characterIndex);
                    string characterName = character.FindPropertyRelative("displayName").stringValue;
                    Animator animator = (Animator)character.FindPropertyRelative("animator").objectReferenceValue;
                    if (animator == null || !animator.gameObject.activeInHierarchy)
                        throw new InvalidOperationException(characterName + " was not selected by its model-row button.");
                    for (int stateIndex = 0; stateIndex < Enum.GetValues(typeof(CharacterStatePreviewController.PreviewState)).Length; stateIndex++)
                    {
                        preview.SelectState(stateIndex);
                        animator.Update(0f);
                        string expectedState = Enum.GetName(typeof(CharacterStatePreviewController.PreviewState), stateIndex);
                        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(expectedState))
                            throw new InvalidOperationException(characterName + " Animator did not enter state " + expectedState + ".");
                    }
                }
                preview.SelectCharacter(0);
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Idle);
                GetSelectedWeapon(preview, 0).enabled = false;
                ScreenCapture.CaptureScreenshot(HinaIdleScreenshotPath, 1);
                SessionState.SetInt(StageKey, 6);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 6 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(HinaIdleScreenshotPath))
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Run);
                ScreenCapture.CaptureScreenshot(HinaRunScreenshotPath, 1);
                SessionState.SetInt(StageKey, 10);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 10 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(HinaRunScreenshotPath))
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Attack);
                MeshRenderer weapon = GetSelectedWeapon(preview, 0);
                weapon.enabled = false;
                ScreenCapture.CaptureScreenshot(HinaAttackScreenshotPath, 1);
                SessionState.SetInt(StageKey, 7);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 7 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(HinaAttackScreenshotPath))
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                ValidateAttackHandSides(preview, 0, "Hina");
                GetSelectedWeapon(preview, 0).enabled = true;
                SerializedObject serialized = new SerializedObject(preview);
                Button momoiButton = (Button)serialized.FindProperty("characterButtons").GetArrayElementAtIndex(1).objectReferenceValue;
                momoiButton.onClick.Invoke();
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Attack);
                GetSelectedWeapon(preview, 1).enabled = false;
                ScreenCapture.CaptureScreenshot(MomoiAttackScreenshotPath, 1);
                SessionState.SetInt(StageKey, 9);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 9 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(MomoiAttackScreenshotPath))
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                ValidateAttackHandSides(preview, 1, "Momoi");
                GetSelectedWeapon(preview, 1).enabled = true;
                SessionState.SetInt(CharacterIndexKey, 2);
                SessionState.SetInt(StageKey, 11);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 11 && EditorApplication.isPlaying && Elapsed() > 0.2d)
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                int characterIndex = SessionState.GetInt(CharacterIndexKey, 2);
                preview.SelectCharacter(characterIndex);
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Attack);
                ScreenCapture.CaptureScreenshot(GetAdditionalAttackScreenshotPath(characterIndex), 1);
                SessionState.SetInt(StageKey, 12);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 12 && EditorApplication.isPlaying && Elapsed() > 1.0d)
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                SerializedObject serialized = new SerializedObject(preview);
                int characterIndex = SessionState.GetInt(CharacterIndexKey, 2);
                string screenshotPath = GetAdditionalAttackScreenshotPath(characterIndex);
                if (!File.Exists(screenshotPath)) return;
                SerializedProperty characters = serialized.FindProperty("characters");
                string characterName = characters.GetArrayElementAtIndex(characterIndex)
                    .FindPropertyRelative("displayName").stringValue;
                ValidateAttackHandSides(preview, characterIndex, characterName);

                if (characterIndex == 4)
                {
                    MeshRenderer[] yuukaWeapons = GetSelectedWeapons(preview, characterIndex);
                    yuukaWeapons[0].enabled = true;
                    yuukaWeapons[1].enabled = false;
                    ScreenCapture.CaptureScreenshot(YuukaWeapon1ScreenshotPath, 1);
                    SessionState.SetInt(StageKey, 13);
                    SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                    return;
                }

                characterIndex++;
                if (characterIndex < characters.arraySize)
                {
                    SessionState.SetInt(CharacterIndexKey, characterIndex);
                    SessionState.SetInt(StageKey, 11);
                    SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                    return;
                }

                preview.SelectCharacter(1);
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Retreat);
                ScreenCapture.CaptureScreenshot(MomoiRetreatScreenshotPath, 1);
                SessionState.SetInt(StageKey, 4);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 13 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(YuukaWeapon1ScreenshotPath))
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                MeshRenderer[] yuukaWeapons = GetSelectedWeapons(preview, 4);
                yuukaWeapons[0].enabled = false;
                yuukaWeapons[1].enabled = true;
                ScreenCapture.CaptureScreenshot(YuukaWeapon2ScreenshotPath, 1);
                SessionState.SetInt(StageKey, 14);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 14 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(YuukaWeapon2ScreenshotPath))
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                foreach (MeshRenderer weapon in GetSelectedWeapons(preview, 4)) weapon.enabled = true;
                preview.SelectCharacter(1);
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Retreat);
                ScreenCapture.CaptureScreenshot(MomoiRetreatScreenshotPath, 1);
                SessionState.SetInt(StageKey, 4);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 4 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(MomoiRetreatScreenshotPath))
            {
                CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
                preview.SelectState((int)CharacterStatePreviewController.PreviewState.Cover);
                ScreenCapture.CaptureScreenshot(MomoiCoverScreenshotPath, 1);
                SessionState.SetInt(StageKey, 8);
                SessionState.SetString(TimeKey, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (stage == 8 && EditorApplication.isPlaying && Elapsed() > 1.0d && File.Exists(MomoiCoverScreenshotPath))
            {
                Debug.Log("CHARACTER_SANDBOX_RUNTIME_VALIDATION_COMPLETE models=5 statesPerModel=5 screenshots=" + string.Join(";", ScreenshotPaths));
                SessionState.SetInt(StageKey, 5);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (stage == 5 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                Debug.Log("CHARACTER_SANDBOX_VALIDATION_COMPLETE");
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void ValidateSceneStructure()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CharacterStatePreviewController preview = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
        if (preview == null) throw new InvalidOperationException("Scene is missing CharacterStatePreviewController.");
        SerializedObject serialized = new SerializedObject(preview);
        SerializedProperty characters = serialized.FindProperty("characters");
        SerializedProperty characterButtons = serialized.FindProperty("characterButtons");
        SerializedProperty stateButtons = serialized.FindProperty("stateButtons");
        if (characters.arraySize != 5) throw new InvalidOperationException("Expected five character options.");
        if (characterButtons.arraySize != 5) throw new InvalidOperationException("Expected five model-row buttons.");
        if (stateButtons.arraySize != 5) throw new InvalidOperationException("Expected five state-column buttons.");
        if (serialized.FindProperty("selectionText").objectReferenceValue == null) throw new InvalidOperationException("Selection text is not assigned.");

        RuntimeAnimatorController sharedController = null;
        for (int i = 0; i < characters.arraySize; i++)
        {
            SerializedProperty character = characters.GetArrayElementAtIndex(i);
            if (character.FindPropertyRelative("root").objectReferenceValue == null) throw new InvalidOperationException("Character root is missing at index " + i + ".");
            Animator animator = (Animator)character.FindPropertyRelative("animator").objectReferenceValue;
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Character animator is not a valid Humanoid at index " + i + ".");
            if (animator.runtimeAnimatorController == null) throw new InvalidOperationException("Character controller is missing at index " + i + ".");
            if (sharedController == null) sharedController = animator.runtimeAnimatorController;
            else if (sharedController != animator.runtimeAnimatorController) throw new InvalidOperationException("Characters do not share one controller.");

            GameObject characterRoot = (GameObject)character.FindPropertyRelative("root").objectReferenceValue;
            CharacterPreviewGrounder grounder = characterRoot.GetComponent<CharacterPreviewGrounder>();
            if (grounder == null) throw new InvalidOperationException("Character grounding is missing at index " + i + ".");

            string displayName = character.FindPropertyRelative("displayName").stringValue;
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            MeshRenderer[] weapons = characterRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith(displayName + " Weapon", StringComparison.Ordinal))
                .ToArray();
            int expectedWeaponCount = displayName == "Yuuka" ? 2 : 1;
            if (weapons.Length != expectedWeaponCount)
                throw new InvalidOperationException(displayName + " expected " + expectedWeaponCount +
                    " independently movable preview weapon object(s), but found " + weapons.Length + ".");
            if (weapons.Any(weapon => weapon.GetComponent<MeshFilter>() == null ||
                                      weapon.GetComponent<MeshFilter>().sharedMesh == null))
                throw new InvalidOperationException("Rigid preview weapon is missing at index " + i + ".");
            if (leftHand == null || rightHand == null || weapons.Any(weapon =>
                    !weapon.transform.IsChildOf(leftHand) && !weapon.transform.IsChildOf(rightHand)))
                throw new InvalidOperationException("Preview weapon is not attached beneath a hand at index " + i + ".");
            if (displayName == "Yuuka" && weapons[0].GetComponent<MeshFilter>().sharedMesh ==
                weapons[1].GetComponent<MeshFilter>().sharedMesh)
                throw new InvalidOperationException("Yuuka's two preview weapon objects still share the combined mesh.");
            if (displayName == "Yuuka")
            {
                int[] triangleCounts = weapons.Select(weapon => weapon.GetComponent<MeshFilter>().sharedMesh)
                    .Select(mesh => Enumerable.Range(0, mesh.subMeshCount)
                        .Sum(subMesh => (int)mesh.GetIndexCount(subMesh) / 3))
                    .ToArray();
                int totalTriangles = triangleCounts.Sum();
                if (triangleCounts.Min() < totalTriangles * 0.30f)
                    throw new InvalidOperationException("Yuuka's preview weapon split is unbalanced: " +
                        string.Join("/", triangleCounts) + " triangles.");
            }
        }

        foreach (string state in Enum.GetNames(typeof(CharacterStatePreviewController.PreviewState)))
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/CharacterPreview/" + state + ".anim");
            if (clip == null || !clip.isLooping) throw new InvalidOperationException(state + " clip is missing or not looping.");
        }
        ValidateRequestedPoseCurves();
        ValidateFaceOverlayMaterials();

        int missingScripts = 0;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            missingScripts += CountMissingScripts(root);
        if (missingScripts != 0) throw new InvalidOperationException("Scene contains " + missingScripts + " missing script references.");
    }

    private static void ValidateFaceOverlayMaterials()
    {
        string[] characters = { "Hina", "Momoi", "Mika", "Ayane", "Yuuka" };
        foreach (string character in characters)
        {
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/CharacterPreview" });
            Material eyeMouth = materialGuids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path)
                    .StartsWith(character + "_", StringComparison.OrdinalIgnoreCase))
                .Select(path => AssetDatabase.LoadAssetAtPath<Material>(path))
                .FirstOrDefault(material => material != null &&
                    material.name.IndexOf("EyeMouth", StringComparison.OrdinalIgnoreCase) >= 0);
            if (eyeMouth == null) throw new InvalidOperationException(character + " EyeMouth preview material is missing.");

            bool expectsSourceAlpha = character == "Hina" || character == "Ayane";
            bool alphaEnabled = eyeMouth.HasProperty("_AlphaClip") && eyeMouth.GetFloat("_AlphaClip") > 0.5f;
            bool colorKeyEnabled = eyeMouth.HasProperty("_ColorKeyEnabled") &&
                                   eyeMouth.GetFloat("_ColorKeyEnabled") > 0.5f;
            if (alphaEnabled != expectsSourceAlpha || colorKeyEnabled == expectsSourceAlpha)
                throw new InvalidOperationException(character + " EyeMouth transparency mode is incorrect.");
            bool expectsMouthAtlas = character != "Hina";
            bool mouthAtlasEnabled = eyeMouth.HasProperty("_UseMouthAtlas") &&
                                     eyeMouth.GetFloat("_UseMouthAtlas") > 0.5f;
            if (mouthAtlasEnabled != expectsMouthAtlas ||
                (mouthAtlasEnabled && eyeMouth.GetTexture("_MouthTex") == null))
                throw new InvalidOperationException(character + " neutral mouth atlas assignment is incorrect.");
            if (eyeMouth.renderQueue != (int)UnityEngine.Rendering.RenderQueue.AlphaTest)
                throw new InvalidOperationException(character + " EyeMouth material is not in the alpha-test queue.");
            if (eyeMouth.GetShaderPassEnabled("SHADOWCASTER"))
                throw new InvalidOperationException(character + " EyeMouth material still casts a face-blocking shadow.");
        }
    }

    private static int CountMissingScripts(GameObject gameObject)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        foreach (Transform child in gameObject.transform) count += CountMissingScripts(child.gameObject);
        return count;
    }

    private static MeshRenderer GetSelectedWeapon(CharacterStatePreviewController preview, int characterIndex)
    {
        return GetSelectedWeapons(preview, characterIndex)[0];
    }

    private static MeshRenderer[] GetSelectedWeapons(CharacterStatePreviewController preview, int characterIndex)
    {
        SerializedObject serialized = new SerializedObject(preview);
        GameObject characterRoot = (GameObject)serialized.FindProperty("characters").GetArrayElementAtIndex(characterIndex)
            .FindPropertyRelative("root").objectReferenceValue;
        MeshRenderer[] weapons = characterRoot.GetComponentsInChildren<MeshRenderer>(true)
            .Where(renderer => renderer.name.IndexOf(" Weapon", StringComparison.Ordinal) >= 0)
            .OrderBy(renderer => renderer.name, StringComparer.Ordinal)
            .ToArray();
        if (weapons.Length == 0) throw new InvalidOperationException("Character preview weapon is missing at index " + characterIndex + ".");
        return weapons;
    }

    private static string GetAdditionalAttackScreenshotPath(int characterIndex)
    {
        switch (characterIndex)
        {
            case 2: return MikaAttackScreenshotPath;
            case 3: return AyaneAttackScreenshotPath;
            case 4: return YuukaAttackScreenshotPath;
            default: throw new ArgumentOutOfRangeException(nameof(characterIndex), characterIndex,
                "Additional attack screenshots are only configured for Mika, Ayane, and Yuuka.");
        }
    }

    private static void ValidateAttackHandSides(CharacterStatePreviewController preview, int characterIndex, string characterName)
    {
        SerializedObject serialized = new SerializedObject(preview);
        Animator animator = (Animator)serialized.FindProperty("characters").GetArrayElementAtIndex(characterIndex)
            .FindPropertyRelative("animator").objectReferenceValue;
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hips == null || leftUpperArm == null || rightUpperArm == null || leftHand == null || rightHand == null)
            throw new InvalidOperationException(characterName + " is missing arm, hand, or hips bones for the attack-pose check.");

        Vector3 localHips = animator.transform.InverseTransformPoint(hips.position);
        Vector3 localLeftUpperArm = animator.transform.InverseTransformPoint(leftUpperArm.position);
        Vector3 localRightUpperArm = animator.transform.InverseTransformPoint(rightUpperArm.position);
        Vector3 localLeftHand = animator.transform.InverseTransformPoint(leftHand.position);
        Vector3 localRightHand = animator.transform.InverseTransformPoint(rightHand.position);
        float leftShoulderOffset = localLeftUpperArm.x - localHips.x;
        float rightShoulderOffset = localRightUpperArm.x - localHips.x;
        float leftOffset = localLeftHand.x - localHips.x;
        float rightOffset = localRightHand.x - localHips.x;
        bool leftStayedOnOwnSide = leftOffset * leftShoulderOffset > 0f;
        bool rightStayedOnOwnSide = rightOffset * rightShoulderOffset > 0f;
        bool handsKeptTheirOrder = (localLeftHand.x - localRightHand.x) *
            (localLeftUpperArm.x - localRightUpperArm.x) > 0f;
        bool weaponGapRemains = Mathf.Abs(leftOffset) >= Mathf.Abs(leftShoulderOffset) * 0.45f &&
            Mathf.Abs(rightOffset) >= Mathf.Abs(rightShoulderOffset) * 0.45f;
        if (!leftStayedOnOwnSide || !rightStayedOnOwnSide || !handsKeptTheirOrder || !weaponGapRemains)
            throw new InvalidOperationException(
                characterName + " attack hands crossed the body centre. Left offset=" + leftOffset.ToString("F6") +
                ", right offset=" + rightOffset.ToString("F6") +
                ", left shoulder=" + leftShoulderOffset.ToString("F6") +
                ", right shoulder=" + rightShoulderOffset.ToString("F6") + ".");

        Debug.Log(characterName + " attack hand sides valid: left=" + leftOffset.ToString("F3") +
            ", right=" + rightOffset.ToString("F3") + ".");

        animator.Play("Attack", 0, 0.20f);
        animator.Update(0.0001f);
        float leftAimReach = Vector3.Distance(leftUpperArm.position, leftHand.position);
        float rightAimReach = Vector3.Distance(rightUpperArm.position, rightHand.position);
        float aimGap = Vector3.Distance(leftHand.position, rightHand.position);

        animator.Play("Attack", 0, 0.23f);
        animator.Update(0.0001f);
        float leftRecoilReach = Vector3.Distance(leftUpperArm.position, leftHand.position);
        float rightRecoilReach = Vector3.Distance(rightUpperArm.position, rightHand.position);
        float recoilGap = Vector3.Distance(leftHand.position, rightHand.position);
        bool handsRetract = leftRecoilReach < leftAimReach * 0.995f && rightRecoilReach < rightAimReach * 0.995f;
        bool handsDoNotOpen = recoilGap <= aimGap * 1.05f;
        if (!handsRetract || !handsDoNotOpen)
            throw new InvalidOperationException(
                characterName + " attack recoil did not pull both hands backward without opening. Aim reach=" +
                leftAimReach.ToString("F6") + "/" + rightAimReach.ToString("F6") + ", recoil reach=" +
                leftRecoilReach.ToString("F6") + "/" + rightRecoilReach.ToString("F6") + ", gap=" +
                aimGap.ToString("F6") + "->" + recoilGap.ToString("F6") + ".");
    }

    private static void ValidateRequestedPoseCurves()
    {
        AssertConstant("Attack", "Left Upper Leg Front-Back", 0f);
        AssertConstant("Attack", "Right Upper Leg Front-Back", 0f);
        AssertConstant("Attack", "Left Lower Leg Stretch", 0.55f);
        AssertConstant("Attack", "Right Lower Leg Stretch", 0.55f);
        AssertConstant("Idle", "Left Upper Leg Front-Back", 0f);
        AssertConstant("Idle", "Right Upper Leg Front-Back", 0f);
        AssertConstant("Idle", "Left Lower Leg Stretch", 0.55f);
        AssertConstant("Idle", "Right Lower Leg Stretch", 0.55f);
        AssertConstant("Retreat", "Left Upper Leg Front-Back");
        AssertConstant("Retreat", "Right Upper Leg Front-Back");
        AssertConstant("Retreat", "Left Lower Leg Stretch");
        AssertConstant("Retreat", "Right Lower Leg Stretch");
        AssertAnimated("Idle", "Spine Front-Back");
        AssertAnimated("Attack", "Spine Front-Back");
        AssertConstant("Attack", "Left Arm Front-Back", -0.48f);
        AssertConstant("Attack", "Right Arm Front-Back", -0.45f);
        AssertAnimated("Attack", "Left Forearm Stretch");
        AssertAnimated("Attack", "Right Forearm Stretch");
        AssertAnimated("Cover", "Spine Front-Back");
        AssertAnimated("Retreat", "Spine Front-Back");

        foreach (string standingState in new[] { "Idle", "Run", "Attack" })
        {
            if (GetMuscleCurve(standingState, "Spine Front-Back").Evaluate(0f) >= -0.01f ||
                GetMuscleCurve(standingState, "Chest Front-Back").Evaluate(0f) >= -0.01f)
                throw new InvalidOperationException(standingState + " chest is not positioned forward of the pelvis.");
        }

        AnimationCurve retreatHead = GetMuscleCurve("Retreat", "Head Nod Down-Up");
        if (retreatHead.Evaluate(0f) > -0.25f)
            throw new InvalidOperationException("Retreat head is not bowed far enough.");

        float coverBody = GetMuscleCurve("Cover", "Spine Front-Back").Evaluate(0f);
        float coverHead = GetMuscleCurve("Cover", "Head Nod Down-Up").Evaluate(0f);
        if (Mathf.Abs(coverBody - coverHead) < 0.35f)
            throw new InvalidOperationException("Cover body and head do not have the requested opposing angle.");

        if (GetMuscleCurve("Attack", "Left Arm Front-Back").Evaluate(0f) > -0.35f ||
            GetMuscleCurve("Attack", "Right Arm Front-Back").Evaluate(0f) > -0.35f)
            throw new InvalidOperationException("Attack arms are not aimed forward.");

        string[] toonMaterials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/CharacterPreview" });
        if (toonMaterials.Length == 0) throw new InvalidOperationException("Character preview toon materials are missing.");
        int validatedToonMaterials = 0;
        foreach (string guid in toonMaterials)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material != null && material.name == "PreviewStage") continue;
            if (material == null || material.shader == null || material.shader.name != "AIFG/Character Preview Toon")
                throw new InvalidOperationException("Character preview material is not using the toon shader: " + AssetDatabase.GUIDToAssetPath(guid));
            validatedToonMaterials++;
        }
        if (validatedToonMaterials == 0) throw new InvalidOperationException("No character materials were validated with the toon shader.");
    }

    private static void AssertConstant(string state, string muscle, float? expected = null)
    {
        AnimationCurve curve = GetMuscleCurve(state, muscle);
        float value = curve.keys[0].value;
        if (curve.keys.Any(key => Mathf.Abs(key.value - value) > 0.0001f))
            throw new InvalidOperationException(state + " must keep " + muscle + " stationary.");
        if (expected.HasValue && Mathf.Abs(value - expected.Value) > 0.0001f)
            throw new InvalidOperationException(state + " has an unexpected " + muscle + " value.");
    }

    private static void AssertAnimated(string state, string muscle)
    {
        AnimationCurve curve = GetMuscleCurve(state, muscle);
        float minimum = curve.keys.Min(key => key.value);
        float maximum = curve.keys.Max(key => key.value);
        if (maximum - minimum < 0.001f)
            throw new InvalidOperationException(state + " is missing subtle body sway.");
    }

    private static AnimationCurve GetMuscleCurve(string state, string muscle)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/CharacterPreview/" + state + ".anim");
        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle));
        if (curve == null || curve.length == 0)
            throw new InvalidOperationException(state + " is missing muscle curve " + muscle + ".");
        return curve;
    }

    private static double Elapsed()
    {
        double start = double.Parse(SessionState.GetString(TimeKey, "0"));
        return EditorApplication.timeSinceStartup - start;
    }

    private static string[] ScreenshotPaths => new[]
    {
        HinaIdleScreenshotPath,
        HinaRunScreenshotPath,
        HinaAttackScreenshotPath,
        MomoiAttackScreenshotPath,
        MikaAttackScreenshotPath,
        AyaneAttackScreenshotPath,
        YuukaAttackScreenshotPath,
        YuukaWeapon1ScreenshotPath,
        YuukaWeapon2ScreenshotPath,
        MomoiRetreatScreenshotPath,
        MomoiCoverScreenshotPath
    };

    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        EditorApplication.Exit(1);
    }
}
