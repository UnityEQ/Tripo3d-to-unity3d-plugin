using UnityEditor;
using UnityEngine;

namespace Tripo3D.Editor
{
    [CustomEditor(typeof(GnomeStudioDirector))]
    sealed class GnomeStudioDirectorEditor : UnityEditor.Editor
    {
        static readonly GnomeStudioDirector.Spot[] Spots =
        {
            GnomeStudioDirector.Spot.Front,
            GnomeStudioDirector.Spot.Right,
            GnomeStudioDirector.Spot.Back,
            GnomeStudioDirector.Spot.Left
        };

        SerializedProperty studioCameraProperty;
        SerializedProperty followProperty;
        SerializedProperty subjectProperty;
        SerializedProperty rackFocusProperty;
        SerializedProperty environmentLightProperty;
        SerializedProperty orbitProperty;
        SerializedProperty distanceProperty;
        SerializedProperty heightProperty;
        SerializedProperty daylightProperty;
        SerializedProperty orbitSpeedProperty;
        SerializedProperty orbitOnPlayProperty;
        SerializedProperty orbitingProperty;

        void OnEnable()
        {
            studioCameraProperty = serializedObject.FindProperty("studioCamera");
            followProperty = serializedObject.FindProperty("follow");
            subjectProperty = serializedObject.FindProperty("subject");
            rackFocusProperty = serializedObject.FindProperty("rackFocus");
            environmentLightProperty = serializedObject.FindProperty("environmentLight");
            orbitProperty = serializedObject.FindProperty("orbit");
            distanceProperty = serializedObject.FindProperty("distance");
            heightProperty = serializedObject.FindProperty("height");
            daylightProperty = serializedObject.FindProperty("daylight");
            orbitSpeedProperty = serializedObject.FindProperty("orbitSpeed");
            orbitOnPlayProperty = serializedObject.FindProperty("orbitOnPlay");
            orbitingProperty = serializedObject.FindProperty("orbiting");
        }

        public override bool RequiresConstantRepaint()
        {
            return ((GnomeStudioDirector)target).orbiting;
        }

        public override void OnInspectorGUI()
        {
            var director = (GnomeStudioDirector)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(studioCameraProperty);
            EditorGUILayout.PropertyField(followProperty);
            EditorGUILayout.PropertyField(subjectProperty);
            EditorGUILayout.PropertyField(rackFocusProperty);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            serializedObject.Update();
            EditorGUILayout.PropertyField(environmentLightProperty);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Orbit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "One Main Camera. The four spots snap around the subject. Orbit, Distance, Height, and Daylight run as one loop when you press Play orbit.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < Spots.Length; i++)
            {
                var s = Spots[i];
                var live = director.spot == s && Mathf.Abs(Mathf.DeltaAngle(director.orbit, GnomeStudioDirector.OrbitOf(s))) < 8f;
                var color = GUI.backgroundColor;
                if (live)
                    GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
                if (GUILayout.Button(s.ToString(), GUILayout.Height(26f)))
                {
                    Undo.RecordObject(director, "Orbit spot");
                    director.SetSpot(s);
                    EditorUtility.SetDirty(director);
                }

                GUI.backgroundColor = color;
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(orbitProperty);
            EditorGUILayout.PropertyField(distanceProperty);
            EditorGUILayout.PropertyField(heightProperty);
            EditorGUILayout.PropertyField(daylightProperty);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                director.SyncSpotFromOrbit();
                director.Apply();
                EditorUtility.SetDirty(director);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Cinematic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play orbit runs one loop: Orbit, Distance, Height, and Daylight all slide together. Stop freezes the sliders where they are.",
                MessageType.Info);
            serializedObject.Update();
            EditorGUILayout.PropertyField(orbitSpeedProperty);
            EditorGUILayout.PropertyField(orbitOnPlayProperty);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.BeginHorizontal();
            var playColor = GUI.backgroundColor;
            if (director.orbiting)
                GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
            if (GUILayout.Button(director.orbiting ? "Orbiting…" : "Play orbit", GUILayout.Height(26f)))
            {
                Undo.RecordObject(director, "Play orbit");
                director.StartOrbit();
                orbitingProperty.boolValue = true;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(director);
            }

            GUI.backgroundColor = playColor;
            if (GUILayout.Button("Stop", GUILayout.Height(26f)))
            {
                Undo.RecordObject(director, "Stop orbit");
                director.StopOrbit();
                orbitingProperty.boolValue = false;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(director);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
