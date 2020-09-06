using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using SharpDX.DirectInput;
#else
using UnityEngine.InputSystem;
#endif

namespace Kirurobo
{
    public class HoloPlayButtonListener
    {
        /// <summary>
        /// Looking Glass が備えるボタン
        /// </summary>
        public enum HoloPlayButton
        {
            Square = 0,
            Left = 1,
            Right = 2,
            Circle = 3,
        }

        /// <summary>
        /// 現在のボタン押下状態
        /// </summary>
        private Dictionary<HoloPlayButton, bool> currentState = new Dictionary<HoloPlayButton, bool>();

        /// <summary>
        /// 前フレームのボタン押下状態
        /// </summary>
        private Dictionary<HoloPlayButton, bool> lastState = new Dictionary<HoloPlayButton, bool>();

        public delegate void KeyEventHandler(HoloPlayButton button);
        public event KeyEventHandler OnkeyDown;
        public event KeyEventHandler OnkeyUp;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        /// <summary>
        /// 発見されたデバイスが保存される
        /// </summary>
        private List<SharpDX.DirectInput.Joystick> holoplayDevices = new List<SharpDX.DirectInput.Joystick>();


        /// <summary>
        /// 指定ボタンが押されているか判定
        /// </summary>
        /// <param name="state"></param>
        /// <param name="button"></param>
        /// <returns></returns>
        private bool IsPressed(JoystickState state, HoloPlayButton button)
        {
            return (state.Buttons[(int)button]);
        }

        /// <summary>
        /// 指定ボタンが押されているか判定
        /// </summary>
        /// <param name="state"></param>
        /// <param name="button"></param>
        /// <returns></returns>
        private bool IsPressed(JoystickState state, HoloPlayButton button)
        {
            return (state.Buttons[(int)button]);
        }

#else
        private int holoplayDevicesCount = 0;
           
#endif

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public HoloPlayButtonListener()
        {
            // 最初にデバイスを取得
            RefreshDevices();
        }

        /// <summary>
        /// 取得されているデバイス数を返す
        /// </summary>
        /// <returns></returns>
        public int GetDeviceCount()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return holoplayDevices.Count;
#else
            return holoplayDevicesCount;
#endif
        }

        /// <summary>
        /// 有効なデバイスを全て取得
        /// </summary>
        public void RefreshDevices()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // 参考 http://csdegame.net/sharpdx/dx_input_pad.html

            DirectInput dinput = new DirectInput();

            // Looking Glass は Supplemental になっているようなのでそこを探す
            foreach (DeviceInstance device in dinput.GetDevices(DeviceType.Supplemental, DeviceEnumerationFlags.AllDevices))
            {
                if (device.ProductName.Contains("HoloPlay"))
                {
                    Joystick joystick = new Joystick(dinput, device.ProductGuid);
                    if (joystick != null)
                    {
                        holoplayDevices.Add(joystick);
                    }
                }
            }
#else
            var joystickNames = Input.GetJoystickNames();
            for (int i = 0; i < joystickNames.Length; i++)
            {
                if (joystickNames[i].ToLower().Contains("holoplay"))
                {
                    holoplayDevicesCount++;
                }
            }
#endif

            // ボタン状態を初期化
            foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
            {
                currentState[button] = false;
                lastState[button] = false;
            }
        }

        /// <summary>
        /// このメソッドを毎フレーム呼んでください
        /// </summary>
        public void Update()
        {
            UpdateButtonState();
            ProcessEvent();
        }

        /// <summary>
        /// 現在のボタン押下状態を調べる
        /// </summary>
        private void UpdateButtonState()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
            {
                currentState[button] = false;
            }
            foreach (var device in holoplayDevices)
            {
                // キャプチャ開始
                device.Acquire();
                device.Poll();

                // データ取得
                var state = device.GetCurrentState();

                // 取得できなければ終了
                if (state == null)
                {
                    break;
                }

                foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
                {
                    // 複数デバイスがあれば、いずれかが押されたら押下と判断
                    if (IsPressed(state, button)) currentState[button] = true;
                }
            }
#endif
        }

        /// <summary>
        /// 前回の状態と現在の状態を比較してイベント処理
        /// </summary>
        private void ProcessEvent()
        {
            // 各キーのイベントを処理
            foreach (HoloPlayButton button in Enum.GetValues(typeof(HoloPlayButton)))
            {
                if (!lastState[button] && currentState[button])
                {
                    // Key down
                    OnkeyDown?.Invoke(button);

                }
                else if (lastState[button] && !currentState[button])
                {
                    // Key up
                    OnkeyUp?.Invoke(button);
                }
                lastState[button] = currentState[button];
            }
        }

        /// <summary>
        /// 現在ボタンが押されているか否かを返す
        /// </summary>
        /// <param name="button"></param>
        /// <returns></returns>
        public bool GetKey(HoloPlayButton button)
        {
            return currentState[button];
        }
    }
}
