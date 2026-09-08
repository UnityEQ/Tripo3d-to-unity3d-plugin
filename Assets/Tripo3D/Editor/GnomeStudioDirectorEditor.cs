using UnityEditor;
using UnityEngine;

namespace Tripo3D.Editor
{
    [CustomEditor(typeof(GnomeStudioDirector))]
    sealed class GnomeStudioDirectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var director = (GnomeStudioDirector)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("follow"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("subject"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookAtFollow"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rackFocus"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("closestHysteresis"));
            EditorGUILayout.Space(6f);

            var names = CameraNames(director);
            var index = Mathf.Clamp(director.activeCamera, 0, Mathf.Max(0, names.Length - 1));
            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup(new GUIContent("Active camera", "Manual pick. Also used as the starting camera."), index, names);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(director, "Active camera");
                director.pickMode = GnomeStudioDirector.PickMode.Manual;
                director.SetLiveIndex(index);
                EditorUtility.SetDirty(director);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Switch camera", EditorStyles.boldLabel);
            var row = 0;
            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < director.cameras.Count; i++)
            {
                var cam = director.cameras[i];
                if (cam == null)
                    continue;
                if (row > 0 && row % 3 == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                var live = director.LiveCamera == cam;
                var color = GUI.backgroundColor;
                if (live)
                    GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
                if (GUILayout.Button(ShortName(cam.name), GUILayout.Height(24f)))
                {
                    Undo.RecordObject(director, "Active camera");
                    director.pickMode = GnomeStudioDirector.PickMode.Manual;
                    director.SetLive(cam);
                    EditorUtility.SetDirty(director);
                }

                GUI.backgroundColor = color;
                row++;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Use closest camera to CameraFollow"))
            {
                Undo.RecordObject(director, "Closest camera");
                director.pickMode = GnomeStudioDirector.PickMode.ClosestToFollow;
                director.Apply();
                EditorUtility.SetDirty(director);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Studio camera Timeline", EditorStyles.boldLabel);
            var preview = director.GetComponent<GnomeCinematicPreview>();
            if (preview != null)
            {
                EditorGUILayout.HelpBox(
                    "Play Mode runs the Timeline on the Studio Cameras (Main, CloseUp, ThreeQuarter, and the rest). CameraFollow animates from body to face. Open the Timeline window to retiming Activation tracks.",
                    MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Play cinematic", GUILayout.Height(26f)))
                {
                    if (!Application.isPlaying)
                        EditorApplication.EnterPlaymode();
                    else
                        preview.PlayPreview();
                }

                if (GUILayout.Button("Stop", GUILayout.Height(26f)))
                    preview.StopPreview();
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed && !director.cinematicActive)
                director.Apply();
        }

        static string[] CameraNames(GnomeStudioDirector director)
        {
            if (director.cameras == null || director.cameras.Count == 0)
                return new[] { "(none)" };
            var names = new string[director.cameras.Count];
            for (var i = 0; i < director.cameras.Count; i++)
            {
                var cam = director.cameras[i];
                names[i] = cam != null ? cam.name : "(missing)";
            }

            return names;
        }

        static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Camera";
            return name.StartsWith("Cam ") ? name.Substring(4) : name;
        }
    }
}
