//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoloPlay
{
    [ExecuteInEditMode]
    public class ExtendedUICamera : MonoBehaviour
    {
        // todo: make this flip appropriately with a shader if it's on the holoplay sreen

        [System.Serializable]
        public class UnityEventBool : UnityEvent<bool> { }
        public UnityEventBool onDisplaySetup;

        public static bool secondScreen { get; private set; }

        Camera cam;

        void OnEnable()
        {
            cam = GetComponent<Camera>();

            StartCoroutine(WaitToSetupDisplay());
        }

        IEnumerator WaitToSetupDisplay()
        {
            while (Quilt.Instance == null)
                yield return null;

            SetupSecondDisplay();
        }

        void SetupSecondDisplay()
        {
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();

                // set the quilt cam to the proper one
                int qd = 0;
                foreach (var d in Display.displays)
                {
                    Debug.Log(d.systemWidth + " " + d.systemHeight);
                    if (d.systemWidth == Quilt.Instance.config.screenW.asInt &&
                        d.systemHeight == Quilt.Instance.config.screenH.asInt)
                    {
                        break;
                    }
                    qd++;
                }
                qd = Mathf.Min(qd, Display.displays.Length - 1);
                Quilt.Instance.QuiltCam.targetDisplay = qd;
                Display.displays[qd].SetRenderingResolution(Quilt.Instance.config.screenW.asInt, Quilt.Instance.config.screenH.asInt);
                // Screen.SetResolution(config.screenW.asInt, config.screenH.asInt, true);

                // set the UI to the other
                for (int i = 0; i < Display.displays.Length; i++)
                {
                    if (i != qd)
                    {
                        cam.targetDisplay = i;
                        break;
                    }
                }
                cam.clearFlags = CameraClearFlags.SolidColor;
                Debug.Log(Misc.debugLogText + "Using multiple displays for separate UI");
                secondScreen = true;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.Nothing;
                if (!Application.isEditor) // don't want to spam editor console with this
                    Debug.Log(Misc.debugLogText + "Cannot extend UI");
                secondScreen = false;
            }


            if (onDisplaySetup.GetPersistentEventCount() > 0)
                onDisplaySetup.Invoke(secondScreen);
        }
    }

#if UNITY_EDITOR
    namespace HoloPlay
    {
        namespace UI
        {
            [CustomEditor(typeof(ExtendedUICamera))]
            public class ExtendedUICameraEditor : Editor
            {
                public override void OnInspectorGUI()
                {
                    EditorGUILayout.Space();

                    GUI.color = Misc.guiColor;
                    EditorGUILayout.LabelField("- Extended UI -", EditorStyles.whiteMiniLabel);
                    GUI.color = Color.white;

                    EditorGUILayout.HelpBox(
                        // "How HoloPlay UI works:\n\n" +
                        // "Some HoloPlay-based products mirror or rotate the screen. " +
                        // "Using this prefab as your canvas ensures that the UI will be flipped properly on all systems.",
                        "Windows only: if using two displays, use this to put the UI on the main 2D display. " +
                        "This is useful if your UI is too dense for display in the Looking Glass.\n\n" +
                        "Use the static bool 'ExtendedUICamera.secondScreen' to check if a second screen was successfully setup.\n\n" +
                        "'onDisplaySetup' will be invoked once this script is done attempting to setup the second screen.",
                        MessageType.None
                    );

                    EditorGUILayout.Space();

                    base.OnInspectorGUI();
                }
            }
        }
    }
#endif
}