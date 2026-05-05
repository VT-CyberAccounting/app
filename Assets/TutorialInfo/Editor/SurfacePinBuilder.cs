using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
#endif

public class SurfacePinBuilder
{
#if UNITY_EDITOR
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/SurfacePin.prefab";
    private const string MaterialPath = "Assets/Prefabs/SurfacePin.mat";

    [MenuItem("Tools/Build Surface Pin Prefab")]
    public static void BuildSurfacePinPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Surface Pin Exists",
                "A SurfacePin prefab already exists. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
        }

        EnsureFolder(PrefabFolder);

        Material material = EnsureMaterial();
        GameObject pin = BuildPinGameObject(material);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pin, PrefabPath);
        Object.DestroyImmediate(pin);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[SurfacePinBuilder] Prefab written to " + PrefabPath);
    }

    private static GameObject BuildPinGameObject(Material material)
    {
        GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pin.name = "SurfacePin";
        pin.transform.localScale = Vector3.one * 0.04f;

        MeshRenderer rend = pin.GetComponent<MeshRenderer>();
        if (rend != null && material != null)
            rend.sharedMaterial = material;

        SphereCollider existing = pin.GetComponent<SphereCollider>();
        if (existing != null) existing.isTrigger = false;

        pin.AddComponent<SurfacePinRef>();

        PointableElement pointable = pin.AddComponent<PointableElement>();
        ColliderSurface surface = pin.AddComponent<ColliderSurface>();
        RayInteractable rayInteractable = pin.AddComponent<RayInteractable>();

        SetPrivateReference(surface, "_collider", existing);
        SetPrivateReference(rayInteractable, "_pointableElement", pointable);
        SetPrivateReference(rayInteractable, "_surface", surface);

        pin.AddComponent<SectionPinInspector>();

        return pin;
    }

    private static Material EnsureMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material mat = new Material(shader) { name = "SurfacePin" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }

    private static void SetPrivateReference(Object target, string fieldName, Object reference)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[SurfacePinBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = reference;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
#endif
}
