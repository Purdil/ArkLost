using System;
using System.IO;
using _Scripts.CombatSystem;
using LostArk.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class KamenBossHpBarBuilder
{
    private const string Root = "Assets/KamenAsset/BossUI";
    private const string ScenePath = "Assets/_Scenes/GameScene.unity";
    private const string FramePath = Root + "/Textures/KamenBossFrame_RefMatched_Clean.png";
    private const string FillPath = Root + "/Textures/KamenBossFillNeutral_RefMatched.png";
    private const string HealthMaskPath = Root + "/Textures/KamenBossHealthFillMask_RefMatched.png";
    private const string ToughnessMaskPath = Root + "/Textures/KamenBossToughnessFillMask_RefMatched.png";

    private const float RefWidth = 1056f;
    private const float RefHeight = 128f;
    private const float FrameWidth = 1120f;
    private const float FrameHeight = 136f;

    [MenuItem("Tools/Lost Ark UI/Apply Kamen Boss HP Bar")]
    public static void Apply()
    {
        EnsureFolders();
        ConfigureSprites();

        var scene = EditorSceneManager.OpenScene(ScenePath);
        var canvas = GameObject.Find("MainCanvas");
        if (canvas == null)
            throw new InvalidOperationException("MainCanvas was not found in GameScene.");

        DestroyChild(canvas.transform, "BossHpBar");
        DestroyChild(canvas.transform, "KamenBossHpBar");

        var root = UiObject("KamenBossHpBar", canvas.transform);
        SetTopCenter(root.GetComponent<RectTransform>(), new Vector2(0f, -4f), new Vector2(FrameWidth, 168f), new Vector2(0.5f, 1f));

        var ui = root.AddComponent<KamenBossHpBarUI>();
        BuildVisual(root.transform, ui);
        AssignTargets(ui);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Applied ref-matched Kamen boss HP/toughness bar to MainCanvas.");
    }

    private static void BuildVisual(Transform parent, KamenBossHpBarUI ui)
    {
        var frameSprite = LoadSprite(FramePath);
        var fillSprite = LoadSprite(FillPath);
        var healthMaskSprite = LoadSprite(HealthMaskPath);
        var toughnessMaskSprite = LoadSprite(ToughnessMaskPath);

        var healthMask = AddMaskRoot(parent, "HealthFillMask", healthMaskSprite);
        SetTopCenter(healthMask.GetComponent<RectTransform>(), new Vector2(0f, -FrameHeight * 0.5f), new Vector2(FrameWidth, FrameHeight), new Vector2(0.5f, 0.5f));

        var healthBackground = AddFilledImage(healthMask.transform, "HealthLineBackground", fillSprite, new Color(1f, 0.42f, 0.11f, 1f));
        Stretch(healthBackground.rectTransform, Vector2.zero);

        var healthDamageGhost = AddFilledImage(healthMask.transform, "HealthDamageGhost", fillSprite, new Color(1f, 0.47f, 0.08f, 0f));
        Stretch(healthDamageGhost.rectTransform, Vector2.zero);
        healthDamageGhost.fillAmount = 0f;

        var healthFill = AddFilledImage(healthMask.transform, "HealthFill", fillSprite, new Color(1f, 0.76f, 0.05f, 1f));
        Stretch(healthFill.rectTransform, Vector2.zero);

        var frame = AddImage(parent, "ExtractedFrame", frameSprite, Color.white);
        SetTopCenter(frame.rectTransform, new Vector2(0f, -FrameHeight * 0.5f), new Vector2(FrameWidth, FrameHeight), new Vector2(0.5f, 0.5f));
        frame.preserveAspect = false;

        var toughnessMask = AddMaskRoot(parent, "ToughnessFillMask", toughnessMaskSprite);
        SetTopCenter(toughnessMask.GetComponent<RectTransform>(), new Vector2(0f, -FrameHeight * 0.5f), new Vector2(FrameWidth, FrameHeight), new Vector2(0.5f, 0.5f));

        var toughnessFill = AddFilledImage(toughnessMask.transform, "ToughnessFill", fillSprite, new Color(0.76f, 0.34f, 1f, 0.92f));
        Stretch(toughnessFill.rectTransform, Vector2.zero);

        var category = AddText(parent, "Category", "\uAD70\uB2E8\uC7A5", 13, TextAnchor.MiddleLeft, new Color(0.92f, 0.14f, 0.22f, 0.96f), FontStyle.Bold);
        SetTopCenter(category.rectTransform, RefCenter(106f, 18f), RefSize(108f, 18f), new Vector2(0.5f, 0.5f));

        var title = AddText(parent, "Title", "\uBE5B\uC744 \uAEBC\uB728\uB9AC\uB294 \uC790, \uCE74\uBA58", 14, TextAnchor.MiddleCenter, new Color(1f, 0.18f, 0.28f, 0.96f), FontStyle.Bold);
        SetTopCenter(title.rectTransform, RefCenter(528f, 18f), RefSize(360f, 20f), new Vector2(0.5f, 0.5f));

        var level = AddText(parent, "Level", "Lv.60", 12, TextAnchor.MiddleLeft, new Color(0.74f, 0.58f, 1f, 0.96f), FontStyle.Bold);
        SetTopCenter(level.rectTransform, RefCenter(154f, 68f), RefSize(78f, 17f), new Vector2(0.5f, 0.5f));

        var healthValue = AddText(parent, "HealthValue", "142,551,779,580/142,551,779,580", 12, TextAnchor.MiddleCenter, new Color(0.96f, 0.9f, 0.72f, 0.96f), FontStyle.Bold);
        SetTopCenter(healthValue.rectTransform, RefCenter(560f, 68f), RefSize(420f, 17f), new Vector2(0.5f, 0.5f));

        var line = AddText(parent, "HealthLine", "x 276", 15, TextAnchor.MiddleRight, new Color(1f, 0.84f, 0.12f, 0.98f), FontStyle.Bold);
        SetTopCenter(line.rectTransform, RefCenter(1026f, 68f), RefSize(92f, 18f), new Vector2(0.5f, 0.5f));

        var berserk = AddText(parent, "Berserk", "\uAD11\uD3ED\uD654\uAE4C\uC9C0", 12, TextAnchor.MiddleCenter, new Color(0.92f, 0.82f, 0.22f, 0.96f), FontStyle.Bold);
        SetTopCenter(berserk.rectTransform, RefCenter(72f, 111f), RefSize(112f, 17f), new Vector2(0.5f, 0.5f));

        var race = AddText(parent, "Race", "\uC545\uB9C8", 12, TextAnchor.MiddleLeft, new Color(0.78f, 0.72f, 0.9f, 0.9f), FontStyle.Bold);
        SetTopCenter(race.rectTransform, RefCenter(170f, 111f), RefSize(58f, 17f), new Vector2(0.5f, 0.5f));

        var region = AddText(parent, "Region", "\uBAA8\uB4E0 \uBA74\uC5ED", 12, TextAnchor.MiddleCenter, new Color(0.78f, 0.72f, 0.9f, 0.9f), FontStyle.Bold);
        SetTopCenter(region.rectTransform, RefCenter(528f, 111f), RefSize(120f, 17f), new Vector2(0.5f, 0.5f));

        var serialized = new SerializedObject(ui);
        serialized.FindProperty("healthFillImage").objectReferenceValue = healthFill;
        serialized.FindProperty("healthLineBackgroundImage").objectReferenceValue = healthBackground;
        serialized.FindProperty("healthDamageGhostImage").objectReferenceValue = healthDamageGhost;
        serialized.FindProperty("healthLineGhostImage").objectReferenceValue = healthBackground;
        serialized.FindProperty("toughnessFillImage").objectReferenceValue = toughnessFill;
        serialized.FindProperty("titleText").objectReferenceValue = title;
        serialized.FindProperty("categoryText").objectReferenceValue = category;
        serialized.FindProperty("levelText").objectReferenceValue = level;
        serialized.FindProperty("healthValueText").objectReferenceValue = healthValue;
        serialized.FindProperty("healthLineText").objectReferenceValue = line;
        serialized.FindProperty("berserkText").objectReferenceValue = berserk;
        serialized.FindProperty("raceText").objectReferenceValue = race;
        serialized.FindProperty("regionText").objectReferenceValue = region;
        serialized.FindProperty("healthLineCount").intValue = 276;
        serialized.FindProperty("overrideFillNormalizedPerLine").boolValue = false;
        serialized.FindProperty("fillNormalizedPerLine").floatValue = 1f / 276f;
        serialized.FindProperty("colorStartIndex").intValue = 0;
        serialized.FindProperty("colorStep").intValue = 1;
        serialized.FindProperty("reverseColorOrder").boolValue = false;
        serialized.FindProperty("animateHealthLine").boolValue = true;
        serialized.FindProperty("healthFillSpeed").floatValue = 4.5f;
        serialized.FindProperty("damageGhostDelay").floatValue = 0.22f;
        serialized.FindProperty("damageGhostBurstDuration").floatValue = 0.34f;
        serialized.FindProperty("damageGhostForceCollapseDelay").floatValue = 0.85f;
        serialized.FindProperty("damageGhostTint").colorValue = new Color(1f, 0.47f, 0.08f, 0.92f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
    }

    private static Vector2 RefCenter(float x, float y)
    {
        return new Vector2((x - RefWidth * 0.5f) * (FrameWidth / RefWidth), -y * (FrameHeight / RefHeight));
    }

    private static Vector2 RefSize(float width, float height)
    {
        return new Vector2(width * (FrameWidth / RefWidth), height * (FrameHeight / RefHeight));
    }

    private static void AssignTargets(KamenBossHpBarUI ui)
    {
        var boss = GameObject.Find("BossEnemy");
        var health = boss != null ? boss.GetComponentInChildren<HealthModule>(true) : null;
        var toughness = boss != null ? boss.GetComponentInChildren<ToughnessModule>(true) : null;

        if (health == null)
            health = UnityEngine.Object.FindFirstObjectByType<HealthModule>();

        if (toughness == null)
            toughness = UnityEngine.Object.FindFirstObjectByType<ToughnessModule>();

        var serialized = new SerializedObject(ui);
        serialized.FindProperty("targetHealth").objectReferenceValue = health;
        serialized.FindProperty("targetToughness").objectReferenceValue = toughness;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ui);
    }

    private static GameObject UiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject AddMaskRoot(Transform parent, string name, Sprite sprite)
    {
        var go = UiObject(name, parent);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;

        var mask = go.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        return go;
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = UiObject(name, parent);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image AddFilledImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var image = AddImage(parent, name, sprite, color);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 1f;
        return image;
    }

    private static Text AddText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Color color, FontStyle style)
    {
        var go = UiObject(name, parent);
        var text = go.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static void SetTopCenter(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect, Vector2 padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
        rect.localScale = Vector3.one;
    }

    private static void DestroyChild(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new FileNotFoundException("Missing Kamen boss UI sprite.", path);
        return sprite;
    }

    private static void ConfigureSprites()
    {
        foreach (var texturePath in new[] { FramePath, FillPath, HealthMaskPath, ToughnessMaskPath })
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "KamenAsset");
        EnsureFolder("Assets/KamenAsset", "BossUI");
        EnsureFolder(Root, "Textures");
        EnsureFolder(Root, "Runtime");
        EnsureFolder(Root, "Editor");
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
