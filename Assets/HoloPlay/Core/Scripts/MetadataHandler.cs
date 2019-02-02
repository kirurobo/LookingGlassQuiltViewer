using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;


namespace HoloPlay
{
    public class Metadata
    {
        public Metadata(int _xViews, int _yViews, float _pixelAspect, float _fps)
        {
            xViews = _xViews;
            yViews = _yViews;
            pixelAspect = _pixelAspect;
            fps = _fps;
        }

        public readonly int xViews;
        public readonly int yViews;
        public readonly float pixelAspect;
        public readonly float fps;

       // public readonly string renderer;
       // public readonly string movie;
    }

    public class MetadataHandler : MonoBehaviour
    {
        public string testFilePath;
        public bool testOnEnable = true;

        public UInt32 endsSearchBytes = 2000;

        public Metadata metadata = null;

        public enum FileIO
        {
            OK = 1,
            NO_FILE = -1,
            NO_METADATA = -2
        }

        //endsSearchBytes = if the file is large, the algorithm searches the last x bytes and the starting x bytes, don't bother with the meat of the file
        //if you don't want to use the optimization set the value to an extremely high amount
        public FileIO getMetadataFromFile(string filePath, out string metadata, UInt32 endsSearchBytes = 2000)
        {
            metadata = "";
            if (!File.Exists(filePath))
            {
                return FileIO.NO_FILE;
            }

            string fileContents = File.ReadAllText(filePath);  //TODO optimize this so that it just reads a chunk of the start and chunk of the end to try.  the middle is really a waste, we know the metadata is not there.

            FileIO returnVal = (FileIO)(-999);
            if (fileContents.Length < endsSearchBytes * 2)
                returnVal = getMetadataFromText(fileContents, out metadata); //the file is small, just search it whole
            else
            {
                returnVal = getMetadataFromText(fileContents.Substring(fileContents.Length - (int)endsSearchBytes, (int)endsSearchBytes), out metadata); //search the end of the file, first
                if (returnVal == FileIO.NO_METADATA)
                {
                    returnVal = getMetadataFromText(fileContents.Substring(0, (int)endsSearchBytes), out metadata); //search the start of the file
                }
            }

            return returnVal;
        }

        public FileIO getMetadataFromText(string text, out string metadata)
        {
            string pattern = @"(?<=\<lkg\>)(.*)(?=\<\/lkg\>)";
            Regex rgx = new Regex(pattern, RegexOptions.None);

            MatchCollection matches = rgx.Matches(text);
            if (matches.Count > 0)
            {
                metadata = "<lkg>" + matches[0].Value + "</lkg>"; //add back a root, so the parser can understand.
                return FileIO.OK;
            }

            metadata = "";
            return FileIO.NO_METADATA;
        }

        public void parseMetadataXML(string xml)
        {
            XmlDocument xdoc = new XmlDocument();
            xdoc.LoadXml(xml);

            XmlNodeList elemList = xdoc.GetElementsByTagName("properties");

            int x = 4; //defaults
            int y = 8;
            float aspect = 1f;
            float fps = 30;

            if (elemList[0].Attributes != null)
            {
                if (elemList[0].Attributes["vX"] != null)
                    x = StringToInt(elemList[0].Attributes["vX"].Value, x);
                if (elemList[0].Attributes["vY"] != null)
                    y = StringToInt(elemList[0].Attributes["vY"].Value, y);
                if (elemList[0].Attributes["pixelAspect"] != null)
                    aspect = StringToFloat(elemList[0].Attributes["pixelAspect"].Value, aspect);
                if (elemList[0].Attributes["fps"] != null)
                    fps = StringToFloat(elemList[0].Attributes["fps"].Value, fps);

                //limits
                if (x < 1)
                    x = 1;
                if (y < 1)
                    y = 1;
                if (aspect < .01f)
                    aspect = .01f;
                if (fps < .000001f)
                    fps = .000001f;
            }

            metadata = new Metadata(x, y, aspect, fps);
        }

        public static int StringToInt(string strVal, int defaultVal)
        {
            int output;
            if (System.Int32.TryParse(strVal, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out output))
                return output;

            return defaultVal;
        }
        public static float StringToFloat(string strVal, float defaultVal)
        {
            float output;
            if (System.Single.TryParse(strVal, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out output))
                return output;

            return defaultVal;
        }



        //testing...
        private void OnEnable()
        {
            if (testOnEnable)
                testMetadata();
        }
        public void testMetadata()
        {
            string metadata;

            FileIO response = getMetadataFromFile(testFilePath, out metadata);
            if (response > 0)
            {
                Debug.Log(metadata);
                parseMetadataXML(metadata);
            }
            else
                Debug.LogWarning("Could not read metadata: " + response.ToString());
        }
    }
}