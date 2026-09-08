using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Tripo3D.Editor
{
    [InitializeOnLoad]
    static class GnomeRecordingStudioSetup
    {
        const string StudioName = "Gnome Studio";
        const string VolumeAsset = "Assets/Settings/GnomeRecordingVolume.asset";
        const string FloorMat = "Assets/Settings/StudioFloor.mat";
        const string WallMat = "Assets/Settings/StudioWall.mat";
        const string BlackMarbleMat = "Assets/Settings/StudioMarbleBlack.mat";
        const string GoldMat = "Assets/Settings/StudioGold.mat";
        const string CeilingMat = "Assets/Settings/StudioCeiling.mat";
        const string ColumnMat = "Assets/Settings/StudioColumn.mat";
        const string FloorTex = "Assets/Settings/MarbleFloor.jpg";
        const string BlackTex = "Assets/Settings/MarbleBlack.jpg";
        const string WallTex = "Assets/Settings/DecoWall.jpg";
        const string GoldTex = "Assets/Settings/DecoGold.jpg";
        const string CeilingTex = "Assets/Settings/DecoCeiling.jpg";
        const string TimelinePath = "Assets/Settings/GnomeStudioCinematic.playable";
        const string FollowAnimPath = "Assets/Settings/CameraFollowLook.anim";
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

        [MenuItem("Tripo 3D/Refresh Gnome Studio Look")]
        public static void RefreshLook()
        {
            var gnome = FindGnome();
            EnsureSet(gnome);
            EnsureCharacterOnlyFill(gnome);
            EnsureCinematic(gnome);
            EnsureDirector(gnome);
            var profile = EnsureVolumeProfile();
            var volume = Object.FindFirstObjectByType<Volume>();
            if (volume == null)
            {
                var go = new GameObject("Global Volume");
                volume = go.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
            Debug.Log("[Gnome Studio] Main Camera orbit spots, CameraFollow, and short-film post are ready.");
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
                var gnome = FindGnome();
                EnsureLookAt();
                EnsureSet(gnome);
                EnsureCharacterOnlyFill(gnome);
                EnsureCinematic(gnome);
                EnsureDirector(gnome);
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

            EnsureSet(gnome);

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
                DressCamera(camera, 32f);

            EnsureDirector(gnome);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
            Debug.Log("[Gnome Studio] Camera look-at, recording volume, and walls are in the scene. Move Main Camera to frame the gnome.");
        }

        static void EnsureLookAt()
        {
            var studio = GameObject.Find(StudioName);
            var director = studio != null ? studio.GetComponent<GnomeStudioDirector>() : null;
            if (director != null)
                director.Apply();
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

        static void EnsureSet(GameObject gnome)
        {
            var studio = GameObject.Find(StudioName);
            if (studio == null || gnome == null)
                return;
            var origin = gnome.transform.position;
            var bounds = CharacterBounds(gnome);
            var floorY = bounds.min.y - 0.01f;
            const float half = 6.5f;
            const float hallH = 5.55f;
            const float thick = 0.2f;
            var span = half * 2f;
            var wallY = floorY + hallH * 0.5f;
            var goldColor = new Color(1f, 0.82f, 0.48f);

            ImportRepeatTexture(FloorTex);
            ImportRepeatTexture(BlackTex);
            ImportRepeatTexture(WallTex);
            ImportRepeatTexture(GoldTex);
            ImportRepeatTexture(CeilingTex);

            var floorMat = EnsureLit(FloorMat, Color.white, 0.04f, 0.92f, FloorTex, new Vector2(6f, 6f));
            var wallMat = EnsureLit(WallMat, Color.white, 0.06f, 0.32f, WallTex, new Vector2(3.2f, 2.2f));
            var blackMat = EnsureLit(BlackMarbleMat, Color.white, 0.05f, 0.88f, BlackTex, new Vector2(2.4f, 1.4f));
            var goldMat = EnsureLit(GoldMat, goldColor, 1f, 0.84f, GoldTex, new Vector2(1f, 1f));
            var ceilingMat = EnsureLit(CeilingMat, Color.white, 0.05f, 0.38f, CeilingTex, new Vector2(4f, 4f));
            var columnMat = EnsureLit(ColumnMat, Color.white, 0.04f, 0.86f, FloorTex, new Vector2(1.2f, 3.4f));

            var floor = Primitive(studio.transform, "Floor", PrimitiveType.Plane,
                new Vector3(origin.x, floorY, origin.z), Vector3.zero, new Vector3(1.4f, 1f, 1.4f), floorMat);
            floor.isStatic = true;

            Primitive(studio.transform, "Wall Back", PrimitiveType.Cube,
                new Vector3(origin.x, wallY, origin.z + half), Vector3.zero, new Vector3(span + thick, hallH, thick), wallMat);
            Primitive(studio.transform, "Wall Front", PrimitiveType.Cube,
                new Vector3(origin.x, wallY, origin.z - half), Vector3.zero, new Vector3(span + thick, hallH, thick), wallMat);
            Primitive(studio.transform, "Wall Left", PrimitiveType.Cube,
                new Vector3(origin.x - half, wallY, origin.z), Vector3.zero, new Vector3(thick, hallH, span), wallMat);
            Primitive(studio.transform, "Wall Right", PrimitiveType.Cube,
                new Vector3(origin.x + half, wallY, origin.z), Vector3.zero, new Vector3(thick, hallH, span), wallMat);
            Primitive(studio.transform, "Ceiling", PrimitiveType.Plane,
                new Vector3(origin.x, floorY + hallH, origin.z), new Vector3(180f, 0f, 0f), new Vector3(1.4f, 1f, 1.4f), ceilingMat);

            WallBand(studio.transform, "Wainscot", origin, floorY + 0.56f, 1.12f, half, 0.14f, 0.12f, blackMat);
            WallBand(studio.transform, "Chair Rail", origin, floorY + 1.15f, 0.05f, half, 0.1f, 0.08f, goldMat);
            WallBand(studio.transform, "Cornice", origin, floorY + hallH - 0.1f, 0.18f, half, 0.08f, 0.14f, goldMat);
            WallBand(studio.transform, "Baseboard", origin, floorY + 0.05f, 0.1f, half, 0.12f, 0.1f, goldMat);

            Primitive(studio.transform, "Medallion Ring", PrimitiveType.Cylinder,
                new Vector3(origin.x, floorY + 0.012f, origin.z), Vector3.zero, new Vector3(2.4f, 0.01f, 2.4f), goldMat);
            Primitive(studio.transform, "Medallion", PrimitiveType.Cylinder,
                new Vector3(origin.x, floorY + 0.02f, origin.z), Vector3.zero, new Vector3(2.15f, 0.012f, 2.15f), blackMat);
            Primitive(studio.transform, "Medallion Inner", PrimitiveType.Cylinder,
                new Vector3(origin.x, floorY + 0.028f, origin.z), Vector3.zero, new Vector3(1.5f, 0.012f, 1.5f), floorMat);

            var colInset = half - 1.35f;
            var colXs = new[] { -colInset, colInset, -colInset, colInset };
            var colZs = new[] { -colInset, -colInset, colInset, colInset };
            for (var i = 0; i < colXs.Length; i++)
            {
                EnsureColumn(studio.transform, "Column " + (i + 1),
                    origin.x + colXs[i], origin.z + colZs[i], floorY, hallH, columnMat, blackMat, goldMat);
            }

            for (var i = 5; i <= 8; i++)
            {
                var leftover = studio.transform.Find("Column " + i);
                if (leftover != null)
                    Object.DestroyImmediate(leftover.gameObject);
            }

            EnsureLight(studio.transform, "Deco Uplight 1", LightType.Point,
                new Vector3(origin.x + colInset, floorY + hallH - 0.55f, origin.z + colInset), goldColor, 1.7f, 5.5f, LightShadows.None);
            EnsureLight(studio.transform, "Deco Uplight 2", LightType.Point,
                new Vector3(origin.x - colInset, floorY + hallH - 0.55f, origin.z + colInset), goldColor, 1.7f, 5.5f, LightShadows.None);
            EnsureLight(studio.transform, "Deco Uplight 3", LightType.Point,
                new Vector3(origin.x + colInset, floorY + hallH - 0.55f, origin.z - colInset), goldColor, 1.7f, 5.5f, LightShadows.None);
            EnsureLight(studio.transform, "Deco Uplight 4", LightType.Point,
                new Vector3(origin.x - colInset, floorY + hallH - 0.55f, origin.z - colInset), goldColor, 1.7f, 5.5f, LightShadows.None);
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
            preview.playOnStart = true;

            StripCinemachineBrains();
        }

        static void EnsureDirector(GameObject gnome)
        {
            var studio = GameObject.Find(StudioName);
            if (studio == null)
                return;

            var cinematic = studio.GetComponent<GnomeCinematicPreview>();
            if (cinematic == null)
                cinematic = studio.AddComponent<GnomeCinematicPreview>();
            cinematic.enabled = true;
            cinematic.playOnStart = true;

            var follow = GameObject.Find("CameraFollow");
            if (follow == null)
                follow = new GameObject("CameraFollow");
            StripVisuals(follow);
            if (follow.GetComponent<CameraFollowMarker>() == null)
                follow.AddComponent<CameraFollowMarker>();

            var bounds = gnome != null ? CharacterBounds(gnome) : new Bounds(Vector3.up, Vector3.one);
            var followPos = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.72f, bounds.center.z);
            var anchor = studio.transform.Find("Follow Anchor");
            if (anchor == null)
            {
                var anchorGo = new GameObject("Follow Anchor");
                anchorGo.transform.SetParent(studio.transform, false);
                anchor = anchorGo.transform;
            }

            anchor.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            anchor.rotation = Quaternion.identity;
            follow.transform.SetParent(anchor, true);
            follow.transform.position = followPos;
            follow.transform.localScale = Vector3.one;

            StripExtraStudioCameras(studio);
            var height = Mathf.Max(0.8f, bounds.size.y);
            var main = FindNamedCamera("Main Camera");
            if (main != null)
            {
                DressCamera(main, 32f);
                var look = main.GetComponent<LookAtTarget>();
                if (look != null)
                    Object.DestroyImmediate(look);
                main.enabled = true;
                if (!main.CompareTag("MainCamera"))
                    main.tag = "MainCamera";
                var listener = main.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = true;
            }

            EnsureLight(studio.transform, "Soft Overhead", LightType.Point, bounds.center + new Vector3(0.1f, 2.4f, -0.35f),
                new Color(1f, 0.96f, 0.9f), 1.55f, 6.5f, LightShadows.None);

            var director = studio.GetComponent<GnomeStudioDirector>();
            if (director == null)
                director = studio.AddComponent<GnomeStudioDirector>();
            director.studioCamera = main;
            director.follow = follow.transform;
            director.subject = gnome != null ? gnome.transform : follow.transform;
            director.spot = GnomeStudioDirector.Spot.Front;
            director.orbit = 0f;
            director.distance = Mathf.Clamp(bounds.size.y * 1.55f, 1.6f, 3.5f);
            director.height = followPos.y + 0.22f;
            director.rackFocus = true;
            director.orbitSpeed = 46f;
            var sun = GameObject.Find("Directional Light");
            director.environmentLight = sun != null ? sun.GetComponent<Light>() : null;
            director.daylight = 1f;
            director.orbiting = false;
            director.orbitOnPlay = true;
            director.Apply();

            EnsureCinematicTimeline(studio, follow, height);
        }

        static void StripExtraStudioCameras(GameObject studio)
        {
            var rig = studio.transform.Find("Studio Cameras");
            if (rig != null)
                Object.DestroyImmediate(rig.gameObject);

            var all = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var cam = all[i];
                if (cam == null || cam.targetTexture != null)
                    continue;
                if (cam.hideFlags != HideFlags.None || !cam.gameObject.scene.IsValid())
                    continue;
                if (cam.name == "Main Camera")
                    continue;
                if (cam.name.StartsWith("Cam ") || cam.name.StartsWith("GnomeCM_"))
                    Object.DestroyImmediate(cam.gameObject);
            }
        }

        static void EnsureCinematicTimeline(GameObject studio, GameObject follow, float height)
        {
            var cmRoot = studio.transform.Find("CM Shots");
            if (cmRoot != null)
                Object.DestroyImmediate(cmRoot.gameObject);
            StripCinemachineBrains();

            var anim = EnsureFollowAnimation(height);
            var animator = follow.GetComponent<Animator>();
            if (animator == null)
                animator = follow.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.editorSettings.frameRate = 30;
                AssetDatabase.CreateAsset(timeline, TimelinePath);
            }

            BuildFollowTimeline(timeline, anim);

            var director = studio.GetComponent<PlayableDirector>();
            if (director == null)
                director = studio.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Loop;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is AnimationTrack)
                    director.SetGenericBinding(track, animator);
            }

            director.Stop();
            director.time = 0;
            var studioDirector = studio.GetComponent<GnomeStudioDirector>();
            if (studioDirector != null)
            {
                studioDirector.orbiting = false;
                studioDirector.Apply();
            }

            var preview = studio.GetComponent<GnomeCinematicPreview>();
            if (preview == null)
                preview = studio.AddComponent<GnomeCinematicPreview>();
            preview.playOnStart = true;
            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(director);
            AssetDatabase.SaveAssets();
        }

        static AnimationClip EnsureFollowAnimation(float height)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FollowAnimPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "CameraFollowLook", frameRate = 30 };
                AssetDatabase.CreateAsset(clip, FollowAnimPath);
            }

            clip.frameRate = 30;
            var chest = height * 0.72f;
            var face = height * 0.88f;
            var x = new AnimationCurve(Key(0f, 0f), Key(9.2f, 0.03f), Key(18.2f, 0.08f), Key(26f, 0f));
            var y = new AnimationCurve(
                Key(0f, chest), Key(3.2f, chest), Key(6.4f, face), Key(9.2f, face),
                Key(12.2f, chest), Key(15.4f, chest + 0.04f), Key(18.2f, face),
                Key(22.4f, chest), Key(26f, chest));
            var z = new AnimationCurve(Key(0f, 0f), Key(9.2f, 0.02f), Key(18.2f, 0.05f), Key(26f, 0f));
            clip.ClearCurves();
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", x);
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", y);
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.z", z);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static Keyframe Key(float time, float value)
        {
            var key = new Keyframe(time, value);
            key.inTangent = 0f;
            key.outTangent = 0f;
            return key;
        }

        static void BuildFollowTimeline(TimelineAsset timeline, AnimationClip followAnim)
        {
            ClearTimeline(timeline);
            var animTrack = timeline.CreateTrack<AnimationTrack>(null, "CameraFollow");
            var animClip = animTrack.CreateClip(followAnim);
            animClip.start = 0;
            animClip.duration = 26;
            animClip.displayName = "Look path";
        }

        static void ClearTimeline(TimelineAsset timeline)
        {
            foreach (var track in new List<TrackAsset>(timeline.GetOutputTracks()))
                timeline.DeleteTrack(track);
            var path = AssetDatabase.GetAssetPath(timeline);
            if (string.IsNullOrEmpty(path))
                return;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset == null || asset == timeline)
                    continue;
                AssetDatabase.RemoveObjectFromAsset(asset);
            }
        }

        static Camera FindNamedCamera(string name)
        {
            var all = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var cam = all[i];
                if (cam != null && cam.name == name && cam.hideFlags == HideFlags.None && cam.gameObject.scene.IsValid())
                    return cam;
            }

            return null;
        }

        static void StripCinemachineBrains()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < cams.Length; i++)
                StripCinemachineBrain(cams[i]);
        }

        static void StripCinemachineBrain(Camera camera)
        {
            if (camera == null)
                return;
            var comps = camera.GetComponents<Component>();
            for (var i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c != null && c.GetType().Name == "CinemachineBrain")
                    Object.DestroyImmediate(c);
            }
        }

        static void DressCamera(Camera cam, float fov)
        {
            if (cam == null)
                return;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.usePhysicalProperties = true;
            cam.sensorSize = new Vector2(36f, 24f);
            cam.gateFit = Camera.GateFitMode.Horizontal;
            cam.fieldOfView = fov;
            cam.backgroundColor = new Color(0.07f, 0.065f, 0.06f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null)
                return;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.stopNaN = true;
            data.dithering = true;
            data.renderShadows = true;
            data.volumeLayerMask = ~0;
        }

        static void StripVisuals(GameObject go)
        {
            foreach (var col in go.GetComponents<Collider>())
                Object.DestroyImmediate(col);
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                Object.DestroyImmediate(renderer);
            var filter = go.GetComponent<MeshFilter>();
            if (filter != null)
                Object.DestroyImmediate(filter);
        }

        static GameObject FindGnome()
        {
            var named = GameObject.Find("Gnome") ?? GameObject.Find("Character");
            if (named != null && named.GetComponent<GnomeStudioDirector>() == null)
                return named;
            var studioGo = GameObject.Find(StudioName);
            var director = studioGo != null ? studioGo.GetComponent<GnomeStudioDirector>() : null;
            if (director != null && director.subject != null
                && director.subject.GetComponent<GnomeStudioDirector>() == null
                && director.subject.GetComponent<CameraFollowMarker>() == null)
                return director.subject.gameObject;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == StudioName || root.name == "CameraFollow" || root.GetComponent<Camera>() != null)
                    continue;
                if (root.GetComponent<GnomeStudioDirector>() != null)
                    continue;
                if (root.GetComponentInChildren<CameraFollowMarker>() != null)
                    continue;
                if (root.name.IndexOf("gnome", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return root;
                if (root.GetComponent<Light>() != null || root.GetComponent<Volume>() != null)
                    continue;
                if (root.GetComponentInChildren<SkinnedMeshRenderer>() != null
                    || root.GetComponentInChildren<MeshRenderer>() != null
                    || root.GetComponentInChildren<Animator>() != null)
                    return root;
            }

            return null;
        }

        static void ImportRepeatTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Material EnsureLit(string path, Color color, float metallic, float smoothness, string texturePath, Vector2 tiling)
        {
            var mat = EnsureMaterial(path, color, metallic, smoothness);
            if (!string.IsNullOrEmpty(texturePath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                }
            }

            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureScale("_MainTex", tiling);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void WallBand(Transform parent, string prefix, Vector3 origin, float y, float height, float half, float inset, float thick, Material mat)
        {
            var span = half * 2f - inset * 2f;
            var depth = half - inset;
            Primitive(parent, prefix + " Back", PrimitiveType.Cube,
                new Vector3(origin.x, y, origin.z + depth), Vector3.zero, new Vector3(span, height, thick), mat);
            Primitive(parent, prefix + " Front", PrimitiveType.Cube,
                new Vector3(origin.x, y, origin.z - depth), Vector3.zero, new Vector3(span, height, thick), mat);
            Primitive(parent, prefix + " Left", PrimitiveType.Cube,
                new Vector3(origin.x - depth, y, origin.z), Vector3.zero, new Vector3(thick, height, span), mat);
            Primitive(parent, prefix + " Right", PrimitiveType.Cube,
                new Vector3(origin.x + depth, y, origin.z), Vector3.zero, new Vector3(thick, height, span), mat);
        }

        static void EnsureColumn(Transform parent, string name, float x, float z, float floorY, float hallH, Material marble, Material dark, Material gold)
        {
            var existing = parent.Find(name);
            GameObject root;
            if (existing != null)
                root = existing.gameObject;
            else
            {
                root = new GameObject(name);
                root.transform.SetParent(parent, false);
            }

            root.transform.position = new Vector3(x, floorY, z);
            root.transform.rotation = Quaternion.identity;
            var shaftH = hallH - 0.62f;
            ChildPrimitive(root.transform, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.09f, 0f), Vector3.zero, new Vector3(0.56f, 0.18f, 0.56f), dark);
            ChildPrimitive(root.transform, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.48f, 0.07f, 0.48f), marble);
            ChildPrimitive(root.transform, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0.29f + shaftH * 0.5f, 0f), Vector3.zero, new Vector3(0.34f, shaftH * 0.5f, 0.34f), marble);
            ChildPrimitive(root.transform, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.29f + shaftH + 0.04f, 0f), Vector3.zero, new Vector3(0.38f, 0.04f, 0.38f), gold);
            ChildPrimitive(root.transform, "Capital", PrimitiveType.Cube, new Vector3(0f, 0.29f + shaftH + 0.14f, 0f), Vector3.zero, new Vector3(0.6f, 0.12f, 0.6f), marble);
            ChildPrimitive(root.transform, "Abacus", PrimitiveType.Cube, new Vector3(0f, 0.29f + shaftH + 0.24f, 0f), Vector3.zero, new Vector3(0.7f, 0.08f, 0.7f), gold);
        }

        static GameObject ChildPrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
            }

            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;
            go.isStatic = true;
            return go;
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

            for (var i = profile.components.Count - 1; i >= 0; i--)
            {
                if (profile.components[i] == null)
                    profile.components.RemoveAt(i);
            }

            var tone = GetOrAdd<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.78f);
            bloom.intensity.Override(0.34f);
            bloom.scatter.Override(0.74f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.88f));
            bloom.highQualityFiltering.Override(true);
            bloom.clamp.Override(24f);

            var dof = GetOrAdd<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(2.2f);
            dof.aperture.Override(5.6f);
            dof.focalLength.Override(50f);
            dof.bladeCount.Override(6);
            dof.bladeCurvature.Override(0.85f);
            dof.bladeRotation.Override(8f);
            dof.gaussianStart.Override(2.4f);
            dof.gaussianEnd.Override(9f);
            dof.gaussianMaxRadius.Override(1.05f);
            dof.highQualitySampling.Override(true);

            var motion = GetOrAdd<MotionBlur>(profile);
            motion.mode.Override(MotionBlurMode.CameraAndObjects);
            motion.quality.Override(MotionBlurQuality.High);
            motion.intensity.Override(0.16f);

            var flare = GetOrAdd<ScreenSpaceLensFlare>(profile);
            flare.intensity.Override(0.1f);
            flare.tintColor.Override(new Color(1f, 0.92f, 0.8f));
            flare.firstFlareIntensity.Override(0.85f);
            flare.secondaryFlareIntensity.Override(0.45f);
            flare.warpedFlareIntensity.Override(0.25f);
            flare.streaksIntensity.Override(0.12f);
            flare.streaksLength.Override(0.35f);
            flare.bloomMip.Override(2);
            flare.samples.Override(2);

            var distort = GetOrAdd<LensDistortion>(profile);
            distort.intensity.Override(-0.055f);
            distort.scale.Override(1.015f);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.48f);
            vignette.color.Override(new Color(0.04f, 0.025f, 0.02f));

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.2f);
            color.contrast.Override(18f);
            color.saturation.Override(8f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.93f));

            var white = GetOrAdd<WhiteBalance>(profile);
            white.temperature.Override(10f);
            white.tint.Override(-2f);

            var grain = GetOrAdd<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.14f);
            grain.response.Override(0.82f);

            var split = GetOrAdd<SplitToning>(profile);
            split.shadows.Override(new Color(0.28f, 0.38f, 0.52f));
            split.highlights.Override(new Color(1f, 0.84f, 0.68f));
            split.balance.Override(8f);

            var smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            smh.shadows.Override(new Vector4(0.88f, 0.93f, 1.08f, -0.04f));
            smh.midtones.Override(new Vector4(1.02f, 0.99f, 0.96f, 0.04f));
            smh.highlights.Override(new Vector4(1.08f, 1.02f, 0.92f, 0.05f));

            var chroma = GetOrAdd<ChromaticAberration>(profile);
            chroma.intensity.Override(0.04f);

            var lift = GetOrAdd<LiftGammaGain>(profile);
            lift.lift.Override(new Vector4(0.97f, 0.985f, 1.03f, -0.015f));
            lift.gamma.Override(new Vector4(1f, 0.99f, 0.975f, 0.01f));
            lift.gain.Override(new Vector4(1.05f, 1.015f, 0.95f, 0.04f));

            var mix = GetOrAdd<ChannelMixer>(profile);
            mix.redOutBlueIn.Override(-6f);
            mix.blueOutRedIn.Override(-8f);
            mix.greenOutRedIn.Override(2f);

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
