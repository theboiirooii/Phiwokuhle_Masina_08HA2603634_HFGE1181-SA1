#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Generates every serialized asset of the SA1 project: sprite import settings,
/// tiles, prefabs, animation clips/controllers, the level, HUD/menus and scenes.
/// Run from Tools/SA1/Build Project Content or through -executeMethod in batch mode.
/// </summary>
public static class SA1ProjectBuilder
{
    private const string ArtRoot = "Assets/Art";
    private const string CharactersFolder = "Assets/Art/Characters";
    private const string LevelFolder = "Assets/Art/Level";
    private const string UiArtFolder = "Assets/Art/UI";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string TileFolder = "Assets/Tiles";
    private const string AnimFolder = "Assets/Animations";
    private const string SceneFolder = "Assets/Scenes";
    private const string AudioFolder = "Assets/Audio";

    private const string TilesetPath = LevelFolder + "/Donut_Level_Tiles.png";
    private const string WallTilePath = LevelFolder + "/Wall_Tile.png";
    private const string FloorTilePath = LevelFolder + "/Floor_Tile.png";
    private const string ArrowPath = UiArtFolder + "/IndicatorArrow.png";
    private const string FoodSpritePath = LevelFolder + "/Food_Donut.png";

    private const string GameScenePath = SceneFolder + "/Game.unity";
    private const string MenuScenePath = SceneFolder + "/MainMenu.unity";

    private const int GridWidth = 24;
    private const int GridHeight = 18;
    private const float CellSize = 2f;
    private static readonly Vector3 GridOrigin = new Vector3(-24f, -18f, 0f);
    private static readonly Vector3 PlayerStart = new Vector3(-1f, -1f, 0f);

    private static readonly Color Ink = new Color(0.24f, 0.13f, 0.07f, 1f);
    private static readonly Color Cream = new Color(1f, 0.93f, 0.82f, 1f);
    private static readonly Color WarmBackdrop = new Color(0.96f, 0.86f, 0.72f, 1f);

    // ------------------------------------------------------------------ entry points

    [MenuItem("Tools/SA1/Build Project Content")]
    public static void BuildAll()
    {
        EnsureFolders();
        ConfigureLayersTagsAndSorting();
        ConfigurePlayerSettings();
        PrepareSpriteImports();
        CreateWallTexture();
        CreateFloorTexture();
        CreateArrowTexture();
        CreateTileAssets();
        CreateAnimationClipsAndControllers();
        CreatePrefabs();
        CreateGameScene();
        CreateMainMenuScene();
        ConfigureBuildSettings();
        RemoveTemplateLeftovers();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SA1 project content generated successfully.");
    }

    public static void BuildAllBatch()
    {
        try
        {
            BuildAll();
            Debug.Log("SA1_BUILD_OK");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("SA1_BUILD_FAILED\n" + exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Builds a desktop player used to smoke-test the scenes without a device.</summary>
    public static void BuildWindowsBatch()
    {
        try
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = "Builds/Windows/EatGrow.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("SA1_WINBUILD " + report.summary.result + " bytes=" + report.summary.totalSize);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogError("SA1_WINBUILD_FAILED\n" + exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Builds the Android APK delivered with the assignment.</summary>
    public static void BuildAndroidBatch()
    {
        try
        {
            ConfigureAndroidExternalTools();
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = "Builds/Android/CasualTopDown.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("SA1_APKBUILD " + report.summary.result + " bytes=" + report.summary.totalSize);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogError("SA1_APKBUILD_FAILED\n" + exception);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Points Unity at the toolchain that ships with the Android build support module.</summary>
    private static void ConfigureAndroidExternalTools()
    {
        string editorRoot = Path.GetDirectoryName(EditorApplication.applicationPath);
        string androidPlayer = Path.Combine(editorRoot, "Data/PlaybackEngines/AndroidPlayer");

        Type settings = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
        if (settings == null)
        {
            Debug.LogWarning("SA1_JDK AndroidExternalToolsSettings type not found.");
            return;
        }

        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
        string[][] tools =
        {
            new[] { "jdkRootPath", Path.Combine(androidPlayer, "OpenJDK") },
            new[] { "sdkRootPath", Path.Combine(androidPlayer, "SDK") },
            new[] { "ndkRootPath", Path.Combine(androidPlayer, "NDK") }
        };

        foreach (string[] tool in tools)
        {
            System.Reflection.PropertyInfo property = settings.GetProperty(tool[0], flags);
            if (property == null)
            {
                Debug.LogWarning("SA1_JDK property not found: " + tool[0]);
                continue;
            }

            property.SetValue(null, tool[1]);
            Debug.Log("SA1_JDK " + tool[0] + "=" + tool[1] + " exists=" + Directory.Exists(tool[1]));
        }
    }

    /// <summary>Imports the TMP essential resources and waits for the asynchronous import to finish.</summary>
    public static void ImportTmpEssentialsBatch()
    {
        string marker = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        if (File.Exists(marker))
        {
            Debug.Log("SA1_TMP_ALREADY_PRESENT");
            EditorApplication.Exit(0);
            return;
        }

        string package = Directory
            .GetFiles(Path.GetFullPath("Library/PackageCache"), "TMP Essential Resources.unitypackage",
                SearchOption.AllDirectories)
            .FirstOrDefault();

        if (package == null)
        {
            Debug.LogError("SA1_TMP_PACKAGE_NOT_FOUND");
            EditorApplication.Exit(1);
            return;
        }

        AssetDatabase.ImportPackage(package, false);
        double started = EditorApplication.timeSinceStartup;

        void Poll()
        {
            AssetDatabase.Refresh();

            if (File.Exists(marker) || EditorApplication.timeSinceStartup - started > 240)
            {
                Debug.Log("SA1_TMP_IMPORT_DONE " + File.Exists(marker));
                EditorApplication.Exit(File.Exists(marker) ? 0 : 1);
            }
        }

        EditorApplication.update += Poll;
    }

    /// <summary>Renders the first frame of the Game scene to PNGs for quick visual review.</summary>
    public static void RenderPreviewBatch()
    {
        try
        {
            EditorSceneManager.OpenScene(GameScenePath);
            Camera camera = Camera.main;

            if (camera == null)
            {
                throw new Exception("No Main Camera found in Game scene.");
            }

            // Overlay canvases are excluded from Camera.Render, so project them into world space for the preview.
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            float worldHeight = camera.orthographicSize * 2f;
            float canvasScale = worldHeight / 1080f;
            Vector3 canvasPosition = camera.transform.position + camera.transform.forward * 1f;

            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    continue;
                }

                RectTransform rect = canvas.GetComponent<RectTransform>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                rect.sizeDelta = new Vector2(1920f, 1080f);
                rect.SetPositionAndRotation(canvasPosition, camera.transform.rotation);
                rect.localScale = Vector3.one * canvasScale;
            }

            Canvas.ForceUpdateCanvases();
            RenderCamera(camera, "Screenshots/GamePreview.png");

            foreach (Canvas canvas in canvases)
            {
                canvas.gameObject.SetActive(false);
            }

            GameObject overviewObject = new GameObject("PreviewOverviewCamera");
            Camera overview = overviewObject.AddComponent<Camera>();
            overview.orthographic = true;
            overview.orthographicSize = 19f;
            overview.transform.position = new Vector3(0f, 0f, -10f);
            overview.clearFlags = CameraClearFlags.SolidColor;
            overview.backgroundColor = camera.backgroundColor;
            RenderCamera(overview, "Screenshots/LevelOverview.png");

            Debug.Log("SA1_PREVIEW_OK");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("SA1_PREVIEW_FAILED\n" + exception);
            EditorApplication.Exit(1);
        }
    }

    private static void RenderCamera(Camera camera, string path)
    {
        const int width = 1600;
        const int height = 900;
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture.active = renderTexture;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;

        camera.targetTexture = null;
        Directory.CreateDirectory("Screenshots");
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        renderTexture.Release();
        Object.DestroyImmediate(renderTexture);
    }

    // ------------------------------------------------------------------ project configuration

    private static void EnsureFolders()
    {
        EnsureFolder(UiArtFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(TileFolder);
        EnsureFolder(AnimFolder + "/Player");
        EnsureFolder(AnimFolder + "/Enemy");
        EnsureFolder(SceneFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        if (Directory.Exists(Path.GetFullPath(path)))
        {
            AssetDatabase.Refresh();
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void ConfigureLayersTagsAndSorting()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets.Length == 0)
        {
            throw new Exception("TagManager.asset could not be loaded.");
        }

        SerializedObject manager = new SerializedObject(assets[0]);
        SerializedProperty layers = manager.FindProperty("layers");

        SetUserLayer(layers, 6, "Player");
        SetUserLayer(layers, 7, "Enemy");
        SetUserLayer(layers, 8, "Food");
        SetUserLayer(layers, 9, "Wall");

        SerializedProperty tags = manager.FindProperty("tags");
        AddTag(tags, "Enemy");
        AddTag(tags, "Food");

        SerializedProperty sortingLayers = manager.FindProperty("m_SortingLayers");
        AddSortingLayer(sortingLayers, "Floor", 101);
        AddSortingLayer(sortingLayers, "Walls", 102);
        AddSortingLayer(sortingLayers, "Units", 103);
        AddSortingLayer(sortingLayers, "FX", 104);

        manager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetUserLayer(SerializedProperty layers, int index, string name)
    {
        while (layers.arraySize <= index)
        {
            layers.arraySize++;
        }

        layers.GetArrayElementAtIndex(index).stringValue = name;
    }

    private static void AddTag(SerializedProperty tags, string name)
    {
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == name)
            {
                return;
            }
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = name;
    }

    private static void AddSortingLayer(SerializedProperty sortingLayers, string name, int uniqueId)
    {
        for (int i = 0; i < sortingLayers.arraySize; i++)
        {
            if (sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
            {
                return;
            }
        }

        sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
        SerializedProperty entry = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
        entry.FindPropertyRelative("name").stringValue = name;
        entry.FindPropertyRelative("uniqueID").intValue = uniqueId;
        entry.FindPropertyRelative("locked").boolValue = false;
    }

    private static void ConfigurePlayerSettings()
    {
        SerializedObject projectSettings =
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        SerializedProperty inputHandler = projectSettings.FindProperty("activeInputHandler");

        if (inputHandler != null)
        {
            inputHandler.intValue = 2; // Both old and new input backends
            projectSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerSettings.companyName = "Phiwokuhle Masina";
        PlayerSettings.productName = "Casual Top Down";
        PlayerSettings.bundleVersion = "1.0.0";

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,
            "com.phiwokuhlemasina.hfge1181sa1");
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Android,
            ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android,
            ManagedStrippingLevel.Low);
    }

    // ------------------------------------------------------------------ art import

    private static void PrepareSpriteImports()
    {
        foreach (string path in Directory.GetFiles(CharactersFolder, "*.png"))
        {
            ConfigureSprite(path, 256, SpriteImportMode.Single);
        }

        ConfigureSprite(FoodSpritePath, 256, SpriteImportMode.Single);

        foreach (string path in Directory.GetFiles(LevelFolder, "Crumbs*.png"))
        {
            ConfigureSprite(path, 256, SpriteImportMode.Single);
        }

        ConfigureSprite(TilesetPath, 64, SpriteImportMode.Single);
    }

    private static void ConfigureSprite(string path, int pixelsPerUnit, SpriteImportMode mode)
    {
        path = path.Replace('\\', '/');

        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            Debug.LogWarning("Missing texture importer for " + path);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = mode;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 2048;
        importer.isReadable = true;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.filterMode = FilterMode.Bilinear;
        settings.mipmapEnabled = false;
        settings.alphaIsTransparency = true;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static void CreateWallTexture()
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetPath);

        if (source == null)
        {
            throw new Exception("Tileset texture not found for wall generation.");
        }

        // Cell (4,2) is the cleanest waffle slice in the tileset (few black edge pixels).
        const int originX = 4 * 128 + 16;
        const int originY = (7 - 2) * 128 + 16;

        Texture2D wall = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[128 * 128];
        Color32 frame = new Color32(30, 18, 10, 255);

        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                bool border = x < 5 || x > 122 || y < 5 || y > 122;

                if (border)
                {
                    pixels[y * 128 + x] = frame;
                    continue;
                }

                int sx = originX + Mathf.Clamp((int)((x - 5) * 96f / 118f), 0, 95);
                int sy = originY + Mathf.Clamp((int)((y - 5) * 96f / 118f), 0, 95);
                pixels[y * 128 + x] = source.GetPixel(sx, sy);
            }
        }

        wall.SetPixels32(pixels);
        wall.Apply();
        File.WriteAllBytes(WallTilePath, wall.EncodeToPNG());
        Object.DestroyImmediate(wall);
        AssetDatabase.ImportAsset(WallTilePath, ImportAssetOptions.ForceUpdate);
        ConfigureSprite(WallTilePath, 64, SpriteImportMode.Single);
    }

    private static void CreateFloorTexture()
    {
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetPath);

        if (source == null)
        {
            throw new Exception("Tileset texture not found for floor generation.");
        }

        // Cell (0,0) provides the flat background colour; a subtle woven crosshatch keeps it readable.
        Color32[] sourcePixels = source.GetPixels32();
        Color32 baseColor = sourcePixels[(7 * 128 + 10) * source.width + 10];

        Texture2D floor = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[128 * 128];

        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                bool warp = ((x + y) & 31) < 2;
                bool weft = ((x - y + 128) & 31) < 2;
                Color32 color = baseColor;

                if (warp)
                {
                    color = Shade(baseColor, 0.92f);
                }
                else if (weft)
                {
                    color = Shade(baseColor, 1.06f);
                }

                pixels[y * 128 + x] = color;
            }
        }

        floor.SetPixels32(pixels);
        floor.Apply();
        File.WriteAllBytes(FloorTilePath, floor.EncodeToPNG());
        Object.DestroyImmediate(floor);
        AssetDatabase.ImportAsset(FloorTilePath, ImportAssetOptions.ForceUpdate);
        ConfigureSprite(FloorTilePath, 64, SpriteImportMode.Single);
    }

    private static Color32 Shade(Color32 color, float factor)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255),
            color.a);
    }

    private static void CreateArrowTexture()
    {
        const int size = 64;
        Texture2D arrow = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        Vector2 a = new Vector2(10f, 32f);
        Vector2 b = new Vector2(50f, 12f);
        Vector2 c = new Vector2(50f, 52f);
        Vector2 innerA = Vector2.Lerp(a, new Vector2(37f, 32f), 0.35f);
        Vector2 innerB = Vector2.Lerp(b, new Vector2(37f, 32f), 0.35f);
        Vector2 innerC = Vector2.Lerp(c, new Vector2(37f, 32f), 0.35f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Color32 color = new Color32(0, 0, 0, 0);

                if (InTriangle(p, a, b, c))
                {
                    color = new Color32(40, 18, 8, 210);
                }

                if (InTriangle(p, innerA, innerB, innerC))
                {
                    color = new Color32(255, 236, 196, 255);
                }

                pixels[y * size + x] = color;
            }
        }

        arrow.SetPixels32(pixels);
        arrow.Apply();
        File.WriteAllBytes(ArrowPath, arrow.EncodeToPNG());
        Object.DestroyImmediate(arrow);
        AssetDatabase.ImportAsset(ArrowPath, ImportAssetOptions.ForceUpdate);
        ConfigureSprite(ArrowPath, 64, SpriteImportMode.Single);
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);
        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    // ------------------------------------------------------------------ tiles

    private static void CreateTileAssets()
    {
        Sprite floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorTilePath);
        Sprite wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallTilePath);

        if (floorSprite == null || wallSprite == null)
        {
            throw new Exception("Tile sprites could not be loaded.");
        }

        CreateTileAsset(TileFolder + "/Floor.asset", floorSprite, Tile.ColliderType.None);
        CreateTileAsset(TileFolder + "/Wall.asset", wallSprite, Tile.ColliderType.Grid);
    }

    private static Tile CreateTileAsset(string path, Sprite sprite, Tile.ColliderType colliderType)
    {
        if (AssetDatabase.LoadAssetAtPath<Tile>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.colliderType = colliderType;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }

    // ------------------------------------------------------------------ animations

    private static void CreateAnimationClipsAndControllers()
    {
        AnimationClip playerIdle = CreateSpriteClip(AnimFolder + "/Player/PlayerIdle.anim", "PlayerIdle",
            new[] { "Player0004", "Player0006" }, 2.5f, true, true);
        AnimationClip playerMove = CreateSpriteClip(AnimFolder + "/Player/PlayerMove.anim", "PlayerMove",
            new[] { "Player0012", "Player0014", "Player0016", "Player0030" }, 10f, true, true);
        AnimationClip playerBite = CreateSpriteClip(AnimFolder + "/Player/PlayerBite.anim", "PlayerBite",
            new[] { "Player0000", "Player0002", "Player0014" }, 16f, false, true);
        AnimationClip playerGrow = CreateSpriteClip(AnimFolder + "/Player/PlayerGrow.anim", "PlayerGrow",
            new[] { "Player0022" }, 2f, false, true);
        AnimationClip playerDie = CreateSpriteClip(AnimFolder + "/Player/PlayerDie.anim", "PlayerDie",
            new[] { "Player0024", "Player0026", "Player0028" }, 8f, false, true);

        AddAnimationEvent(playerBite, 0.02f, "PlaySound", "Eat");
        AddAnimationEvent(playerBite, 0.05f, "SpawnParticle", null);
        AddAnimationEvent(playerGrow, 0.05f, "PlaySound", "Grow");

        AnimationClip enemyIdle = CreateSpriteClip(AnimFolder + "/Enemy/EnemyIdle.anim", "EnemyIdle",
            new[] { "Enemy0004", "Enemy0006" }, 2.5f, true, false);
        AnimationClip enemyMove = CreateSpriteClip(AnimFolder + "/Enemy/EnemyMove.anim", "EnemyMove",
            new[] { "Enemy0000", "Enemy0012", "Enemy0014", "Enemy0016" }, 9f, true, false);
        AnimationClip enemyBite = CreateSpriteClip(AnimFolder + "/Enemy/EnemyBite.anim", "EnemyBite",
            new[] { "Enemy0002", "Enemy0014", "Enemy0018" }, 14f, false, false);

        AddAnimationEvent(enemyBite, 0.02f, "PlaySound", "EnemyBite");
        AddAnimationEvent(enemyBite, 0.05f, "SpawnParticle", null);

        CreatePlayerController(playerIdle, playerMove, playerBite, playerGrow, playerDie);
        CreateEnemyController(enemyIdle, enemyMove, enemyBite);
    }

    private static AnimationClip CreateSpriteClip(string path, string clipName, string[] spriteNames, float fps,
        bool loop, bool playerFrames)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        Sprite[] sprites = spriteNames
            .Select(name => AssetDatabase.LoadAssetAtPath<Sprite>($"{CharactersFolder}/{name}.png"))
            .ToArray();

        if (sprites.Any(sprite => sprite == null))
        {
            throw new Exception("Missing sprite for clip " + clipName);
        }

        AnimationClip clip = new AnimationClip { name = clipName, frameRate = fps };
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        List<ObjectReferenceKeyframe> keys = new List<ObjectReferenceKeyframe>();
        for (int i = 0; i < sprites.Length; i++)
        {
            keys.Add(new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] });
        }

        keys.Add(new ObjectReferenceKeyframe
        {
            time = sprites.Length / fps,
            value = loop ? sprites[0] : sprites[sprites.Length - 1]
        });

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void AddAnimationEvent(AnimationClip clip, float time, string functionName, string parameter)
    {
        List<AnimationEvent> events = AnimationUtility.GetAnimationEvents(clip).ToList();
        events.Add(new AnimationEvent { time = time, functionName = functionName, stringParameter = parameter ?? "" });
        AnimationUtility.SetAnimationEvents(clip, events.ToArray());
    }

    private static void CreatePlayerController(AnimationClip idle, AnimationClip move, AnimationClip bite,
        AnimationClip grow, AnimationClip die)
    {
        const string path = AnimFolder + "/Player/PlayerAnimator.controller";

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Bite", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Grow", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle;
        AnimatorState moveState = machine.AddState("Move");
        moveState.motion = move;
        AnimatorState biteState = machine.AddState("Bite");
        biteState.motion = bite;
        AnimatorState growState = machine.AddState("Grow");
        growState.motion = grow;
        AnimatorState dieState = machine.AddState("Die");
        dieState.motion = die;
        machine.defaultState = idleState;

        AnimatorStateTransition idleToMove = idleState.AddTransition(moveState);
        idleToMove.hasExitTime = false;
        idleToMove.duration = 0.08f;
        idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition moveToIdle = moveState.AddTransition(idleState);
        moveToIdle.hasExitTime = false;
        moveToIdle.duration = 0.08f;
        moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AddAnyStateTriggerTransition(machine, biteState, "Bite", 0.02f);
        AnimatorStateTransition biteOut = biteState.AddTransition(idleState);
        biteOut.hasExitTime = true;
        biteOut.exitTime = 1f;
        biteOut.duration = 0.05f;

        AddAnyStateTriggerTransition(machine, growState, "Grow", 0.05f);
        AnimatorStateTransition growOut = growState.AddTransition(idleState);
        growOut.hasExitTime = true;
        growOut.exitTime = 1f;
        growOut.duration = 0.08f;

        AddAnyStateTriggerTransition(machine, dieState, "Die", 0.02f);
    }

    private static void CreateEnemyController(AnimationClip idle, AnimationClip move, AnimationClip bite)
    {
        const string path = AnimFolder + "/Enemy/EnemyAnimator.controller";

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Bite", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle;
        AnimatorState moveState = machine.AddState("Move");
        moveState.motion = move;
        AnimatorState biteState = machine.AddState("Bite");
        biteState.motion = bite;
        machine.defaultState = idleState;

        AnimatorStateTransition idleToMove = idleState.AddTransition(moveState);
        idleToMove.hasExitTime = false;
        idleToMove.duration = 0.1f;
        idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition moveToIdle = moveState.AddTransition(idleState);
        moveToIdle.hasExitTime = false;
        moveToIdle.duration = 0.1f;
        moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AddAnyStateTriggerTransition(machine, biteState, "Bite", 0.02f);
        AnimatorStateTransition biteOut = biteState.AddTransition(idleState);
        biteOut.hasExitTime = true;
        biteOut.exitTime = 1f;
        biteOut.duration = 0.05f;
    }

    private static void AddAnyStateTriggerTransition(AnimatorStateMachine machine, AnimatorState targetState,
        string triggerName, float duration)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(targetState);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    // ------------------------------------------------------------------ prefabs

    private static GameObject playerPrefab;
    private static GameObject enemyPrefab;
    private static GameObject foodPrefab;
    private static GameObject indicatorPrefab;
    private static GameObject crumbBurstPrefab;

    private static void CreatePrefabs()
    {
        crumbBurstPrefab = CreateCrumbBurstPrefab();

        playerPrefab = CreatePlayerPrefab();
        enemyPrefab = CreateEnemyPrefab();
        foodPrefab = CreateFoodPrefab();
        indicatorPrefab = CreateIndicatorPrefab();
    }

    private static GameObject CreateCrumbBurstPrefab()
    {
        GameObject particleObject = new GameObject("CrumbBurst");
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingLayerName = "FX";
        particleRenderer.sortingOrder = 20;
        particleRenderer.material = CreateParticleMaterial();

        particleObject.transform.position = Vector3.zero;
        return SavePrefab(particleObject, PrefabFolder + "/CrumbBurst.prefab");
    }

    private static Material CreateParticleMaterial()
    {
        const string path = PrefabFolder + "/CrumbParticle.mat";

        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        Material material = new Material(shader) { name = "CrumbParticle" };
        Texture2D crumbTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LevelFolder + "/Crumbs0004.png");
        material.mainTexture = crumbTexture;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreatePlayerPrefab()
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Player");

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CharactersFolder + "/Player0004.png");
        renderer.sortingLayerName = "Units";
        renderer.sortingOrder = 0;

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D collider = player.AddComponent<CircleCollider2D>();
        collider.radius = 0.7f;

        Animator animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimFolder + "/Player/PlayerAnimator.controller");
        animator.applyRootMotion = false;

        player.AddComponent<PlayerSizeHandler>();
        player.AddComponent<PlayerController>();
        player.AddComponent<PlaySoundEvent>();

        SpawnParticleEvent spawnParticle = player.AddComponent<SpawnParticleEvent>();
        spawnParticle.particlePrefab = crumbBurstPrefab;

        EnemyCollisionFeedback collisionFeedback = player.AddComponent<EnemyCollisionFeedback>();
        SetObjectField(collisionFeedback, "particleEvent", spawnParticle);

        return SavePrefab(player, PrefabFolder + "/Player.prefab");
    }

    private static GameObject CreateEnemyPrefab()
    {
        GameObject enemy = new GameObject("Enemy");
        enemy.tag = "Enemy";
        enemy.layer = LayerMask.NameToLayer("Enemy");

        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CharactersFolder + "/Enemy0004.png");
        renderer.sortingLayerName = "Units";
        renderer.sortingOrder = 0;

        Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
        collider.radius = 0.72f;
        collider.isTrigger = true;

        Animator animator = enemy.AddComponent<Animator>();
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimFolder + "/Enemy/EnemyAnimator.controller");
        animator.applyRootMotion = false;

        EnemySizeHandler sizeHandler = enemy.AddComponent<EnemySizeHandler>();
        EnemyAI ai = enemy.AddComponent<EnemyAI>();
        SetObjectField(ai, "anim", animator);
        SetLayerMask(ai, "obstacleLayers", "Wall");
        SetFloat(ai, "obstacleCheckRadius", 0.72f);
        SetVector2(ai, "boundsMinimum", new Vector2(-21f, -15f));
        SetVector2(ai, "boundsMaximum", new Vector2(21f, 15f));

        enemy.AddComponent<PlaySoundEvent>();

        SpawnParticleEvent spawnParticle = enemy.AddComponent<SpawnParticleEvent>();
        spawnParticle.particlePrefab = crumbBurstPrefab;

        return SavePrefab(enemy, PrefabFolder + "/Enemy.prefab");
    }

    private static GameObject CreateFoodPrefab()
    {
        GameObject food = new GameObject("Food");
        food.tag = "Food";
        food.layer = LayerMask.NameToLayer("Food");

        SpriteRenderer renderer = food.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FoodSpritePath);
        renderer.sortingLayerName = "Units";
        renderer.sortingOrder = -1;

        Rigidbody2D body = food.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.gravityScale = 0f;

        CircleCollider2D collider = food.AddComponent<CircleCollider2D>();
        collider.radius = 0.6f;
        collider.isTrigger = true;

        food.AddComponent<FoodSizeHandler>();

        return SavePrefab(food, PrefabFolder + "/Food.prefab");
    }

    private static GameObject CreateIndicatorPrefab()
    {
        GameObject indicator = new GameObject("TargetIndicator");
        indicator.AddComponent<TargetIndicator>();

        GameObject arrow = new GameObject("Arrow");
        arrow.transform.SetParent(indicator.transform, false);
        SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
        renderer.sortingLayerName = "FX";
        renderer.sortingOrder = 40;
        arrow.transform.localScale = Vector3.one * 0.6f;

        return SavePrefab(indicator, PrefabFolder + "/TargetIndicator.prefab");
    }

    private static GameObject SavePrefab(GameObject source, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
        Object.DestroyImmediate(source);
        return prefab;
    }

    // ------------------------------------------------------------------ game scene

    private static void CreateGameScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(new Vector3(PlayerStart.x, PlayerStart.y, -10f), 5.5f, new Color(0.13f, 0.08f, 0.05f));
        CreateGlobalLight();

        GameObject player =
            (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Player.prefab"));
        player.name = "Player";
        player.transform.position = PlayerStart;

        CameraHandler cameraHandler = Object.FindFirstObjectByType<CameraHandler>();
        SetObjectField(cameraHandler, "target", player);
        SetFloat(cameraHandler, "baseZOffset", -10f);
        SetFloat(cameraHandler, "baseOrthographicSize", 5.5f);
        SetFloat(cameraHandler, "zoomPerSize", 0.35f);

        CreateLevelTilemaps();

        GameObject gameStateManager = new GameObject("GameStateManager");
        gameStateManager.AddComponent<GameStateManager>();

        CreateSoundManager(true);

        GameObject systems = new GameObject("Systems");

        GameObject indicators = new GameObject("Indicators");
        indicators.transform.SetParent(systems.transform, false);
        FollowTarget follow = indicators.AddComponent<FollowTarget>();
        SetObjectField(follow, "target", player.transform);

        GameObject spawnManagerObject = new GameObject("SpawnManager");
        spawnManagerObject.transform.SetParent(systems.transform, false);
        SpawnManager spawnManager = spawnManagerObject.AddComponent<SpawnManager>();
        SetObjectField(spawnManager, "playerSizeHandler", player.GetComponent<PlayerSizeHandler>());
        SetObjectField(spawnManager, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Enemy.prefab"));
        SetObjectField(spawnManager, "foodPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Food.prefab"));
        SetObjectField(spawnManager, "targetIndicatorPrefab",
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/TargetIndicator.prefab"));
        SetObjectField(spawnManager, "targetIndicatorParent", indicators.transform);
        SetVector2(spawnManager, "spawnOffsetMinimum", new Vector2(-21f, -15f));
        SetVector2(spawnManager, "spawnOffsetMaximum", new Vector2(21f, 15f));
        SetLayerMaskField(spawnManager, "overlapLayers", "Wall", "Player", "Enemy", "Food");

        CreateGameUi(player);

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), GameScenePath);
    }

    private static void CreateCamera(Vector3 position, float orthographicSize, Color background)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = position;

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = orthographicSize;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 100f;

        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<CameraHandler>();
    }

    private static void CreateGlobalLight()
    {
        GameObject lightObject = new GameObject("Global Light 2D");
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = Color.white;
        light.intensity = 1f;
        light.shadowsEnabled = false;
    }

    private static void CreateLevelTilemaps()
    {
        GameObject gridObject = new GameObject("Grid");
        Grid grid = gridObject.AddComponent<Grid>();
        grid.cellSize = new Vector3(CellSize, CellSize, 0f);
        gridObject.transform.position = GridOrigin;

        GameObject floorObject = new GameObject("Floor");
        floorObject.transform.SetParent(gridObject.transform, false);
        Tilemap floorTilemap = floorObject.AddComponent<Tilemap>();
        TilemapRenderer floorRenderer = floorObject.AddComponent<TilemapRenderer>();
        floorRenderer.sortingLayerName = "Floor";
        floorRenderer.sortingOrder = 0;

        GameObject wallObject = new GameObject("Walls");
        wallObject.transform.SetParent(gridObject.transform, false);
        wallObject.layer = LayerMask.NameToLayer("Wall");
        Tilemap wallTilemap = wallObject.AddComponent<Tilemap>();
        TilemapRenderer wallRenderer = wallObject.AddComponent<TilemapRenderer>();
        wallRenderer.sortingLayerName = "Walls";
        wallRenderer.sortingOrder = 0;

        TilemapCollider2D tilemapCollider = wallObject.AddComponent<TilemapCollider2D>();
        Rigidbody2D wallBody = wallObject.AddComponent<Rigidbody2D>();
        wallBody.bodyType = RigidbodyType2D.Static;
        CompositeCollider2D composite = wallObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        tilemapCollider.usedByComposite = true;

        Tile floorTile = AssetDatabase.LoadAssetAtPath<Tile>(TileFolder + "/Floor.asset");
        Tile wallTile = AssetDatabase.LoadAssetAtPath<Tile>(TileFolder + "/Wall.asset");
        bool[,] wallMap = BuildWallMap();

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                floorTilemap.SetTile(cell, floorTile);

                if (wallMap[x, y])
                {
                    wallTilemap.SetTile(cell, wallTile);
                }
            }
        }

        floorTilemap.RefreshAllTiles();
        wallTilemap.RefreshAllTiles();
        composite.GenerateGeometry();
    }

    private static bool[,] BuildWallMap()
    {
        bool[,] wall = new bool[GridWidth, GridHeight];

        for (int x = 0; x < GridWidth; x++)
        {
            wall[x, 0] = true;
            wall[x, GridHeight - 1] = true;
        }

        for (int y = 0; y < GridHeight; y++)
        {
            wall[0, y] = true;
            wall[GridWidth - 1, y] = true;
        }

        // Four enclosed buildings with apertures of 1, 2, 3 and 4 cells.
        AddBuilding(wall, 3, 10, 9, 14, new[]
        {
            new Vector2Int(5, 10), new Vector2Int(6, 10), new Vector2Int(7, 10), new Vector2Int(9, 12)
        });
        AddBuilding(wall, 14, 10, 20, 13, new[]
        {
            new Vector2Int(15, 10), new Vector2Int(16, 10), new Vector2Int(17, 10), new Vector2Int(18, 10),
            new Vector2Int(14, 11), new Vector2Int(14, 12)
        });
        AddBuilding(wall, 3, 3, 9, 7, new[]
        {
            new Vector2Int(4, 7), new Vector2Int(5, 7), new Vector2Int(6, 7), new Vector2Int(9, 5)
        });
        AddBuilding(wall, 14, 3, 20, 7, new[]
        {
            new Vector2Int(15, 7), new Vector2Int(16, 7), new Vector2Int(17, 7), new Vector2Int(14, 5),
            new Vector2Int(14, 6)
        });

        return wall;
    }

    private static void AddBuilding(bool[,] wall, int minX, int minY, int maxX, int maxY, Vector2Int[] doors)
    {
        for (int x = minX; x <= maxX; x++)
        {
            wall[x, minY] = true;
            wall[x, maxY] = true;
        }

        for (int y = minY; y <= maxY; y++)
        {
            wall[minX, y] = true;
            wall[maxX, y] = true;
        }

        foreach (Vector2Int door in doors)
        {
            wall[door.x, door.y] = false;
        }
    }

    // ------------------------------------------------------------------ audio

    private static void CreateSoundManager(bool includeMusic)
    {
        GameObject soundObject = new GameObject("SoundManager");
        SoundManager manager = soundObject.AddComponent<SoundManager>();

        List<Sound> sounds = new List<Sound>();

        if (includeMusic)
        {
            sounds.Add(new Sound
            {
                name = "Music",
                clip = LoadClip("music_kulluh_Exploring_The_Ocean_Floor_42"),
                volume = 0.35f,
                pitch = 1f,
                loop = true
            });
        }

        sounds.Add(new Sound
        {
            name = "Eat",
            clip = LoadClip("zapsplat_cartoon_frog_lizard_tongue_lick_eat_insect_001_94311"),
            volume = 0.8f,
            pitch = 1f,
            loop = false
        });
        sounds.Add(new Sound
        {
            name = "EnemyBite",
            clip = LoadClip("zapsplat_cartoon_frog_lizard_tongue_lick_eat_insect_002_94312"),
            volume = 0.75f,
            pitch = 1f,
            loop = false
        });
        sounds.Add(new Sound
        {
            name = "Squelch",
            clip = LoadClip("zapsplat_foley_liquid_gel_squelch_splat_short_47413"),
            volume = 0.6f,
            pitch = 1.05f,
            loop = false
        });
        sounds.Add(new Sound
        {
            name = "Death",
            clip = LoadClip("zapsplat_foley_liquid_gel_squelch_splat_short_47413"),
            volume = 0.9f,
            pitch = 0.85f,
            loop = false
        });
        sounds.Add(new Sound
        {
            name = "Grow",
            clip = LoadClip("zapsplat_foley_liquid_gel_squelch_splat_short_47413"),
            volume = 0.7f,
            pitch = 1.35f,
            loop = false
        });
        sounds.Add(new Sound
        {
            name = "Click",
            clip = LoadClip("zapsplat_cartoon_frog_lizard_tongue_lick_eat_insect_002_94312"),
            volume = 0.5f,
            pitch = 1.6f,
            loop = false
        });

        manager.soundList = sounds.ToArray();

        if (includeMusic)
        {
            PlaySoundOnStart music = soundObject.AddComponent<PlaySoundOnStart>();
            SetStringField(music, "soundName", "Music");
            SetFloat(music, "delay", 0.75f);
        }
    }

    private static AudioClip LoadClip(string fileName)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + fileName + ".mp3");

        if (clip == null)
        {
            Debug.LogWarning("Audio clip missing: " + fileName);
        }

        return clip;
    }

    // ------------------------------------------------------------------ UI

    private static void CreateGameUi(GameObject player)
    {
        SceneFader fader = CreateFaderCanvas();

        GameObject canvasObject = new GameObject("GameCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>());

        GameObject uiRoot = new GameObject("UI");
        canvasObject.transform.SetParent(uiRoot.transform);

        CreateEventSystem();

        // ---- HUD
        GameObject hud = NewUiObject("HUD", canvasObject.transform);
        Stretch(hud.GetComponent<RectTransform>());

        TextMeshProUGUI sizeText = CreateText(hud.transform, "SizeValue", "Size: 1.00", 50f, Ink,
            TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(52f, -36f), new Vector2(520f, 80f), FontStyles.Bold);

        TextMeshProUGUI timerText = CreateText(hud.transform, "Timer", "00:00", 56f, Ink,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(420f, 90f), FontStyles.Bold);

        GameObject pauseButtonObject = NewUiObject("PauseButton", hud.transform);
        Image pauseImage = pauseButtonObject.AddComponent<Image>();
        pauseImage.color = new Color(1f, 0.93f, 0.82f, 0.92f);
        SetRect(pauseButtonObject.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-44f, -30f), new Vector2(130f, 96f));
        Button pauseButton = pauseButtonObject.AddComponent<Button>();
        pauseButton.targetGraphic = pauseImage;

        CreateText(pauseButtonObject.transform, "Label", "II", 46f, Ink, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, FontStyles.Bold);

        // ---- virtual joysticks
        Sprite donut = AssetDatabase.LoadAssetAtPath<Sprite>(FoodSpritePath);
        CreateJoystick(canvasObject.transform, "JoystickMove", VirtualJoystick.StickType.Move, donut,
            new Vector2(0f, 0f), new Vector2(320f, 260f), 360f);
        CreateJoystick(canvasObject.transform, "JoystickAim", VirtualJoystick.StickType.Aim, donut,
            new Vector2(1f, 0f), new Vector2(-320f, 260f), 360f);

        // ---- pause menu
        GameObject pauseMenu = CreateMenuRoot("PauseMenu", canvasObject.transform);
        GameObject pausePanel = CreateMenuPanel(pauseMenu.transform, new Vector2(640f, 820f));
        CreateText(pausePanel.transform, "Title", "PAUSED", 100f, Ink, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f),
            new Vector2(560f, 140f), FontStyles.Bold);

        Button resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "RESUME",
            new Vector2(0f, 60f), new Vector2(440f, 120f), 52f);
        Button restartButton = CreateButton(pausePanel.transform, "RestartButton", "RESTART",
            new Vector2(0f, -90f), new Vector2(440f, 120f), 52f);
        Button quitButton = CreateButton(pausePanel.transform, "QuitButton", "QUIT",
            new Vector2(0f, -240f), new Vector2(440f, 120f), 52f);

        // ---- game over menu
        GameObject gameOverMenu = CreateMenuRoot("GameOverMenu", canvasObject.transform);
        GameObject gameOverPanel = CreateMenuPanel(gameOverMenu.transform, new Vector2(760f, 700f));
        CreateText(gameOverPanel.transform, "Title", "GAME OVER", 98f, new Color(0.55f, 0.16f, 0.08f),
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(680f, 140f), FontStyles.Bold);

        TextMeshProUGUI stats = CreateText(gameOverPanel.transform, "Stats", "", 44f, Ink,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -215f), new Vector2(660f, 180f), FontStyles.Normal);

        Button retryButton = CreateButton(gameOverPanel.transform, "RetryButton", "RETRY",
            new Vector2(0f, -150f), new Vector2(440f, 120f), 52f);
        Button menuButton = CreateButton(gameOverPanel.transform, "MenuButton", "MAIN MENU",
            new Vector2(0f, -300f), new Vector2(440f, 120f), 52f);

        pauseMenu.SetActive(false);
        gameOverMenu.SetActive(false);

        // ---- manager + wiring
        GameObject managerObject = new GameObject("UIManager");
        managerObject.transform.SetParent(uiRoot.transform, false);
        UIManager ui = managerObject.AddComponent<UIManager>();
        SetObjectField(ui, "hud", hud);
        SetObjectField(ui, "playerSizeHandler", player.GetComponent<PlayerSizeHandler>());
        SetObjectField(ui, "tmpSize", sizeText);
        SetObjectField(ui, "tmpTimer", timerText);
        SetObjectField(ui, "pauseMenu", pauseMenu);
        SetObjectField(ui, "gameOverMenu", gameOverMenu);
        SetObjectField(ui, "tmpGameOverStats", stats);
        SetObjectField(ui, "sceneFader", fader);
        SetStringField(ui, "mainMenuSceneName", "MainMenu");

        PlaySoundEvent uiAudio = managerObject.AddComponent<PlaySoundEvent>();

        WireButton(pauseButton, uiAudio, "Click", ui.Pause, null);
        WireButton(resumeButton, uiAudio, "Click", ui.Resume, null);
        WireButton(restartButton, uiAudio, "Click", ui.Restart, null);
        WireButton(quitButton, uiAudio, "Click", ui.QuitGame, null);
        WireButton(retryButton, uiAudio, "Click", ui.Restart, null);
        WireButton(menuButton, uiAudio, "Click", ui.MainMenu, null);
    }

    private static void CreateJoystick(Transform parent, string name, VirtualJoystick.StickType type, Sprite sprite,
        Vector2 anchor, Vector2 position, float diameter)
    {
        GameObject baseObject = NewUiObject(name, parent);
        Image baseImage = baseObject.AddComponent<Image>();
        baseImage.sprite = sprite;
        baseImage.color = new Color(1f, 1f, 1f, 0.38f);
        baseImage.preserveAspect = true;
        baseImage.raycastTarget = true;
        SetRect(baseObject.GetComponent<RectTransform>(), anchor, anchor, new Vector2(0.5f, 0.5f), position,
            new Vector2(diameter, diameter));

        GameObject handle = NewUiObject("Handle", baseObject.transform);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.sprite = sprite;
        handleImage.color = new Color(1f, 0.96f, 0.88f, 0.9f);
        handleImage.preserveAspect = true;
        handleImage.raycastTarget = false;
        SetRect(handle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter * 0.46f, diameter * 0.46f));

        VirtualJoystick joystick = baseObject.AddComponent<VirtualJoystick>();
        SetEnumField(joystick, "stickType", (int)type);
        SetObjectField(joystick, "handle", handle.GetComponent<RectTransform>());
        SetFloat(joystick, "handleRange", diameter * 0.28f);
        SetFloat(joystick, "deadZone", 0.12f);
    }

    private static GameObject CreateMenuRoot(string name, Transform parent)
    {
        GameObject root = NewUiObject(name, parent);
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0.09f, 0.05f, 0.03f, 0.72f);
        Stretch(root.GetComponent<RectTransform>());
        return root;
    }

    private static GameObject CreateMenuPanel(Transform parent, Vector2 size)
    {
        GameObject panel = NewUiObject("Panel", parent);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.98f, 0.91f, 0.8f, 1f);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return panel;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position,
        Vector2 size, float fontSize)
    {
        GameObject buttonObject = NewUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = Cream;
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, size);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.62f);
        colors.pressedColor = new Color(0.94f, 0.74f, 0.5f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        CreateText(buttonObject.transform, "Label", label, fontSize, Ink, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, FontStyles.Bold);

        return button;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize,
        Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size, FontStyles style)
    {
        GameObject textObject = NewUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        SetRect(textObject.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, position, size);

        if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
        {
            Stretch(textObject.GetComponent<RectTransform>());
        }

        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static GameObject NewUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem),
            typeof(StandaloneInputModule));
        eventSystem.GetComponent<StandaloneInputModule>();
    }

    private static SceneFader CreateFaderCanvas()
    {
        GameObject canvasObject = new GameObject("FaderCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>());

        GameObject fadeObject = NewUiObject("Fade", canvasObject.transform);
        Image fadeImage = fadeObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        Stretch(fadeObject.GetComponent<RectTransform>());

        SceneFader fader = canvasObject.AddComponent<SceneFader>();

        CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        return fader;
    }

    private static void ConfigureCanvasScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static void WireButton(Button button, PlaySoundEvent sound, string soundName,
        UnityEngine.Events.UnityAction action, UnityEngine.Events.UnityAction<string> unused)
    {
        if (sound != null && !string.IsNullOrEmpty(soundName))
        {
            UnityEventTools.AddStringPersistentListener(button.onClick, sound.PlaySound, soundName);
        }

        if (action != null)
        {
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }
    }

    // ------------------------------------------------------------------ main menu scene

    private static void CreateMainMenuScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(new Vector3(0f, 0f, -10f), 5.5f, WarmBackdrop);
        CreateGlobalLight();

        SceneFader fader = CreateFaderCanvas();

        GameObject canvasObject = new GameObject("MenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        ConfigureCanvasScaler(canvasObject.GetComponent<CanvasScaler>());

        CreateEventSystem();

        GameObject background = NewUiObject("Background", canvasObject.transform);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = WarmBackdrop;
        Stretch(background.GetComponent<RectTransform>());

        Sprite donut = AssetDatabase.LoadAssetAtPath<Sprite>(FoodSpritePath);

        CreateDecorativeDonut(canvasObject.transform, donut, new Vector2(-610f, 250f), 220f, 18f, 0.55f);
        CreateDecorativeDonut(canvasObject.transform, donut, new Vector2(610f, 300f), 170f, -24f, 0.45f);
        CreateDecorativeDonut(canvasObject.transform, donut, new Vector2(520f, -240f), 260f, 40f, 0.35f);
        CreateDecorativeDonut(canvasObject.transform, donut, new Vector2(-520f, -260f), 140f, -12f, 0.4f);

        GameObject hero = NewUiObject("HeroDonut", canvasObject.transform);
        Image heroImage = hero.AddComponent<Image>();
        heroImage.sprite = donut;
        heroImage.preserveAspect = true;
        heroImage.raycastTarget = false;
        SetRect(hero.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(480f, 480f));
        hero.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

        CreateText(canvasObject.transform, "Title", "CASUAL TOP DOWN", 132f, Ink, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f),
            new Vector2(1600f, 220f), FontStyles.Bold);

        CreateText(canvasObject.transform, "Subtitle", "Eat. Grow. Don't get eaten.", 52f,
            new Color(0.45f, 0.27f, 0.14f), TextAlignmentOptions.Center, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1200f, 90f),
            FontStyles.Italic);

        Button playButton = CreateButton(canvasObject.transform, "PlayButton", "PLAY", new Vector2(0f, -190f),
            new Vector2(500f, 130f), 60f);
        Button exitButton = CreateButton(canvasObject.transform, "ExitButton", "EXIT", new Vector2(0f, -345f),
            new Vector2(500f, 130f), 60f);

        CreateText(canvasObject.transform, "Footer", "Phiwokuhle Masina  |  08HA2603634  |  HFGE1181 SA1",
            30f, new Color(0.42f, 0.25f, 0.13f), TextAlignmentOptions.Center, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(1500f, 60f),
            FontStyles.Normal);

        CreateSoundManager(false);

        GameObject navigationObject = new GameObject("SceneNavigation");
        SceneNavigation navigation = navigationObject.AddComponent<SceneNavigation>();
        SetStringField(navigation, "sceneName", "Game");
        SetObjectField(navigation, "sceneFader", fader);
        PlaySoundEvent menuAudio = navigationObject.AddComponent<PlaySoundEvent>();

        WireButton(playButton, menuAudio, "Click", navigation.LoadScene, null);
        WireButton(exitButton, menuAudio, "Click", navigation.ExitGame, null);

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), MenuScenePath);
    }

    private static void CreateDecorativeDonut(Transform parent, Sprite sprite, Vector2 position, float size,
        float rotation, float alpha)
    {
        if (sprite == null)
        {
            return;
        }

        GameObject donut = NewUiObject("DecorDonut", parent);
        Image image = donut.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, alpha);
        SetRect(donut.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        donut.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    // ------------------------------------------------------------------ build settings

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }

    private static void RemoveTemplateLeftovers()
    {
        DeleteAssetIfPresent("Assets/Scenes/SampleScene.unity");
        DeleteAssetIfPresent("Assets/InputSystem_Actions.inputactions");
        DeleteAssetIfPresent("Assets/Settings/Scenes/URP2DSceneTemplate.unity");
    }

    private static void DeleteAssetIfPresent(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    // ------------------------------------------------------------------ serialized field helpers

    private static SerializedProperty FindRequired(Object target, string field)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(field);

        if (property == null)
        {
            throw new Exception($"Field '{field}' not found on {target.GetType().Name}.");
        }

        return property;
    }

    private static void SetObjectField(Object target, string field, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(field);

        if (property == null)
        {
            throw new Exception($"Field '{field}' not found on {target.GetType().Name}.");
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(field).floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStringField(Object target, string field, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(field).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector2(Object target, string field, Vector2 value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(field).vector2Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnumField(Object target, string field, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(field).enumValueIndex = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(Object target, string field, params string[] layerNames)
    {
        int mask = 0;

        foreach (string layerName in layerNames)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer >= 0)
            {
                mask |= 1 << layer;
            }
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(field);

        if (property == null)
        {
            throw new Exception($"Field '{field}' not found on {target.GetType().Name}.");
        }

        property.intValue = mask;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMaskField(Object target, string field, params string[] layerNames)
    {
        SetLayerMask(target, field, layerNames);
    }
}
#endif
