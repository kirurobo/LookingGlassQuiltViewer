using UnityEngine;
namespace LookingGlass
{
    public class CalibrationManager
    {
        private static bool isInit = false;
        private static Calibration[] calibrations;

        public static int GetCalibrationCount(){
            if(!isInit){
                Debug.Log("calibration is not inited yet");
                return 0;
            }
            return calibrations.Length;
        }

        public static void Init()
        {
            // Debug.Log("init calibrations");
            calibrations = PluginCore.GetCalibrationArray();
            isInit = calibrations.Length > 0;
        }

        public static Calibration GetCalibration(int index)
        {
            if (!isInit){
                Debug.Log("[HoloPlay] Calibration is not inited yet");
                return new Calibration(0);
            }
            if (!isIndexValid(index)){
                Debug.Log("calibration index is unvalid");
                return new Calibration(0);
            }

            return calibrations[index];
        }
        public static bool isIndexValid(int index)  { return index >= 0 && isInit && index < calibrations.Length; }
    }

    public struct Calibration
    {
        public Calibration(int index, int unityIndex, int screenW, int screenH,
            float subp, float viewCone, float aspect, 
            float pitch, float slope, float center,
            float fringe, string serial, string LKGname,
            int xpos, int ypos)
        {
            this.index = index;
            this.unityIndex = unityIndex;
            this.screenWidth = screenW;
            this.screenHeight = screenH;
            this.subp = subp;
            this.viewCone = viewCone;
            this.aspect = aspect;
            this.pitch = pitch;
            this.slope = slope;
            this.center = center;
            this.fringe = fringe;
            this.serial = serial;
            this.LKGname = LKGname;
            this.xpos = xpos;
            this.ypos = ypos;
        }
        public Calibration(int index)
        {
            this.index = index;
            this.screenWidth = 1600;
            this.screenHeight = 900;
            this.subp = 0;
            this.viewCone = 0;
            this.aspect = 16f/9f;
            this.pitch = 10;
            this.slope = 1;
            this.center = 0;
            this.fringe = 0;
            this.serial = "";
            this.LKGname = "";
            this.unityIndex = 0;
            this.xpos = 0;
            this.ypos = 0;
        }

        public int index;
        public int screenWidth;
        public int screenHeight;
        public float subp;
        public float viewCone;
        public float aspect;
        public float pitch;
        public float slope;
        public float center;
        public float fringe;
        public string serial;
        public string LKGname;
        public int unityIndex;

        public int xpos, ypos;
    }
}