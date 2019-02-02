#if !EEPROM_DISABLED
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Reflection;

namespace HoloPlay
{
    public class EEPROMCalibration
    {
        private static bool debugprints = false;
        private static bool debugdump = false;
        private const int USB_VID = 0x04d8;
        private const int USB_PID = 0xef7e;
        /* DO NOT CHANGE ANY OF THE FOLLOWING VALUES!*/
        private const byte pagelen = 64;
        private const byte addrlen = 3;
        private const byte sendlen = pagelen + addrlen + 1;
        private const byte recvlen = pagelen + addrlen + 1;
        private const byte max_addr = 1024 / pagelen;
        private const uint EEPROM_START_PAGE = 0x0;
        public static bool led_brightness_locked = false;
        private static byte[] crc7_table = {
    0x00, 0x12, 0x24, 0x36, 0x48, 0x5a, 0x6c, 0x7e,
    0x90, 0x82, 0xb4, 0xa6, 0xd8, 0xca, 0xfc, 0xee,
    0x32, 0x20, 0x16, 0x04, 0x7a, 0x68, 0x5e, 0x4c,
    0xa2, 0xb0, 0x86, 0x94, 0xea, 0xf8, 0xce, 0xdc,
    0x64, 0x76, 0x40, 0x52, 0x2c, 0x3e, 0x08, 0x1a,
    0xf4, 0xe6, 0xd0, 0xc2, 0xbc, 0xae, 0x98, 0x8a,
    0x56, 0x44, 0x72, 0x60, 0x1e, 0x0c, 0x3a, 0x28,
    0xc6, 0xd4, 0xe2, 0xf0, 0x8e, 0x9c, 0xaa, 0xb8,
    0xc8, 0xda, 0xec, 0xfe, 0x80, 0x92, 0xa4, 0xb6,
    0x58, 0x4a, 0x7c, 0x6e, 0x10, 0x02, 0x34, 0x26,
    0xfa, 0xe8, 0xde, 0xcc, 0xb2, 0xa0, 0x96, 0x84,
    0x6a, 0x78, 0x4e, 0x5c, 0x22, 0x30, 0x06, 0x14,
    0xac, 0xbe, 0x88, 0x9a, 0xe4, 0xf6, 0xc0, 0xd2,
    0x3c, 0x2e, 0x18, 0x0a, 0x74, 0x66, 0x50, 0x42,
    0x9e, 0x8c, 0xba, 0xa8, 0xd6, 0xc4, 0xf2, 0xe0,
    0x0e, 0x1c, 0x2a, 0x38, 0x46, 0x54, 0x62, 0x70,
    0x82, 0x90, 0xa6, 0xb4, 0xca, 0xd8, 0xee, 0xfc,
    0x12, 0x00, 0x36, 0x24, 0x5a, 0x48, 0x7e, 0x6c,
    0xb0, 0xa2, 0x94, 0x86, 0xf8, 0xea, 0xdc, 0xce,
    0x20, 0x32, 0x04, 0x16, 0x68, 0x7a, 0x4c, 0x5e,
    0xe6, 0xf4, 0xc2, 0xd0, 0xae, 0xbc, 0x8a, 0x98,
    0x76, 0x64, 0x52, 0x40, 0x3e, 0x2c, 0x1a, 0x08,
    0xd4, 0xc6, 0xf0, 0xe2, 0x9c, 0x8e, 0xb8, 0xaa,
    0x44, 0x56, 0x60, 0x72, 0x0c, 0x1e, 0x28, 0x3a,
    0x4a, 0x58, 0x6e, 0x7c, 0x02, 0x10, 0x26, 0x34,
    0xda, 0xc8, 0xfe, 0xec, 0x92, 0x80, 0xb6, 0xa4,
    0x78, 0x6a, 0x5c, 0x4e, 0x30, 0x22, 0x14, 0x06,
    0xe8, 0xfa, 0xcc, 0xde, 0xa0, 0xb2, 0x84, 0x96,
    0x2e, 0x3c, 0x0a, 0x18, 0x66, 0x74, 0x42, 0x50,
    0xbe, 0xac, 0x9a, 0x88, 0xf6, 0xe4, 0xd2, 0xc0,
    0x1c, 0x0e, 0x38, 0x2a, 0x54, 0x46, 0x70, 0x62,
    0x8c, 0x9e, 0xa8, 0xba, 0xc4, 0xd6, 0xe0, 0xf2
        };
        public static void PrintConfig(Config.VisualConfig h)
        {
            String s = "";
            FieldInfo[] fields = h.GetType().GetFields();

            foreach (FieldInfo field in fields)
            {
                if (field != null)
                {
                    object val = field.GetValue(h);
                    if (val is float)
                    {
                        s += String.Format("{0:G}f ", (float)val);
                    }
                    else if (val is Config.ConfigValue)
                    {
                        Config.ConfigValue val_ = (Config.ConfigValue)val;
                        if (val_.isInt)
                        {
                            s += String.Format("{0:D}i ", (Int16)val_);
                        }
                        else
                        {
                            s += String.Format("{0:G}f ", (float)val_);
                        }
                    }
                }
            }
            print(s);
        }

        private static byte[] SerializeHoloPlayConfig(Config.VisualConfig hpc)
        {
            List<byte> byte_out = new List<byte>();
            FieldInfo[] fields = hpc.GetType().GetFields();
            foreach (FieldInfo field in fields)
            {
                if (field != null)
                {
                    object val = field.GetValue(hpc);
                    if (val is float)
                    {
                        byte[] bytes = BitConverter.GetBytes((float)val);
                        foreach (byte b in bytes) byte_out.Add(b);
                    }
                    else if (val is Config.ConfigValue)
                    {
                        Config.ConfigValue val_ = (Config.ConfigValue)val;
                        byte[] bytes;
                        if (val_.isInt)
                        {
                            bytes = BitConverter.GetBytes((Int16)val_);
                        }
                        else
                        {
                            bytes = BitConverter.GetBytes((float)val_);
                        }
                        foreach (byte b in bytes) byte_out.Add(b);
                    }
                }
            }
            return byte_out.ToArray();
        }
        private static int BytesInHPC(Config.VisualConfig hpc)
        {
            int bytes_in_hpc_ = 0;
            int intlen = sizeof(Int16);
            int floatlen = sizeof(float);
            FieldInfo[] fields = hpc.GetType().GetFields();
            foreach (FieldInfo field in fields)
            {
                if (field != null)
                {
                    object val = field.GetValue(hpc);
                    if (val is float)
                    {
                        bytes_in_hpc_ += floatlen;
                    }
                    else if (val is Config.ConfigValue)
                    {
                        Config.ConfigValue val_ = (Config.ConfigValue)val;
                        if (val_.isInt)
                        {
                            bytes_in_hpc_ += intlen;
                        }
                        else
                        {
                            bytes_in_hpc_ += floatlen;
                        }
                    }
                }
            }
            return bytes_in_hpc_;
        }

        private static Config.VisualConfig DeserializeHoloPlayConfig(byte[] byte_in)
        {
            Config.VisualConfig hpc = new Config.VisualConfig();
            if (byte_in.Length != BytesInHPC(hpc))
            {
                printerr("HoloPlayConfig length mismatch! Aborting...");
                return null;
            }
            int intlen = sizeof(Int16);
            int floatlen = sizeof(float);
            int ind = 0;
            FieldInfo[] fields = hpc.GetType().GetFields();
            foreach (FieldInfo field in fields)
            {
                if (field != null)
                {
                    object val = field.GetValue(hpc);
                    if (val is float)
                    {
                        field.SetValue(hpc, BitConverter.ToSingle(byte_in, ind));
                        ind += floatlen;
                    }
                    else if (val is Config.ConfigValue)
                    {
                        Config.ConfigValue val_ = (Config.ConfigValue)val;
                        if (val_.isInt)
                        {
                            val_.Value = BitConverter.ToInt16(byte_in, ind);
                            ind += intlen;
                        }
                        else
                        {
                            val_.Value = BitConverter.ToSingle(byte_in, ind);
                            ind += floatlen;
                        }
                    }
                }
            }
            return hpc;
        }

        public static string LoadConfigFromEEPROM()
        {
            byte[] first_page = new byte[pagelen];
            int err = rw(EEPROM_START_PAGE, pagelen, first_page, true);
            if (err != 0)
            {
                printerr(String.Format("Error {0:D}: HoloPlay Config could not be loaded from EEPROM! Using default HoloPlay Config.", -1 * err));
                return "";
            }
            if (first_page[0] == (byte)0xff && first_page[1] == (byte)0xff && first_page[2] == (byte)0xff)
            {
                err = -6;
                printerr(String.Format("Error {0:D}: Flash memory does not contain Holoplay Config!", -1 * err));
                return "";
            }
            UInt32 jsonlength = (UInt32)((UInt32)(first_page[0] << 24) | ((UInt32)(first_page[1] << 16)) | ((UInt32)(first_page[2] << 8)) | (UInt32)(first_page[3]));
            byte[] out_buf = new byte[jsonlength];
            for (int i = 4; (i < first_page.Length && i < jsonlength); ++i)
            {
                out_buf[i - 4] = first_page[i];
            }
            if (jsonlength > pagelen)
            {
                byte[] tail_buf = new byte[(int)jsonlength - (pagelen - 4)];
                err = rw(EEPROM_START_PAGE + 1, tail_buf.Length, tail_buf, true, false);
                if (err != 0)
                {
                    printerr(String.Format("Error {0:D}: HoloPlay Config could not be loaded from EEPROM! Using default HoloPlay Config.", -1 * err));
                    return "";
                }
                for (int i = 0; i < tail_buf.Length; ++i)
                {
                    out_buf[pagelen + i - 4] = tail_buf[i];
                }
            }
            return System.Text.Encoding.UTF8.GetString(out_buf);
        }
        private static void print(String s, bool err = false)
        {
            if (debugprints) Debug.Log(s);
        }
        private static void printerr(String s)
        {
            if (debugprints) Debug.LogError(s);
        }
        public static int WriteConfigToEEPROM(Config.VisualConfig hpc)
        {
            print("Writing config to EEPROM, please wait...");
            PrintConfig(hpc);
            byte[] JSONString = Encoding.ASCII.GetBytes(JsonUtility.ToJson(hpc, false));
            UInt32 jsonlength = (UInt32)JSONString.Length;
            byte[] out_buf = new byte[jsonlength + 4];
            out_buf[0] = (byte)(jsonlength >> 24);
            out_buf[1] = (byte)(jsonlength >> 16);
            out_buf[2] = (byte)(jsonlength >> 8);
            out_buf[3] = (byte)(jsonlength);
            print(string.Format("JSON length: {0:D}", jsonlength));
            for (int i = 0; i < jsonlength; ++i) out_buf[i + 4] = JSONString[i];
            int err = rw(EEPROM_START_PAGE, out_buf.Length, out_buf, false);
            if (err != 0)
            {
                printerr(String.Format("Error {0:D}: HoloPlay Config could not be written to EEPROM! Using default HoloPlay Config.", -1 * err));
                return err;
            }
            PrintArray(out_buf, (uint)out_buf.Length, 0, format: "out: {0:S}");
            print("Config successfully written to EEPROM!");
            return err;
        }
        public static int SetLEDBrightness(float brightness_)
        {
            if (led_brightness_locked) {
                return -4;
            }
            if (brightness_ > 1f) return -5;
            HIDapi.hid_init();
            byte brightness = (byte)(brightness_ * 255);
            IntPtr ptr = HIDapi.hid_enumerate(USB_VID, USB_PID);
            if (ptr == IntPtr.Zero)
            {
                HIDapi.hid_free_enumeration(ptr);
                HIDapi.hid_exit();
                return -4;
            }
            hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));
            IntPtr handle = HIDapi.hid_open_path(enumerate.path);
            HIDapi.hid_set_nonblocking(handle, 1);
            HIDapi.hid_free_enumeration(ptr);
            byte[] r = new byte[recvlen];
            byte[] s = new byte[sendlen];
            for (int i = 0; i < sendlen; ++i) s[i] = 0;
            s[1] = (byte)0x10;
            s[2] = brightness;
            int res = 0;
            int err = 0;
            res = HIDapi.hid_send_feature_report(handle, s, new UIntPtr(sendlen));
            if (res < 1)
            {
                err = -1;
            }
            else
            {
                res = HIDapi.hid_read_timeout(handle, r, new UIntPtr(recvlen), 1000);
                if (res < 1)
                {
                    err = -2;
                }
                if (r[1] != s[1] || r[2] != s[2] || r[3] != s[3])
                {
                    led_brightness_locked = true;
                    err = -3;
                }
                if (r[4] == 0xff){
                    led_brightness_locked = true;
                    err = -4;
                }
            }
            HIDapi.hid_close(handle);
            HIDapi.hid_exit();
            return err;
        }
        private static int rw(uint start_page, int bytes, byte[] in_buf, bool read = true, bool eeprom_write = false)
        {
            if (eeprom_write) read = true;
            bool connect_success = true;
            HIDapi.hid_init();
            IntPtr ptr = HIDapi.hid_enumerate(USB_VID, USB_PID);
            if (ptr == IntPtr.Zero)
            {
                HIDapi.hid_free_enumeration(ptr);
                HIDapi.hid_exit();
                connect_success = false;
                return -4;
            }
            hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));
            IntPtr handle = HIDapi.hid_open_path(enumerate.path);
            HIDapi.hid_set_nonblocking(handle, 1);
            HIDapi.hid_free_enumeration(ptr);
            if (!connect_success)
            {
                HIDapi.hid_close(handle);
                HIDapi.hid_exit();
                return -4;
            }
            int numPages = bytes / pagelen;
            if (bytes % pagelen != 0) ++numPages;
            int in_buf_ind = 0;
            byte[] r = new byte[recvlen];
            byte[] s = new byte[sendlen];
            uint i = 0;
            for (i = start_page; i < start_page + numPages; ++i)
            {
                s[0] = 0;
                // pack address bytes in big endian
                s[1] = 0;
                s[2] = (byte)((i >> 8) & 0xff);
                s[3] = (byte)(i & 0xff);
                byte crc = 0;
                string formatstr;
                if (!read | eeprom_write)
                {
                    for (int j = addrlen + 1; j < sendlen; ++j)
                    {
                        if (in_buf_ind < bytes)
                        {
                            s[j] = in_buf[in_buf_ind];
                            ++in_buf_ind;
                        }
                        crc = crc7_table[crc ^ s[j]];
                    }
                    crc >>= 1;
                    s[1] = (byte)0x80;
                    s[1] |= crc;
                }
                if (eeprom_write)
                {
                    s[1] = (byte)0x20;
                }
                if (debugdump)
                {
                    formatstr = "sent page: {0:S}";
                    PrintArray(s, format: formatstr);
                }
                int res = 0;
                int err = 0;
                res = HIDapi.hid_send_feature_report(handle, s, new UIntPtr(sendlen));
                if (res < 1)
                {
                    err = -1;
                }
                else
                {
                    res = HIDapi.hid_read_timeout(handle, r, new UIntPtr(recvlen), 1000);
                    if (res < 1)
                    {
                        err = -2;
                    }
                    if (r[1] != s[1] || r[2] != s[2] || r[3] != s[3])
                    {
                        err = -3;
                    }
                }
                if (err == 0)
                {
                    if (read)
                    {
                        for (int j = addrlen + 1; j < recvlen; ++j)
                        {
                            if (in_buf_ind < bytes)
                            {
                                in_buf[in_buf_ind] = r[j];
                                ++in_buf_ind;
                            }
                        }
                    }
                    if (debugdump)
                    {
                        formatstr = string.Concat(String.Format("page {0:D}, read: {1:B}, err: {2:D}, data: ", i, read, err), "{0:S}");
                        PrintArray(r, format: formatstr);
                    }
                }
                else
                {
                    if (debugdump)
                    {
                        print("error reading");
                        print(err.ToString());
                    }

                    HIDapi.hid_close(handle);
                    HIDapi.hid_exit();
                    return err;
                }
                formatstr = string.Concat(String.Format("page {0:D}, read: {1:B}, data: ", i, read), "{0:S}");
                PrintArray(r, format: formatstr);
            }
            // tell device to copy flash memory out of buffer
            if (!read && !eeprom_write)
            {
                s[1] = 0x40;
                s[2] = (byte)((start_page >> 8) & 0xff);
                s[3] = (byte)(start_page & 0xff);
                s[4] = (byte)((i >> 8) & 0xff);
                s[5] = (byte)(i & 0xff);
                int res = HIDapi.hid_send_feature_report(handle, s, new UIntPtr(sendlen));
                if (res < 1)
                {
                    HIDapi.hid_close(handle);
                    HIDapi.hid_exit();
                    return -2;
                }
                res = HIDapi.hid_read_timeout(handle, r, new UIntPtr(recvlen), 1000);
                if (res < 1)
                {
                    HIDapi.hid_close(handle);
                    HIDapi.hid_exit();
                    return -2;
                }
                if (r[1] != s[1] || r[2] != s[2] || r[3] != s[3])
                {
                    HIDapi.hid_close(handle);
                    HIDapi.hid_exit();
                    return -3;
                }
            }
            HIDapi.hid_close(handle);
            HIDapi.hid_exit();

            return 0;
        }
        private static void PrintArray<T>(T[] arr, uint len = 0, uint start = 0, string format = "{0:S}")
        {
            if (len == 0) len = (uint)arr.Length;
            string tostr = "";
            for (int i = 0; i < len; ++i)
            {
                tostr += string.Format((arr[0] is byte) ? "{0:X2} " : ((arr[0] is float) ? "{0:G} " : "{0:D} "), arr[i + start]);
            }
            print(string.Format(format, tostr));
        }

    }
}
#endif
