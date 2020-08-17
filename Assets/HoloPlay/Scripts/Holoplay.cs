//Copyright 2017-2019 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

namespace LookingGlass {
    [ExecuteInEditMode]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Camera))]
    [HelpURL("https://docs.lookingglassfactory.com/Unity/Scripts/Holoplay/")]
	public class Holoplay : MonoBehaviour {

		// variables
		// singleton
		private static Holoplay instance;
		public static Holoplay  Instance { 
			get{ 
				if (instance != null) return instance; 
				instance = FindObjectOfType<Holoplay>();
				// Debug.Log("assign first instance when getting");
				return instance;	
			} 
		}

		// info
		public static readonly Version version = new Version(1,3,1);
		public const string versionLabel = "";

		// camera
		public CameraClearFlags clearFlags = CameraClearFlags.Color;
		public Color background = Color.black;
		public LayerMask cullingMask = -1;
		public float size = 5f;
		public float depth;
		public RenderingPath renderingPath = RenderingPath.UsePlayerSettings;
		public bool occlusionCulling = true;
		public bool allowHDR = true;
		public bool allowMSAA = true;
#if UNITY_2017_3_OR_NEWER
		public bool allowDynamicResolution = false;
#endif
		public int targetDisplay;
		[System.NonSerialized] public int targetLKG;
		public bool preview2D = false;
		[Range(5f, 90f)] public float fov = 14f;
		[Range(0.01f, 5f)] public float nearClipFactor = 1.5f;
		[Range(0.01f, 40f)] public float farClipFactor = 4f;
		public bool scaleFollowsSize;
		[Range(0f, 1f)] public float viewconeModifier = 1f;
		[Range(0f, 1f)] public float centerOffset;
		[Range(-90f, 90f)] public float horizontalFrustumOffset;
		[Range(-90f, 90f)] public float verticalFrustumOffset;
		public bool useFrustumTarget;
		public Transform frustumTarget;

		// quilt
		[SerializeField] Quilt.Preset quiltPreset = Quilt.Preset.Automatic;
		public Quilt.Preset GetQuiltPreset() { return quiltPreset; }
		public void SetQuiltPreset(Quilt.Preset preset) {
			quiltPreset = preset;
			SetupQuilt();
		}
		public Quilt.Settings quiltSettings {
			get { 
				if (quiltPreset == Quilt.Preset.Custom)
					return customQuiltSettings;	
				return Quilt.GetPreset(quiltPreset, cal); 
			} 
		}
		public Quilt.Settings customQuiltSettings = new Quilt.Settings(4096, 4096, 5, 9, 45); // hi res
		public KeyCode screenshot2DKey = KeyCode.F9;
        public KeyCode screenshotQuiltKey = KeyCode.F10;
		public Texture overrideQuilt;
		public bool renderOverrideBehind;
		public RenderTexture quiltRT;
		[System.NonSerialized] public Material lightfieldMat;

		// gizmo
		public Color frustumColor = new Color32(0, 255, 0, 255);
		public Color middlePlaneColor = new Color32(150, 50, 255, 255);
		public Color handleColor = new Color32(75, 100, 255, 255);
		public bool drawHandles = true;
		float[] cornerDists = new float[3];
		Vector3[] frustumCorners = new Vector3[12];

		// events
		[Tooltip("If you have any functions that rely on the calibration having been loaded " +
			"and the screen size having been set, let them trigger here")]
		public LoadEvent onHoloplayReady;
		[System.NonSerialized] public LoadResults loadResults;
		[Tooltip("Will fire before each individual view is rendered. " +
			"Passes [0, numViews), then fires once more passing numViews (in case cleanup is needed)")]
		public ViewRenderEvent onViewRender;
		[System.Serializable]
		public class ViewRenderEvent : UnityEvent<Holoplay, int> {};

		// optimization
		public enum ViewInterpolationType {
			None,
			EveryOther,
			Every4th,
			Every8th,
			_4Views,
			_2Views
		}
		public ViewInterpolationType viewInterpolation = ViewInterpolationType.None;
		public int ViewInterpolation { get {
			switch (viewInterpolation) {
				case ViewInterpolationType.None:
				default:
					return 1;				
				case ViewInterpolationType.EveryOther:
					return 2;				
				case ViewInterpolationType.Every4th:
					return 4;				
				case ViewInterpolationType.Every8th:
					return 8;				
				case ViewInterpolationType._4Views:
					return quiltSettings.numViews / 3;				
				case ViewInterpolationType._2Views:
					return quiltSettings.numViews;				
			}
		} }
		// [Range(1, 4)] public int computeCycles = 1;
		public bool reduceFlicker;
		public bool fillGaps;
		public bool blendViews;
		ComputeShader interpolationComputeShader = null;

		// not in inspector
		// camera w/o post-process effects at all, or just our regular cam
		[System.NonSerialized] public Camera cam;
		[System.NonSerialized] public Camera postProcessCam;
		[System.NonSerialized] public Camera lightfieldCam;
		const string postProcessCamName = "postProcessCam";
		const string lightfieldCamName = "lightfieldCam";
		[System.NonSerialized] public Calibration cal;
		bool frameRendered;
		[System.NonSerialized] public float camDist;
		bool debugInfo;

		// functions
		void OnEnable() {
#if UNITY_POST_PROCESSING_STACK_V2
            PostProcessLayer postLayer = GetComponent<PostProcessLayer>();
			if (postLayer != null && postLayer.enabled) {
				postProcessCam = GetComponent<Camera>();
				postProcessCam.hideFlags = HideFlags.HideInInspector;
				var camGO = new GameObject(postProcessCamName);
				camGO.hideFlags = HideFlags.HideAndDontSave;
				camGO.transform.SetParent(transform);
				camGO.transform.localPosition = Vector3.zero;
				camGO.transform.localRotation = Quaternion.identity;
				cam = camGO.AddComponent<Camera>();
				cam.CopyFrom(postProcessCam);
				Debug.Log("set up cam");
			} else
#endif 
			{
				cam = GetComponent<Camera>();
				//Debug.Log("set up cam"+cam.projectionMatrix);
				cam.hideFlags = HideFlags.HideInInspector;
			}
			lightfieldMat = new Material(Shader.Find("Holoplay/Lightfield"));
			instance = this; // most recently enabled Capture set as instance
			// lightfield camera (only does blitting of the quilt into a lightfield)
			var lightfieldCamGO = new GameObject(lightfieldCamName);
			lightfieldCamGO.hideFlags = HideFlags.HideAndDontSave;
			lightfieldCamGO.transform.SetParent(transform);
			var lightfieldPost = lightfieldCamGO.AddComponent<LightfieldPostProcess>();
			lightfieldPost.holoplay = this;
			lightfieldCam = lightfieldCamGO.AddComponent<Camera>();
#if UNITY_2017_3_OR_NEWER
			lightfieldCam.allowDynamicResolution = false;
#endif
			lightfieldCam.allowHDR = false;
			lightfieldCam.allowMSAA = false;
			lightfieldCam.cullingMask = 0;
			lightfieldCam.clearFlags = CameraClearFlags.Nothing;

#if !UNITY_EDITOR
			if(instance == null){
				Debug.Log("empty when running");
			}
#endif
			ReloadCalibration();

			// // load calibration
			// if (!loadResults.calibrationFound)
			// 	Debug.Log("[HoloPlay] Attempting to load calibration, but none found!");
			// if (!loadResults.lkgDisplayFound)
			// 	Debug.Log("[HoloPlay] No LKG display detected");
			
			// setup the window to play on the looking glass
			Screen.SetResolution(cal.screenWidth, cal.screenHeight, true);
#if UNITY_2019_3_OR_NEWER
			if (!Application.isEditor && targetDisplay == 0) {
#if UNITY_STANDALONE_OSX
				targetDisplay = PluginCore.GetLKGunityIndex(targetLKG);
				lightfieldCam.targetDisplay = targetDisplay;
				Display.displays[targetDisplay].Activate();
#else
				Display.displays[targetDisplay].Activate(0, 0, 0);
				
#if UNITY_STANDALONE_WIN
                Display.displays[targetDisplay].SetParams(
					cal.screenWidth, cal.screenHeight,
					cal.xpos, cal.ypos
				);
				// Debug.Debug.LogFormat("{0}, {1}, {2}, {3}", cal.screenWidth, cal.screenHeight,
				// 	cal.xpos, cal.ypos)
#endif

#endif
            }
#endif

			// setup the quilt
			SetupQuilt();

			// call initialization event
			if (onHoloplayReady != null)
				onHoloplayReady.Invoke(loadResults);
		}

		void OnDisable() {
			if (lightfieldMat != null)
				DestroyImmediate(lightfieldMat);
			if (RenderTexture.active == quiltRT) 
				RenderTexture.active = null;
			if (quiltRT != null) 
				DestroyImmediate(quiltRT);
			if (lightfieldCam != null)
				DestroyImmediate(lightfieldCam.gameObject);
			if (postProcessCam != null) {
				if (cam.gameObject == gameObject) {
					Debug.LogWarning("Something is very wrong");
				} else {
					DestroyImmediate(cam.gameObject);
				}
			}
		}


		private void OnDestroy() {
# if !UNITY_2018_1_OR_NEWER || !UNITY_EDITOR
			PluginCore.Reset();
#endif
		}

        void Update() {
            // 2d screenshot input
            if (Input.GetKeyDown(screenshot2DKey)) {
                RenderTexture renderTexture = RenderTexture.GetTemporary(Screen.width, Screen.height, 24); // allocate a temporary rt
                cam.targetTexture = renderTexture; // set it as the target render texture
                cam.Render(); // so that it can be rendered by camera
                TakeScreenShot(renderTexture); // save the render texture to png file
                cam.targetTexture = null; // reset the target texture to null
                RenderTexture.ReleaseTemporary(renderTexture); // don't forget to release the memory
            }
            // quilt screenshot input
            if (Input.GetKeyDown(screenshotQuiltKey)) {
				// todo: removed logic to standardize quilt here
                TakeScreenShot(quiltRT);
            }
			// debug info
			if (Input.GetKey(KeyCode.RightShift) && Input.GetKeyDown(KeyCode.F8))
				debugInfo = !debugInfo;
			if (Input.GetKeyDown(KeyCode.Escape))
				debugInfo = false;
			
			frameRendered = false;
        }

		void LateUpdate() {
			// if(loadResults.calibrationFound == false){
			// 	return;
			// }
			camDist = ResetCamera();
		}

        public void RenderQuilt (bool forceRender = false) {
            // if (!loadResults.calibrationFound) return;
			// Debug.Log("[Info] rendering quilt");
			if (!forceRender && frameRendered) return;
			frameRendered = true;
			// pass the calibration values to lightfield material
			PassSettingsToMaterial(lightfieldMat);
			// set up camera
			var aspect = cal.aspect;
			if(cam == null){
				Debug.Log("cam is null");
			}
			var centerViewMatrix = cam.worldToCameraMatrix;
			var centerProjMatrix = cam.projectionMatrix;
			depth = Mathf.Clamp(depth, -100f, 100f);
			cam.depth = lightfieldCam.depth = depth;
			cam.targetDisplay = lightfieldCam.targetDisplay = targetDisplay;
			// override quilt
			bool hasOverrideQuilt = overrideQuilt;
			if (hasOverrideQuilt) {
				Graphics.Blit(overrideQuilt, quiltRT);
				// if only rendering override, exit here
				if (!renderOverrideBehind) {
					cam.enabled = false;
					lightfieldCam.enabled = true;
					PassSettingsToMaterial(lightfieldMat);
					return;
				}
			}
			// if it's a 2D preview, exit here
			cam.enabled = preview2D;
			lightfieldCam.enabled = !preview2D;
			if (preview2D) {
				cam.targetDisplay = targetDisplay;
				return;
			}

			// get viewcone sweep
			float viewConeSweep = -camDist * Mathf.Tan(cal.viewCone * viewconeModifier * Mathf.Deg2Rad);
			// projection matrices must be modified in terms of focal plane size
			float projModifier = 1f / (size * cam.aspect);
			// fov trick to keep shadows from disappearing
			cam.fieldOfView = 135f;

			// optimization viewinterp
			RenderTexture viewRT = null;
			RenderTexture viewRTDepth = null;
			RenderTexture quiltRTDepth = null;
			var quiltDepthDesc = quiltRT.descriptor;
			quiltDepthDesc.colorFormat = RenderTextureFormat.RFloat;
			quiltRTDepth = RenderTexture.GetTemporary(quiltDepthDesc);
			quiltRTDepth.Create();
			// clear the textures as well
			if (!hasOverrideQuilt) {
				RenderTexture.active = quiltRT;
				GL.Clear(true, true, background, 0f);
			}
			RenderTexture.active = quiltRTDepth;
			GL.Clear(true, true, Color.black, 2f);
			RenderTexture.active = null;

			// set ppcam
#if UNITY_POST_PROCESSING_STACK_V2
			var hasPPCam = postProcessCam != null;
			if (hasPPCam) {
				postProcessCam.CopyFrom(cam);
			}
#endif
			
			// render the views
			for (int i = 0; i < quiltSettings.numViews; i++) {
				if (i % ViewInterpolation != 0 && i != quiltSettings.numViews - 1)
					continue;
				// onViewRender
				if (onViewRender != null)
					onViewRender.Invoke(this, i);
				// get view rt
				viewRT = RenderTexture.GetTemporary(quiltSettings.viewWidth, quiltSettings.viewHeight, 24);
				// optimization viewinterp
				viewRTDepth = RenderTexture.GetTemporary(quiltSettings.viewWidth, quiltSettings.viewHeight, 24, RenderTextureFormat.Depth);
				cam.SetTargetBuffers(viewRT.colorBuffer, viewRTDepth.depthBuffer);
				cam.aspect = aspect;
				// move the camera
				var viewMatrix = centerViewMatrix;
				var projMatrix = centerProjMatrix;
				float currentViewLerp = 0f; // if numviews is 1, take center view
				if (quiltSettings.numViews > 1)
					currentViewLerp = (float)i / (quiltSettings.numViews - 1) - 0.5f;
				viewMatrix.m03 += currentViewLerp * viewConeSweep;
				projMatrix.m02 += currentViewLerp * viewConeSweep * projModifier;
				cam.worldToCameraMatrix = viewMatrix;
				cam.projectionMatrix = projMatrix;
				// render and copy the quilt
				cam.Render();
				// copy to quilt
				CopyViewToQuilt(i, viewRT, quiltRT);
				CopyViewToQuilt(i, viewRTDepth, quiltRTDepth);
				// done copying to quilt, release view rt
				cam.targetTexture = null;
				RenderTexture.ReleaseTemporary(viewRT);
				RenderTexture.ReleaseTemporary(viewRTDepth);
				// this helps 3D cursor ReadPixels faster
				GL.Flush();
				// move to next view
        	}
			// onViewRender final pass
			if (onViewRender != null)
				onViewRender.Invoke(this, quiltSettings.numViews);
			// reset to center view
			cam.worldToCameraMatrix = centerViewMatrix;
			cam.projectionMatrix = centerProjMatrix;
			// not really necessary, but keeps gizmo from looking messed up sometimes
			cam.aspect = aspect;
			// reset fov after fov trick
			cam.fieldOfView = fov;
			// if interpolation is happening, release
			if (ViewInterpolation > 1) {
				// todo: interpolate on the quilt itself
				InterpolateViewsOnQuilt(quiltRTDepth);
			}
#if UNITY_POST_PROCESSING_STACK_V2
			if (hasPPCam) {
#if !UNITY_2018_1_OR_NEWER
				if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
					SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
				{
					FlipRenderTexture(quiltRT);
				}
#endif
				RunPostProcess(quiltRT, quiltRTDepth);				
			}
#endif
			var simpleDof = GetComponent<SimpleDOF>();
			if (simpleDof != null && simpleDof.enabled) {
				simpleDof.DoDOF(quiltRT, quiltRTDepth);
			}
			RenderTexture.ReleaseTemporary(quiltRTDepth);
        }

		void RunPostProcess(RenderTexture rt, RenderTexture rtDepth) {
			postProcessCam.cullingMask = 0;
			postProcessCam.clearFlags = CameraClearFlags.Nothing;
			postProcessCam.targetTexture = rt;
			Shader.SetGlobalTexture("_FAKEDepthTexture", rtDepth);
			postProcessCam.Render();
		}

		void OnValidate() {
			// make sure size can't go negative
			// using this here instead of [Range()] attribute because size shouldn't need a slider
			size = Mathf.Clamp(size, 0.01f, Mathf.Infinity);
			// custom quilt settings
			if (quiltPreset == Quilt.Preset.Custom) {
				SetupQuilt();
			}
		}

		void OnGUI() {
			if (debugInfo) {
				// save settings
				Color oldColor = GUI.color;
				// start drawing stuff
				int unitDiv = 20;
				int unit = Mathf.Min(Screen.width, Screen.height) / unitDiv;
				Rect rect = new Rect(unit, unit, unit*(unitDiv-2), unit*(unitDiv-2));
				GUI.color = Color.black;
				GUI.DrawTexture(rect, Texture2D.whiteTexture);
				rect = new Rect(unit*2, unit*2, unit*(unitDiv-4), unit*(unitDiv-4));
				GUILayout.BeginArea(rect);
				var labelStyle = new GUIStyle(GUI.skin.label);
				labelStyle.fontSize = unit;
				GUI.color = new Color(.5f, .8f, .5f, 1f);
				// Debug.Log(Application.version);
				GUILayout.Label("Holoplay SDK " + version.ToString() + versionLabel, labelStyle);
				GUILayout.Space(unit);
				GUI.color = loadResults.calibrationFound ? new Color(.5f, 1f, .5f) : new Color(1f, .5f, .5f);
				GUILayout.Label("calibration: " + (loadResults.calibrationFound ? "loaded" : "not found"), labelStyle);
				// todo: this is giving a false positive currently
				// GUILayout.Space(unit);
				// GUI.color = new Color(.5f, .5f, .5f, 1f);
				// GUILayout.Label("lkg display: " + (loadResults.lkgDisplayFound ? "found" : "not found"), labelStyle);
				GUILayout.EndArea();
				// restore settings
				GUI.color = oldColor;
			}
		}

		public void CopyViewToQuilt(int view, RenderTexture viewRT, RenderTexture quiltRT) {
			// note: not using graphics.copytexture because it does not honor alpha
			// reverse view because Y is taken from the top
			int ri = quiltSettings.viewColumns * quiltSettings.viewRows - view - 1;
			int x = (view % quiltSettings.viewColumns) * quiltSettings.viewWidth;
			int y = (ri / quiltSettings.viewColumns) * quiltSettings.viewHeight;
			// again owing to the reverse Y
			Rect rtRect = new Rect(x, y + quiltSettings.paddingVertical, quiltSettings.viewWidth, quiltSettings.viewHeight);
			Graphics.SetRenderTarget(quiltRT);
			GL.PushMatrix();
				GL.LoadPixelMatrix(0, (int)quiltSettings.quiltWidth, (int)quiltSettings.quiltHeight, 0);
			Graphics.DrawTexture(rtRect, viewRT);
			GL.PopMatrix();
			Graphics.SetRenderTarget(null);
		}

		public void FlipRenderTexture(RenderTexture rt) {
			RenderTexture rtTemp = RenderTexture.GetTemporary(rt.descriptor);
			rtTemp.Create();
			Graphics.CopyTexture(rt, rtTemp);
			Graphics.SetRenderTarget(rt);
			Rect rtRect = new Rect(0, 0, rt.width, rt.height);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, (int)quiltSettings.quiltWidth, 0, (int)quiltSettings.quiltHeight);
			Graphics.DrawTexture(rtRect, rtTemp);
			GL.PopMatrix();
			Graphics.SetRenderTarget(null);
			RenderTexture.ReleaseTemporary(rtTemp);
		}

		public void InterpolateViewsOnQuilt(
			RenderTexture quiltRTDepth)
		{
			if (interpolationComputeShader == null) {
				interpolationComputeShader = Resources.Load<ComputeShader>("ViewInterpolation");
			}
			int kernelFwd = interpolationComputeShader.FindKernel("QuiltInterpolationForward");
			int kernelBack = blendViews ? 
				interpolationComputeShader.FindKernel("QuiltInterpolationBackBlend") :
				interpolationComputeShader.FindKernel("QuiltInterpolationBack");
			int kernelFwdFlicker = interpolationComputeShader.FindKernel("QuiltInterpolationForwardFlicker");
			int kernelBackFlicker = blendViews ? 
				interpolationComputeShader.FindKernel("QuiltInterpolationBackBlendFlicker") :
				interpolationComputeShader.FindKernel("QuiltInterpolationBackFlicker");
			interpolationComputeShader.SetTexture(kernelFwd, "Result", quiltRT);
			interpolationComputeShader.SetTexture(kernelFwd, "ResultDepth", quiltRTDepth);
			interpolationComputeShader.SetTexture(kernelBack, "Result", quiltRT);
			interpolationComputeShader.SetTexture(kernelBack, "ResultDepth", quiltRTDepth);
			interpolationComputeShader.SetTexture(kernelFwdFlicker, "Result", quiltRT);
			interpolationComputeShader.SetTexture(kernelFwdFlicker, "ResultDepth", quiltRTDepth);
			interpolationComputeShader.SetTexture(kernelBackFlicker, "Result", quiltRT);
			interpolationComputeShader.SetTexture(kernelBackFlicker, "ResultDepth", quiltRTDepth);
			interpolationComputeShader.SetFloat("_NearClip", cam.nearClipPlane);
			interpolationComputeShader.SetFloat("_FarClip", cam.farClipPlane);
			interpolationComputeShader.SetFloat("focalDist", GetCamDistance()); // todo: maybe just pass cam dist in w the funciton call
			// aspect corrected fov, used for perspective w component
			float afov = Mathf.Atan(cal.aspect * Mathf.Tan(0.5f * fov * Mathf.Deg2Rad));
			interpolationComputeShader.SetFloat("perspw", 2f * Mathf.Tan(afov));
			interpolationComputeShader.SetVector("viewSize", new Vector4(
				quiltSettings.viewWidth,
				quiltSettings.viewHeight,
				1f / quiltSettings.viewWidth,
				1f / quiltSettings.viewHeight
			));

			List<int> viewPositions = new List<int>();
			List<float> viewOffsets = new List<float>();
			List<int> baseViewPositions = new List<int>();
			int validViewIndex = -1;
			int currentInterp = 1;
			for (int i = 0; i < quiltSettings.numViews; i++) {
				var positions = new [] {
					i % quiltSettings.viewColumns * quiltSettings.viewWidth,
					i / quiltSettings.viewColumns * quiltSettings.viewHeight,
				};
				if (i != 0 && i != quiltSettings.numViews - 1 && i % ViewInterpolation != 0) {
					viewPositions.AddRange(positions);
					viewPositions.AddRange(new [] { validViewIndex, validViewIndex + 1 });
					int div = Mathf.Min(ViewInterpolation, quiltSettings.numViews - 1);
					int divTotal = quiltSettings.numViews / div;
					if (i > divTotal * ViewInterpolation) {
						div = quiltSettings.numViews - divTotal * ViewInterpolation;
					}
					float offset = div * Mathf.Tan(cal.viewCone * viewconeModifier * Mathf.Deg2Rad) / (quiltSettings.numViews - 1f);
					float lerp = (float)currentInterp / div;
					currentInterp++;
					viewOffsets.AddRange(new [] { offset, lerp });
				} else {
					baseViewPositions.AddRange(positions);
					validViewIndex++;
					currentInterp = 1;
				}
			}

			int viewCount = viewPositions.Count / 4;
			ComputeBuffer viewPositionsBuffer = new ComputeBuffer(viewPositions.Count / 4, 4 * sizeof(int));
			ComputeBuffer viewOffsetsBuffer = new ComputeBuffer(viewOffsets.Count / 2, 2 * sizeof(float));
			ComputeBuffer baseViewPositionsBuffer = new ComputeBuffer(baseViewPositions.Count / 2, 2 * sizeof(int));
			viewPositionsBuffer.SetData(viewPositions);
			viewOffsetsBuffer.SetData(viewOffsets);
			baseViewPositionsBuffer.SetData(baseViewPositions);

			interpolationComputeShader.SetBuffer(kernelFwd, "viewPositions", viewPositionsBuffer);
			interpolationComputeShader.SetBuffer(kernelFwd, "viewOffsets", viewOffsetsBuffer);
			interpolationComputeShader.SetBuffer(kernelFwd, "baseViewPositions", baseViewPositionsBuffer);
			interpolationComputeShader.SetBuffer(kernelBack, "viewPositions", viewPositionsBuffer);
			interpolationComputeShader.SetBuffer(kernelBack, "viewOffsets", viewOffsetsBuffer);
			interpolationComputeShader.SetBuffer(kernelBack, "baseViewPositions", baseViewPositionsBuffer);
			interpolationComputeShader.SetBuffer(kernelFwdFlicker, "viewPositions", viewPositionsBuffer);
			interpolationComputeShader.SetBuffer(kernelFwdFlicker, "viewOffsets", viewOffsetsBuffer);
			interpolationComputeShader.SetBuffer(kernelFwdFlicker, "baseViewPositions", baseViewPositionsBuffer);
			interpolationComputeShader.SetBuffer(kernelBackFlicker, "viewPositions", viewPositionsBuffer);
			interpolationComputeShader.SetBuffer(kernelBackFlicker, "viewOffsets", viewOffsetsBuffer);
			interpolationComputeShader.SetBuffer(kernelBackFlicker, "baseViewPositions", baseViewPositionsBuffer);
			// interpolationComputeShader.SetInt("viewPositionsCount", viewCount);

			uint blockX,  blockY,  blockZ;
			interpolationComputeShader.GetKernelThreadGroupSizes(kernelFwd, out blockX, out blockY, out blockZ);
			int computeX = quiltSettings.viewWidth / (int)blockX + Mathf.Min(quiltSettings.viewWidth % (int)blockX, 1);
			int computeY = quiltSettings.viewHeight / (int)blockY + Mathf.Min(quiltSettings.viewHeight % (int)blockY, 1);
			int computeZ = viewCount / (int)blockZ + Mathf.Min(viewCount % (int)blockZ, 1);

			if (reduceFlicker) {
				int spanSize = 2 * ViewInterpolation;
				interpolationComputeShader.SetInt("spanSize", spanSize);
				for (int i = 0; i < spanSize; i++) {
					interpolationComputeShader.SetInt("px", i);
					interpolationComputeShader.Dispatch(kernelFwd, quiltSettings.viewWidth / spanSize, computeY, computeZ);
					interpolationComputeShader.Dispatch(kernelBack, quiltSettings.viewWidth / spanSize, computeY, computeZ);
				}
			} else {
				interpolationComputeShader.Dispatch(kernelFwdFlicker, computeX, computeY, computeZ);
				interpolationComputeShader.Dispatch(kernelBackFlicker, computeX, computeY, computeZ);
			}

			if (fillGaps) {
				var fillgapsKernel = interpolationComputeShader.FindKernel("FillGaps");
				interpolationComputeShader.SetTexture(fillgapsKernel, "Result", quiltRT);
				interpolationComputeShader.SetTexture(fillgapsKernel, "ResultDepth", quiltRTDepth);
				interpolationComputeShader.SetBuffer(fillgapsKernel, "viewPositions", viewPositionsBuffer);
				interpolationComputeShader.Dispatch(fillgapsKernel, computeX, computeY, computeZ);
			}

			viewPositionsBuffer.Dispose();
			viewOffsetsBuffer.Dispose();
			baseViewPositionsBuffer.Dispose();
		}

		// [Range(1, 32)]
		// public int spanSize = 4;

		public float ResetCamera() {
			// scale follows size
			if (scaleFollowsSize) {
				transform.localScale = Vector3.one * size;
			}
			// force it to render in perspective
			cam.orthographic = false;
			// set up the center view / proj matrix
			if (useFrustumTarget) {
				cam.fieldOfView = 2f * Mathf.Atan(Mathf.Abs(size / frustumTarget.localPosition.z)) * Mathf.Rad2Deg;
			} else {
				cam.fieldOfView = fov;
			}
			// get distance
			float dist = GetCamDistance();
			// set near and far clip planes based on dist
			cam.nearClipPlane = Mathf.Max(dist - size * nearClipFactor, 0.1f);
			cam.farClipPlane = Mathf.Max(dist + size * farClipFactor, cam.nearClipPlane);
			// reset matrices, save center for later
			cam.ResetWorldToCameraMatrix();
			cam.ResetProjectionMatrix();
			var centerViewMatrix = cam.worldToCameraMatrix;
			var centerProjMatrix = cam.projectionMatrix;
			centerViewMatrix.m23 -= dist;

			if (useFrustumTarget) {
				Vector3 targetPos = -frustumTarget.localPosition;
				centerViewMatrix.m03 += targetPos.x;
				centerProjMatrix.m02 += targetPos.x / (size * cal.aspect);
				centerViewMatrix.m13 += targetPos.y;
				centerProjMatrix.m12 += targetPos.y / size;
				Debug.Log(
					"View Matrix:\n" +
					centerViewMatrix + "\n" +
					"Proj Matrix:\n" +
					centerProjMatrix
				);
			} else {
				// if we have offsets, handle them here
				if (horizontalFrustumOffset != 0f) {
					// centerViewMatrix.m03 += horizontalFrustumOffset * size * cal.aspect;
					float offset = dist * Mathf.Tan(Mathf.Deg2Rad * horizontalFrustumOffset);
					centerViewMatrix.m03 += offset;
					centerProjMatrix.m02 += offset / (size * cal.aspect);
				}
				if (verticalFrustumOffset != 0f) {
					float offset = dist * Mathf.Tan(Mathf.Deg2Rad * verticalFrustumOffset);
					centerViewMatrix.m13 += offset;
					centerProjMatrix.m12 += offset / size;
				}
			}
			cam.worldToCameraMatrix = centerViewMatrix;
			cam.projectionMatrix = centerProjMatrix;
			// set some of the camera properties from inspector
			cam.clearFlags = clearFlags;
			cam.backgroundColor = background;
			cam.cullingMask = cullingMask;
			cam.renderingPath = renderingPath;
			cam.useOcclusionCulling = occlusionCulling;
			cam.allowHDR = allowHDR;
			cam.allowMSAA = allowMSAA;
#if UNITY_2017_3_OR_NEWER
			cam.allowDynamicResolution = allowDynamicResolution;
#endif
			// return distance (since it is useful after the fact and we have it anyway)
			return dist;
		}

		public void PassSettingsToMaterial(Material lightfieldMat) {
			if (lightfieldMat == null) return;
			// pass values
			lightfieldMat.SetFloat("pitch", cal.pitch);
			// Debug.Log("pitch:"+lightfieldMat.GetFloat("pitch"));
			lightfieldMat.SetFloat("slope", cal.slope);
			lightfieldMat.SetFloat("center", cal.center + centerOffset);
			lightfieldMat.SetFloat("fringe", cal.fringe);
			lightfieldMat.SetFloat("subpixelSize", cal.subp);
			lightfieldMat.SetVector("tile", new Vector4(
                quiltSettings.viewColumns,
                quiltSettings.viewRows,
                quiltSettings.numViews,
                quiltSettings.viewColumns * quiltSettings.viewRows
            ));
            lightfieldMat.SetVector("viewPortion", new Vector4(
                quiltSettings.viewPortionHorizontal,
                quiltSettings.viewPortionVertical
            ));
            lightfieldMat.SetVector("aspect", new Vector4(
                cal.aspect,
                // if it's the default aspect (-1), just use the same aspect as the screen
                quiltSettings.aspect < 0 ? cal.aspect : quiltSettings.aspect,
                quiltSettings.overscan ? 1 : 0
            ));
			// Debug.Log( string.Format("set uniforms: \n pitch: {0}, slope: {1}, center: {2}, fringe: {3}, subp: {4}, aspect: {5}",
			// 	cal.pitch, cal.slope, cal.center, cal.fringe, cal.subp, cal.aspect ));

			// so its come to this...
#if UNITY_EDITOR_OSX && UNITY_2019_3_OR_NEWER
			lightfieldMat.SetFloat("verticalOffset", -21f / cal.screenHeight);
#elif UNITY_EDITOR_OSX && UNITY_2019_1_OR_NEWER
			lightfieldMat.SetFloat("verticalOffset", -19f / cal.screenHeight);
#endif
		}

		public LoadResults ReloadCalibration() {
			// Debug.Log("reload calibration");
			var results = PluginCore.GetLoadResults(); // loads calibration as well
                                                       // create a calibration object. 
                                                       // if we find that the target display matches a plugged in looking glass,
                                                       // use matching calibration
            cal = CalibrationManager.GetCalibration(0);
            // Debug.Log(this.name + " try looking for the Looking glass!");
            if (results.calibrationFound && results.lkgDisplayFound) {
                // Debug.Log("try matching unity index:" + targetDisplay);
				for (int i = 0; i < CalibrationManager.GetCalibrationCount(); i++) {
                    // Debug.Log("try matching " + i + " LKG" + " whose unity index is " + PluginCore.GetLKGunityIndex(i));
                    cal = CalibrationManager.GetCalibration(i);
                    if (targetDisplay != cal.unityIndex) {
                        continue;
                    }
                    // Debug.Log("matched!");
                    targetLKG = i;
                    // go out of the for loop once it found the matched calibration
                    break;
                }
			}
            // Debug.Log(this.name + " cal index is " + cal.index + " : target lkg is" + targetLKG);

            PassSettingsToMaterial(lightfieldMat);
            this.loadResults = results;
			return results;
		}

		/// <summary>
		/// Returns the camera's distance from the center.
		/// Will be a positive number.
		/// </summary>
		public float GetCamDistance() {
			if (!useFrustumTarget) {
				return size / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
			}
			return Mathf.Abs(frustumTarget.localPosition.z);
		}

		/// <summary>
		/// Will set up the quilt and the quilt rendertexture.
		/// Should be called after modifying custom quilt settings.
		/// </summary>
		public void SetupQuilt() {
			// Debug.Log("set up quiltRT");
			customQuiltSettings.Setup(); // even if not custom quilt, just set this up anyway
			if (quiltRT != null) DestroyImmediate(quiltRT);
			quiltRT = new RenderTexture(quiltSettings.quiltWidth, quiltSettings.quiltHeight, 0) {
				filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
			quiltRT.enableRandomWrite = true;
			quiltRT.Create();
			PassSettingsToMaterial(lightfieldMat);

			// pass some stuff globally for pp
			float viewSizeX = (float)quiltSettings.viewWidth / quiltSettings.quiltWidth;
			float viewSizeY = (float)quiltSettings.viewHeight / quiltSettings.quiltHeight;
			Shader.SetGlobalVector("hp_quiltViewSize", new Vector4(
				viewSizeX, viewSizeY,
				quiltSettings.viewWidth, quiltSettings.viewHeight
			));
		}

        // save screenshot as png file with the given rendertexture
        void TakeScreenShot(RenderTexture rt) {
            Texture2D screenShot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            Graphics.SetRenderTarget(rt); // same as RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            Graphics.SetRenderTarget(null);// same as RenderTexture.active = null;
            byte[] bytes = screenShot.EncodeToPNG();
            string filename = string.Format("{0}/screen_{1}x{2}_{3}.png",
											System.IO.Path.GetFullPath("."), rt.width, rt.height,
											System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            System.IO.File.WriteAllBytes(filename, bytes);
            Debug.Log(string.Format("Took screenshot to: {0}", filename));
        }

		void OnDrawGizmos() {
            // if (!loadResults.calibrationFound) return;
			Gizmos.color = QualitySettings.activeColorSpace == ColorSpace.Gamma ?
				frustumColor.gamma : frustumColor;
			// float focalDist = size / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
			float focalDist = GetCamDistance();
			cornerDists[0] = focalDist;
			cornerDists[1] = cam.nearClipPlane;
			cornerDists[2] = cam.farClipPlane;
			for (int i = 0; i < cornerDists.Length; i++) {
				float dist = cornerDists[i];
				int offset = i * 4;
                frustumCorners[offset+0] = cam.ViewportToWorldPoint(new Vector3(0, 0, dist));
                frustumCorners[offset+1] = cam.ViewportToWorldPoint(new Vector3(0, 1, dist));
                frustumCorners[offset+2] = cam.ViewportToWorldPoint(new Vector3(1, 1, dist));
                frustumCorners[offset+3] = cam.ViewportToWorldPoint(new Vector3(1, 0, dist));
				// draw each square
				for (int j = 0; j < 4; j++) {
					Vector3 start = frustumCorners[offset+j];
					Vector3 end = frustumCorners[offset+(j+1)%4];
					if (i > 0) {
						// draw a normal line for front and back
						Gizmos.color = QualitySettings.activeColorSpace == ColorSpace.Gamma ?
							frustumColor.gamma : frustumColor;
						Gizmos.DrawLine(start, end);
					} else {
						// draw a broken, target style frame for focal plane
						Gizmos.color = QualitySettings.activeColorSpace == ColorSpace.Gamma ?
							middlePlaneColor.gamma : middlePlaneColor;
						Gizmos.DrawLine(start, Vector3.Lerp(start, end, 0.333f));
						Gizmos.DrawLine(end, Vector3.Lerp(end, start, 0.333f));
					}
				}
			}
			// connect them
			for (int i = 0; i < 4; i++)
				Gizmos.DrawLine(frustumCorners[4+i], frustumCorners[8+i]);
		}
	}
}
