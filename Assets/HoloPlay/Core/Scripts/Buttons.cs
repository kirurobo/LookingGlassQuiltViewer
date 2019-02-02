//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloPlay
{
    public enum ButtonType
    {
        SQUARE, ONE = 0,
        LEFT, TWO = 1,
        RIGHT, THREE = 2,
        CIRCLE, FOUR = 3,
        HOME = 4,
    }

    public class Buttons : MonoBehaviour
    {
        [Tooltip("Emulate the HoloPlayer Buttons using 1/2/3/4/5 on alphanumeric keyboard.")]
        public bool emulateWithKeyboard = true;

        int joystickNumber = -2;

        // balance checkInterval so it starts right away
        float timeSinceLastCheck = -3f;

        readonly float checkInterval = 3f;

        private static Buttons instance;
        public static Buttons Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindObjectOfType<Buttons>();
                if (instance != null)
                    return instance;

                instance = new GameObject("HoloPlay Buttons").AddComponent<Buttons>();
                return instance;
            }
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }

            DontDestroyOnLoad(this);
        }

        /// <summary>
        /// This happens automatically every x seconds as called from HoloPlay.
        /// No need for manually calling this function typically
        /// </summary>
        public void ScanForHoloPlayerJoystick()
        {
            if (Time.unscaledTime - timeSinceLastCheck > checkInterval)
            {
                var joyNames = Input.GetJoystickNames();
                int i = 1;
                foreach (var joyName in joyNames)
                {
                    if (joyName.ToLower().Contains("holoplay"))
                    {
                        Debug.Log(Misc.debugLogText + "Found HID named: " + joyName);
                        joystickNumber = i; // for whatever reason unity starts their joystick list at 1 and not 0
                        return;
                    }
                    i++;
                }

                if (joystickNumber == -2)
                {
                    Debug.LogWarning(Misc.debugLogText + "No HoloPlay HID found");
                    joystickNumber = -1;
                }

                timeSinceLastCheck = Time.unscaledTime;
            }
        }

        public static bool GetButton(ButtonType button)
        {
            return CheckButton((x) => UnityEngine.Input.GetKey(x), button);
        }

        public static bool GetButtonDown(ButtonType button)
        {
            return CheckButton((x) => UnityEngine.Input.GetKeyDown(x), button);
        }

        public static bool GetButtonUp(ButtonType button)
        {
            return CheckButton((x) => UnityEngine.Input.GetKeyUp(x), button);
        }

        /// <summary>
        /// Get any button down. By default, includeHome is false and it will only return on buttons 1-4
        /// </summary>
        public static bool GetAnyButtonDown(bool includeHome = false)
        {
            for (int i = 0; i < Enum.GetNames(typeof(ButtonType)).Length; i++)
            {
                var button = (ButtonType)i;
                if (includeHome && button == ButtonType.HOME)
                    continue;

                if (GetButtonDown(button)) return true;
            }
            return false;
        }

        static bool CheckButton(Func<KeyCode, bool> buttonFunc, ButtonType button)
        {
            bool buttonPress = false;
            // check keyboard if emulated
            if (Instance.emulateWithKeyboard)
                buttonPress = buttonFunc(ButtonToNumberOnKeyboard(button));

            if (Instance.joystickNumber < 0)
            {
                Instance.ScanForHoloPlayerJoystick();
            }

            if (Instance.joystickNumber >= 0)
            {
                buttonPress = buttonPress || buttonFunc(ButtonToJoystickKeycode(button));
            }
            return buttonPress;
        }

        static KeyCode ButtonToJoystickKeycode(ButtonType button)
        {
            return
                (KeyCode)Enum.Parse(
                    typeof(KeyCode),
                    "Joystick" + Instance.joystickNumber + "Button" + (int)button
                );
        }

        static KeyCode ButtonToNumberOnKeyboard(ButtonType button)
        {
            switch (button)
            {
                case ButtonType.ONE:
                    return KeyCode.Alpha1;
                case ButtonType.TWO:
                    return KeyCode.Alpha2;
                case ButtonType.THREE:
                    return KeyCode.Alpha3;
                case ButtonType.FOUR:
                    return KeyCode.Alpha4;
                case ButtonType.HOME:
                    return KeyCode.Alpha5;
                default:
                    return KeyCode.Alpha5;
            }
        }
    }
}