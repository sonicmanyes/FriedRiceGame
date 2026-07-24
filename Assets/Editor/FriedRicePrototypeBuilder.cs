using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FriedRicePrototypeBuilder
{
    private const string ScenePath = "Assets/Scenes/FriedRicePrototype.unity";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MaterialFolder = "Assets/Materials";

    [InitializeOnLoadMethod]
    private static void CreateAutomaticallyOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!File.Exists(ScenePath))
            {
                CreatePrototypeScene();
                return;
            }

            EnsureSceneInBuildSettings();

            GameObject pan = GameObject.Find("PanRoot");
            if (pan != null && pan.GetComponent<RiceGuideTube>() == null)
            {
                pan.AddComponent<RiceGuideTube>();
                EditorSceneManager.MarkSceneDirty(pan.scene);
                EditorSceneManager.SaveScene(pan.scene);
                Debug.Log("Invisible rice guide tube added to PanRoot.");
            }

            if (pan != null)
            {
                if (pan.GetComponent<SlowMotionCommandSystem>() == null)
                    pan.AddComponent<SlowMotionCommandSystem>();
                if (pan.GetComponent<DragonRiseTechnique>() == null)
                    pan.AddComponent<DragonRiseTechnique>();
                if (pan.GetComponent<TornadoSpinTechnique>() == null)
                    pan.AddComponent<TornadoSpinTechnique>();
                if (pan.GetComponent<PanCookingAudio>() == null)
                    pan.AddComponent<PanCookingAudio>();
                if (pan.GetComponent<GameSessionController>() == null)
                    pan.AddComponent<GameSessionController>();

                SlowMotionCommandSystem commandSystem = pan.GetComponent<SlowMotionCommandSystem>();
                SerializedObject commandData = new SerializedObject(commandSystem);
                commandData.FindProperty("commandTime").floatValue = 3.5f;
                SerializedProperty goodGain = commandData.FindProperty("goodGaugeGain");
                SerializedProperty greatGain = commandData.FindProperty("greatGaugeGain");
                SerializedProperty perfectGain = commandData.FindProperty("perfectGaugeGain");
                if (goodGain != null) goodGain.floatValue = 5f;
                if (greatGain != null) greatGain.floatValue = 10f;
                if (perfectGain != null) perfectGain.floatValue = 15f;
                commandData.ApplyModifiedPropertiesWithoutUndo();

                TornadoSpinTechnique tornadoTechnique = pan.GetComponent<TornadoSpinTechnique>();
                SerializedObject tornadoData = new SerializedObject(tornadoTechnique);
                tornadoData.FindProperty("mashDuration").floatValue = 3.5f;
                tornadoData.FindProperty("stageTwoMashes").intValue = 6;
                tornadoData.FindProperty("stageThreeMashes").intValue = 12;
                tornadoData.FindProperty("maximumScoringMashes").intValue = 18;
                tornadoData.ApplyModifiedPropertiesWithoutUndo();

                DragonRiseTechnique dragonTechnique = pan.GetComponent<DragonRiseTechnique>();
                SerializedObject dragonData = new SerializedObject(dragonTechnique);
                SerializedProperty riseSoundProperty = dragonData.FindProperty("riseSound");
                SerializedProperty impactSoundProperty = dragonData.FindProperty("impactSound");
                SerializedProperty impactBoomProperty = dragonData.FindProperty("impactBoomSound");
                if (riseSoundProperty.objectReferenceValue == null)
                    riseSoundProperty.objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/龍昇飯SE.mp3");
                if (impactSoundProperty.objectReferenceValue == null)
                    impactSoundProperty.objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/龍昇飯ラスト.mp3");
                if (impactBoomProperty.objectReferenceValue == null)
                    impactBoomProperty.objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RyushoB0m.mp3");
                dragonData.ApplyModifiedPropertiesWithoutUndo();

                RiceGuideTube guideTube = pan.GetComponent<RiceGuideTube>();
                SerializedObject guideData = new SerializedObject(guideTube);
                guideData.FindProperty("innerRadius").floatValue = 0.70f;
                guideData.FindProperty("height").floatValue = 2.2f;
                guideData.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(pan.scene);
                EditorSceneManager.SaveScene(pan.scene);

                Transform oldBase = pan.transform.Find("PanBase");
                if (oldBase != null && oldBase.TryGetComponent(out MeshRenderer oldBaseRenderer))
                    oldBaseRenderer.enabled = false;

                if (pan.transform.Find("WokVisual") == null)
                {
                    GameObject visualBowl = new GameObject("WokVisual");
                    visualBowl.transform.SetParent(pan.transform, false);
                    visualBowl.AddComponent<MeshFilter>();
                    MeshRenderer bowlRenderer = visualBowl.AddComponent<MeshRenderer>();
                    bowlRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Pan.mat");
                    visualBowl.AddComponent<WokVisualMesh>();
                    EditorSceneManager.MarkSceneDirty(pan.scene);
                    EditorSceneManager.SaveScene(pan.scene);
                    Debug.Log("Curved wok visual added without changing colliders.");
                }

                if (pan.transform.Find("DecorativeRice") == null)
                {
                    GameObject decorativeRice = new GameObject("DecorativeRice");
                    decorativeRice.transform.SetParent(pan.transform, false);
                    decorativeRice.AddComponent<ParticleSystem>();
                    decorativeRice.AddComponent<DecorativeRiceParticles>();
                    EditorSceneManager.MarkSceneDirty(pan.scene);
                    EditorSceneManager.SaveScene(pan.scene);
                    Debug.Log("260 decorative rice particles added without physics cost.");
                }
            }

            RiceSpawner openSceneSpawner = Object.FindFirstObjectByType<RiceSpawner>();
            if (openSceneSpawner != null)
            {
                SerializedObject spawnerData = new SerializedObject(openSceneSpawner);
                SerializedProperty amountProperty = spawnerData.FindProperty("amount");
                if (amountProperty != null && amountProperty.intValue < 126)
                {
                    amountProperty.intValue = 126;
                    spawnerData.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(openSceneSpawner.gameObject.scene);
                    EditorSceneManager.SaveScene(openSceneSpawner.gameObject.scene);
                    Debug.Log("Ingredient amount upgraded to 126.");
                }
            }
        };
    }

    [MenuItem("Fried Rice/Create Prototype Scene")]
    public static void CreatePrototypeScene()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Material panMat = CreateMaterial("Pan", new Color(0.05f, 0.06f, 0.075f), 0.7f, 0.7f);
        Material handleMat = CreateMaterial("Handle", new Color(0.025f, 0.018f, 0.015f), 0.05f, 0.35f);
        Material riceMat = CreateMaterial("Rice", new Color(1f, 0.78f, 0.25f), 0f, 0.25f);
        Material stoveMat = CreateMaterial("Stove", new Color(0.1f, 0.12f, 0.15f), 0.45f, 0.55f);

        CreateCamera();
        CreateLights();
        CreateStove(stoveMat);
        GameObject pan = CreatePan(panMat, handleMat);
        Rigidbody ricePrefab = CreateRicePrefab(riceMat);
        CreateSpawner(pan.transform, ricePrefab);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = pan;
        EditorUtility.DisplayDialog("Fried Rice", "完成！ 再生して Space キーで鍋を振ってみよう。", "OK");
    }

    private static GameObject CreatePan(Material panMat, Material handleMat)
    {
        GameObject root = new GameObject("PanRoot");
        root.transform.position = new Vector3(0f, 1.25f, 0f);
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        root.AddComponent<PanTossController>();
        root.AddComponent<RiceGuideTube>();
        root.AddComponent<SlowMotionCommandSystem>();
        root.AddComponent<DragonRiseTechnique>();
        root.AddComponent<TornadoSpinTechnique>();
        root.AddComponent<PanCookingAudio>();
        root.AddComponent<GameSessionController>();

        PhysicsMaterial panPhysics = CreatePhysicsMaterial("PanPhysics", 0.08f, 0f);
        GameObject bottom = Primitive("PanBase", PrimitiveType.Cylinder, root.transform,
            Vector3.zero, new Vector3(0.78f, 0.045f, 0.78f), panMat);
        Object.DestroyImmediate(bottom.GetComponent<Collider>());
        BoxCollider bottomCollider = bottom.AddComponent<BoxCollider>();
        bottomCollider.size = new Vector3(1.72f, 2f, 1.72f);
        bottomCollider.material = panPhysics;
        bottom.GetComponent<MeshRenderer>().enabled = false;

        GameObject visualBowl = new GameObject("WokVisual");
        visualBowl.transform.SetParent(root.transform, false);
        visualBowl.AddComponent<MeshFilter>();
        MeshRenderer bowlRenderer = visualBowl.AddComponent<MeshRenderer>();
        bowlRenderer.sharedMaterial = panMat;
        visualBowl.AddComponent<WokVisualMesh>();

        GameObject decorativeRice = new GameObject("DecorativeRice");
        decorativeRice.transform.SetParent(root.transform, false);
        decorativeRice.AddComponent<ParticleSystem>();
        decorativeRice.AddComponent<DecorativeRiceParticles>();

        const int count = 16;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 pos = new Vector3(Mathf.Sin(angle) * 0.78f, 0.19f, Mathf.Cos(angle) * 0.78f);
            GameObject wall = Primitive("PanWall_" + i.ToString("00"), PrimitiveType.Cube,
                root.transform, pos, new Vector3(0.34f, 0.34f, 0.08f), panMat);
            wall.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            wall.GetComponent<Collider>().material = panPhysics;
        }

        Primitive("Handle", PrimitiveType.Cube, root.transform,
            new Vector3(0f, 0.06f, -1.47f), new Vector3(0.2f, 0.16f, 1.65f), handleMat);
        GameObject cap = Primitive("HandleEnd", PrimitiveType.Sphere, root.transform,
            new Vector3(0f, 0.06f, -2.28f), new Vector3(0.25f, 0.19f, 0.32f), handleMat);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        return root;
    }

    private static Rigidbody CreateRicePrefab(Material material)
    {
        GameObject grain = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        grain.name = "RiceGrain";
        grain.transform.localScale = new Vector3(0.035f, 0.075f, 0.035f);
        grain.GetComponent<Renderer>().sharedMaterial = material;
        grain.GetComponent<Collider>().material = CreatePhysicsMaterial("RicePhysics", 0.12f, 0.03f);
        Rigidbody rb = grain.AddComponent<Rigidbody>();
        rb.mass = 0.002f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        grain.AddComponent<RiceGrain>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(grain, PrefabFolder + "/RiceGrain.prefab");
        Object.DestroyImmediate(grain);
        return prefab.GetComponent<Rigidbody>();
    }

    private static void CreateSpawner(Transform pan, Rigidbody prefab)
    {
        GameObject point = new GameObject("RiceSpawnPoint");
        point.transform.SetParent(pan, false);
        point.transform.localPosition = new Vector3(0f, 0.31f, 0.05f);
        RiceSpawner spawner = point.AddComponent<RiceSpawner>();
        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("ricePrefab").objectReferenceValue = prefab;
        so.FindProperty("amount").intValue = 126;
        so.FindProperty("spawnArea").vector3Value = new Vector3(0.40f, 0.08f, 0.40f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateCamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        cam.fieldOfView = 47f;
        cam.nearClipPlane = 0.05f;
        cam.backgroundColor = new Color(0.035f, 0.05f, 0.08f);
        go.transform.position = new Vector3(0f, 2.8f, -5.25f);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.25f, 0f) - go.transform.position);
    }

    private static void CreateLights()
    {
        GameObject keyGo = new GameObject("Key Light");
        Light key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.35f;
        key.color = new Color(1f, 0.84f, 0.67f);
        key.shadows = LightShadows.Soft;
        keyGo.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

        GameObject fillGo = new GameObject("Warm Fill Light");
        Light fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.range = 7f;
        fill.intensity = 4f;
        fill.color = new Color(1f, 0.3f, 0.05f);
        fillGo.transform.position = new Vector3(0f, 0.65f, 0.2f);
    }

    private static void CreateStove(Material material)
    {
        Primitive("Counter", PrimitiveType.Cube, null,
            new Vector3(0f, 0.35f, 0.35f), new Vector3(4.4f, 0.72f, 3.1f), material);
        Primitive("Stove", PrimitiveType.Cylinder, null,
            new Vector3(0f, 0.82f, 0.12f), new Vector3(1.15f, 0.14f, 1.15f), material);
    }

    private static GameObject Primitive(string name, PrimitiveType type, Transform parent,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private static PhysicsMaterial CreatePhysicsMaterial(string name, float friction, float bounce)
    {
        string path = MaterialFolder + "/" + name + ".physicMaterial";
        PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (mat == null)
        {
            mat = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.dynamicFriction = friction;
        mat.staticFriction = friction;
        mat.bounciness = bounce;
        mat.frictionCombine = PhysicsMaterialCombine.Minimum;
        mat.bounceCombine = PhysicsMaterialCombine.Minimum;
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void EnsureSceneInBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == ScenePath)
                return;
        }
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
