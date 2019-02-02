//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using UnityEngine;
using UnityEditor;

namespace HoloPlay
{
    namespace UI
    {
        [InitializeOnLoad]
        [CustomEditor(typeof(Buttons))]
        public class ButtonsEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.Space();

                GUI.color = Misc.guiColor;
                EditorGUILayout.LabelField("- Buttons -", EditorStyles.whiteMiniLabel);
                GUI.color = Color.white;

                EditorGUILayout.HelpBox(
                    "Call one of these static methods like you would Input.GetButton:\n\n" +
                    "HoloPlay.Buttons.GetButton(HoloPlay.ButtonType)\n" +
                    "HoloPlay.Buttons.GetButtonDown(HoloPlay.ButtonType)\n" +
                    "HoloPlay.Buttons.GetButtonUp(HoloPlay.ButtonType)\n\n" +
                    "You don't need an instance of this script in your scene to use them, but if it doesn't exist, it will be created on call.\n\n" +
                    "Be sure not to attach this component to game objects arbitrarily because this script calls DoNotDestroyOnLoad() on itself.",
                    MessageType.None
                );

                DrawDefaultInspector();
            }
        }
    }
}
