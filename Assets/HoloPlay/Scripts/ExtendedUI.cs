//Copyright 2017-2019 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using UnityEngine;

namespace LookingGlass {
    [ExecuteInEditMode]
    [HelpURL("https://docs.lookingglassfactory.com/Unity/Scripts/ExtendedUI/")]
    public class ExtendedUI : MonoBehaviour {

		Holoplay holoplay { get{ return Holoplay.Instance; } }
        public Canvas canvas;
        public Camera bgCam;
        [Tooltip("Copies the Holoplay camera position and settings to the UI's background camera")]
        public bool copyBGCamSettings;
        public bool singleDisplayMode = true;

        void OnEnable() {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            Debug.Log("[Holoplay] Multi-display not supported on OSX or Linux.");
            bgCam.enabled = false;
            canvas.targetDisplay = 0;
            holoplay.targetDisplay = PluginCore.GetLKGunityIndex(0);
            singleDisplayMode = true;
#else 
            singleDisplayMode = false;
            if (!Application.isEditor)
                singleDisplayMode = Display.displays.Length < 2;

            if (singleDisplayMode) {
                Debug.Log("[Holoplay] Extended UI: single display mode");
                bgCam.enabled = false;
                return;
            }
            PluginCore.GetLoadResults();
            if (CalibrationManager.GetCalibrationCount() < 1) {
                Debug.Log("[Holoplay] No LKG detected for extended UI");
                bgCam.enabled = false;
                holoplay.targetDisplay = 0;
                return;
            }
            // continue with actual extended ui logic
            // set Holoplay target display to the lkg display
            holoplay.targetDisplay = PluginCore.GetLKGunityIndex(0);
            holoplay.ReloadCalibration(); // must reload calibration after setting targetdisplay
            if (!Application.isEditor){
                Display.displays[holoplay.targetDisplay].Activate();
                // Display.displays[holoplay.targetDisplay].SetRenderingResolution(holoplay.cal.screenHeight,
                // holoplay.cal.screenHeight);
                // TODO: nothing works here
// #if UNITY_STANDALONE_WIN
//                 Display.displays[holoplay.targetDisplay].SetParams(
// 					holoplay.cal.screenWidth, holoplay.cal.screenHeight,
// 					holoplay.cal.xpos, holoplay.cal.ypos
// 				);
// 				Debug.LogFormat("{0}, {1}, {2}, {3}", 
//                     holoplay.cal.screenWidth, holoplay.cal.screenHeight,
// 					holoplay.cal.xpos, holoplay.cal.ypos);
// #endif
            }
                
            // set the canvas target display to the main display
            canvas.targetDisplay = 0;
            if (bgCam) {
                bgCam.enabled = true;
                bgCam.targetDisplay = 0;
            }
#endif
        }

        void Update() {
            if (!singleDisplayMode) {
                if (holoplay == null) {
                    Debug.LogWarning("[Holoplay] No holoplay detected for extended UI!");
                    enabled = false;
                    return;
                }
                holoplay.targetDisplay = PluginCore.GetLKGunityIndex(0);
                if (bgCam && bgCam.enabled && copyBGCamSettings) {
                    bgCam.CopyFrom(holoplay.cam);
                    bgCam.targetDisplay = 0;
                }
            }
        }
    }
}