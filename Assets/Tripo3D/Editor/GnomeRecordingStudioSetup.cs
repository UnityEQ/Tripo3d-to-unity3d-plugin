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
            Debug.Log("[Gnome Studio] Cinematic cameras, CameraFollow, and short-film post are ready.");
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

            EnsureDirector(gnome);

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

        static void EnsureSet(GameObject gnome)
        {
            var studio = GameObject.Find(StudioName);
            if (studio == null || gnome == null)
                return;
            var origin = gnome.transform.position;
            var bounds = CharacterBounds(gnome);
            var floorY = bounds.min.y - 0.01f;
            EnsureMaterial(FloorMat, new Color(0.16f, 0.14f, 0.12f), 0.08f, 0.38f);
            EnsureMaterial(WallMat, new Color(0.78f, 0.74f, 0.68f), 0.02f, 0.22f);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorMat);
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>(WallMat);

            var floor = Primitive(studio.transform, "Floor", PrimitiveType.Plane, new Vector3(origin.x, floorY, origin.z), Vector3.zero, new Vector3(1.5f, 1f, 1.5f), floorMat);
            floor.isStatic = true;

            Primitive(studio.transform, "Wall Back", PrimitiveType.Cube,
                new Vector3(origin.x, 3.15f, origin.z + 5.8f), Vector3.zero, new Vector3(14f, 6.4f, 0.16f), wallMat);
            Primitive(studio.transform, "Wall Left", PrimitiveType.Cube,
                new Vector3(origin.x - 5.8f, 3.15f, origin.z + 0.1f), new Vector3(0f, 90f, 0f), new Vector3(12.5f, 6.4f, 0.16f), wallMat);
            Primitive(studio.transform, "Wall Right", PrimitiveType.Cube,
                new Vector3(origin.x + 5.8f, 3.15f, origin.z + 0.1f), new Vector3(0f, 90f, 0f), new Vector3(12.5f, 6.4f, 0.16f), wallMat);
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
            cinematic.loop = true;
            if (gnome != null)
                cinematic.subject = gnome.transform;

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

            var rig = studio.transform.Find("Studio Cameras");
            if (rig == null)
            {
                var rigGo = new GameObject("Studio Cameras");
                rigGo.transform.SetParent(studio.transform, false);
                rig = rigGo.transform;
            }

            var height = Mathf.Max(0.8f, bounds.size.y);
            var main = FindNamedCamera(null, "Main Camera");
            if (main != null)
            {
                DressCamera(main, 32f);
                var mainLook = main.GetComponent<LookAtTarget>();
                if (mainLook == null)
                    mainLook = main.gameObject.AddComponent<LookAtTarget>();
                mainLook.target = follow.transform;
                mainLook.useRendererBounds = false;
                mainLook.worldOffset = Vector3.zero;
                mainLook.extraPitch = 3f;
                main.transform.position = followPos + new Vector3(-1.35f, 0.22f, -2.40f);
                mainLook.ReadOrbitFromPose();
                mainLook.ApplyLook();
            }

            var list = new List<Camera>();
            if (main != null)
                list.Add(main);

            list.Add(EnsureShot(rig, "Cam CloseUp", followPos + new Vector3(0.10f, 0.32f, -0.58f), 24f, 2f));
            list.Add(EnsureShot(rig, "Cam ThreeQuarter", followPos + new Vector3(1.12f, 0.08f, -1.72f), 30f, 7f));
            list.Add(EnsureShot(rig, "Cam Profile", followPos + new Vector3(1.92f, 0.06f, 0.12f), 32f, 5f));
            list.Add(EnsureShot(rig, "Cam Low", followPos + new Vector3(0.52f, -0.48f, -1.78f), 34f, -7f));
            list.Add(EnsureShot(rig, "Cam High", followPos + new Vector3(0.42f, 0.82f, -1.78f), 34f, 12f));
            list.Add(EnsureShot(rig, "Cam Back", followPos + new Vector3(-1.18f, 0.1f, 1.82f), 32f, 6f));

            EnsureLight(studio.transform, "Soft Overhead", LightType.Point, bounds.center + new Vector3(0.1f, 2.4f, -0.35f),
                new Color(1f, 0.96f, 0.9f), 1.55f, 6.5f, LightShadows.None);

            var director = studio.GetComponent<GnomeStudioDirector>();
            if (director == null)
                director = studio.AddComponent<GnomeStudioDirector>();
            director.follow = follow.transform;
            director.subject = gnome != null ? gnome.transform : follow.transform;
            director.cameras = list;
            director.lookAtFollow = true;
            director.rackFocus = true;
            director.Apply();

            EnsureCinematicTimeline(studio, gnome, follow, height, list);
        }

        static Camera EnsureShot(Transform parent, string name, Vector3 worldPos, float fov, float extraPitch)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, true);
                go.AddComponent<Camera>();
                go.AddComponent<UniversalAdditionalCameraData>();
                go.AddComponent<AudioListener>().enabled = false;
            }

            go.transform.position = worldPos;
            var cam = go.GetComponent<Camera>();
            DressCamera(cam, fov);
            cam.enabled = false;
            if (cam.CompareTag("MainCamera"))
                cam.tag = "Untagged";
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
            var look = cam.GetComponent<LookAtTarget>();
            if (look == null)
                look = cam.gameObject.AddComponent<LookAtTarget>();
            look.target = GameObject.Find("CameraFollow") != null ? GameObject.Find("CameraFollow").transform : look.target;
            look.useRendererBounds = false;
            look.worldOffset = Vector3.zero;
            look.extraPitch = extraPitch;
            look.ReadOrbitFromPose();
            look.ApplyLook();
            return cam;
        }

        static void EnsureCinematicTimeline(GameObject studio, GameObject gnome, GameObject follow, float height, List<Camera> cameras)
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

            BuildStudioCameraTimeline(timeline, anim);

            var director = studio.GetComponent<PlayableDirector>();
            if (director == null)
                director = studio.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Loop;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;

            BindStudioCameraTimeline(director, timeline, animator, cameras);
            director.Stop();
            director.time = 0;
            var studioDirector = studio.GetComponent<GnomeStudioDirector>();
            if (studioDirector != null)
            {
                studioDirector.cinematicActive = false;
                studioDirector.ActivateCameraObjects();
                var liveCam = FindNamedCamera(cameras, "Main Camera");
                if (liveCam != null)
                    studioDirector.SetLive(liveCam);
                else
                    studioDirector.Apply();
            }

            var preview = studio.GetComponent<GnomeCinematicPreview>();
            if (preview == null)
                preview = studio.AddComponent<GnomeCinematicPreview>();
            preview.subject = gnome != null ? gnome.transform : follow.transform;
            preview.follow = follow.transform;
            preview.playable = director;
            preview.playOnStart = true;
            preview.loop = true;
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

        static void BuildStudioCameraTimeline(TimelineAsset timeline, AnimationClip followAnim)
        {
            ClearTimeline(timeline);

            var main = AddActivationTrack(timeline, "Main Camera");
            AddActivationClip(main, "Wide", 0f, 3.4f);
            AddActivationClip(main, "WideOut", 22.4f, 3.6f);
            AddActivationClip(AddActivationTrack(timeline, "Cam ThreeQuarter"), "ThreeQuarter", 3.4f, 3.4f);
            AddActivationClip(AddActivationTrack(timeline, "Cam CloseUp"), "CloseUp", 6.8f, 3.2f);
            AddActivationClip(AddActivationTrack(timeline, "Cam Profile"), "Profile", 10f, 3.4f);
            AddActivationClip(AddActivationTrack(timeline, "Cam Low"), "Low", 13.4f, 3.2f);
            AddActivationClip(AddActivationTrack(timeline, "Cam High"), "High", 16.6f, 3.2f);
            AddActivationClip(AddActivationTrack(timeline, "Cam Back"), "Back", 19.8f, 2.6f);

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

        static ActivationTrack AddActivationTrack(TimelineAsset timeline, string name)
        {
            var track = timeline.CreateTrack<ActivationTrack>(null, name);
            track.postPlaybackState = ActivationTrack.PostPlaybackState.Active;
            return track;
        }

        static void AddActivationClip(ActivationTrack track, string name, float start, float duration)
        {
            var clip = track.CreateDefaultClip();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = name;
        }

        static void BindStudioCameraTimeline(
            PlayableDirector director,
            TimelineAsset timeline,
            Animator followAnimator,
            List<Camera> cameras)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is AnimationTrack)
                {
                    director.SetGenericBinding(track, followAnimator);
                    continue;
                }

                if (!(track is ActivationTrack))
                    continue;
                var cam = FindNamedCamera(cameras, track.name);
                director.SetGenericBinding(track, cam != null ? cam.gameObject : null);
            }
        }

        static Camera FindNamedCamera(List<Camera> cameras, string name)
        {
            if (cameras != null)
            {
                for (var i = 0; i < cameras.Count; i++)
                {
                    var cam = cameras[i];
                    if (cam != null && cam.name == name)
                        return cam;
                }
            }

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
            var look = Camera.main != null ? Camera.main.GetComponent<LookAtTarget>() : null;
            if (look != null && look.target != null
                && look.target.GetComponent<CameraFollowMarker>() == null
                && look.target.GetComponentInParent<GnomeStudioDirector>() == null)
                return look.target.gameObject;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == StudioName || root.name == "CameraFollow" || root.GetComponent<Camera>() != null)
                    continue;
                if (root.GetComponent<GnomeStudioDirector>() != null)
                    continue;
                if (root.name.IndexOf("gnome", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return root;
                if (root.GetComponentInChildren<Animator>() != null
                    && root.GetComponentInChildren<CameraFollowMarker>() == null)
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
