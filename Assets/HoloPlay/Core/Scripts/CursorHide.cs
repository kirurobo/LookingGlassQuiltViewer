//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoloPlay
{
    namespace Extras
    {
        public class CursorHide : MonoBehaviour
        {
            public bool hideInBuildsOnly;

            void OnEnable()
            {
                if (hideInBuildsOnly && Application.isEditor)
                    return;

                Cursor.visible = false;
            }

            void OnDisable()
            {
                if (hideInBuildsOnly && Application.isEditor)
                    return;

                Cursor.visible = true;
            }
        }
    }
}

#if UNITY_EDITOR
namespace HoloPlay
{
    namespace UI
    {
        [CustomEditor(typeof(Extras.CursorHide))]
        public class CursorHideEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                EditorGUILayout.LabelField(
                    "Used to hide cursor. Useful for HoloPlay apps, " +
                    "but if you want a mouse cursor, just disable or remove this component.",
                    EditorStyles.helpBox
                );
            }
        }
    }
}
#endif