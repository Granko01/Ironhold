using UnityEditor;
using UnityEngine;

// Shared low-level helpers for building chunky, low-poly primitive-based
// models and editing their prefabs in place (keeps the same asset GUID
// across rebuilds so existing scene instances stay linked).
public static class LowPolyKit
{
    public const string MaterialFolder = "Assets/Materials";

    public static Transform CreateBlock(string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 size, Material mat, PrimitiveType primitive = PrimitiveType.Cube)
    {
        GameObject go = GameObject.CreatePrimitive(primitive);
        go.name = name;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    // Creates an empty pivot ("hinge") with a mesh block hanging below it, so animating
    // the hinge's localEulerAngles swings the block like a pendulum from its top - the
    // standard trick for rigging arms/legs out of primitives without real bones.
    public static Transform CreateHingeLimb(string name, Transform parent, Vector3 hingeLocalPos, Vector3 size, Material mat, out Transform mesh)
    {
        GameObject hinge = new GameObject(name);
        hinge.transform.SetParent(parent, false);
        hinge.transform.localPosition = hingeLocalPos;

        mesh = CreateBlock(name + "_Mesh", hinge.transform, new Vector3(0f, -size.y * 0.5f, 0f), Vector3.zero, size, mat);
        return hinge.transform;
    }

    public static Material GetOrCreateMaterial(string name, Color color, bool unlit = false)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        string shaderName = unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";

        if (mat == null)
        {
            mat = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = color;
        if (mat.shader == null || mat.shader.name != shaderName)
        {
            mat.shader = Shader.Find(shaderName);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static readonly System.Collections.Generic.Dictionary<PrimitiveType, string> BuiltinMeshNames = new System.Collections.Generic.Dictionary<PrimitiveType, string>
    {
        { PrimitiveType.Cube, "Cube.fbx" },
        { PrimitiveType.Sphere, "Sphere.fbx" },
        { PrimitiveType.Cylinder, "Cylinder.fbx" },
        { PrimitiveType.Capsule, "Capsule.fbx" },
        { PrimitiveType.Plane, "Plane.fbx" },
        { PrimitiveType.Quad, "Quad.fbx" },
    };

    // Turns `go` itself into a primitive-shaped mesh (unlike CreateBlock, which makes a
    // new child) - for prefab roots that need to be edited in place across rebuilds
    // while still being a simple Cube/Sphere/etc. visually.
    public static void SetPrimitiveMesh(GameObject go, PrimitiveType type, bool addCollider = true)
    {
        MeshFilter filter = GetOrAddComponent<MeshFilter>(go);
        filter.sharedMesh = Resources.GetBuiltinResource<Mesh>(BuiltinMeshNames[type]);
        GetOrAddComponent<MeshRenderer>(go);

        if (!addCollider) return;

        if (type == PrimitiveType.Sphere) GetOrAddComponent<SphereCollider>(go);
        else if (type == PrimitiveType.Capsule) GetOrAddComponent<CapsuleCollider>(go);
        else GetOrAddComponent<BoxCollider>(go);
    }

    public static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    // Opens the prefab at `path` for in-place editing if it exists (preserving its GUID
    // and every reference to it), or creates a fresh root GameObject named after the file.
    public static GameObject BeginPrefabEdit(string path, out bool wasLoadedFromDisk)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            wasLoadedFromDisk = true;
            return PrefabUtility.LoadPrefabContents(path);
        }

        wasLoadedFromDisk = false;
        return new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
    }

    // Strips any mesh the root itself was rendering (e.g. an older CreatePrimitive-based
    // build put a MeshFilter/MeshRenderer directly on the root) so it goes back to being
    // a plain, invisible component container once the visuals move onto child rig parts.
    public static void RemoveOwnMesh(GameObject go)
    {
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) Object.DestroyImmediate(renderer);

        MeshFilter filter = go.GetComponent<MeshFilter>();
        if (filter != null) Object.DestroyImmediate(filter);
    }

    public static void ClearChildren(GameObject go)
    {
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }
    }

    public static GameObject EndPrefabEdit(GameObject go, string path, bool wasLoadedFromDisk)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        if (wasLoadedFromDisk) PrefabUtility.UnloadPrefabContents(go);
        else Object.DestroyImmediate(go);
        return saved;
    }

    // Builds `path` relative to `root` (e.g. "Hips/Torso/ArmR"), the format AnimationClip
    // curves need. Returns "" when target IS root.
    public static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return string.Empty;

        System.Collections.Generic.List<string> segments = new System.Collections.Generic.List<string>();
        Transform t = target;
        while (t != null && t != root)
        {
            segments.Add(t.name);
            t = t.parent;
        }
        segments.Reverse();
        return string.Join("/", segments);
    }
}
