using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

namespace LookingGlass.ProWorkstation {

    // enum for pro workstation stuff
    public enum ProWorkstationDisplay {
        LookingGlass, 
        Foldout2D
    }

    public class ProWorkstationManager : MonoBehaviour {

        // cause path.combine seems to be glitchy?
        public const string separator = 
#if UNITY_EDITOR_WIN
            "\\";
#else
            "/";
#endif
        public ProWorkstationDisplay display;
        public Process process;
        public static ProWorkstationManager instance;
        public const string extendedUIString = "_extendedUI";
        public const string lkgDisplayString = "LKGDisplay";
        public const int sidePanelResolutionX = 800;
        public const int sidePanelResolutionY = 1280;

        // must specify display before creation
        public ProWorkstationManager(ProWorkstationDisplay display) {
            this.display = display;
        }

        void Awake() {
            // only one should exist at a time, check for existing instances on awake
            var existingManagers = FindObjectsOfType<ProWorkstationManager>();
            if (existingManagers.Length > 1) {
                // delete self if found
                DestroyImmediate(this.gameObject);
                return;
            }

            // otherwise this should be the only manager, make it an instance and keep it from being destroyed on scene change
            instance = this;
            DontDestroyOnLoad(this.gameObject);

            // if this is the side panel scene
            if (!Application.isEditor && display == ProWorkstationDisplay.Foldout2D) {

                // fist adjust position
                UnityEngine.Display.displays[0].Activate(0,0,0);
                UnityEngine.Display.displays[0].SetParams(sidePanelResolutionX, sidePanelResolutionY, 0, 0);
                // worried this might mess something up on the pro workstations that aren't production models
                //// Screen.SetResolution(sidePanelResolutionX, sidePanelResolutionY, false);

                // launch the lkg version of the application
                if (process == null) {
                    var processPath = Application.streamingAssetsPath + separator + lkgDisplayString + ".exe";
                    ProcessStartInfo processStartInfo = new ProcessStartInfo( processPath );
                    //? not needed
                    //? processStartInfo.Arguments = "--args ";
                    //? processStartInfo.Arguments += "-screen-fullscreen 0 ";
                    process = Process.Start(processStartInfo);
                }
            }
            
            // if it's a looking glass
            if (display == ProWorkstationDisplay.LookingGlass) {

                //? not necessary, this happens automatically in the holoplay capture now
                //// just set the window position
                //// UnityEngine.Display.displays[0].Activate(0,0,0);
                //// UnityEngine.Display.displays[0].SetParams(Plugin.GetScreenWidth(0), Plugin.GetScreenHeight(0), Plugin.GetLKGxpos(0), Plugin.GetLKGypos(0));
            }
        }
    }
}
