//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HoloPlay
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class Quilt : MonoBehaviour
    {
        //**********/
        //* fields */
        //**********/

        /// <summary>
        /// Static ref to the most recently active Quilt.
        /// </summary>
        private static Quilt instance;
        public static Quilt Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindObjectOfType<Quilt>();
                return instance;
            }
        }

        private Camera quiltCam;
        public Camera QuiltCam
        {
            get
            {
                if (quiltCam != null)
                    return quiltCam;

                quiltCam = GetComponent<Camera>();
                return quiltCam;
            }
        }

        /// <summary>
        /// The Captures this quilt will call render from
        /// </summary>
        [Tooltip("The HoloPlay Captures rendering to the QuiltRT. This Quilt calls Render on each of the Captures in this array in order")]
        public Capture[] captures;

        /// <summary>
        /// The material with the lenticular shader. The Quilt sets values for this material based on the calibration
        /// </summary>
        public Material lenticularMat;

        /// <summary>
        /// The actual rendertexture that gets drawn to the screen
        /// </summary>
        [Tooltip("The rendertexture that gets processed through the Lenticular material and spit to the screen")]
        public RenderTexture quiltRT;

        /// <summary>
        /// Useful for loading quilts directly instead of depending on a capture
        /// </summary>
        [Tooltip("Set this texture to load a quilt manually. Make sure to adjust the tiling settings to match.")]
        public Texture overrideQuilt;

        /// <summary>
        /// Used to load indivial views directly instead of depending on a capture
        /// </summary>
        public Texture[] overrideViews;

        [Tooltip("If true, the captures attached to this quilt will render on top of the override texture. " +
            "Make sure the capture camera backgrounds have an alpha value < 1.")]
        public bool renderOverrideBehind;

        private RenderTexture tileRT;

        /// <summary>
        /// Gets called for each view being rendered. Passes first the view number being rendered, then the number of views. 
        /// Gets called once per view render, then a final time after rendering is complete with viewBeingRendered equal to the number of views.
        /// </summary>
        public static Action<int, int> onViewRender;

        [Serializable]
        public struct Tiling
        {
            public string presetName;
            [Range(1, 16)]
            public int tilesX;
            [Range(1, 16)]
            public int tilesY;
            [Range(512, 4096)]
            public int quiltW;
            [Range(512, 4096)]
            public int quiltH;
            public float aspect;
            public bool overscan;
            public int numViews;
            public int tileSizeX;
            public int tileSizeY;
            public int paddingX;
            public int paddingY;
            public float portionX;
            public float portionY;
            public const float DefaultAspect = -1f;

            public Tiling(string presetName, int tilesX, int tilesY, int quiltW, int quiltH, float aspect = DefaultAspect, bool overscan = false) : this()
            {
                this.presetName = presetName;
                this.tilesX = tilesX;
                this.tilesY = tilesY;
                this.quiltW = quiltW;
                this.quiltH = quiltH;
                this.aspect = aspect;
                this.overscan = overscan;
                Setup();
            }

            public void Setup()
            {
                numViews = tilesX * tilesY;
                tileSizeX = (int)quiltW / tilesX;
                tileSizeY = (int)quiltH / tilesY;
                paddingX = (int)quiltW - tilesX * tileSizeX;
                paddingY = (int)quiltH - tilesY * tileSizeY;
                portionX = (float)tilesX * tileSizeX / (float)quiltW;
                portionY = (float)tilesY * tileSizeY / (float)quiltH;
            }
        }

        public Tiling tiling = new Tiling("Default", 4, 8, 2048, 2048);

        public static readonly Tiling[] tilingPresets = new Tiling[]{
            new Tiling(
                "Standard", 4, 8, 2048, 2048
            ),
            new Tiling(
                "High Res", 5, 9, 4096, 4096
            ),
            new Tiling(
                "High View", 6, 10, 4096, 4096
            ),
            new Tiling(
                "Extra Low", 4, 6, 1600, 1600
            )
        };

        [SerializeField]
        private int tilingPresetIndex;
        public int TilingPresetIndex
        {
            get { return tilingPresetIndex; }
            set
            {
                tilingPresetIndex = value;
                ApplyPreset();
            }
        }

        public Config.VisualConfig config;

        // todo: implement
        // public string preferredConfigDrive;

        // public int display;

        [SerializeField]
        private KeyCode debugPrintoutKey = KeyCode.F8;

        [SerializeField]
        private KeyCode screenshot2DKey = KeyCode.F9;

        [SerializeField]
        private KeyCode screenshot3DKey = KeyCode.F10;

        /// <summary>
        /// Happens in OnEnable after config is loaded, screen is setup, material is created, and config is sent to shader
        /// </summary>
        public UnityEvent onQuiltSetup;

#if UNITY_EDITOR
        // for the editor script
        [SerializeField]
        bool advancedFoldout;

        [SerializeField]
        [Tooltip("Render in 2D. If set to true, the application will still render in 3D in play mode and in builds.")]
        bool renderIn2D = false;
#endif

        [SerializeField]
        [Tooltip("On startup, the resolution will automatically be set to the one read by the config. On by default.")]
        bool forceConfigResolution = true;

        //***********/
        //* methods */
        //***********/

        // void Awake()
        // {
        // }

        void OnEnable()
        {
            instance = this;
            LoadConfig();
            SetupScreen();
            ApplyPreset();

            foreach (var capture in captures)
            {
                if (!capture) continue;
                capture.SetupCam(tiling.aspect, config.verticalAngle);
            }

            if (onQuiltSetup.GetPersistentEventCount() > 0)
                onQuiltSetup.Invoke();
        }

        void OnDisable()
        {
            if (quiltRT && quiltRT.IsCreated())
            {
                quiltRT.Release();
                DestroyImmediate(quiltRT);
            }
            DestroyImmediate(lenticularMat);
        }

        void Update()
        {
            if (Input.GetKeyDown(debugPrintoutKey))
            {
                var currentDebugPrintouts = GetComponents<Extras.DebugPrintout>();
                if (currentDebugPrintouts.Length > 0)
                {
                    foreach (var c in currentDebugPrintouts)
                    {
                        Destroy(c);
                    }
                }
                else
                {
                    var printout = gameObject.AddComponent<Extras.DebugPrintout>();
                    printout.keyName = debugPrintoutKey.ToString();
                }
            }

#if CALIBRATOR || UNITY_EDITOR //! temporary fix
            // if the calibrator is running, ALWAYS be passing config to the material
            PassConfigToMaterial();
#endif

            if (Input.GetKeyDown(screenshot2DKey))
                Screenshot2D();

            if (Input.GetKeyDown(screenshot3DKey))
                StartCoroutine(Screenshot3D());
        }

        void OnValidate()
        {
            ApplyPreset();

#if UNITY_EDITOR
            quiltCam.enabled = !renderIn2D;
            foreach (var capture in captures)
            {
                if (capture != null && capture.Cam != null)
                {
                    capture.Cam.enabled = renderIn2D;
                }
            }
#endif
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            // clear rt
            Graphics.SetRenderTarget(quiltRT);
            GL.Clear(false, true, Color.black);
            if (overrideQuilt)
            {
                GL.PushMatrix();
                GL.LoadOrtho();
                Graphics.DrawTexture(new Rect(0, 1, 1, -1), overrideQuilt);
                GL.PopMatrix();

                if (!renderOverrideBehind)
                {
                    Graphics.Blit(quiltRT, dest, lenticularMat);
                    return;
                }
            }

            // render views
            for (int i = 0; i < tiling.numViews; i++)
            {
                // broadcast the onViewRender action
                if (onViewRender != null && Application.isPlaying)
                    onViewRender(i, tiling.numViews);

                int j = 0;
                foreach (var capture in captures)
                {
                    if (!capture || !capture.isActiveAndEnabled)
                        continue;

                    capture.SetupCam(tiling.aspect, config.verticalAngle, false);
                    tileRT = RenderTexture.GetTemporary(tiling.tileSizeX, tiling.tileSizeY, 24);
                    capture.Cam.targetTexture = tileRT;
                    var bgColor = capture.Cam.backgroundColor;
                    if (j != 0)
                    {
                        capture.Cam.backgroundColor = bgColor * new Color(1, 1, 1, 0);
                    }
                    capture.RenderView(i, tiling.numViews, config.viewCone, config.verticalAngle);
                    CopyToQuiltRT(i, tileRT);
                    capture.Cam.targetTexture = null;
                    RenderTexture.ReleaseTemporary(tileRT);
                    capture.Cam.backgroundColor = bgColor;
                    j++;
                }
            }

            // reset cameras so they are back to center
            foreach (var capture in captures)
            {
                if (!capture)
                    continue;

                capture.HandleOffset(tiling.aspect, config.verticalAngle);
            }

            Graphics.Blit(quiltRT, dest, lenticularMat);
        }

        // todo: let the user load config 2 or 3 for second displays
        public void LoadConfig()
        {
            Config.VisualConfig loadedConfig = new Config.VisualConfig();
            if (!Config.LoadVisualFromFile(out loadedConfig, Config.visualFileName))
            {
                // todo: print an on-screen warning about the config not being available
            }

            config = loadedConfig;
        }

        public void SetupQuilt()
        {
            quiltCam = GetComponent<Camera>();
            if (QuiltCam == null)
            {
                gameObject.AddComponent<Camera>();
                quiltCam = GetComponent<Camera>();
            }

            QuiltCam.enabled = true;
            QuiltCam.useOcclusionCulling = false;
            QuiltCam.cullingMask = 0;
            QuiltCam.clearFlags = CameraClearFlags.Nothing;
            QuiltCam.orthographic = true;
            QuiltCam.orthographicSize = 0.01f;
            QuiltCam.nearClipPlane = -0.01f;
            QuiltCam.farClipPlane = 0.01f;
            QuiltCam.stereoTargetEye = StereoTargetEyeMask.None;

            var shader = Shader.Find("HoloPlay/Lenticular");
            lenticularMat = new Material(shader);

            if (config != null)
            {
                PassConfigToMaterial();
            }

            if (quiltRT != null)
                quiltRT.Release();

            quiltRT = new RenderTexture((int)tiling.quiltW, (int)tiling.quiltH, 0)
            {
                filterMode = FilterMode.Point,
                autoGenerateMips = false,
                useMipMap = false
            };
            quiltRT.Create();
        }

        public void CopyToQuiltRT(int view, Texture rt)
        {
            // copy to fullsize rt
            int ri = tiling.numViews - view - 1;
            int x = (view % tiling.tilesX) * tiling.tileSizeX;
            int y = (ri / tiling.tilesX) * tiling.tileSizeY;
            // the padding is necessary because the shader takes y from the opposite spot as this does
            Rect rtRect = new Rect(x, y + tiling.paddingY, tiling.tileSizeX, tiling.tileSizeY);

            if (quiltRT.IsCreated())
            {
                Graphics.SetRenderTarget(quiltRT);
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, (int)tiling.quiltW, (int)tiling.quiltH, 0);
                Graphics.DrawTexture(rtRect, rt);
                GL.PopMatrix();
            }
            else
            {
                Debug.Log(Misc.debugLogText + "quilt not created yet");
            }
        }

        //* sending variables to the shader */
        public void PassConfigToMaterial()
        {
            float screenInches = (float)config.screenW / config.DPI;
            float newPitch = config.pitch * screenInches;
            newPitch *= Mathf.Cos(Mathf.Atan(1f / config.slope));
            lenticularMat.SetFloat("pitch", newPitch);

            float newTilt = config.screenH / (config.screenW * config.slope);
            newTilt *= config.flipImageX.asBool ? -1 : 1;
            lenticularMat.SetFloat("tilt", newTilt);

            float newCenter = config.center;
            newCenter += config.flipImageX.asBool ? 0.5f : 0;
            lenticularMat.SetFloat("center", newCenter);
            lenticularMat.SetFloat("invView", config.invView);
            lenticularMat.SetFloat("flipX", config.flipImageX);
            lenticularMat.SetFloat("flipY", config.flipImageY);

            float subp = 1f / (config.screenW * 3f);
            subp *= config.flipImageX.asBool ? -1 : 1;
            lenticularMat.SetFloat("subp", subp);

            lenticularMat.SetInt("ri", !config.flipSubp.asBool ? 0 : 2);
            lenticularMat.SetInt("bi", !config.flipSubp.asBool ? 2 : 0);

            lenticularMat.SetVector("tile", new Vector4(
                tiling.tilesX,
                tiling.tilesY,
                tiling.portionX,
                tiling.portionY
            ));

            lenticularMat.SetVector("aspect", new Vector4(
                config.screenW / config.screenH,
                tiling.aspect == Tiling.DefaultAspect ? config.screenW / config.screenH : tiling.aspect,
                tiling.overscan ? 1 : 0
            ));
        }

        public void ApplyPreset()
        {
            if (tilingPresetIndex < tilingPresets.Length)
            {
                tiling = tilingPresets[tilingPresetIndex];
            }
            else if (tilingPresetIndex == tilingPresets.Length)
            {
                // if it's default (dynamic with player settings)
                if (QualitySettings.lodBias < 0.5f)
                {
                    tiling = tilingPresets[3]; // extra low
                }
                else if (QualitySettings.lodBias < 1)
                {
                    tiling = tilingPresets[0]; // standard
                }
                else
                {
                    tiling = tilingPresets[1]; // hq
                }
            }

            tiling.Setup();

            SetupQuilt();
        }

        void SetupScreen()
        {
            if (!forceConfigResolution)
                return;

#if UNITY_EDITOR
            if (UnityEditor.PlayerSettings.defaultScreenWidth != config.screenW.asInt ||
                UnityEditor.PlayerSettings.defaultScreenHeight != config.screenH.asInt)
            {
                UnityEditor.PlayerSettings.defaultScreenWidth = config.screenW.asInt;
                UnityEditor.PlayerSettings.defaultScreenHeight = config.screenH.asInt;
            }
#endif

            // if the config is already set, return out
            if (Screen.width == config.screenW.asInt &&
                Screen.height == config.screenH.asInt)
            {
                return;
            }

            Screen.SetResolution(config.screenW.asInt, config.screenH.asInt, true);
        }

        public static string SerializeTilingSettings(Tiling tiling)
        {
            return
                "tx" + tiling.tilesX.ToString("00") +
                "ty" + tiling.tilesY.ToString("00") +
                "qw" + tiling.quiltW.ToString("0000") +
                "qh" + tiling.quiltH.ToString("0000");
        }

        public static Tiling DeserializeTilingSettings(string str)
        {
            int xi = str.IndexOf("tx");
            int yi = str.IndexOf("ty");
            int wi = str.IndexOf("qw");
            int hi = str.IndexOf("qh");

            if (xi < 0 || yi < 0 || wi < 0 || hi < 0)
            {
                Debug.Log(Misc.debugLogText + "Couldn't deserialize tiling settings -- using default");
                return tilingPresets[0];
            }
            else
            {
                string xs = str.Substring(xi + 2, 2);
                string ys = str.Substring(yi + 2, 2);
                string ws = str.Substring(wi + 2, 4);
                string hs = str.Substring(hi + 2, 4);

                Tiling tiling = new Tiling(
                    "deserialized",
                    int.Parse(xs),
                    int.Parse(ys),
                    int.Parse(ws),
                    int.Parse(hs)
                );

                return tiling;
            }
        }

        void Screenshot2D()
        {
            Texture2D screenTex = new Texture2D(Config.Instance.screenW.asInt, Config.Instance.screenH.asInt, TextureFormat.RGB24, false);
            RenderTexture screenRT = RenderTexture.GetTemporary(screenTex.width, screenTex.height, 24);
            // var previousRT = Capture.Instance.cam.targetTexture;
            Capture.Instance.Cam.targetTexture = screenRT;
            Capture.Instance.Cam.ResetWorldToCameraMatrix();
            Capture.Instance.Cam.ResetProjectionMatrix();
            Capture.Instance.Cam.Render();
            // Capture.Instance.cam.targetTexture = previousRT;

            RenderTexture.active = screenRT;
            screenTex.ReadPixels(new Rect(0, 0, quiltRT.width, quiltRT.height), 0, 0);
            RenderTexture.active = null;
            var bytes = screenTex.EncodeToPNG();
            string fullPath;
            string fullName;
            if (!Misc.GetNextFilename(Path.GetFullPath("."), Application.productName, ".png", out fullName, out fullPath))
            {
                Debug.LogWarning(Misc.debugLogText + "Couldn't save screenshot");
            }
            else
            {
                // fullFileName += DateTime.Now.ToString(" yyyy MMdd HHmmss");
                // fullFileName = fullFileName.Replace(" ", "_") + ".png";
                File.WriteAllBytes(fullPath, bytes);
                Debug.Log(Misc.debugLogText + "Wrote screenshot to " + fullName);
            }

            RenderTexture.ReleaseTemporary(screenRT);
            // Destroy(screenTex);
        }

        IEnumerator Screenshot3D()
        {
            var previousTiling = tiling;
            tiling = tilingPresets[0];
            SetupQuilt();

            yield return null;

            Texture2D quiltTex = new Texture2D(quiltRT.width, quiltRT.height, TextureFormat.RGB24, false);
            RenderTexture.active = quiltRT;
            quiltTex.ReadPixels(new Rect(0, 0, quiltRT.width, quiltRT.height), 0, 0);
            RenderTexture.active = null;
            var bytes = quiltTex.EncodeToPNG();
            string fullPath;
            string fullName;
            if (!Misc.GetNextFilename(Path.GetFullPath("."), Application.productName + "_" + SerializeTilingSettings(tiling), ".png", out fullName, out fullPath))
            {
                Debug.LogWarning(Misc.debugLogText + "Couldn't save screenshot");
            }
            else
            {
                // fullFileName += DateTime.Now.ToString(" yyyy MMdd HHmmss");
                // fullFileName = fullFileName.Replace(" ", "_") + ".png";
                File.WriteAllBytes(fullPath, bytes);
                Debug.Log(Misc.debugLogText + "Wrote screenshot to " + fullName);
            }

            tiling = previousTiling;
            SetupQuilt();
        }

#if UNITY_EDITOR
        // public class FileModificationWarning : UnityEditor.AssetModificationProcessor
        // {
        //     static string[] OnWillSaveAssets(string[] paths)
        //     {
        //         Quilt.Instance.LoadConfig();

        //         return paths;
        //     }
        // }

        void OnDrawGizmos()
        {
            if (captures == null)
                return;

            int i = 0;
            foreach (var capture in captures)
            {
                if (!capture) continue;
                capture.DrawCaptureGizmos(i++);
                i = i % Misc.gizmoColor.Length;
            }
        }
#endif
    }
}