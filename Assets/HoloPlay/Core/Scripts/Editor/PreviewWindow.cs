//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace HoloPlay
{
    namespace UI
    {
        public static class PreviewWindow
        {
            static object gameViewSizesInstance;
            static MethodInfo getGroup;
            static int updateCount = 0;
            static bool windowOpen;
            public static Vector2 position;

            [MenuItem("HoloPlay/Toggle Preview %e", false, 1)]
            public static void ToggleWindow()
            {
                Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                bool isMac = Application.platform == RuntimePlatform.OSXEditor;
                EditorWindow gameView = EditorWindow.GetWindow(gameViewType, !isMac, "Game");
                if (windowOpen)
                {
                    gameView.Close();
                    windowOpen = false;
                    return;
                }

                // ? this is gonna cause lots of problems if there is no Config
                LoadSettings();

                // necessary to close and reopen
                gameView.Close();
                gameView = EditorWindow.GetWindow(gameViewType, !isMac, "Game");
                int tabSize = 22 - 5; //this makes sense i promise
                                      // ? make it so toggling the preview from a 2nd display lentil will show THAT one
                gameView.maxSize = new Vector2(Config.Instance.screenW, Config.Instance.screenH + tabSize);
                gameView.minSize = gameView.maxSize;
                gameView.position = new Rect(position.x, position.y - tabSize, gameView.maxSize.x, gameView.maxSize.y);
                gameView.ShowPopup();
                PropertyInfo selectedSizeIndexProp = gameViewType.GetProperty
                (
                    "selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                // Debug.Log(selectedSizeIndexProp.GetValue(gameView, null));
                Type sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instanceProp = singleType.GetProperty("instance");
                getGroup = sizesType.GetMethod("GetGroup");
                gameViewSizesInstance = instanceProp.GetValue(null, null);
                var group = GetGroup(GetCurrentGroupType());

                var getDisplayTexts = group.GetType().GetMethod("GetDisplayTexts");
                var displayTexts = (string[])getDisplayTexts.Invoke(group, null);
                int index = 0;
                for (int i = 0; i < displayTexts.Length; i++)
                {
                    if (displayTexts[i].Contains("Standalone"))
                    {
                        index = i;
                        break;
                    }
                }
                if (index == 0)
                {
                    Debug.LogWarning(Misc.debugLogText + "couldn't find standalone resolution in preview window");
                }

                selectedSizeIndexProp.SetValue(gameView, index, null);
                updateCount = 0;
                windowOpen = true;
                EditorApplication.update += DelayedStuff;
            }

            public static void SaveSettings()
            {
                EditorPrefs.SetFloat("HoloPlay-preview-pos-x", position.x);
                EditorPrefs.SetFloat("HoloPlay-preview-pos-y", position.y);
            }

            public static void LoadSettings()
            {
                float x = -2560;
                float y = 0;
                if (Config.Instance != null)
                {
                    x = -Config.Instance.screenW;
                    y = Screen.currentResolution.height - Config.Instance.screenH;
                }
                position = new Vector2
                (
                    EditorPrefs.GetFloat("HoloPlay-preview-pos-x", x),
                    EditorPrefs.GetFloat("HoloPlay-preview-pos-y", y)
                );
            }

            public static GameViewSizeGroupType GetCurrentGroupType()
            {
                var getCurrentGroupTypeProp = gameViewSizesInstance.GetType().GetProperty("currentGroupType");
                return (GameViewSizeGroupType)(int)getCurrentGroupTypeProp.GetValue(gameViewSizesInstance, null);
            }

            static object GetGroup(GameViewSizeGroupType type)
            {
                return getGroup.Invoke(gameViewSizesInstance, new object[] { (int)type });
            }

            static void DelayedStuff()
            {
                SetSize();
                updateCount++;
                if (updateCount > 10)
                    EditorApplication.update -= DelayedStuff;
            }

            public static void SetSize()
            {
                float targetScale = 1;
                var gvWndType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                var gvWnd = EditorWindow.GetWindow(gvWndType);
                var areaField = gvWndType.GetField("m_ZoomArea", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var areaObj = areaField.GetValue(gvWnd);
                var scaleField = areaObj.GetType().GetField("m_Scale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                scaleField.SetValue(areaObj, new Vector2(targetScale, targetScale));
            }

            public static EditorWindow GetMainGameView(bool dontCreate = false)
            {
                if (!dontCreate)
                    EditorApplication.ExecuteMenuItem("Window/Game");

                System.Type T = System.Type.GetType("UnityEditor.GameView,UnityEditor");
                System.Reflection.MethodInfo GetMainGameView = T.GetMethod(
                    "GetMainGameView",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                );
                System.Object Res = GetMainGameView.Invoke(null, null);

                return (EditorWindow)Res;
            }
        }

        public class PreviewWindowEditor : EditorWindow
        {
            static EditorWindow window;
            const float pad = 6;
            Texture2D displayFull;

            [MenuItem("HoloPlay/Preview Settings %#e", false, 2)]
            static void Init()
            {
                PreviewWindow.LoadSettings();
                window = EditorWindow.GetWindow(typeof(PreviewWindowEditor));
                window.maxSize = new Vector2(320, 380);
                window.minSize = window.maxSize;

                if (window != null) window.Show();
            }

            void OnEnable()
            {
                displayFull = Resources.Load<Texture2D>("display_full");
                displayFull.filterMode = FilterMode.Point;
            }

            void OnDisable()
            {
                Resources.UnloadAsset(displayFull);
            }

            void OnGUI()
            {
                PreviewWindow.position = EditorGUILayout.Vector2Field("Position", PreviewWindow.position);
                EditorGUILayout.Space();

                GUILayout.Label("Presets: ");
                GUILayout.BeginHorizontal();
                bool left = GUILayout.Button("Left", EditorStyles.miniButton);
                bool right = GUILayout.Button("Right", EditorStyles.miniButton);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                bool lowerLeft = GUILayout.Button("Lower Left", EditorStyles.miniButton);
                bool lowerRight = GUILayout.Button("Lower Right", EditorStyles.miniButton);
                GUILayout.EndHorizontal();

                if (left)
                {
                    PreviewWindow.position = new Vector2(-Config.Instance.screenW, 0);
                    EditorGUI.FocusTextInControl("");
                    PreviewWindow.SaveSettings();
                }

                if (right)
                {
                    PreviewWindow.position = new Vector2(Screen.currentResolution.width, 0);
                    EditorGUI.FocusTextInControl("");
                    PreviewWindow.SaveSettings();
                }

                if (lowerLeft)
                {
                    PreviewWindow.position = new Vector2(-Config.Instance.screenW, Screen.currentResolution.height - Config.Instance.screenH);
                    EditorGUI.FocusTextInControl("");
                    PreviewWindow.SaveSettings();
                }

                if (lowerRight)
                {
                    PreviewWindow.position = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height - Config.Instance.screenH);
                    EditorGUI.FocusTextInControl("");
                    PreviewWindow.SaveSettings();
                }

                // GUILayout.FlexibleSpace();

                EditorGUILayout.Space();

                if (GUILayout.Button("Toggle Preview"))
                {
                    EditorApplication.ExecuteMenuItem("HoloPlay/Toggle Preview");
                    PreviewWindow.SaveSettings();
                }

                EditorGUILayout.HelpBox("Toggle the previewer to affect changes", MessageType.Info);

                EditorGUILayout.HelpBox
                (
                    "Note: keeping your HoloPlay Preview Window to the left is recommended. " +
                    "If you are using it to the right of your main display, you may need to " +
                    "adjust the x position manually, as OS zoom can sometimes cause the positioning to fail.",
                    MessageType.Warning
                );

                // experimental
                if (Config.Instance != null)
                {
                    EditorGUILayout.LabelField("Positioning:");
                    Rect position = EditorGUILayout.BeginVertical();
                    position.y += 30; // a little padding
                    float factor = 0.033f; // how much smaller this prop screen is

                    Rect mainDisplay = position;
                    mainDisplay.width = Screen.currentResolution.width * factor;
                    mainDisplay.height = Screen.currentResolution.height * factor;
                    mainDisplay.x += position.width * 0.5f - mainDisplay.width * 0.5f;
                    // mainDisplay.x = Mathf.FloorToInt(mainDisplay.x);
                    // mainDisplay.y = Mathf.FloorToInt(mainDisplay.y);

                    Rect lkgDisplay = position;
                    lkgDisplay.width = Config.Instance.screenW * factor;
                    lkgDisplay.height = Config.Instance.screenH * factor;
                    lkgDisplay.x = mainDisplay.x + PreviewWindow.position.x * factor;
                    lkgDisplay.y = mainDisplay.y + PreviewWindow.position.y * factor;

                    GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.black;
                    GUI.DrawTexture(mainDisplay, displayFull);
                    mainDisplay.x += 4;
                    mainDisplay.y += 2;
                    GUI.Label(mainDisplay, "main\ndisplay", EditorStyles.whiteMiniLabel);

                    GUI.color = Misc.guiColor;
                    GUI.DrawTexture(lkgDisplay, displayFull);
                    lkgDisplay.x += 4;
                    lkgDisplay.y += 2;
                    GUI.Label(lkgDisplay, "HoloPlay\ndisplay", EditorStyles.whiteMiniLabel);

                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
}