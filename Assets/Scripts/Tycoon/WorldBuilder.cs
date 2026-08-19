using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// Everything that constructs the 3D scene: camera, lighting, post
    /// processing, ground/roads, and every building's primitive mesh + price
    /// tag. No economy/plot-state knowledge lives here - PlotManager tells it
    /// what to build and where.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        [Header("Camera")]
        public float orthographicSize = 4f;
        public float cameraDistance = 13f;
        public Vector3 cameraEuler = new Vector3(32f, 45f, 0f);

        public Camera MapCamera { get; private set; }
        static Shader worldShader;

        GameManager game;

        public void Init(GameManager owner)
        {
            game = owner;
        }

        // ---------------------------------------------------------------
        // Camera / lighting / post-processing
        // ---------------------------------------------------------------

        public void SetupCamera()
        {
            MapCamera = Camera.main;
            if (MapCamera == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                MapCamera = camGO.AddComponent<Camera>();
            }
            MapCamera.orthographic = true;
            MapCamera.orthographicSize = orthographicSize;
            MapCamera.nearClipPlane = 0.1f;
            MapCamera.farClipPlane = 30f;
            MapCamera.clearFlags = CameraClearFlags.SolidColor;
            MapCamera.backgroundColor = new Color(0.8f, 0.86f, 0.9f);
            MapCamera.transform.rotation = Quaternion.Euler(cameraEuler);
            MapCamera.transform.position = -MapCamera.transform.forward * cameraDistance;
        }

        /// <summary>Bright, warm, flat-shadowed lighting - matches the reference
        /// games' cheerful sunny look, where buildings read via their own tier
        /// color and wall-shading rather than a raking cast shadow across the
        /// map. At this small scale, dynamic shadows just look like dark smudges.</summary>
        public void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.92f, 0.9f, 0.85f);

            var light = FindAnyObjectByType<Light>();
            if (light != null && light.type == LightType.Directional)
            {
                light.shadows = LightShadows.None;
                light.color = new Color(1f, 0.96f, 0.85f); // warm sunlight, not neutral white
                if (light.intensity < 1.35f) light.intensity = 1.35f;
            }
        }

        /// <summary>Bloom/saturation/vignette plus a tilt-shift depth of field -
        /// the signature look of this whole genre (Township, Hay Day, etc.):
        /// blurring the far edge of an isometric scene reads as "photo of a
        /// physical miniature" and is the single biggest lever for making
        /// primitive-built geometry look like a real shipped mobile game.</summary>
        public void SetupPostProcessing()
        {
            var camData = MapCamera.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = MapCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f); // tighter catch - only real highlights glow, not every lit cube face
            bloom.intensity.Override(0.32f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(16f);
            color.contrast.Override(10f);
            color.postExposure.Override(0.08f);

            var whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(6f); // a hair warmer across the board

            // Rolls off highlights instead of clipping to flat white - needed now
            // that bloom/exposure read hotter than the old flat/no-tonemap setup.
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.8f);

            // Focus band centered on the map (which sits cameraDistance units
            // in front of the camera); kept wide/gentle so world-space price
            // tags near the focus plane stay legible - this is meant to read
            // as a soft depth cue at the scene's edges, not a strong blur.
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(cameraDistance - 2f);
            dof.gaussianEnd.Override(cameraDistance + 6f);
            dof.gaussianMaxRadius.Override(0.5f);

            var volumeGO = new GameObject("PostProcessVolume");
            volumeGO.transform.SetParent(transform, false);
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = profile;
        }

        // ---------------------------------------------------------------
        // Ground / roads
        // ---------------------------------------------------------------

        public void BuildGroundAndRoads(Transform mapRoot)
        {
            float mapWidth = (PlotManager.Columns - 1) * PlotManager.CellSpacing;
            float mapDepth = (PlotManager.Rows - 1) * PlotManager.CellSpacing;

            // Ground is a grassy border around the plot grid, sized off the grid's
            // own dimensions (not a fixed constant) so it keeps framing the board
            // snugly however many rows/columns PlotManager ends up with.
            const float margin = 2.4f;
            const float planeBaseSize = 10f; // Unity's default Plane primitive is 10x10 at scale 1
            var ground = CreatePrimitiveChild(mapRoot, PrimitiveType.Plane, new Color(0.4f, 0.66f, 0.35f), Vector3.zero,
                new Vector3((mapWidth + margin) / planeBaseSize, 1f, (mapDepth + margin) / planeBaseSize));
            var groundMat = ground.GetComponent<Renderer>().material;
            groundMat.mainTexture = CreateGroundTexture();
            // Constant tile density (world units per texture repeat) instead of a
            // fixed tile count, so the grass doesn't stretch/squash as the ground
            // resizes with the grid.
            const float tilesPerUnit = 0.82f;
            groundMat.mainTextureScale = new Vector2((mapWidth + margin) * tilesPerUnit, (mapDepth + margin) * tilesPerUnit);

            for (int col = 0; col < PlotManager.Columns - 1; col++)
            {
                float x = -mapWidth / 2f + PlotManager.CellSpacing / 2f + col * PlotManager.CellSpacing;
                BuildRoadStrip(mapRoot, new Vector3(x, 0f, 0f), true, mapDepth + 1.6f);
            }
            for (int row = 0; row < PlotManager.Rows - 1; row++)
            {
                float z = -mapDepth / 2f + PlotManager.CellSpacing / 2f + row * PlotManager.CellSpacing;
                BuildRoadStrip(mapRoot, new Vector3(0f, 0f, z), false, mapWidth + 1.6f);
            }
        }

        void BuildRoadStrip(Transform mapRoot, Vector3 center, bool vertical, float length)
        {
            var scale = vertical ? new Vector3(0.35f, 0.02f, length) : new Vector3(length, 0.02f, 0.35f);
            CreatePrimitiveChild(mapRoot, PrimitiveType.Cube, new Color(0.3f, 0.3f, 0.32f), center + Vector3.up * 0.015f, scale);

            const float dash = 0.22f, gap = 0.14f, step = dash + gap;
            int count = Mathf.FloorToInt(length / step);
            float start = -length / 2f + dash / 2f;
            for (int i = 0; i < count; i++)
            {
                float d = start + i * step;
                var pos = vertical ? center + new Vector3(0f, 0.02f, d) : center + new Vector3(d, 0.02f, 0f);
                var dashScale = vertical ? new Vector3(0.05f, 0.01f, dash) : new Vector3(dash, 0.01f, 0.05f);
                CreatePrimitiveChild(mapRoot, PrimitiveType.Cube, new Color(0.95f, 0.85f, 0.25f), pos, dashScale);
            }
        }

        static Texture2D CreateGroundTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            var baseColor = new Color(0.42f, 0.66f, 0.35f);
            var rand = new System.Random(1);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = (float)rand.NextDouble() * 0.14f - 0.07f;
                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseColor.r + n),
                        Mathf.Clamp01(baseColor.g + n),
                        Mathf.Clamp01(baseColor.b + n)));
                }
            }
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------------
        // Buildings
        // ---------------------------------------------------------------

        public PropertyTileView CreateBuildingShell(Transform mapRoot, Vector3 slot)
        {
            var root = new GameObject("Building");
            root.transform.SetParent(mapRoot, false);
            root.transform.localPosition = slot;
            return root.AddComponent<PropertyTileView>();
        }

        /// <summary>
        /// Clears and regrows a plot's visuals for a (possibly new) tier. Used
        /// both for first construction and when an unowned plot upgrades tier
        /// after a wealth unlock.
        /// </summary>
        public void RebuildBuildingMesh(PropertyTileView view, PropertyDefinition def)
        {
            for (int i = view.transform.childCount - 1; i >= 0; i--)
                Destroy(view.transform.GetChild(i).gameObject);

            EnsureClickHitbox(view);

            CreateContactShadow(view.transform);

            var plate = CreatePrimitiveChild(view.transform, PrimitiveType.Cube, Color.white, new Vector3(0, 0.01f, 0), new Vector3(0.95f, 0.02f, 0.95f));
            view.statusPlate = plate.GetComponent<Renderer>();

            CreatePriceTag(view, def);

            BuildBuildingMesh(view.transform, def, TierHue(def.tier));

            StartCoroutine(PlayBuildingPopAnimation(view.transform));
        }

        /// <summary>Buildings snapping instantly into existence reads as
        /// placeholder/debug art; a quick scale-up pop with a slight overshoot
        /// (squash-and-stretch) is a cheap, standard piece of "game feel" that
        /// makes every reroll/tier-up feel like an impact instead of just an
        /// appearance.</summary>
        IEnumerator PlayBuildingPopAnimation(Transform t)
        {
            const float duration = 0.3f;
            const float overshoot = 1.06f;
            const float growPhase = 0.7f; // fraction of duration spent growing past 1.0 before settling back
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (p < growPhase)
                {
                    float e = p / growPhase;
                    float eased = 1f - (1f - e) * (1f - e) * (1f - e); // ease-out cubic
                    scale = Mathf.Lerp(0.25f, overshoot, eased);
                }
                else
                {
                    float e = (p - growPhase) / (1f - growPhase);
                    scale = Mathf.Lerp(overshoot, 1f, e);
                }
                t.localScale = Vector3.one * scale;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        static Texture2D contactShadowTexture;
        static Material contactShadowMaterial;

        static Material GetContactShadowMaterial()
        {
            if (contactShadowMaterial != null) return contactShadowMaterial;
            const int size = 64;
            contactShadowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            contactShadowTexture.filterMode = FilterMode.Bilinear;
            var center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size / 2f);
                    float falloff = Mathf.Clamp01(1f - dist);
                    falloff *= falloff; // soft edge - center reads darkest, rim nearly invisible
                    contactShadowTexture.SetPixel(x, y, new Color(0f, 0f, 0f, falloff * 0.35f));
                }
            }
            contactShadowTexture.Apply();
            contactShadowMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = contactShadowTexture };
            return contactShadowMaterial;
        }

        /// <summary>Soft dark blob under each building, larger than its footprint
        /// so it peeks out past the status plate. Dynamic shadows are off at this
        /// scale (SetupLighting), which otherwise leaves every building looking
        /// like it's floating a millimeter above the ground - this is the cheap
        /// substitute grounding it instead.</summary>
        void CreateContactShadow(Transform root)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, 0.004f, 0); // above the ground, below the status plate
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat
            go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            go.GetComponent<Renderer>().material = GetContactShadowMaterial();
        }

        /// <summary>One consistent hitbox per plot, sized the same at every
        /// tier, so a click always resolves to whatever building is actually
        /// under the cursor - not whichever taller building the ray happened to
        /// reach next. Added once and left alone across rerolls (the visual
        /// children get destroyed/rebuilt each time, but this collider lives on
        /// the plot root, which persists for the plot's lifetime).</summary>
        void EnsureClickHitbox(PropertyTileView view)
        {
            if (view.GetComponent<BoxCollider>() != null) return;
            var box = view.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 1.5f, 0);
            box.size = new Vector3(1.4f, 3f, 1.4f);
        }

        /// <summary>Y of the highest point of a tier's roof - lets the price tag
        /// sit a small, consistent gap above each building's *actual* roofline
        /// instead of a fixed height that floats miles above a Tent and sits
        /// flush on a Skyscraper.</summary>
        static float BuildingTopY(PropertyDefinition def)
        {
            if (def.tier == PropertyTier.Tent) return TentRidgeHeight * TentSizeScale(def);
            int t = (int)def.tier;
            float height = 0.4f + t * 0.28f; // matches BuildBuildingMesh's body height
            return height + 0.14f; // roof cap spans [height, height+0.14] - see BuildBuildingMesh
        }

        /// <summary>Small World Space Canvas pill (rounded background + circular
        /// trend badge + compact text) above the building - reuses the same
        /// rounded-rect/circle sprites as the HUD so it matches the reference
        /// game's style instead of being a plain 3D quad. Height scales with the
        /// building's own roofline so it stays visually attached at every tier.</summary>
        void CreatePriceTag(PropertyTileView view, PropertyDefinition def)
        {
            var tagGO = new GameObject("PriceTag");
            tagGO.transform.SetParent(view.transform, false);
            tagGO.transform.localPosition = new Vector3(0, BuildingTopY(def) + 0.32f, 0);
            tagGO.transform.rotation = MapCamera.transform.rotation;
            tagGO.transform.localScale = Vector3.one * 0.01f; // UI pixels -> ~world units

            var canvas = tagGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRT = canvas.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(190, 52);

            var pillGO = new GameObject("Pill", typeof(RectTransform));
            pillGO.transform.SetParent(tagGO.transform, false);
            var pill = pillGO.AddComponent<Image>();
            pill.sprite = UIFactory.GetRoundedRectSprite();
            pill.type = Image.Type.Sliced;
            var pillRT = pill.rectTransform;
            pillRT.anchorMin = pillRT.anchorMax = pillRT.pivot = new Vector2(0.5f, 0.5f);
            pillRT.sizeDelta = new Vector2(180, 46);
            view.priceTagPill = pill;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(tagGO.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = game.Font;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var textRT = text.rectTransform;
            textRT.anchorMin = textRT.anchorMax = textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.anchoredPosition = new Vector2(16, 0);
            textRT.sizeDelta = new Vector2(130, 42);
            view.priceTagText = text;

            var badgeGO = new GameObject("Badge", typeof(RectTransform));
            badgeGO.transform.SetParent(tagGO.transform, false);
            var badge = badgeGO.AddComponent<Image>();
            badge.sprite = UIFactory.GetCircleSprite();
            var badgeRT = badge.rectTransform;
            badgeRT.anchorMin = badgeRT.anchorMax = badgeRT.pivot = new Vector2(0.5f, 0.5f);
            badgeRT.anchoredPosition = new Vector2(-88, 0); // overlaps the pill's left edge
            badgeRT.sizeDelta = new Vector2(42, 42);
            view.priceTagBadge = badge;

            var arrowGO = new GameObject("Arrow", typeof(RectTransform));
            arrowGO.transform.SetParent(badgeGO.transform, false);
            var arrow = arrowGO.AddComponent<Text>();
            arrow.font = game.Font;
            arrow.fontSize = 22;
            arrow.fontStyle = FontStyle.Bold;
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.color = Color.white;
            var arrowRT = arrow.rectTransform;
            arrowRT.anchorMin = Vector2.zero;
            arrowRT.anchorMax = Vector2.one;
            arrowRT.sizeDelta = Vector2.zero;
            arrowRT.anchoredPosition = Vector2.zero;
            view.priceTagArrow = arrow;
        }

        void BuildBuildingMesh(Transform root, PropertyDefinition def, Color hue)
        {
            if (def.tier == PropertyTier.Tent)
            {
                BuildTent(root, def);
                return;
            }

            int t = (int)def.tier;
            float width = 0.55f + t * 0.09f;
            float height = 0.4f + t * 0.28f;

            CreatePrimitiveChild(root, PrimitiveType.Cube, hue, new Vector3(0, height / 2f, 0), new Vector3(width, height, width));

            bool modern = t >= 5; // apartment towers / skyscraper read as flat-roofed glass, not shingled houses
            var roofColor = modern ? Color.Lerp(hue, Color.white, 0.55f) : new Color(0.6f, 0.24f, 0.18f);
            CreatePrimitiveChild(root, PrimitiveType.Cube, roofColor, new Vector3(0, height + 0.07f, 0), new Vector3(width * 1.08f, 0.14f, width * 1.08f));

            if (!modern)
            {
                var trimColor = Color.Lerp(hue, Color.black, 0.35f);
                float hx = width / 2f - 0.03f;
                foreach (var cx in new[] { -hx, hx })
                    foreach (var cz in new[] { -hx, hx })
                        CreatePrimitiveChild(root, PrimitiveType.Cube, trimColor, new Vector3(cx, height / 2f, cz), new Vector3(0.045f, height, 0.045f));

                CreatePrimitiveChild(root, PrimitiveType.Cube, new Color(0.32f, 0.2f, 0.12f), new Vector3(0, 0.14f, width / 2f + 0.01f), new Vector3(0.16f, 0.28f, 0.02f));
            }

            int floors = Mathf.Clamp(t, 1, 4);
            var windowColor = new Color(1f, 0.92f, 0.5f);
            float windowOffsetX = width * 0.28f;
            for (int f = 0; f < floors; f++)
            {
                float wy = 0.34f + f * (height - 0.46f) / floors;
                CreatePrimitiveChild(root, PrimitiveType.Cube, windowColor, new Vector3(-windowOffsetX, wy, width / 2f + 0.01f), new Vector3(0.13f, 0.13f, 0.02f));
                CreatePrimitiveChild(root, PrimitiveType.Cube, windowColor, new Vector3(windowOffsetX, wy, width / 2f + 0.01f), new Vector3(0.13f, 0.13f, 0.02f));
                CreatePrimitiveChild(root, PrimitiveType.Cube, windowColor, new Vector3(width / 2f + 0.01f, wy, 0), new Vector3(0.02f, 0.13f, 0.13f));
            }
        }

        const float TentRidgeHeight = 0.44f;
        const float TentBaseHalfWidth = 0.34f;

        /// <summary>Ragged Tent reads smaller/sagging, Canvas Tent reads
        /// bigger/prouder, the middle Tent is the baseline - keyed off price so
        /// it stays sensible even if the catalog numbers are retuned later.</summary>
        static float TentSizeScale(PropertyDefinition def)
        {
            if (def.buyPrice < 32) return 0.85f;
            if (def.buyPrice > 50) return 1.15f;
            return 1f;
        }

        /// <summary>Real A-frame ridge-tent silhouette (two sloped fabric panels
        /// meeting at a peak) instead of a plain box, so the cheapest tier
        /// actually reads as "tent" at a glance. Endpoints are solved with
        /// FromToRotation rather than hand-picked Euler angles, so the panels
        /// are guaranteed to meet exactly at the ridge with no gap or guesswork
        /// about rotation direction. Three price-keyed looks: Ragged Tent is
        /// smaller, off-kilter, and dirtier with scattered ground junk; the
        /// baseline Tent is the original clean A-frame; Canvas Tent is bigger,
        /// brighter, and flies a small flag - so "cheap" vs "nicer" starter
        /// shelters are visibly different, not just a price number.</summary>
        void BuildTent(Transform root, PropertyDefinition def)
        {
            bool ragged = def.buyPrice < 32;
            bool nice = def.buyPrice > 50;
            float scale = TentSizeScale(def);

            var canvas = nice ? new Color(0.8f, 0.74f, 0.58f)
                : ragged ? new Color(0.5f, 0.44f, 0.34f)
                : new Color(0.66f, 0.58f, 0.44f);
            var canvasDark = Color.Lerp(canvas, Color.black, ragged ? 0.28f : 0.18f);
            var stake = new Color(0.3f, 0.28f, 0.24f);

            const float tentDepth = 0.58f;
            const float panelThickness = 0.035f;
            float ridgeHeight = TentRidgeHeight * scale;
            float baseHalfWidth = TentBaseHalfWidth * scale;
            // Ragged tent's ridge leans off-center, so the two panels come out
            // different lengths/angles - reads as "sagging, about to collapse"
            // using the same robust endpoint-solve, no extra parts needed.
            float ridgeLean = ragged ? 0.06f : 0f;

            var ridge = new Vector3(ridgeLean, ridgeHeight, 0f);
            var rightBase = new Vector3(baseHalfWidth, 0f, 0f);
            var leftBase = new Vector3(-baseHalfWidth, 0f, 0f);

            CreateTentPanel(root, ridge, rightBase, tentDepth, panelThickness, canvas);
            CreateTentPanel(root, ridge, leftBase, tentDepth, panelThickness, canvasDark);

            if (ragged)
            {
                // Scattered junk on the ground reads as neglected/run-down.
                CreatePrimitiveChild(root, PrimitiveType.Cube, new Color(0.35f, 0.32f, 0.28f), new Vector3(-baseHalfWidth * 0.8f, 0.02f, tentDepth / 2f + 0.16f), new Vector3(0.1f, 0.04f, 0.08f));
                CreatePrimitiveChild(root, PrimitiveType.Cube, new Color(0.42f, 0.4f, 0.3f), new Vector3(baseHalfWidth * 0.7f, 0.015f, tentDepth / 2f + 0.24f), new Vector3(0.08f, 0.03f, 0.06f));
            }
            else if (nice)
            {
                // A small flag on the ridge - a touch of pride/investment.
                CreatePrimitiveChild(root, PrimitiveType.Cube, stake, new Vector3(ridgeLean, ridgeHeight + 0.07f, 0), new Vector3(0.015f, 0.14f, 0.015f));
                CreatePrimitiveChild(root, PrimitiveType.Cube, new Color(0.75f, 0.25f, 0.2f), new Vector3(ridgeLean + 0.045f, ridgeHeight + 0.11f, 0), new Vector3(0.08f, 0.06f, 0.01f));
            }

            // Entrance mat: a bit of grounded detail at the open front end.
            CreatePrimitiveChild(root, PrimitiveType.Cube, stake, new Vector3(0, 0.005f, tentDepth / 2f + 0.05f), new Vector3(baseHalfWidth * 1.4f, 0.01f, 0.12f));
        }

        Transform CreateTentPanel(Transform root, Vector3 ridge, Vector3 baseEdge, float depth, float thickness, Color color)
        {
            float length = Vector3.Distance(ridge, baseEdge);
            var panel = CreatePrimitiveChild(root, PrimitiveType.Cube, color, (ridge + baseEdge) / 2f, new Vector3(length, thickness, depth));
            panel.localRotation = Quaternion.FromToRotation(Vector3.right, baseEdge - ridge);
            return panel;
        }

        static Color TierHue(PropertyTier tier)
        {
            switch (tier)
            {
                case PropertyTier.Tent: return new Color(0.82f, 0.68f, 0.35f);
                case PropertyTier.Hut: return new Color(0.62f, 0.42f, 0.28f);
                case PropertyTier.SmallHouse: return new Color(0.76f, 0.38f, 0.32f);
                case PropertyTier.House: return new Color(0.55f, 0.4f, 0.68f);
                case PropertyTier.Apartment: return new Color(0.35f, 0.55f, 0.78f);
                case PropertyTier.Tower: return new Color(0.42f, 0.46f, 0.56f);
                case PropertyTier.Skyscraper: return new Color(0.25f, 0.58f, 0.66f);
                default: return Color.gray;
            }
        }

        /// <summary>Purely visual - always strips the collider CreatePrimitive
        /// auto-adds. Click detection uses one explicit hitbox per building
        /// (see AddClickHitbox) instead of these irregular per-part colliders,
        /// which used to vary wildly in height by tier and could let a raycast
        /// skip past a short building (e.g. a Tent) into a taller one behind it
        /// under the angled isometric camera.</summary>
        Transform CreatePrimitiveChild(Transform parent, PrimitiveType type, Color color, Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().material = MakeMaterial(color);
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        static Shader GetWorldShader()
        {
            if (worldShader == null)
                worldShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            return worldShader;
        }

        static Material MakeMaterial(Color color)
        {
            var mat = new Material(GetWorldShader());
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.28f);
            else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.28f);
            return mat;
        }

        // ---------------------------------------------------------------
        // Floating +$ / market-move indicators
        // ---------------------------------------------------------------

        public void SpawnFloatingIndicator(PropertyState state, string label, Color color)
        {
            var go = new GameObject("FloatingIncome");
            // Parented to the stable Map root, not the building itself: the
            // building's children get wiped by RebuildBuildingMesh whenever the
            // plot re-rolls or sells, which would destroy this mid-animation.
            go.transform.SetParent(state.view.transform.parent, false);
            go.transform.position = state.view.transform.position + Vector3.up * 1.6f;
            go.transform.rotation = MapCamera.transform.rotation;

            var tm = go.AddComponent<TextMesh>();
            tm.text = label;
            tm.characterSize = 0.06f;
            tm.fontSize = 48;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            StartCoroutine(AnimateFloatingIndicator(go.transform, tm));
        }

        IEnumerator AnimateFloatingIndicator(Transform t, TextMesh tm)
        {
            if (t == null) yield break;
            var start = t.localPosition;
            var color = tm.color;
            float elapsed = 0f;
            const float duration = 1.2f;
            while (elapsed < duration)
            {
                if (t == null) yield break; // defensive: guards any other path that could destroy this early
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                t.localPosition = start + Vector3.up * (0.8f * p);
                tm.color = new Color(color.r, color.g, color.b, 1f - p);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}
