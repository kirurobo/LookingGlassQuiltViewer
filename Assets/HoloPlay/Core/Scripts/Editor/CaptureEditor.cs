//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

//parts taken from Game Window Mover script, original notes for that here:

//Source from http://answers.unity3d.com/questions/179775/game-window-size-from-editor-window-in-editor-mode.html
//Modified by seieibob for use at the Virtual Environment and Multimodal Interaction Lab at the University of Maine.
//Use however you'd like!

using UnityEditor;
using UnityEngine;

namespace HoloPlay
{
    namespace UI
    {
        // ? add handles
        // https://docs.unity3d.com/ScriptReference/Handles.html
        [InitializeOnLoad]
        [CustomEditor(typeof(Capture))]
        public class CaptureEditor : Editor
        {
            SerializedProperty size;
            SerializedProperty nearClipFactor;
            SerializedProperty farClipFactor;
            SerializedProperty orthographic;
            SerializedProperty fov;
            SerializedProperty advancedFoldout;
            SerializedProperty useCustomVerticalAngle;
            SerializedProperty customVerticalAngle;
            SerializedProperty useCustomViewCone;
            SerializedProperty customViewCone;
            Capture capture;
            SerializedObject serializedCam;

            void OnEnable()
            {
                size = serializedObject.FindProperty("size");
                nearClipFactor = serializedObject.FindProperty("nearClipFactor");
                farClipFactor = serializedObject.FindProperty("farClipFactor");
                fov = serializedObject.FindProperty("fov");
                advancedFoldout = serializedObject.FindProperty("advancedFoldout");
                useCustomVerticalAngle = serializedObject.FindProperty("useCustomVerticalAngle");
                customVerticalAngle = serializedObject.FindProperty("customVerticalAngle");
                useCustomViewCone = serializedObject.FindProperty("useCustomViewCone");
                customViewCone = serializedObject.FindProperty("customViewCone");

                capture = (Capture)target;
                if (capture.Cam != null)
                {
                    serializedCam = new SerializedObject(capture.Cam);
                    orthographic = serializedCam.FindProperty("orthographic");
                }
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                if (serializedCam != null)
                    serializedCam.Update();

                GUI.color = Misc.guiColorLight;
                EditorGUILayout.LabelField("HoloPlay " + Misc.version, EditorStyles.centeredGreyMiniLabel);
                GUI.color = Color.white;

                GUI.color = Misc.guiColor;
                EditorGUILayout.LabelField("- Camera -", EditorStyles.whiteMiniLabel);
                GUI.color = Color.white;

                EditorGUILayout.PropertyField(size);

                advancedFoldout.boolValue = EditorGUILayout.Foldout(advancedFoldout.boolValue, "Advanced", true);
                if (advancedFoldout.boolValue)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(nearClipFactor);
                    EditorGUILayout.PropertyField(farClipFactor);

                    if (orthographic != null)
                        EditorGUILayout.PropertyField(orthographic);

                    if (orthographic != null)
                        GUI.enabled = !orthographic.boolValue;
                    EditorGUILayout.PropertyField(fov);
                    GUI.enabled = true;

                    EditorGUILayout.PropertyField(useCustomVerticalAngle);
                    GUI.enabled = useCustomVerticalAngle.boolValue;
                    EditorGUILayout.PropertyField(customVerticalAngle);
                    GUI.enabled = true;

                    EditorGUILayout.PropertyField(useCustomViewCone);
                    GUI.enabled = useCustomViewCone.boolValue;
                    EditorGUILayout.PropertyField(customViewCone);
                    GUI.enabled = true;

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                serializedObject.ApplyModifiedProperties();
                if (serializedCam != null)
                    serializedCam.ApplyModifiedProperties();
            }
        }
    }
}