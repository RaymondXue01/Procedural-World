using UnityEngine;
using System;
using System.Collections.Generic;
using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;
using Random = UnityEngine.Random;

// Generates a tree procedurally at runtime: a Bezier-curved trunk with
// recursively spawned branches and clustered leaf quads, all hand-built
// meshes (no imported models).
public class TreeCreator : MonoBehaviour
{
    public int seed = 7;

    [Header("Trunk")]
    public float maxTrunkHeight = 200f;
    public float minTrunkHeight = 75f;
    public float maxBaseRadius = 10f;
    public float minBaseRadius = 3f;
    public float minTopRadiusRatio = 0.2f;
    public float maxTopRadiusRatio = 0.8f;
    public int trunkSegments = 48;

    [Header("Branches")]
    public int maxDepth = 3;
    [Range(0f, 1f)] public float terminateChance = 0.06f;
    public int minTerminateIndex = 3;
    [Range(0f, 1f)] public float minTerminateRatio = 0.25f;
    const int minSegments = 4;
    [Range(0f, 1f)] public float branchChance = 0.5f;
    [Range(0f, 1f)] public float branchChanceFalloff = 0.7f;
    [Range(0f, 1f)] public float minSproutRatio = 0.5f;
    public int minSproutIndex = 2;
    public int maxChildren = 6;
    [Range(0f, 1f)] public float branchLengthRatio = 0.9f;
    [Range(0f, 1f)] public float branchRadiusRatio = 0.45f;

    [Header("Leaves")]
    public float leafRadius = 8f;
    public float leafMinRadius = 1.5f;
    public float leafMaxRadius = 5f;
    [Range(0f, 1f)] public float leafChance = 0.9f;
    public float leafSizeVariance = 0.25f;
    public float minLeafBranchRadius = 0.1f;
    public int leafCountMin = 12;
    public int leafCountMax = 30;
    public float leafMinSize = 1.5f;
    public float leafMaxSize = 4f;
    private Mesh leafMesh;

    [Header("Colors")]
    public Color[] woodColors = new Color[]
    {
        new Color(0.49f, 0.36f, 0.23f), //light brown
        new Color(0.4f, 0.32f, 0.26f), //dark brown
        new Color(0.47f, 0.51f, 0.54f), //grey
        new Color(0.96f, 0.91f, 0.83f) //white
    };

    public Color[] leafColors = new Color[]
    {
        new Color(0.25f, 0.65f, 0.2f), //green
        new Color(0.64f, 0.29f, 0.45f), //darkish pink red
        new Color(0.47f, 0.10f, 0.23f), //mahogony
        new Color(0.89f, 0.64f, 0.19f), //yellow
        new Color(0.65f, 0.62f, 0.06f), //autumn green
        new Color(0.99f, 0.82f, 0.84f) //pink
    };

    private int ntris;
    private int[] trunkTris;
    private Vector3[] trunkVerts;

    // Call once before generating trees (seeds the RNG used by GenerateTree).
    public void Initialize()
    {
        Random.InitState(seed);
    }

    // Generate a full tree with its base at `position` and return its root object.
    public GameObject GenerateTree(Vector3 position, Color barkColor, Color leafColor)
    {
        GameObject root = new GameObject("Tree");
        root.transform.position = position;

        float x = Random.Range(-8, 8);
        float z = Random.Range(-8, 8);
        float y = Random.Range(minTrunkHeight, maxTrunkHeight);
        float controlY = Random.Range(1/4f * y, 3/4f * y);
        (Vector3[] skeleton, Frenet[] frenets) = CreateSkeleton(Vector3.zero, new Vector3(0, controlY, 0), new Vector3(x, y, z), trunkSegments);

        GameObject trunkObject = CreateBranchObject("Trunk", root.transform, null);

        Material material = new Material(Shader.Find("Standard"));
        material.color = barkColor;
        trunkObject.GetComponent<MeshRenderer>().sharedMaterial = material;

        Material leafMaterial = new Material(Shader.Find("Standard"));
        leafMaterial.color = leafColor;
        leafMesh = CreateLeafMesh();

        float baseRadius = Random.Range(minBaseRadius, maxBaseRadius);
        float topRadius = baseRadius * Random.Range(minTopRadiusRatio, maxTopRadiusRatio);

        BranchState trunk = new BranchState{skeleton = skeleton, frenets = frenets, index = 1, depth = 0, baseRadius = baseRadius, topRadius = topRadius, g = trunkObject, material = material, leafMaterial = leafMaterial, plagiotropic = (Random.value < 0.5)};

        GrowBranch(trunk, false);

        return root;
    }

    public (Vector3[] pos, Frenet[] frenets) CreateSkeleton(Vector3 start, Vector3 control, Vector3 end, int segments)
    {
        Vector3[] skeleton = new Vector3[segments];
        Frenet[] frenets = new Frenet[segments];

        for (int i = 0; i < segments; i++)
        {
            skeleton[i] = Bezier.pos(start, control, end, i/(float)(segments-1));
            frenets[i] = Bezier.getFrenet(start, control, end, i/(float)(segments-1));
        }

        return (skeleton, frenets);
    }

    public static class Bezier{
        public static Vector3 pos(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            return (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
        }

        private static Vector3 firstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            return 2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1);
        }

        private static Vector3 secondDerivative(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            return 2 * (p2 - 2 * p1 + p0);
        }

        public static Vector3 tangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            return firstDerivative(p0, p1, p2, t).normalized;
        }

        public static Vector3 normal(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            Vector3 second = secondDerivative(p0, p1, p2);
            Vector3 tan = tangent(p0, p1, p2, t);
            return (second - Vector3.Dot(second, tan) * tan).normalized;
        }

        public static Vector3 binormal(Vector3 tangent, Vector3 normal)
        {
            return Vector3.Cross(tangent, normal).normalized;
        }

        public static Frenet getFrenet(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            Vector3 first = firstDerivative(p0, p1, p2, t);
            Vector3 second = secondDerivative(p0, p1, p2);
            Vector3 tangent = first.normalized;
            Vector3 normal = (second - Vector3.Dot(second, tangent) * tangent).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            return new Frenet(tangent, normal, binormal);
        }
    }

    public class Frenet
    {
        public Vector3 tangent;
        public Vector3 normal;
        public Vector3 binormal;
        public Frenet(Vector3 tangent, Vector3 normal, Vector3 binormal)
        {
            this.tangent = tangent;
            this.normal = normal;
            this.binormal = binormal;
        }
    }

    public Mesh CreateTube(Vector3[] skeleton, Frenet[] frenets, float baseRadius, float topRadius, int segmentCount = minSegments)
    {
        int count = Mathf.Min(segmentCount, skeleton.Length);

        // create a mesh object
        Mesh mesh = new Mesh();

        ntris = 0;
        int ring_verts = 32; // verts per ring
        trunkVerts = new Vector3[ring_verts * (skeleton.Length+1) + 1];
        trunkTris = new int[(ring_verts * (skeleton.Length - 1) * 2 + ring_verts) * 3];

        for (int i = 0; i < count; i++)
        {
            float radius = Mathf.Lerp(baseRadius, topRadius, (float) i / (skeleton.Length-1));
            for (int j = 0; j < ring_verts ; j++)
            {
                float theta = ((float)j / ring_verts) * 2 * (float) Math.PI;
                Vector3 v = radius * (float)Math.Cos(theta) * frenets[i].normal + radius * (float)Math.Sin(theta) * frenets[i].binormal + skeleton[i];
                trunkVerts[i * ring_verts + j] = v;
            }
        }

        //cap off top
        float tipRadius = Mathf.Lerp(baseRadius, topRadius, (float)(count-1) / (skeleton.Length-1));
        for (int j = 0; j < ring_verts; j++)
        {
            float theta = ((float)j / ring_verts) * 2 * (float) Math.PI;
            Vector3 v = tipRadius * (float)Math.Cos(theta) * frenets[count-1].normal + tipRadius * (float)Math.Sin(theta) * frenets[count-1].binormal + skeleton[count-1];
            trunkVerts[count * ring_verts + j] = v;
        }

        int capCenter = count * ring_verts + ring_verts;
        trunkVerts[capCenter] = skeleton[count-1];
        for (int j = 0; j < ring_verts; j++)
        {
            MakeTri(count * ring_verts + j, count * ring_verts + (j + 1) % ring_verts, capCenter);
        }


        //fill in rest
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = 0; j < ring_verts; j++)
            {
                MakeQuad(i * ring_verts + j,
                    i * ring_verts + ((j + 1) % ring_verts),
                    ((i + 1) * ring_verts) + j,
                    (i + 1) * ring_verts + (j + 1) % ring_verts);
            }
        }

        // save the vertices and the triangles in the mesh object
        mesh.vertices = trunkVerts;
        mesh.triangles = trunkTris;

        mesh.RecalculateNormals();  // automatically calculate the vertex normals

        return mesh;
    }

    public class BranchState
    {
        public Vector3[] skeleton;
        public Frenet[] frenets;
        public int index = 1;
        public bool terminated;
        public int depth;
        public float baseRadius;
        public float topRadius;
        public Material material;
        public Material leafMaterial;
        public GameObject g;
        public Boolean plagiotropic;
    }

    public GameObject CreateBranchObject(string name, Transform parent, Material material)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = g.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        return g;
    }

    public void GrowBranch(BranchState state, bool canTerminate = true)
    {
        int end = state.skeleton.Length - 1;
        int sproutStart = Mathf.Max(minSproutIndex, Mathf.FloorToInt(end * minSproutRatio));
        int children = 0;
        int minTerminate = Mathf.Max(minTerminateIndex, Mathf.FloorToInt(end * minTerminateRatio));

        for (state.index = 1; state.index <= end; state.index++)
        {
            if (canTerminate && state.index >= minTerminate && Random.value < terminateChance)
            {
                state.terminated = true;
                break;
            }

            bool canSprout = (state.depth < maxDepth && children < maxChildren && state.index >= sproutStart && state.index < end);
            if (canSprout && Random.value < Math.Pow(branchChanceFalloff, state.depth) * branchChance)
            {
                SpawnBranch(state, state.index);
                children++;
            }
        }

        int ringCount = state.terminated ? state.index + 1 : state.skeleton.Length;
        Mesh mesh = CreateTube(state.skeleton, state.frenets, state.baseRadius, state.topRadius, ringCount);

        state.g.GetComponent<MeshFilter>().mesh = mesh;

        if (state.depth != 0 && state.topRadius >= minLeafBranchRadius && Random.value < leafChance)
        {
            SpawnLeaf(state, ringCount-1);
        }
    }

    public void SpawnBranch(BranchState parent, int i)
    {
        Frenet frame = parent.frenets[i];
        int end = parent.skeleton.Length-1;
        float t = (float)i / (end);
        float parentRadius = Mathf.Lerp(parent.baseRadius, parent.topRadius, t);
        float minAngle, maxAngle;
        if (parent.plagiotropic)
        {
            minAngle = 80f;
            maxAngle = 100f;
        } else
        {
            minAngle = 20f;
            maxAngle = 45f;
        }


        float phi = Random.Range(0f, 2 * Mathf.PI);
        float theta = Random.Range(minAngle, maxAngle) * Mathf.Deg2Rad;

        Vector3 perp = Mathf.Cos(phi) * frame.normal + Mathf.Sin(phi) * frame.binormal;
        Vector3 dir = (frame.tangent * Mathf.Cos(theta) + perp * Mathf.Sin(theta)).normalized;

        float parentLength = (parent.skeleton[end] - parent.skeleton[0]).magnitude;
        float remaining = (float)(end - i) / end;
        float length = Mathf.Max(parentLength * remaining * 1.3f * branchLengthRatio, 0.5f);

        float childBase = Mathf.Max(parentRadius * branchRadiusRatio, 0.05f);
        float childTop = childBase * Random.Range(0.3f, 0.8f);

        Vector3 start = parent.skeleton[i];
        Vector3 control = start + dir * (length * 0.5f) + frame.tangent * (length * Random.Range(-0.15f, 0.15f));
        Vector3 vEnd = start + dir * length;
        int segments = Mathf.Clamp(Mathf.RoundToInt(length * Random.Range(0.3f, 0.8f)), minSegments, 24);

        (Vector3[] skeleton, Frenet[] frenets) = CreateSkeleton(start, control, vEnd, segments);

        GameObject g = CreateBranchObject($"Branch {parent.depth + 1}", parent.g.transform, parent.material);

        GrowBranch(new BranchState {skeleton = skeleton, frenets = frenets, depth = parent.depth + 1, baseRadius = childBase, topRadius = childTop, g = g, material = parent.material, leafMaterial = parent.leafMaterial, plagiotropic = parent.plagiotropic});
    }

    public Mesh CreateLeafMesh()
    {
        Mesh mesh = new Mesh();
        ntris = 0;
        trunkVerts = new Vector3[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f)
        };
        trunkTris = new int[4 * 3];

        MakeQuad(0, 1, 2, 3);
        MakeQuad(4, 6, 5, 7);

        mesh.vertices = trunkVerts;
        mesh.triangles = trunkTris;
        mesh.RecalculateNormals();
        return mesh;
    }


    public GameObject SpawnLeaf(BranchState state, int tipIndex)
    {
        GameObject cluster = new GameObject("Leaves");
        cluster.transform.SetParent(state.g.transform, false);

        Vector3 tipPos = state.skeleton[tipIndex];
        Frenet tip = state.frenets[tipIndex];

        // cluster size scales with the thickness of the branch tip
        float t = (float)tipIndex / (state.skeleton.Length - 1);
        float tipRadius = Mathf.Lerp(state.baseRadius, state.topRadius, t);
        float clusterRadius = Mathf.Clamp(tipRadius * leafRadius, leafMinRadius, leafMaxRadius);

        int count = Random.Range(leafCountMin, leafCountMax);
        for (int i = 0; i <= count; i++)
        {
            GameObject leaf = new GameObject("Leaf");
            leaf.transform.SetParent(cluster.transform, false);

            // scatter around the tip, nudged forward along the branch direction
            Vector3 offset = Random.insideUnitSphere * clusterRadius;
            leaf.transform.localPosition = tipPos + offset + tip.tangent * (clusterRadius * 0.3f);

            // point the quad's +Y outward from the cluster center, then spin it randomly
            Vector3 outward = (offset + tip.tangent * (clusterRadius * 0.5f)).normalized;

            leaf.transform.up = outward;
            leaf.transform.Rotate(0, Random.Range(0, 360), 0, Space.Self);

            leaf.transform.localScale = Vector3.one * Random.Range(leafMinSize, leafMaxSize);

            leaf.AddComponent<MeshFilter>().sharedMesh = leafMesh;
            leaf.AddComponent<MeshRenderer>().sharedMaterial = state.leafMaterial;
        }

        return cluster;
    }


    // make a triangle from three vertex indices (clockwise order)
    void MakeTri(int i1, int i2, int i3)
    {
        int index = ntris * 3;  // figure out the base index for storing triangle indices
        ntris++;

        trunkTris[index] = i1;
        trunkTris[index + 1] = i2;
        trunkTris[index + 2] = i3;
    }

    // make a quadrilateral from four vertex indices (clockwise order)
    void MakeQuad(int i1, int i2, int i3, int i4)
    {
        MakeTri(i1, i2, i3);
        MakeTri(i2, i4, i3);
    }
}
