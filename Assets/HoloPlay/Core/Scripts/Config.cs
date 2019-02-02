//Copyright 2017 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HoloPlay.Extras;
using UnityEngine;
using UnityEngine.Events;

namespace HoloPlay
{
    [ExecuteInEditMode]
    public static class Config
    {
        [Serializable]
        public class ConfigValue
        {
            public readonly bool isInt;
            [SerializeField]
            float value;
            public float Value
            {
                get { return value; }
                set
                {
                    this.value = isInt ? Mathf.Round(value) : value;
                    this.value = Mathf.Clamp(this.value, min, max);
                }
            }
            public readonly float defaultValue;
            public readonly float min;
            public readonly float max;
            public readonly string name;
            public ConfigValue(float defaultValue, float min, float max, string name, bool isInt = false)
            {
                this.defaultValue = defaultValue;
                this.min = min;
                this.max = max;
                this.Value = defaultValue;
                this.name = name;
                this.isInt = isInt;
            }

            // just to make life easier
            public int asInt { get { return (int)value; } }
            public bool asBool { get { return (int)value == 1; } }

            public static implicit operator float(ConfigValue configValue)
            {
                return configValue.Value;
            }
        }

        /// <summary>
        /// Type for visual lenticular calibration
        /// </summary>
        [Serializable]
        public class VisualConfig
        {
            public string configVersion = "1.0";
            public string serial = "00000";
            public ConfigValue pitch = new ConfigValue(49.91f, 1f, 200, "Pitch");
            public ConfigValue slope = new ConfigValue(5.8f, -30, 30, "Slope");
            public ConfigValue center = new ConfigValue(0, -1, 1, "Center");
            public ConfigValue viewCone = new ConfigValue(40, 0, 180, "View Cone");
            public ConfigValue invView = new ConfigValue(0, 0, 1, "View Inversion", true);
            public ConfigValue verticalAngle = new ConfigValue(0, -20, 20, "Vert Angle");
            public ConfigValue DPI = new ConfigValue(338, 1, 1000, "DPI", true);
            public ConfigValue screenW = new ConfigValue(2560, 640, 6400, "Screen Width", true);
            public ConfigValue screenH = new ConfigValue(1600, 480, 4800, "Screen Height", true);
            public ConfigValue flipImageX = new ConfigValue(0, 0, 1, "Flip Image X", true);
            public ConfigValue flipImageY = new ConfigValue(0, 0, 1, "Flip Image Y", true);
            public ConfigValue flipSubp = new ConfigValue(0, 0, 1, "Flip Subpixels", true);
            [NonSerialized] public string loadedFrom = "not loaded -- default used";
            [NonSerialized] public bool loadedSuccess = false;
        }

        //**********/
        //* fields */
        //**********/

        private static VisualConfig instance;
        /// <summary>
        /// Most recently loaded config
        /// </summary>
        /// <returns></returns>
        public static VisualConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    LoadVisualFromFile(out instance, visualFileName);
                    Quilt.Instance.config = instance;
                }
                return instance;
            }
#if CALIBRATOR
            set { instance = value; }
#endif
        }

        public static readonly string visualFileName = "visual.json";
        public static readonly string configDirName = "LKG_calibration";
        public static readonly string relativeConfigPath = Path.Combine(configDirName, visualFileName);

        //***********/
        //* methods */
        //***********/

        public static ConfigValue[] EnumerateConfigFields(VisualConfig visualConfig)
        {
            System.Reflection.FieldInfo[] configFields = typeof(Config.VisualConfig).GetFields();
            List<ConfigValue> configValues = new List<ConfigValue>();
            for (int i = 0; i < configFields.Length; i++)
            {
                if (configFields[i].FieldType == typeof(Config.ConfigValue))
                {
                    Config.ConfigValue val = (Config.ConfigValue)configFields[i].GetValue(visualConfig);
                    configValues.Add(val);
                }
            }
            return configValues.ToArray();
        }

        public static string FormatPathToOS(string path)
        {
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            path = path.Replace('/', Path.DirectorySeparatorChar);
            return path;
        }

        public static bool GetDoesConfigFileExist(string relativePathToConfig)
        {
            string temp;
            return GetConfigPathToFile(relativePathToConfig, out temp);
        }

        //this method is used to figure out which drive is the usb flash drive is related to HoloPlayer, and then returns that path so that our settings can load normally from there.
        public static bool GetConfigPathToFile(string relativePathToConfig, out string fullPath, string preferredDrive = "")
        {
            string tempPath = Path.GetFileName(relativePathToConfig); //return the base name of the file only.
            List<string> possiblePaths = new List<string>();
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                relativePathToConfig = FormatPathToOS(relativePathToConfig);

                string[] drives = System.Environment.GetLogicalDrives();

                foreach (string drive in drives)
                {
                    if (File.Exists(drive + relativePathToConfig))
                    {
                        tempPath = drive + relativePathToConfig;
                        possiblePaths.Add(tempPath);
                    }
                }
            }
            else  //osx,  TODO: linux untested in standalone
            {
                string[] directories = Directory.GetDirectories("/Volumes/");
                foreach (string d in directories)
                {
                    string fixedPath = d + "/" + relativePathToConfig;
                    fixedPath = FormatPathToOS(fixedPath);

                    FileInfo f = new FileInfo(fixedPath);
                    if (f.Exists)
                    {
                        tempPath = f.FullName;
                        possiblePaths.Add(tempPath);
                    }
                }
            }

            foreach (var p in possiblePaths)
            {
                if (p.Contains(preferredDrive))
                {
                    tempPath = p;
                }
            }

            if (possiblePaths.Count > 0)
            {
                fullPath = tempPath;
                return true;
            }

            fullPath = tempPath;
            return false;
        }

        public static void SaveVisualToFile(VisualConfig configToSave, string fileName, bool eeprom = true)
        {
#if !EEPROM_DISABLED
            if (eeprom)
            {
                /****** save config to EEPROM ******/
                int ret = EEPROMCalibration.WriteConfigToEEPROM(configToSave);
                if (ret == 0)
                {
                    // EEPROM saving was successful
                    Debug.Log(Misc.debugLogText + "Calibration saved to memory on device.");
                    return;
                }
                else
                {
                    // EEPROM saving was unsuccesful.
                    Debug.Log(Misc.debugLogText + "Onboard calibration save failed.");
                }
            }
#endif
            string filePath;
            if (!GetConfigPathToFile(Path.Combine(configDirName, fileName), out filePath))
            {
                // ? throw a big, in-game visible warning if this fails
                Debug.LogWarning(Misc.debugLogText + "Unable to save config to drive!");
                return;
            }
            // Debug.Log(filePath + " \n is the filepath");

            string json = JsonUtility.ToJson(configToSave, true);

            File.WriteAllText(filePath, json);
            Debug.Log(Misc.debugLogText + "Config saved to " + filePath);

#if CALIBRATOR
            if (BackupHandler.Instance != null)
            {
                var b = BackupHandler.Instance;
                for (int i = 0; i < b.drives.Count; i++)
                {
                    var d = b.drives[i];
                    var dActive = b.backupActive[i];

                    if (dActive)
                    {
                        var lkgBackup = Path.Combine(d, "LKG_backup");
                        if (!Directory.Exists(lkgBackup))
                        {
                            Directory.CreateDirectory(lkgBackup);
                        }

                        var backupNumber = Path.Combine(lkgBackup, configToSave.serial);
                        if (!Directory.Exists(backupNumber))
                        {
                            Directory.CreateDirectory(backupNumber);
                        }

                        var backupDirName = Path.Combine(backupNumber, configDirName);
                        if (!Directory.Exists(backupDirName))
                        {
                            Directory.CreateDirectory(backupDirName);
                        }

                        var backupFilePath = Path.Combine(backupDirName, visualFileName);
                        File.WriteAllText(backupFilePath, json);

                        Debug.Log(Misc.debugLogText + "Config backed up to " + backupFilePath);
                    }
                }
            }
#endif

#if UNITY_EDITOR
            if (UnityEditor.PlayerSettings.defaultScreenWidth != configToSave.screenW.asInt ||
                UnityEditor.PlayerSettings.defaultScreenHeight != configToSave.screenH.asInt)
            {
                UnityEditor.PlayerSettings.defaultScreenWidth = configToSave.screenW.asInt;
                UnityEditor.PlayerSettings.defaultScreenHeight = configToSave.screenH.asInt;
            }
#endif
        }

        /// <summary>
        /// Loads a config
        /// </summary>
        /// <param name="loadedConfig">the config to populate</param>
        /// <returns>true if successfully loaded, otherwise false</returns>
        public static bool LoadVisualFromFile(out VisualConfig loadedConfig, string fileName)
        {
            bool fileExists = false;
            string filePath;
            bool eepromFound = false;
            string configStr;
            loadedConfig = new VisualConfig();
#if !EEPROM_DISABLED
            configStr = EEPROMCalibration.LoadConfigFromEEPROM();
            if (configStr != "")
            {
                eepromFound = true;
                try
                {
                    loadedConfig = JsonUtility.FromJson<VisualConfig>(configStr);
                }
                catch
                {
                    eepromFound = false;
                }
            }

            if (eepromFound)
            {
                Debug.Log(Misc.debugLogText + "Config loaded! loaded from device memory");
                loadedConfig.loadedFrom = "Serial Flash / EEPROM";
            }
            else
            {
                Debug.Log(Misc.debugLogText + "Failed to load config file from device memory.");
            }
#endif
            if (!eepromFound)
            {
                if (!GetConfigPathToFile(Path.Combine(configDirName, fileName), out filePath))
                {
                    Debug.LogWarning(Misc.debugLogText + "Config file not found!");
                }
                else
                {
                    configStr = File.ReadAllText(filePath);
                    if (configStr.IndexOf('{') < 0 || configStr.IndexOf('}') < 0)
                    {
                        // if the file exists but is unpopulated by any info, don't try to parse it
                        // this is a bug with jsonUtility that it doesn't know how to handle a fully empty text file >:(
                        Debug.LogWarning(Misc.debugLogText + "Config file not found!");
                    }
                    else
                    {
                        // if it's made it this far, just load it
                        fileExists = true;
                        Debug.Log(Misc.debugLogText + "Config loaded! loaded from " + filePath);
                        loadedConfig = JsonUtility.FromJson<VisualConfig>(configStr);
                        loadedConfig.loadedFrom = filePath;
                    }
                }
            }
            // make sure test value is always 0 unless specified by calibrator
            // inverted viewcone is handled separately now, so just take the abs of it
            loadedConfig.viewCone.Value = Mathf.Abs(loadedConfig.viewCone.Value);

            // note: instance static ref is legacy
            instance = loadedConfig;

            return fileExists || eepromFound;
        }


        public static bool LoadVisualFromSpecificPath(out VisualConfig loadedConfig, string path, bool immediatelySaveToSerial = true)
        {
            bool fileExists = false;
            string configStr;
            loadedConfig = new VisualConfig();
            if (!File.Exists(path))
            {
                Debug.LogWarning(Misc.debugLogText + "Config file not found!");
            }
            else
            {
                configStr = File.ReadAllText(path);
                if (configStr.IndexOf('{') < 0 || configStr.IndexOf('}') < 0)
                {
                    // if the file exists but is unpopulated by any info, don't try to parse it
                    // this is a bug with jsonUtility that it doesn't know how to handle a fully empty text file >:(
                    Debug.LogWarning(Misc.debugLogText + "Config file not found!");
                }
                else
                {
                    // if it's made it this far, just load it
                    fileExists = true;
                    Debug.Log(Misc.debugLogText + "Config loaded! loaded from " + path);
                    loadedConfig = JsonUtility.FromJson<VisualConfig>(configStr);
                    loadedConfig.loadedFrom = path;
                }
            }
            // make sure test value is always 0 unless specified by calibrator
            // inverted viewcone is handled separately now, so just take the abs of it
            loadedConfig.viewCone.Value = Mathf.Abs(loadedConfig.viewCone.Value);

            // note: instance static ref is legacy
            instance = loadedConfig;

            return fileExists;
        }
    }
}