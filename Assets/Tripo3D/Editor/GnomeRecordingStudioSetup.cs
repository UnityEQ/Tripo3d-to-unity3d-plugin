using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Tripo3D.Editor
{
    [InitializeOnLoad]
    static class GnomeRecordingStudioSetup
    {
        const string StudioName = "Gnome Studio";
        const string VolumeAsset = "Assets/Settings/GnomeRecordingVolume.asset";
        const string FloorMat = "Assets/Settings/StudioFloor.mat";
        const string WallMat = "Assets/Settings/StudioWall.mat";
        const uint LayerDefault = 1u;
        const uint LayerCharacter = 2u;

        static GnomeRecordingStudioSetup()
        {
            EditorApplication.delayCall += TrySetup;
        }

        [MenuItem("Tripo 3D/Setup Gnome Recording Studio")]
        public static void SetupFromMenu()
        {
            Build(true);
        }

        static void TrySetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TrySetup;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (GameObject.Find(StudioName) != null)
            {
                EnsureLookAt();
                EnsureCharacterOnlyFill(FindGnome());
                EnsureCinematic(FindGnome());
                return;
            }

            if (FindGnome() == null)
                return;
            Build(false);
        }

        static void Build(bool force)
        {
            var gnome = FindGnome();
            if (gnome == null)
            {
                if (force)
                    EditorUtility.DisplayDialog("Tripo Studio", "Place the gnome Character in the open scene first.", "OK");
                return;
            }

            gnome.name = "Gnome";
            var origin = gnome.transform.position;
            var studio = GameObject.Find(StudioName);
            if (studio != null && force)
                Object.DestroyImmediate(studio);
            studio = GameObject.Find(StudioName);
            if (studio == null)
                studio = new GameObject(StudioName);

            EnsureMaterial(FloorMat, new Color(0.16f, 0.14f, 0.12f), 0.08f, 0.38f);
            EnsureMaterial(WallMat, new Color(0.78f, 0.74f, 0.68f), 0.02f, 0.22f);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMat);
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>(WallMat);

            var floor = Primitive(studio.transform, "Floor", PrimitiveType.Plane, origin + new Vector3(0f, -0.01f, 0f), Vector3.zero, new Vector3(0.85f, 1f, 0.85f), floorMat);
            floor.isStatic = true;

            Primitive(studio.transform, "Wall Back", PrimitiveType.Cube,
                origin + new Vector3(0f, 1.6f, 2.35f), Vector3.zero, new Vector3(8.5f, 3.3f, 0.12f), wallMat);
            Primitive(studio.transform, "Wall Left", PrimitiveType.Cube,
                origin + new Vector3(-2.45f, 1.6f, 0.15f), new Vector3(0f, 90f, 0f), new Vector3(4.6f, 3.3f, 0.12f), wallMat);
            Primitive(studio.transform, "Wall Right", PrimitiveType.Cube,
                origin + new Vector3(2.45f, 1.6f, 0.15f), new Vector3(0f, 90f, 0f), new Vector3(4.6f, 3.3f, 0.12f), wallMat);

            var key = GameObject.Find("Directional Light");
            if (key != null)
            {
                key.transform.position = origin + new Vector3(-1.6f, 3.2f, -1.8f);
                key.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
                var light = key.GetComponent<Light>();
                if (light != null)
                {
                    light.intensity = 1.55f;
                    light.color = new Color(1f, 0.96f, 0.9f);
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.85f;
                    light.useColorTemperature = true;
                    light.colorTemperature = 5200f;
                }
            }

            EnsureLight(studio.transform, "Fill Light", LightType.Point, origin + new Vector3(1.4f, 1.5f, -1.7f),
                new Color(0.72f, 0.82f, 1f), 3.2f, 8f, LightShadows.None);
            EnsureLight(studio.transform, "Rim Light", LightType.Spot, origin + new Vector3(-0.4f, 2.4f, 1.9f),
                new Color(1f, 0.93f, 0.82f), 6.5f, 10f, LightShadows.Soft, origin + Vector3.up * 0.9f, 55f);
            EnsureCharacterOnlyFill(gnome);
            EnsureCinematic(gnome);

            var profile = EnsureVolumeProfile();
            var volume = Object.FindFirstObjectByType<Volume>();
            if (volume == null)
            {
                var go = new GameObject("Global Volume");
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
            }

            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            var camera = Camera.main;
            if (camera != null)
            {
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.fieldOfView = 38f;
                camera.nearClipPlane = 0.05f;
                var data = camera.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    data.stopNaN = true;
                    data.dithering = true;
                    data.renderShadows = true;
                    data.volumeLayerMask = ~0;
                }

                var look = camera.GetComponent<LookAtTarget>();
                if (look == null)
                    look = camera.gameObject.AddComponent<LookAtTarget>();
                look.target = gnome.transform;
                look.useRendererBounds = true;
                look.worldOffset = new Vector3(0f, 0.7f, 0f);
            }

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
            Debug.Log("[Gnome Studio] Camera look-at, recording volume, and walls are in the scene. Move Main Camera to frame the gnome.");
        }

        static void EnsureLookAt()
        {
            var gnome = FindGnome();
            var camera = Camera.main;
            if (gnome == null || camera == null)
                return;
            var look = camera.GetComponent<LookAtTarget>();
            if (look == null)
                look = camera.gameObject.AddComponent<LookAtTarget>();
            if (look.target == null)
                look.target = gnome.transform;
        }

        static void EnsureCharacterOnlyFill(GameObject gnome)
        {
            if (gnome == null)
                return;

            foreach (var renderer in gnome.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask = LayerDefault | LayerCharacter;

            var bounds = CharacterBounds(gnome);
            var center = bounds.center;
            var camera = Camera.main;
            var toCamera = camera != null
                ? (camera.transform.position - center)
                : new Vector3(0f, 0.2f, -1f);
            if (toCamera.sqrMagnitude < 1e-6f)
                toCamera = new Vector3(0f, 0.2f, -1f);
            toCamera.Normalize();

            var studio = GameObject.Find(StudioName);
            var parent = studio != null ? studio.transform : gnome.transform;
            var bodyPos = center + toCamera * Mathf.Clamp(bounds.extents.magnitude * 0.55f, 0.35f, 0.85f) + Vector3.up * (bounds.extents.y * 0.08f);
            var facePos = new Vector3(center.x, bounds.max.y - bounds.size.y * 0.18f, center.z) + toCamera * 0.32f + Vector3.up * 0.04f + Vector3.Cross(toCamera, Vector3.up).normalized * 0.08f;

            var body = EnsureLight(parent, "Character Body Fill", LightType.Point, bodyPos,
                new Color(1f, 0.97f, 0.93f), 2.15f, Mathf.Clamp(bounds.size.y * 1.15f, 1.1f, 2.0f), LightShadows.None);
            SetLightLayers(body, LayerCharacter);

            var eyes = EnsureLight(parent, "Character Eye Light", LightType.Point, facePos,
                new Color(1f, 0.99f, 0.97f), 1.35f, 0.95f, LightShadows.None);
            SetLightLayers(eyes, LayerCharacter);

            Light catchLight = null;
            if (camera != null)
            {
                catchLight = EnsureLight(camera.transform, "Camera Catchlight", LightType.Point,
                    camera.transform.TransformPoint(new Vector3(0.11f, 0.08f, 0.28f)),
                    new Color(1f, 1f, 1f), 0.85f, 3.5f, LightShadows.None);
                catchLight.transform.SetParent(camera.transform, true);
                catchLight.transform.localPosition = new Vector3(0.11f, 0.08f, 0.28f);
                SetLightLayers(catchLight, LayerCharacter);
            }

            var studioGo = studio != null ? studio : parent.gameObject;
            var fill = studioGo.GetComponent<GnomeCharacterFill>();
            var created = fill == null;
            if (created)
            {
                fill = studioGo.AddComponent<GnomeCharacterFill>();
                fill.brightness = 0.7f;
            }

            fill.bodyFill = body;
            fill.eyeLight = eyes;
            fill.catchlight = catchLight;
            fill.bodyBase = 2.15f;
            fill.eyeBase = 1.35f;
            fill.catchBase = 0.85f;
            fill.Apply();

            if (created)
            {
                var scene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        static Bounds CharacterBounds(GameObject gnome)
        {
            var renderers = gnome.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return new Bounds(gnome.transform.position + Vector3.up * 0.6f, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void SetLightLayers(Light light, uint mask)
        {
            if (light == null)
                return;
            var extra = light.GetComponent<UniversalAdditionalLightData>();
            if (extra == null)
                extra = light.gameObject.AddComponent<UniversalAdditionalLightData>();
            extra.renderingLayers = (RenderingLayerMask)mask;
            extra.customShadowLayers = false;
        }

        static void EnsureCinematic(GameObject gnome)
        {
            if (gnome == null)
                return;
            var studio = GameObject.Find(StudioName);
            if (studio == null)
                return;
            var preview = studio.GetComponent<GnomeCinematicPreview>();
            if (preview == null)
                preview = studio.AddComponent<GnomeCinematicPreview>();
            preview.subject = gnome.transform;
            preview.playOnStart = true;
            preview.loop = true;

            var camera = Camera.main;
            if (camera != null && camera.GetComponent<CinemachineBrain>() == null)
                camera.gameObject.AddComponent<CinemachineBrain>();
        }

        static GameObject FindGnome()
        {
            var named = GameObject.Find("Gnome") ?? GameObject.Find("Character");
            if (named != null)
                return named;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.IndexOf("gnome", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return root;
            }

            return null;
        }

        static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, true);
            }

            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static Light EnsureLight(Transform parent, string name, LightType type, Vector3 pos, Color color, float intensity, float range, LightShadows shadows, Vector3? lookAt = null, float spotAngle = 60f)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, true);
                go.AddComponent<Light>();
            }

            go.transform.position = pos;
            if (lookAt.HasValue)
                go.transform.LookAt(lookAt.Value);
            var light = go.GetComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
            light.spotAngle = spotAngle;
            var extra = go.GetComponent<UniversalAdditionalLightData>();
            if (extra == null)
                extra = go.AddComponent<UniversalAdditionalLightData>();
            extra.usePipelineSettings = true;
            return light;
        }

        static Material EnsureMaterial(string path, Color color, float metallic, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (mat == null)
            {
                mat = new Material(shader);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets/Settings");
                AssetDatabase.CreateAsset(mat, path);
            }
            else
                mat.shader = shader;

            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile EnsureVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeAsset);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeAsset);
            }

            var tone = GetOrAdd<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.32f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.9f));
            bloom.highQualityFiltering.Override(true);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.42f);
            vignette.color.Override(new Color(0.05f, 0.03f, 0.02f));

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.18f);
            color.contrast.Override(16f);
            color.saturation.Override(14f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.93f));

            var white = GetOrAdd<WhiteBalance>(profile);
            white.temperature.Override(14f);
            white.tint.Override(-3f);

            var grain = GetOrAdd<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.75f);

            var split = GetOrAdd<SplitToning>(profile);
            split.shadows.Override(new Color(0.32f, 0.4f, 0.55f));
            split.highlights.Override(new Color(1f, 0.86f, 0.7f));
            split.balance.Override(10f);

            var smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.9f, 0.94f, 1.06f, -0.05f));
            smh.midtones.Override(new Vector4(1.01f, 0.99f, 0.96f, 0.03f));
            smh.highlights.Override(new Vector4(1.06f, 1.02f, 0.94f, 0.04f));

            var chroma = GetOrAdd<ChromaticAberration>(profile);
            chroma.intensity.Override(0.035f);

            var lift = GetOrAdd<LiftGammaGain>(profile);
            lift.lift.Override(new Vector4(0.98f, 0.99f, 1.02f, -0.01f));
            lift.gamma.Override(new Vector4(1f, 0.99f, 0.98f, 0f));
            lift.gain.Override(new Vector4(1.04f, 1.01f, 0.96f, 0.03f));

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component) && component != null)
                return component;
            return profile.Add<T>(true);
        }
    }
}
