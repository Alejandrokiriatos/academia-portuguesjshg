using UnityEngine;

namespace RustSurvival.World
{
    /// <summary>
    /// Deterministic 500m terrain prototype with forest, plain and rocky biome masks.
    /// Assign authored TerrainLayer assets in the Inspector for production PBR surfaces.
    /// </summary>
    public sealed class WorldGenerator : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private int worldSeed = 24771;
        [SerializeField] private int worldSize = 500;
        [SerializeField] private int terrainHeight = 55;
        [SerializeField, Range(129, 513)] private int heightmapResolution = 257;
        [SerializeField, Range(64, 512)] private int alphamapResolution = 256;
        [SerializeField] private bool generateOnStart = true;

        [Header("Biome surfaces (optional authored TerrainLayer assets)")]
        [SerializeField] private TerrainLayer forestLayer;
        [SerializeField] private TerrainLayer plainsLayer;
        [SerializeField] private TerrainLayer rockLayer;

        private Terrain _terrain;

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        [ContextMenu("Generate 500m World")]
        public void Generate()
        {
            if (_terrain != null)
                Destroy(_terrain.gameObject);

            TerrainData data = new()
            {
                heightmapResolution = heightmapResolution,
                size = new Vector3(worldSize, terrainHeight, worldSize),
                alphamapResolution = alphamapResolution
            };

            data.SetHeights(0, 0, BuildHeights());
            TerrainLayer[] layers =
            {
                forestLayer != null ? forestLayer : CreateFallbackLayer(new Color(0.20f, 0.30f, 0.13f), 11),
                plainsLayer != null ? plainsLayer : CreateFallbackLayer(new Color(0.49f, 0.43f, 0.27f), 23),
                rockLayer != null ? rockLayer : CreateFallbackLayer(new Color(0.37f, 0.38f, 0.36f), 37)
            };
            data.terrainLayers = layers;
            data.SetAlphamaps(0, 0, BuildBiomeWeights());

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "GeneratedWorld_500m";
            terrainObject.transform.SetParent(transform, false);
            _terrain = terrainObject.GetComponent<Terrain>();
            _terrain.heightmapPixelError = 8;
            _terrain.basemapDistance = 700;
            _terrain.drawInstanced = true;
            _terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _terrain.Flush();
        }

        private float[,] BuildHeights()
        {
            int resolution = heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            Random.State previous = Random.state;
            Random.InitState(worldSeed);
            float offsetX = Random.Range(-10000f, 10000f);
            float offsetZ = Random.Range(-10000f, 10000f);
            Random.state = previous;

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float nx = x / (float)(resolution - 1);
                float nz = z / (float)(resolution - 1);
                float broad = Mathf.PerlinNoise(offsetX + nx * 3.2f, offsetZ + nz * 3.2f);
                float detail = Mathf.PerlinNoise(offsetX + nx * 13f, offsetZ + nz * 13f);
                float ridge = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(offsetX + nx * 5.5f, offsetZ + nz * 5.5f) - 1f);
                float h = 0.12f + broad * 0.46f + detail * 0.08f + ridge * 0.06f;
                float edgeFalloff = Mathf.SmoothStep(0f, 0.12f, Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(nz, 1f - nz)));
                heights[z, x] = Mathf.Clamp01(h * edgeFalloff + 0.035f);
            }
            return heights;
        }

        private float[,,] BuildBiomeWeights()
        {
            int resolution = alphamapResolution;
            float[,,] map = new float[resolution, resolution, 3];

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float nx = x / (float)(resolution - 1);
                float nz = z / (float)(resolution - 1);
                float variation = Mathf.PerlinNoise(nx * 7.5f + worldSeed * 0.001f, nz * 7.5f);
                float forest = Mathf.Clamp01((0.52f - nx) * 2.5f + (variation - 0.5f) * 0.75f);
                float rock = Mathf.Clamp01((nx - 0.68f) * 2.7f + (variation - 0.5f) * 0.8f);
                float plain = Mathf.Clamp01(1f - forest * 0.75f - rock * 0.75f);

                float total = forest + plain + rock;
                map[z, x, 0] = forest / total;
                map[z, x, 1] = plain / total;
                map[z, x, 2] = rock / total;
            }
            return map;
        }

        private TerrainLayer CreateFallbackLayer(Color tint, int seed)
        {
            const int textureSize = 128;
            Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, true, true)
            {
                name = "RuntimeBiomeAlbedo",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Random.State previous = Random.state;
            Random.InitState(worldSeed + seed);
            float[] noiseOffsets = { Random.Range(0f, 1000f), Random.Range(0f, 1000f) };
            Random.state = previous;

            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float broad = Mathf.PerlinNoise(noiseOffsets[0] + x * 0.045f, noiseOffsets[1] + y * 0.045f);
                float fine = Mathf.PerlinNoise(noiseOffsets[0] + x * 0.19f, noiseOffsets[1] + y * 0.19f);
                float variation = 0.72f + broad * 0.22f + fine * 0.12f;
                texture.SetPixel(x, y, new Color(tint.r * variation, tint.g * variation, tint.b * variation, 1f));
            }

            texture.Apply(true, true);
            TerrainLayer layer = new() { diffuseTexture = texture, tileSize = new Vector2(16f, 16f), smoothness = 0.12f };
            return layer;
        }
    }
}
