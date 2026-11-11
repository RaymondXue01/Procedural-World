using System.Collections.Generic;
using UnityEngine;

public class CreateMesh : MonoBehaviour {

	private Vector3[] verts;  // the vertices of the mesh
	private int[] tris;       // the triangles of the mesh (triplets of integer references to vertices)
	private int ntris = 0;    // the number of triangles that have been created so far
	private int num_verts = 7396; // number of verts per mesh: 86 x 86 vertices, 85 x 85 quads]

	[Header("Heightmap (fBm)")]
	public float amplitude = 1000f;
	public float frequency = 0.0005f;
	public float lacunarity = 2.0f;
	public float gain = 0.5f;
	public int scale = 10;
	public int octaves = 5;
	public float gradientStrength = 2.5f; 
	public float firstOctaveWeight = 1.5f;

	[Header("Chunks")]
	public int loadRange = 3;

	[Header("Trees")]
	[Tooltip("Chance per terrain vertex that a tree is generated (procedural trees are expensive — start small and tune)")]
	public float treeChance = 0.0005f;
	[Range(0f, 1f), Tooltip("Minimum up-component of the surface normal for a tree to be placed")]
	public float minFlatness = 0.7f;
	[Tooltip("Scale range for generated trees (the generator's demo trees are much larger than the terrain needs)")]
	public float minTreeScale = 0.2f;
	public float maxTreeScale = 0.45f;
	[SerializeField] private TreeCreator treeCreator;

	private float snowStart = 0.1f;
	private float snowBlend = 0.06f;
	private float snowFrequency = 0.009f;
	private float rockFrequency = 0.015f;
	private float gradientApproxDist = 0.01f;

	private float[,] heightGrid = new float[88, 88]; //86 vertices, 1 cell border

	const int OFFSET = 8000;

	private int chunkSize;
	private int currChunkX, currChunkZ;

	private Material mat;
	private Material chunkMat;

	private Dictionary<(int, int), bool> chunkMap = new Dictionary<(int, int), bool>();

	private bool treesInitialized;

	// Create the mesh and put several copies of it into the scene.
	void Start() {
		chunkSize = 85 * scale;
		mat = new Material(Shader.Find("Standard"));

		if (treeCreator != null && !treesInitialized)
		{
			treeCreator.Initialize();
			treesInitialized = true;
		}

		currChunkX = Mathf.FloorToInt(Camera.main.transform.position.x / chunkSize);
		currChunkZ = Mathf.FloorToInt(Camera.main.transform.position.z / chunkSize);

		loadChunks(currChunkX, currChunkZ);
	}

	Mesh CreateChunk(int x, int z) {
		ntris = 0;

		// create a mesh object
		Mesh mesh = new Mesh();
		getHeightGrid(x, z);

		// list of vertices (85 x 85 grid)
		verts = new Vector3[num_verts];
		Vector2[] uv = new Vector2[num_verts];

		int total_tris = 85 * 85 * 2;  // 85 x 85 squares, 2 triangles per
		tris = new int[total_tris * 3];  // need three vertices per triangle

		// vertices
		for (int i = 0; i < 86; i++)
		{
			for (int j = 0; j < 86; j ++)
			{
				int currX = x + j * scale;
				int currZ = z + i * scale;
				float height = heightGrid[i + 1, j + 1];
				verts[i * 86 + j] = new Vector3(currX, height, currZ);

				uv[i * 86 + j] = new Vector2(j / 85f, i / 85f);
			}
		}

		SpawnTrees(x, z);

		for (int i = 0; i < 85; i++)
		{
			for (int j = 0; j < 85; j ++)
			{
				MakeQuad(i * 86 + j, i * 86 + j + 1, (i + 1) * 86 + j + 1, (i + 1) * 86 + j);
			}
		}

		Texture2D texture = createTexture(86, x, z);

		chunkMat = new Material(mat);
		chunkMat.mainTexture = texture;

		// save the vertices and the triangles in the mesh object
		mesh.vertices = verts;
		mesh.triangles = tris;
		mesh.uv = uv;

		mesh.SetNormals(getNormals(x, z));

		return mesh;
	}

	// Generate procedural trees on this chunk's vertices
	void SpawnTrees(int x, int z) {
		if (treeCreator == null) return;

		for (int i = 0; i < 86; i++)
		{
			for (int j = 0; j < 86; j++)
			{
				int currX = x + j * scale;
				int currZ = z + i * scale;

				System.Random rng = new System.Random((currX * 73856093) ^ (currZ * 19349663));
				if ((float)rng.NextDouble() > treeChance) continue;

				float height = heightGrid[i + 1, j + 1];

				float left = heightGrid[i + 1, j];
				float right = heightGrid[i + 1, j + 2];
				float up = heightGrid[i + 2, j + 1];
				float down = heightGrid[i, j + 1];
				Vector3 horz = new Vector3(2 * scale, right - left, 0);
				Vector3 vert = new Vector3(0, up - down, 2 * scale);
				Vector3 normal = -Vector3.Cross(horz, vert).normalized;
				if (normal.y < minFlatness) continue;

				// ensure trees stay below the snow band
				float yNorm = height / amplitude;
				if (yNorm > snowStart - 0.1f) continue;

				Color barkColor = treeCreator.woodColors[(int)(rng.NextDouble() * treeCreator.woodColors.Length) % treeCreator.woodColors.Length];
				Color leafColor = treeCreator.leafColors[(int)(rng.NextDouble() * treeCreator.leafColors.Length) % treeCreator.leafColors.Length];

				GameObject tree = treeCreator.GenerateTree(new Vector3(currX, height - 0.15f, currZ), barkColor, leafColor);

				float s = Mathf.Lerp(minTreeScale, maxTreeScale, (float)rng.NextDouble());
				tree.transform.localScale = Vector3.one * s;
			}
		}
	}

	// make a triangle from three vertex indices (clockwise order)
	void MakeTri(int i1, int i2, int i3) {
		int index = ntris * 3;  // figure out the base index for storing triangle indices
		ntris++;

		tris[index]     = i1;
		tris[index + 1] = i2;
		tris[index + 2] = i3;
	}

	// make a quadrilateral from four vertex indices (clockwise order)
	void MakeQuad(int i1, int i2, int i3, int i4) {
		MakeTri (i1, i4, i3);
		MakeTri (i1, i3, i2);
	}

	// load chunks around the camera
	void Update ()
	{
		int newChunkX = Mathf.FloorToInt(Camera.main.transform.position.x / chunkSize);
		int newChunkZ = Mathf.FloorToInt(Camera.main.transform.position.z / chunkSize);
		if (currChunkX != newChunkX || currChunkZ != newChunkZ)
		{
			currChunkX = newChunkX;
			currChunkZ = newChunkZ;
			loadChunks(currChunkX, currChunkZ);
		}
	}

	float GetY(int x, int z, int octave)
	{
		float curr_amp = amplitude;
		float curr_freq = frequency;
		float y = 0f;
		float maxHeight = 0f;

		Vector2 gradient = new Vector2(0, 0);

		for (int o = 1; o <= octave; o++)
		{
			float weight = (o == 1) ? firstOctaveWeight : 1f;

			float noise_x = x * curr_freq + OFFSET;
			float noise_z = z * curr_freq + OFFSET;
			float d = gradientApproxDist;

			float n = UnityEngine.Mathf.PerlinNoise(noise_x, noise_z) * 2 - 1;

			float dx = (UnityEngine.Mathf.PerlinNoise(noise_x + d, noise_z) - UnityEngine.Mathf.PerlinNoise(noise_x - d, noise_z)) * 2;
			float dz = (UnityEngine.Mathf.PerlinNoise(noise_x, noise_z + d) - UnityEngine.Mathf.PerlinNoise(noise_x, noise_z - d)) * 2;

			gradient += new Vector2(dx, dz) * (curr_amp * curr_freq) / (2 * d);
			float magnitude = gradient.magnitude;

			y += weight * curr_amp * n / (1 + gradientStrength * magnitude);

			maxHeight += weight * curr_amp;
			curr_amp *= gain;
			curr_freq *= lacunarity;
		}

		return amplitude * (y / maxHeight);
	}

	// fill the 88 x 88 bordered height grid for the chunk at (x, z)
	public void getHeightGrid(int x, int z)
	{
		//grid is 86 + 1 + 1 size
		for (int i = 0; i < 88; i++)
		{
			for (int j = 0; j < 88; j++)
			{
				int currX = x + (j - 1) * scale;
				int currZ = z + (i - 1) * scale;
				heightGrid[i, j] = GetY(currX, currZ, octaves);
			}
		}
	}

	// compute normals from height grid
	public Vector3[] getNormals(int x, int z)
	{
		Vector3[] normals = new Vector3[num_verts];

		for (int i = 0; i < 86; i++)
		{
			for (int j = 0; j < 86; j++)
			{
				float left = heightGrid[i + 1, j];
				float right = heightGrid[i + 1, j + 2];
				float up = heightGrid[i + 2, j + 1];
				float down = heightGrid[i, j + 1];

				Vector3 horz = new Vector3(2 * scale, right - left, 0);
				Vector3 vert = new Vector3(0, up - down, 2 * scale);

				normals[i * 86 + j] = -Vector3.Cross(horz, vert).normalized;
			}
		}

		return normals;
	}

	public void loadChunks(int chunkX, int chunkZ)
	{
		for (int i = chunkZ - loadRange; i <= chunkZ + loadRange; i++)
		{
			for (int j = chunkX - loadRange; j <= chunkX + loadRange; j ++)
			{
				if (!chunkMap.ContainsKey((j, i)))
				{
					Mesh temp = CreateChunk(j * chunkSize, i * chunkSize);
					GameObject c = new GameObject("chunk");
					c.AddComponent<MeshFilter>();
					c.AddComponent<MeshRenderer>();
					c.GetComponent<MeshRenderer>().sharedMaterial = chunkMat;
					c.GetComponent<MeshFilter>().mesh = temp;

					chunkMap.Add((j, i), true);
				}
			}
		}
	}

	// procedural snow and rock texture with noisy snow line
	public Texture2D createTexture(int size, int x, int z)
	{
		Texture2D texture = new Texture2D(size, size);
		texture.wrapMode = TextureWrapMode.Clamp;
		Color[] colors = new Color[size * size];

		Color rockBase = new Color(0.475f, 0.447f, 0.443f);
		Color snowBase = new Color(0.95f, 0.95f, 0.97f);

		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				int currX = x + j * scale;
				int currZ = z + i * scale;

				float y = heightGrid[i + 1, j + 1] / amplitude;

				float snowDisplace = Mathf.PerlinNoise(currX * snowFrequency + OFFSET, currZ * snowFrequency + OFFSET);
				float snowLine = snowStart + 0.1f * (snowDisplace - 0.5f);

				float t = Mathf.InverseLerp(snowLine - snowBlend, snowLine + snowBlend, y);

				float rockNoise = Mathf.PerlinNoise(currX * rockFrequency + OFFSET, currZ * rockFrequency + OFFSET);
				Color rockColor = rockBase * (0.50f + 0.5f * rockNoise);
				colors[i * size + j] = Color.Lerp(rockColor, snowBase, t);
			}
		}

		texture.SetPixels(colors);
		texture.Apply();

		return texture;
	}
}
