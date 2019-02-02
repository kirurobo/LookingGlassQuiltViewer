//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloPlay
{
    namespace Extras
    {
        public class SimpleQuit : MonoBehaviour
        {
            public KeyCode quitKey = KeyCode.Escape;

            private void Update()
            {
                if (quitKey != KeyCode.None && UnityEngine.Input.GetKeyDown(quitKey))
                {
                    quitApp();
                }
            }

            //this should stay as a separate method, so it can be called from elsewhere, if desired.
            public void quitApp()
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
