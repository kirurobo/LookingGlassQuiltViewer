using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloPlay
{
    namespace Extras
    {
        public class DebugPrintout : MonoBehaviour
        {
            public string keyName;
            // ? make one for quilt settings, include an override for quilt settings
            Quilt quilt;
            const float standardHeight = 0.08f;
            float percentHeight = standardHeight;
            float yStartPos = 0.2f;
            Rect position;
            Rect borderRect;
            Texture2D whiteTexture;
            float width { get { return Screen.width * 0.7f; } }
            float height { get { return Screen.height * percentHeight; } }
            float border { get { return 0.1f * height; } }
            float heightPlusBorder { get { return height + border; } }
            int configValueIndex;

            void OnEnable()
            {
                quilt = GetComponent<Quilt>();
            }

            void Update()
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    configValueIndex++;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    configValueIndex--;
                }
            }

            void OnGUI()
            {
                whiteTexture = Texture2D.whiteTexture;
                var previousLabelFontSize = GUI.skin.label.fontSize;
                var previousLabelAlignment = GUI.skin.label.alignment;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                position = new Rect(
                    Screen.width / 2f - width * 0.5f,
                    Screen.height * yStartPos,
                    width,
                    height
                );

                // top border
                SetPercentHeight(0.2f);
                DrawFill();
                AdvancePosition();

                // holoplay version
                SetPercentHeight(1.4f);
                DrawFill();
                GUI.color = Color.HSVToRGB(0.3f, 1.0f, 1.0f);
                GUI.Label(position, "HoloPlay " + Misc.version);
                AdvancePosition();

                // margin
                SetPercentHeight(0.1f);
                DrawFill();
                AdvancePosition();

                // config loaded info
                SetPercentHeight(1);
                DrawFill();
                GUI.color = Color.HSVToRGB(0.4f, 1.0f, 1.0f);
                GUI.Label(position, "Config loaded from:");
                AdvancePosition();

                SetPercentHeight(0.85f);
                DrawFill(2);
                GUI.color = Color.HSVToRGB(0.45f, 0.8f, 1.0f);
                GUI.Label(position, quilt.config.loadedFrom);
                AdvancePosition();

                // margin before config printout
                SetPercentHeight(0.1f);
                DrawFill();
                AdvancePosition();

                // config printout
                SetPercentHeight(1);
                DrawFill();
                GUI.color = Color.HSVToRGB(0.5f, 0.8f, 1.0f);
                var configValues = Config.EnumerateConfigFields(quilt.config);
                while (configValueIndex < 0)
                {
                    configValueIndex += configValues.Length;
                }
                configValueIndex = configValueIndex % configValues.Length;
                var c = configValues[configValueIndex];
                var prevColor = GUI.color;
                GUI.color = Color.gray;
                if (GUI.Button(position, ""))
                {
                    configValueIndex++;
                }
                GUI.color = prevColor;
                string configString = "<   " + c.name + ": " + (c.isInt ? c.asInt : c.Value) + "   >";
                GUI.Label(position, configString);
                AdvancePosition();

                SetPercentHeight(1f);
                DrawFill();
                GUI.color = Color.HSVToRGB(0.0f, 0.0f, 0.8f);
                GUI.Label(position, "Press " + keyName + " to toggle this panel");
                AdvancePosition();

                var buttons = new Dictionary<ButtonType, string>{
                    { ButtonType.ONE, "Square / 1" },
                    { ButtonType.TWO, "Left / 2" },
                    { ButtonType.THREE, "Right / 3" },
                    { ButtonType.FOUR, "Circle / 4" },
                    { ButtonType.HOME, "Home / 5" },
                };

                string buttonTestString = "";
                foreach (var b in buttons)
                {
                    if (Buttons.GetButton(b.Key))
                    {
                        buttonTestString += "  " + b.Value + "  ";
                    }
                }
                SetPercentHeight(.8f);
                DrawFill();
                GUI.color = Color.green;
                GUI.Label(position, buttonTestString);
                AdvancePosition();

                // bottom border
                SetPercentHeight(0.2f);
                DrawFill();
                AdvancePosition();

                GUI.skin.label.fontSize = previousLabelFontSize;
                GUI.skin.label.alignment = previousLabelAlignment;
                GUI.color = Color.white;
            }

            void AdvancePosition()
            {
                position.y += position.height;
            }

            void DrawFill(float heightMod = 1f)
            {
                position.height = height * heightMod;
                GUI.color = Color.black;
                GUI.DrawTexture(position, whiteTexture, ScaleMode.StretchToFill, false);
                GUI.color = Color.white;
            }

            void SetPercentHeight(float heightMod)
            {
                percentHeight = standardHeight * heightMod;
                GUI.skin.label.fontSize = Mathf.FloorToInt(height * 0.7f);
            }
        }
    }
}