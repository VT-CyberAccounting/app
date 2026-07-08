using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;

public static class PanelBuilderUI
{
    public const string RayCanvasInteractionName = "ISDK_RayCanvasInteraction";
    public const string PokeCanvasInteractionName = "ISDK_PokeCanvasInteraction";
    public const string UIPanelLayerName = "UIPanel";

    private const string RayCanvasPrefabPath = "Assets/Prefabs/ISDK_RayCanvasInteraction.prefab";
    private const string PokeCanvasPrefabPath = "Assets/Prefabs/ISDK_PokeCanvasInteraction.prefab";
    private const string BoundsClipperScriptGuid = "e08ab46e8fb05dc46b34e54466dc11e3";

    public static void SetLayerRecursive(GameObject root, string layerName)
    {
        if (root == null) return;

        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[PanelBuilderUI] Layer \"{layerName}\" not found; leaving layers unchanged.");
            return;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            all[i].gameObject.layer = layer;
    }



    public static void EnsureRayAndPokeInteractions(GameObject canvasObj, float topReserve = 0f)
    {
        EnsureCanvasInteraction(canvasObj, RayCanvasInteractionName, RayCanvasPrefabPath, topReserve);
        EnsureCanvasInteraction(canvasObj, PokeCanvasInteractionName, PokeCanvasPrefabPath, topReserve);
    }

    public static GameObject EnsureCanvasInteraction(GameObject canvasObj, string name, string prefabPath, float topReserve)
    {
        if (canvasObj == null) return null;

        Transform existing = FindChildByName(canvasObj.transform, name);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[PanelBuilderUI] Interaction rig prefab not found at \"{prefabPath}\". " +
                $"Create it once from a working {name} before building (see builder docs).");
            return null;
        }

        GameObject created = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (created == null) return null;
        created.name = name;
        Undo.RegisterCreatedObjectUndo(created, "Add " + name);

        AttachCanvasInteraction(created, canvasObj, canvasObj.GetComponent<Canvas>(), resetRect: true);
        SetClipperBounds(created, canvasObj.transform as RectTransform, topReserve);
        return created;
    }

    private static void SetClipperBounds(GameObject interactionRoot, RectTransform canvasRect, float topReserve)
    {
        if (interactionRoot == null) return;

        Vector2 canvasSize = canvasRect != null
            ? canvasRect.sizeDelta
            : new Vector2(MetaTokens.PanelWidth, MetaTokens.PanelHeight);
        Vector3 clipSize = new Vector3(canvasSize.x, canvasSize.y + topReserve, 0.01f);
        Vector3 clipPos = new Vector3(0f, topReserve * 0.5f, 0f);

        MonoBehaviour[] behaviours = interactionRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null) continue;

            MonoScript script = MonoScript.FromMonoBehaviour(mb);
            if (script == null) continue;
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)) != BoundsClipperScriptGuid) continue;

            SerializedObject so = new SerializedObject(mb);
            SerializedProperty sizeProp = so.FindProperty("_size");
            SerializedProperty posProp = so.FindProperty("_position");
            if (sizeProp != null) sizeProp.vector3Value = clipSize;
            if (posProp != null) posProp.vector3Value = clipPos;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void AttachCanvasInteraction(GameObject obj, GameObject canvasObj, Canvas canvas, bool resetRect)
    {
        if (obj == null || canvasObj == null) return;

        Undo.SetTransformParent(obj.transform, canvasObj.transform, false, "Reattach Canvas Interaction");
        obj.transform.SetAsLastSibling();

        if (resetRect && obj.transform is RectTransform rt)
        {
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            StretchFull(rt);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        RewireCanvasReference(obj, canvas);
    }

    private static void RewireCanvasReference(GameObject obj, Canvas canvas)
    {
        if (obj == null || canvas == null) return;

        MonoBehaviour[] behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;

            SerializedObject so = new SerializedObject(behaviours[i]);
            SerializedProperty canvasProp = so.FindProperty("_canvas");
            if (canvasProp != null && canvasProp.propertyType == SerializedPropertyType.ObjectReference)
            {
                canvasProp.objectReferenceValue = canvas;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        Transform[] all = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == name) return all[i];
        return null;
    }

    public static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(go);
    }

    public static void DestroyChildren(GameObject go)
    {
        if (go == null) return;
        Transform t = go.transform;
        for (int i = t.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(t.GetChild(i).gameObject);
    }

    public static void RewireWatchReferences(DataPanelUI dataPanel, ToolPanelUI toolPanel)
    {
        Watch watch = Object.FindAnyObjectByType<Watch>(FindObjectsInactive.Include);
        if (watch == null) return;

        SerializedObject so = new SerializedObject(watch);
        if (dataPanel != null)
        {
            SerializedProperty p = so.FindProperty("dataPanelUI");
            if (p != null) p.objectReferenceValue = dataPanel;
        }
        if (toolPanel != null)
        {
            SerializedProperty p = so.FindProperty("toolPanelUI");
            if (p != null) p.objectReferenceValue = toolPanel;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void WarnIfGrabStackMissing(GameObject root, string builderTag)
    {
        if (root.GetComponent<PanelGrab>() != null) return;
        Debug.LogWarning($"{builderTag} No grab/interaction stack found on this panel. " +
            "Grab components (PanelGrab, GrabFeedback, Grabbable, HandGrabInteractable, OneGrabTranslateTransformer, BoxCollider) " +
            "require Oculus SDK configuration and are not auto-generated from defaults. Duplicate an existing panel or set them up " +
            "manually once; rebuilding preserves them thereafter.");
    }

    public static void ConfigureTintColors(Button btn, Color normal, Color pressed)
    {
        ColorBlock colors = btn.colors;
        colors.normalColor = normal;
        colors.highlightedColor = normal;
        colors.pressedColor = pressed;
        colors.selectedColor = normal;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
    }

    public static void CreateTextElement(Transform parent, string name, string content, float fontSize,
        FontStyles style, Color color, TextAlignmentOptions alignment, float preferredWidth)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
    }

    public static GameObject CreatePillButton(Transform parent, string label, Color bg, Color border,
        Color textColor, float width, float fontSize = MetaTokens.Body2, FontStyles fontStyle = FontStyles.Normal, string displayText = null)
    {
        return UIButton.Create(parent, $"{label}_Btn", displayText ?? label, width: width, fontSize: fontSize).Root;
    }

    private const string BackCullShaderName = "UI/PanelBackCull";
    private const string BackCullMaterialPath = "Assets/Materials/PanelBack.mat";
    private const string FrontCullShaderName = "UI/PanelFrontCull";
    private const string FrontCullMaterialPath = "Assets/Materials/PanelFront.mat";

    public static GameObject CreateBackCover(GameObject borderObj, Color color, float inset = 2f, int radius = 12, string name = "PanelBack")
    {
        GameObject cover = new GameObject(name);
        cover.transform.SetParent(borderObj.transform, false);

        RectTransform rect = cover.AddComponent<RectTransform>();
        StretchFull(rect);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);

        Image img = cover.AddComponent<Image>();
        img.sprite = RoundedSprite.Get(radius);
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;

        Material mat = LoadBackCullMaterial();
        if (mat != null) img.material = mat;

        cover.transform.SetAsLastSibling();
        return cover;
    }

    public static Material LoadBackCullMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BackCullMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find(BackCullShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[PanelBuilderUI] Shader \"{BackCullShaderName}\" not found; the panel back will use the " +
                "default UI material and will be visible (mirrored) from the front. Ensure PanelBackCull.shader has imported.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        Material mat = new Material(shader) { name = "PanelBack" };
        AssetDatabase.CreateAsset(mat, BackCullMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    public static Material LoadFrontCullMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(FrontCullMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find(FrontCullShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[PanelBuilderUI] Shader \"{FrontCullShaderName}\" not found; button fills will stay " +
                "double-sided and visible from behind. Ensure PanelFrontCull.shader has imported.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        Material mat = new Material(shader) { name = "PanelFront" };
        AssetDatabase.CreateAsset(mat, FrontCullMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    public static void MakeFrontFacing(GameObject buttonRoot)
    {
        Material mat = LoadFrontCullMaterial();
        if (mat == null) return;

        Image[] images = buttonRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].gameObject.name == "ButtonBack") continue;
            images[i].material = mat;
        }
    }

    public static void WireBackSideFilter(GameObject root, GameObject canvasObj)
    {
        BackSideInteractionFilter filter = root.GetComponent<BackSideInteractionFilter>();
        if (filter == null) filter = Undo.AddComponent<BackSideInteractionFilter>(root);

        SerializedObject fso = new SerializedObject(filter);
        SerializedProperty planeProp = fso.FindProperty("_panel");
        if (planeProp != null) planeProp.objectReferenceValue = canvasObj.transform;
        fso.ApplyModifiedPropertiesWithoutUndo();

        Transform canvasT = canvasObj.transform;
        int wired = 0;
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb == null || mb == filter) continue;

            SerializedObject so = new SerializedObject(mb);
            SerializedProperty list = so.FindProperty("_interactableFilters");
            if (list == null || !list.isArray) continue;

            bool onCanvas = mb.transform == canvasT || mb.transform.IsChildOf(canvasT);
            bool changed = onCanvas
                ? AddReferenceIfMissing(list, filter, ref wired)
                : RemoveReference(list, filter);

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (wired == 0)
            Debug.LogWarning("[PanelBuilderUI] No poke/ray interactables with an \"_interactableFilters\" field were found " +
                $"under the canvas of \"{root.name}\"; back-side clicks will not be blocked until those interactables exist. " +
                "(The hand-grab interactable on the panel root is intentionally left two-sided.)");
    }

    private static bool AddReferenceIfMissing(SerializedProperty array, Object target, ref int added)
    {
        if (ArrayContainsReference(array, target)) return false;
        int idx = array.arraySize;
        array.InsertArrayElementAtIndex(idx);
        array.GetArrayElementAtIndex(idx).objectReferenceValue = target;
        added++;
        return true;
    }

    private static bool RemoveReference(SerializedProperty array, Object target)
    {
        bool removed = false;
        for (int i = array.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty el = array.GetArrayElementAtIndex(i);
            if (el.propertyType == SerializedPropertyType.ObjectReference && el.objectReferenceValue == target)
            {
                el.objectReferenceValue = null;
                array.DeleteArrayElementAtIndex(i);
                removed = true;
            }
        }
        return removed;
    }

    private static bool ArrayContainsReference(SerializedProperty array, Object target)
    {
        for (int i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == target) return true;
        return false;
    }

    // ---- shared panel shell -------------------------------------------

    private static readonly Color PanelBg = MetaTokens.Alpha(MetaTokens.Sheet, 0.92f);
    private static readonly Color PanelBackBg = MetaTokens.Alpha(MetaTokens.Sheet, 1f);
    private static readonly Color PanelBorderColor = MetaTokens.Alpha(MetaTokens.White, 0.1f);
    private static readonly Color TitleBarBg = MetaTokens.Alpha(MetaTokens.SheetAlt, 0.95f);
    private static readonly Color TitleTextColor = MetaTokens.TextPrimary;
    private static readonly Color ResetBg = MetaTokens.Alpha(MetaTokens.White, 0.05f);
    private static readonly Color ResetBorder = MetaTokens.SheetAlt;
    private static readonly Color ResetText = MetaTokens.NeutralC0;

    public class PanelShell
    {
        public GameObject canvas;
        public GameObject panelRoot;
    }

    public static GameObject PrepareRoot(string objectName)
    {
        GameObject root = GameObject.Find(objectName);
        if (root != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                $"{objectName} Exists",
                $"A {objectName} already exists in the scene. Rebuild its UI? The grab stack on the root is " +
                "preserved; the canvas and interaction rigs are regenerated from prefabs.",
                "Rebuild", "Cancel");
            if (!replace) return null;
            DestroyChildren(root);
        }
        else
        {
            root = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(root, "Build " + objectName);
        }
        return root;
    }

    public static PanelShell BuildShell(GameObject root, string canvasName, string title)
    {
        GameObject canvasObj = CreateCanvas(root, canvasName);
        Undo.RegisterCreatedObjectUndo(canvasObj, "Build " + root.name);

        GameObject primaryPanel = CreatePanelArea(canvasObj, "PrimaryPanel", MetaTokens.PanelHeight);
        GameObject borderObj = CreatePanelBorder(primaryPanel);
        GameObject panelRoot = CreatePanelRoot(borderObj);
        CreateTitleBar(panelRoot, title);
        CreateBackCover(borderObj, PanelBackBg);

        return new PanelShell { canvas = canvasObj, panelRoot = panelRoot };
    }

    public static void FinishShell(GameObject root, GameObject canvasObj, string builderTag)
    {
        ApplyFonts(root);
        EnsureRayAndPokeInteractions(canvasObj);
        SetLayerRecursive(canvasObj, UIPanelLayerName);
        WireBackSideFilter(root, canvasObj);
        WarnIfGrabStackMissing(root, builderTag);
        Selection.activeGameObject = root;
    }

    private static GameObject CreateCanvas(GameObject root, string canvasName)
    {
        GameObject canvasObj = new GameObject(canvasName);
        canvasObj.transform.SetParent(root.transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(MetaTokens.PanelWidth, MetaTokens.PanelHeight);
        canvasRect.pivot = new Vector2(0.5f, 1f);
        canvasRect.localScale = Vector3.one * 0.001f;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.ignoreReversedGraphics = true;

        return canvasObj;
    }

    private static GameObject CreatePanelArea(GameObject canvasObj, string name, float height)
    {
        GameObject area = new GameObject(name);
        area.transform.SetParent(canvasObj.transform, false);

        RectTransform rect = area.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = Vector2.zero;

        return area;
    }

    private static GameObject CreatePanelBorder(GameObject parent)
    {
        GameObject borderObj = new GameObject("PanelBorder");
        borderObj.transform.SetParent(parent.transform, false);

        RectTransform rect = borderObj.AddComponent<RectTransform>();
        StretchFull(rect);

        Image img = borderObj.AddComponent<Image>();
        img.sprite = RoundedSprite.Get(12);
        img.type = Image.Type.Sliced;
        img.color = PanelBorderColor;

        return borderObj;
    }

    private static GameObject CreatePanelRoot(GameObject borderObj)
    {
        GameObject panelRoot = new GameObject("PanelRoot");
        panelRoot.transform.SetParent(borderObj.transform, false);

        RectTransform rect = panelRoot.AddComponent<RectTransform>();
        StretchFull(rect);
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);

        Image img = panelRoot.AddComponent<Image>();
        img.sprite = RoundedSprite.Get(12);
        img.type = Image.Type.Sliced;
        img.color = PanelBg;

        Mask mask = panelRoot.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        VerticalLayoutGroup vlg = panelRoot.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        return panelRoot;
    }

    private static void CreateTitleBar(GameObject panelRoot, string title)
    {
        GameObject titleBar = new GameObject("TitleBar");
        titleBar.transform.SetParent(panelRoot.transform, false);
        titleBar.AddComponent<RectTransform>();

        LayoutElement le = titleBar.AddComponent<LayoutElement>();
        le.minHeight = 60f;
        le.preferredHeight = 60f;
        le.flexibleHeight = 0f;

        Image bg = titleBar.AddComponent<Image>();
        bg.color = TitleBarBg;

        HorizontalLayoutGroup hlg = titleBar.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset((int)MetaTokens.PanelGutter, (int)MetaTokens.PanelGutter,
            (int)MetaTokens.Spacing, (int)MetaTokens.Spacing);
        hlg.spacing = MetaTokens.Spacing;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        CreateTextElement(titleBar.transform, "Title", title, MetaTokens.PanelTitle,
            FontStyles.Normal, TitleTextColor, TextAlignmentOptions.MidlineLeft, 140f);

        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(titleBar.transform, false);
        spacer.AddComponent<RectTransform>();
        LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.flexibleWidth = 1f;

        CreatePillButton(titleBar.transform, "Reset All", ResetBg, ResetBorder, ResetText, 80f);
    }

    private static TMP_FontAsset LoadFont(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:TMP_FontAsset");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    public static void ApplyFonts(GameObject root)
    {
        TMP_FontAsset body = LoadFont("Inter-Medium SDF");
        TMP_FontAsset headline = LoadFont("Inter-Bold SDF");

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (body != null) texts[i].font = body;
            if (headline != null && texts[i].gameObject.name == "Title") texts[i].font = headline;
        }

        MonoBehaviour[] behaviours = root.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;
            SerializedObject so = new SerializedObject(behaviours[i]);
            SerializedProperty bodyProp = so.FindProperty("bodyFont");
            SerializedProperty titleProp = so.FindProperty("titleFont");
            bool changed = false;
            if (bodyProp != null && body != null) { bodyProp.objectReferenceValue = body; changed = true; }
            if (titleProp != null && headline != null) { titleProp.objectReferenceValue = headline; changed = true; }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

#endif
