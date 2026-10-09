using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Streams the world around the player: sections within <see cref="ViewDistance"/> above and
    /// below exist, everything else is destroyed and rebuilt when the player comes back. Fish are
    /// re-stocked only after <see cref="FishRespawnDelay"/>, so swimming up and down does not farm.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        public float SectionHeight = 25f;
        public float ViewDistance = 80f;
        public float FishRespawnDelay = 90f;

        const float FirstSectionY = -10.5f;

        readonly Dictionary<int, GameObject> _sections = new();
        readonly Dictionary<int, float> _fishSpawnedAt = new();
        readonly List<int> _toRemove = new();
        Transform _fishRoot;
        Transform _worldRoot;
        float _lowestSectionY = FirstSectionY;

        public void Initialize(Transform worldRoot)
        {
            _worldRoot = worldRoot;
            _fishRoot = new GameObject("FishRoot").transform;
            _fishRoot.SetParent(worldRoot, false);

            BuildSurfaceDock();
            BuildSideWalls();
            UpdateSections(0f);
        }

        void Update()
        {
            var player = GameState.Instance?.PlayerTransform;
            if (player == null) return;
            UpdateSections(player.position.y);
            GameState.Instance.FishesLowerBorder = _lowestSectionY - SectionHeight / 2f - 1f;
        }

        float SectionY(int index) => FirstSectionY - index * SectionHeight;
        int IndexAt(float y) => Mathf.Max(0, Mathf.RoundToInt((FirstSectionY - y) / SectionHeight));

        void UpdateSections(float playerY)
        {
            int first = IndexAt(playerY + ViewDistance);
            int last = IndexAt(playerY - ViewDistance);
            for (int i = first; i <= last; i++)
            {
                if (!_sections.ContainsKey(i)) SpawnSection(i);
            }

            _toRemove.Clear();
            foreach (var kv in _sections)
            {
                if (kv.Value == null || kv.Key < first - 1 || kv.Key > last + 1) _toRemove.Add(kv.Key);
            }
            foreach (int i in _toRemove)
            {
                if (_sections[i] != null) Destroy(_sections[i]);
                _sections.Remove(i);
            }

            _lowestSectionY = SectionY(last);
        }

        static Stage StageForDepth(int depth)
        {
            int band = (depth / 100) * 100;
            Stage stage = Stage.Surface;
            foreach (var kv in GameState.DepthStageMap)
                if (band >= kv.Key) stage = kv.Value;
            return stage;
        }

        void SpawnSection(int index)
        {
            float y = SectionY(index);
            int depth = Mathf.Max(0, Mathf.RoundToInt(-y));
            Stage stage = StageForDepth(depth);

            // A barrier seals the first section of every new stage band below the surface
            int prevDepth = index == 0 ? 0 : Mathf.Max(0, Mathf.RoundToInt(-SectionY(index - 1)));
            bool stageTransition = (depth / 100) != (prevDepth / 100) && depth >= 100;

            var section = new GameObject($"Section_{stage}_{index}");
            section.transform.SetParent(_worldRoot, false);
            section.transform.position = new Vector3(0f, y, 0f);
            _sections[index] = section;

            // Background panel, facing the camera (the camera looks toward -Z, so the quad faces +Z)
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            bg.transform.SetParent(section.transform, false);
            bg.transform.localPosition = new Vector3(-4f, 0f, -8f);
            bg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bg.transform.localScale = new Vector3(40f, SectionHeight + 2f, 1f);
            Object.Destroy(bg.GetComponent<Collider>());
            var fog = FishConfig.StageFogColor(stage);
            bg.GetComponent<Renderer>().sharedMaterial = Materials.Opaque(Color.Lerp(fog, Color.black, 0.4f), glossiness: 0.1f);

            // Decorative rocks
            var rockMat = Materials.Opaque(stage >= Stage.Hot ? new Color(0.35f, 0.15f, 0.1f) : new Color(0.25f, 0.28f, 0.32f));
            int rocks = Random.Range(2, 5);
            for (int i = 0; i < rocks; i++)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Rock";
                rock.transform.SetParent(section.transform, false);
                float side = Random.value > 0.5f ? -12f : 4f;
                rock.transform.localPosition = new Vector3(side + Random.Range(-1.5f, 1.5f), Random.Range(-SectionHeight / 2f, SectionHeight / 2f), -1f);
                rock.transform.localScale = new Vector3(Random.Range(1f, 3f), Random.Range(1f, 4f), Random.Range(1f, 2f));
                rock.transform.rotation = Quaternion.Euler(Random.Range(0, 30), Random.Range(0, 360), Random.Range(0, 30));
                rock.GetComponent<Renderer>().sharedMaterial = rockMat;
            }

            var gs = GameState.Instance;
            if (stageTransition && gs != null && !gs.IsIntro() && !gs.DestroyedBarriers.Contains(index))
            {
                int hp = 2 + (int)stage;
                DestroyableBarrier.Create(section.transform, y, hp, index);
            }

            if (stage >= Stage.Lava)
            {
                var lava = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lava.name = "Lava";
                lava.tag = "Lava";
                lava.transform.SetParent(section.transform, false);
                lava.transform.localPosition = new Vector3(-4f, -SectionHeight / 2f + 1f, 0f);
                lava.transform.localScale = new Vector3(20f, 2f, 2f);
                Object.Destroy(lava.GetComponent<Collider>());
                var trigger = lava.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = Vector3.one;
                lava.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(1f, 0.25f, 0.05f), new Color(2f, 0.4f, 0.05f));
                lava.AddComponent<LavaZone>();
            }

            if (!_fishSpawnedAt.TryGetValue(index, out float spawnedAt) || Time.time - spawnedAt > FishRespawnDelay)
            {
                SpawnFishInSection(y, stage);
                _fishSpawnedAt[index] = Time.time;
            }
        }

        void SpawnFishInSection(float y, Stage stage)
        {
            var cfg = FishConfig.Sections[stage];
            int count = Random.Range(cfg.MaxFishAmount / 2, cfg.MaxFishAmount + 1);
            for (int i = 0; i < count; i++)
            {
                var type = FishConfig.PickType(stage);
                var pos = new Vector3(
                    Random.Range(-11f, 3f),
                    y + Random.Range(-SectionHeight / 2f + 1f, SectionHeight / 2f - 1f),
                    -0.3f);
                FishBehaviour.Spawn(pos, type, stage, _fishRoot);
            }
        }

        void BuildSurfaceDock()
        {
            var dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dock.name = "Dock";
            dock.tag = "Dock";
            dock.transform.SetParent(_worldRoot, false);
            dock.transform.position = new Vector3(-2f, 0.4f, 0f);
            dock.transform.localScale = new Vector3(8f, 0.4f, 3f);
            dock.GetComponent<Renderer>().sharedMaterial = Materials.Opaque(new Color(0.45f, 0.3f, 0.15f));

            // Surface water plane
            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "SurfaceWater";
            water.transform.SetParent(_worldRoot, false);
            water.transform.position = new Vector3(-4f, 0f, -2f);
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.localScale = new Vector3(40f, 20f, 1f);
            Object.Destroy(water.GetComponent<Collider>());
            water.GetComponent<Renderer>().sharedMaterial = Materials.Transparent(new Color(0.2f, 0.55f, 0.8f, 0.5f), glossiness: 0.9f);

            // Sky above the surface, facing the camera
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            sky.transform.SetParent(_worldRoot, false);
            sky.transform.position = new Vector3(-4f, 8f, -8f);
            sky.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            sky.transform.localScale = new Vector3(40f, 16f, 1f);
            Object.Destroy(sky.GetComponent<Collider>());
            sky.GetComponent<Renderer>().sharedMaterial = Materials.Opaque(new Color(0.45f, 0.7f, 0.95f));
        }

        void BuildSideWalls()
        {
            // Tall walls so the player can't swim too far sideways; deep enough for the Void stage
            var mat = Materials.Opaque(new Color(0.15f, 0.18f, 0.22f));
            foreach (var x in new[] { -15f, 7f })
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "SideWall";
                wall.transform.SetParent(_worldRoot, false);
                wall.transform.position = new Vector3(x, -1000f, 0f);
                wall.transform.localScale = new Vector3(2f, 2100f, 4f);
                wall.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
    }
}
