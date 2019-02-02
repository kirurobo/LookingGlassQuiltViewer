//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using UnityEngine;
using UnityEditor;

namespace HoloPlay
{
    namespace UI
    {
        [CustomEditor(typeof(Extras.DebugPrintout))]
        public class DebugPrintoutEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                GUI.color = Misc.guiColor;
                EditorGUILayout.LabelField("- Debug -", EditorStyles.whiteMiniLabel);
                GUI.color = Color.white;

                EditorGUILayout.HelpBox(
                    "Press F9 while in-game to enable debug printout.\n" +
                    "Used to display a printout of the SDK version and calibration info. Leave disabled--is controlled by quilt",
                    MessageType.None
                );
            }
        }
    }
}