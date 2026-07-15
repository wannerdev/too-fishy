using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    public class LevelGenerator : MonoBehaviour
    {
        public float SectionHeight = 25f;
        public int Preload = 2;

        float _lastSpawned = -10f;
        readonly List<GameObject> _sections = new();
        Transform _fishRoot;
        Transform _worldRoot;

        public void Initialize(Transform worldRoot)
        {
            _worldRoot = worldRoot;
            _fishRoot = new GameObject("FishRoot").transform;
            _fishRoot.SetParent(worldRoot, false);

            BuildSurfaceDock();
            BuildSideWalls();
            // Initial sections
            SpawnSection(-10.5f, Stage.Surface, false);
            _lastSpawned = -35f;
            SpawnSection(_lastSpawned, Stage.Surface, false);
            for (int i = 0; i < Preload; i++)
                TrySpawnAhead(-999f);
        }

        void Update()
        {
            var player = GameState.Instance?.PlayerTransform;
            if (player == null) return;
            TrySpawnAhead(player.position.y);

            // Cull far sections
            float py = player.position.y;
            for (int i = _sections.Count - 1; i >= 0; i--)
            {
                if (_sections[i] == null) { _sections.RemoveAt(i); continue; }
                if (_sections[i].transform.position.y > py + 80f)
                {
                    Destroy(_sections[i]);
                    _sections.RemoveAt(i);
                }
            }

            GameState.Instance.FishesLowerBorder = _lastSpawned - SectionHeight / 2f - 1f;
        }

        void TrySpawnAhead(float playerY)
        {
            while (playerY < _lastSpawned + SectionHeight)
            {
                _lastSpawned -= SectionHeight;
                int depth = Mathf.Max(0, Mathf.RoundToInt(-_lastSpawned));
                int band = (depth / 100) * 100;
                Stage stage = Stage.Surface;
                foreach (var kv in GameState.DepthStageMap)
                    if (band >= kv.Key) stage = kv.Value;

                bool barrier = IsStageTransition(_lastSpawned + SectionHeight, _lastSpawned);
                SpawnSection(_lastSpawned, stage, barrier);
            }
        }

        bool IsStageTransition(float prevY, float newY)
        {
            int d0 = (Mathf.Max(0, Mathf.RoundToInt(-prevY)) / 100) * 100;
            int d1 = (Mathf.Max(0, Mathf.RoundToInt(-newY)) / 100) * 100;
            return d0 != d1 && d1 > 0;
        }

        void SpawnSection(float y, Stage stage, bool withBarrier)
        {
            var section = new GameObject($"Section_{stage}_{y:F0}");
            section.transform.SetParent(_worldRoot, false);
            section.transform.position = new Vector3(0f, y, 0f);
            _sections.Add(section);

            // Background panel
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            bg.transform.SetParent(section.transform, false);
            bg.transform.localPosition = new Vector3(-4f, 0f, -8f);
            bg.transform.localScale = new Vector3(40f, SectionHeight + 2f, 1f);
            Object.Destroy(bg.GetComponent<Collider>());
            var bgMat = new Material(Shader.Find("Standard"));
            var fog = FishConfig.StageFogColor(stage);
            bgMat.color = Color.Lerp(fog, Color.black, 0.4f);
            bgMat.SetFloat("_Glossiness", 0.1f);
            bg.GetComponent<Renderer>().material = bgMat;

            // Decorative rocks
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
                var rm = new Material(Shader.Find("Standard"));
                rm.color = stage >= Stage.Hot
                    ? new Color(0.35f, 0.15f, 0.1f)
                    : new Color(0.25f, 0.28f, 0.32f);
                rock.GetComponent<Renderer>().material = rm;
            }

            if (withBarrier && !GameState.Instance.IsIntro())
            {
                int hp = 2 + (int)stage;
                DestroyableBarrier.Create(section.transform, y, hp);
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
                var lm = new Material(Shader.Find("Standard"));
                lm.color = new Color(1f, 0.25f, 0.05f);
                lm.EnableKeyword("_EMISSION");
                lm.SetColor("_EmissionColor", new Color(2f, 0.4f, 0.05f));
                lava.GetComponent<Renderer>().material = lm;
                lava.AddComponent<LavaZone>();
            }

            SpawnFishInSection(section.transform, y, stage);
        }

        void SpawnFishInSection(Transform section, float y, Stage stage)
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
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.45f, 0.3f, 0.15f);
            dock.GetComponent<Renderer>().material = mat;

            // Surface water plane
            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "SurfaceWater";
            water.transform.SetParent(_worldRoot, false);
            water.transform.position = new Vector3(-4f, 0f, -2f);
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.localScale = new Vector3(40f, 20f, 1f);
            Object.Destroy(water.GetComponent<Collider>());
            var wm = new Material(Shader.Find("Standard"));
            wm.color = new Color(0.2f, 0.55f, 0.8f, 0.5f);
            wm.SetFloat("_Mode", 3);
            wm.SetFloat("_Glossiness", 0.9f);
            water.GetComponent<Renderer>().material = wm;

            // Sky box above surface
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            sky.transform.SetParent(_worldRoot, false);
            sky.transform.position = new Vector3(-4f, 8f, -8f);
            sky.transform.localScale = new Vector3(40f, 16f, 1f);
            Object.Destroy(sky.GetComponent<Collider>());
            var sm = new Material(Shader.Find("Standard"));
            sm.color = new Color(0.45f, 0.7f, 0.95f);
            sky.GetComponent<Renderer>().material = sm;
        }

        void BuildSideWalls()
        {
            // Tall walls so player can't swim too far sideways
            foreach (var x in new[] { -15f, 7f })
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "SideWall";
                wall.transform.SetParent(_worldRoot, false);
                wall.transform.position = new Vector3(x, -300f, 0f);
                wall.transform.localScale = new Vector3(2f, 700f, 4f);
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.18f, 0.22f);
                wall.GetComponent<Renderer>().material = mat;
            }
        }
    }
}
