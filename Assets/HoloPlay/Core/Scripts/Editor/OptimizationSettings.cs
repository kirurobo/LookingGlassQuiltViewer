//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoloPlay
{
    namespace UI
    {
        [InitializeOnLoad]
        public class OptimizationSettings : EditorWindow
        {
            // public static HoloPlaySettingsPrompt Instance { get; private set; }
            // public static bool IsOpen { get { return Instance != null; } }

            class setting
            {
                public string label;
                public bool on;
                public bool isQualitySetting;
                public Action settingChange;
                public setting(string label, bool isQualitySetting, Action settingChange)
                {
                    this.label = label;
                    this.on = true;
                    this.isQualitySetting = isQualitySetting;
                    this.settingChange = settingChange;
                }
            }
            List<setting> settings = new List<setting>
        {
            new setting("Shadows: Hard Only", true,
                () => { QualitySettings.shadows = ShadowQuality.HardOnly; }
            ),
            new setting("Shadow Projection: Close Fit", true,
                () => { QualitySettings.shadowProjection = ShadowProjection.CloseFit; }
            ),
            new setting("Shadow Distance: 1000", true,
                () => { QualitySettings.shadowDistance = 1000; }
            ),
            new setting("Shadow Cascades: 0", true,
                () => { QualitySettings.shadowCascades = 0; }
            ),
            new setting("vSync: off", true,
                () => { QualitySettings.vSyncCount = 0; }
            ),
            new setting("macOS Graphics API: OpenGLCore", false,
                () => {
                    foreach (var b in buildPlatforms)
                    {
                        PlayerSettings.SetUseDefaultGraphicsAPIs(b, false);
                        var graphicsAPIs = new GraphicsDeviceType[]{
                            GraphicsDeviceType.OpenGLCore,
                            GraphicsDeviceType.Metal
                        };
                        PlayerSettings.SetGraphicsAPIs(b, graphicsAPIs);
                        if (EditorUserBuildSettings.activeBuildTarget == b)
                        {
                            Debug.LogWarning("For graphics API switch to take effect, a project re-open is required");
                        }
                    }
                }
            ),
            new setting("Splash Screen: off (pro/plus only)", false,
                () => { PlayerSettings.SplashScreen.show = false; }
            ),
            new setting("Run in Background", false,
                () => { PlayerSettings.runInBackground = true; }
            ),
            // new setting("Color Space: Linear", false,
            //     () => { PlayerSettings.colorSpace = ColorSpace.Linear; }
            // ),
            // new setting("Resolution Dialog: disabled", false,
            //     () => { PlayerSettings.displayResolutionDialog = ResolutionDialogSetting.Disabled; }
            // ),
            new setting("Default is Fullscreen: true", false,
#if UNITY_2018_1_OR_NEWER
                () => { } // empty because depracated
#else
                () => { PlayerSettings.defaultIsFullScreen = true; }
#endif
            ),
            new setting("Fullscreen Mode: Fullscreen Window", false,
#if UNITY_2018_1_OR_NEWER
                () => { PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; }
#else
                () => { PlayerSettings.macFullscreenMode = MacFullscreenMode.FullscreenWindow; }
#endif
            )
        };

            //last changed significantly at 0.60--show it again!
            static string editorPrefName = "HoloPlay Proj Settings 0_60";

            static BuildTarget[] buildPlatforms = new[]
            {
#if UNITY_2017_3_OR_NEWER
            BuildTarget.StandaloneOSX,
#else
            BuildTarget.StandaloneOSXIntel,
            BuildTarget.StandaloneOSXIntel64,
#endif
        };

            static bool settingsApplied; //purely cosmetic

            static OptimizationSettings()
            {
                EditorApplication.update += CheckIfPromptedYet;
                settingsApplied = false;
            }

            //***********/
            //* methods */
            //***********/

            static void CheckIfPromptedYet()
            {
                if (!EditorPrefs.GetBool(editorPrefName + PlayerSettings.productName, false))
                {
                    Init();
                }
                EditorApplication.update -= CheckIfPromptedYet;
            }

            [MenuItem("HoloPlay/Optimization Settings", false, 53)]
            static void Init()
            {
                OptimizationSettings window = EditorWindow.GetWindow<OptimizationSettings>();
                window.Show();
            }

            void OnEnable()
            {
                titleContent = new GUIContent("HoloPlay Settings");

                float spacing = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                Vector2 size = new Vector2(360, 240 + spacing * settings.Count);
                maxSize = size;
                minSize = size;
            }

            void OnGUI()
            {
                EditorGUILayout.HelpBox(
                    "It is recommended you change the following project settings " +
                    "to ensure the best performance for your HoloPlay application",
                    MessageType.Warning
                );


                EditorGUILayout.HelpBox(
                    "WARNING: there is no undo for this function! " +
                    "Make sure you have backed up/committed your project before applying these changes.",
                    MessageType.Error
                );


                EditorGUILayout.LabelField("Select which options to change:", EditorStyles.miniLabel);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var s in settings)
                {
                    EditorGUILayout.BeginHorizontal();
                    s.on = EditorGUILayout.ToggleLeft(s.label, s.on);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                // GUILayout.FlexibleSpace();
                GUI.backgroundColor = EditorGUIUtility.isProSkin ? Color.green : Color.Lerp(Color.green, Color.white, 0.5f);
                if (GUILayout.Button("Apply Changes"))
                {
                    var qs = QualitySettings.names;
                    int currentQuality = QualitySettings.GetQualityLevel();

                    for (int i = 0; i < qs.Length; i++)
                    {
                        QualitySettings.SetQualityLevel(i, false);
                        foreach (var setting in settings)
                        {
                            if (setting.isQualitySetting)
                            {
                                setting.settingChange();
                            }
                        }
                    }

                    foreach (var setting in settings)
                    {
                        if (!setting.isQualitySetting)
                        {
                            setting.settingChange();
                        }
                    }

                    QualitySettings.SetQualityLevel(currentQuality, true);
                    EditorPrefs.SetBool(editorPrefName + PlayerSettings.productName, true);
                    Debug.Log(Misc.debugLogText + "Optimization settings applied!");
                    settingsApplied = true;
                }
                EditorGUILayout.EndHorizontal();

                if (settingsApplied)
                {
                    EditorGUILayout.HelpBox("Applied! By default, this popup will no longer appear, but you can access it by clicking HoloPlay/Optimization Settings Prompt", MessageType.Info);
                }

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = EditorGUIUtility.isProSkin ? Color.yellow : Color.Lerp(Color.yellow, Color.white, 0.5f);
                if (GUILayout.Button("Never display this popup again"))
                {
                    EditorPrefs.SetBool(editorPrefName + PlayerSettings.productName, true);
                    Debug.Log(Misc.debugLogText + "Optimization popup hidden--" +
                        "to show again, open in inspector window on HoloPlay Capture");
                    Close();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}