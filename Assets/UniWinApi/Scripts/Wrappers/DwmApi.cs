/**
 * DWM API wrapper
 * 
 * License: CC0, https://creativecommons.org/publicdomain/zero/1.0/
 * 
 * Author: Kirurobo, http://twitter.com/kirurobo
 * Author: Ru--en, http://twitter.com/ru__en
 * Reference: Ron Fosner, http://msdn.microsoft.com/ja-jp/magazine/cc163435.aspx
 */
using System;
using System.Runtime.InteropServices;

namespace Kirurobo
{
    /// <summary>
    /// A wrapper of Desktop Window Manager (DWM) API dwmapi.h
    /// </summary>
    class DwmApi
    {
        #region Some additional structures
        /// <summary>
        /// RECT structure defined in windef.h
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }
        }

        /// <summary>
        /// MARGINS structure defined in uxtheme.h
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;

            public MARGINS(int left, int top, int right, int bottom)
            {
                cxLeftWidth = left;
                cyTopHeight = top;
                cxRightWidth = right;
                cyBottomHeight = bottom;
            }
        }
        #endregion

        #region APIs defined in dwmapi.h
        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_BLURBEHIND
        {
            public uint dwFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fEnable;
            public RECT? hRgnBlur;
            [MarshalAs(UnmanagedType.Bool)] public bool fTransitionOnMaximized;

            // Blur behind flags.
            public const uint DWM_BB_ENABLE = 0x00000001;
            public const uint DWM_BB_BLURREGION = 0x00000002;
            public const uint DWM_BB_TRANSITIONONMAXIMIZED = 0x00000004;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public uint dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)] public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly;

            // Thumbnail property flags.
            public const uint DWM_TNP_RECTDESTINATION = 0x00000001;
            public const uint DWM_TNP_RECTSOURCE = 0x00000002;
            public const uint DWM_TNP_OPACITY = 0x00000004;
            public const uint DWM_TNP_VISIBLE = 0x00000008;
            public const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;
        }

        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_NCRENDERING_ENABLED = 1,
            DWMWA_NCRENDERING_POLICY = 2,
            DWMWA_TRANSITIONS_FORCEDISABLED = 3,
            DWMWA_ALLOW_NCPAINT = 4,
            DWMWA_CAPTION_BUTTON_BOUNDS = 5,
            DWMWA_NONCLIENT_RTL_LAYOUT = 6,
            DWMWA_FORCE_ICONIC_REPRESENTATION = 7,
            DWMWA_FLIP3D_POLICY = 8,
            DWMWA_EXTENDED_FRAME_BOUNDS = 9,
            DWMWA_HAS_ICONIC_BITMAP = 10,
            DWMWA_DISALLOW_PEEK = 11,
            DWMWA_EXCLUDED_FROM_PEEK = 12,
            DWMWA_CLOAK = 13,
            DWMWA_CLOAKED = 14,
            DWMWA_FREEZE_REPRESENTATION = 15,
            DWMWA_PASSIVE_UPDATE_MODE = 16,
            DWMWA_LAST = 17,
        }

        public enum DWMNCRENDERINGPOLICY
        {
            DWMNCRP_USEWINDOWSTYLE = 0,
            DWMNCRP_DISABLED = 1,
            DWMNCRP_ENABLED = 2,
            DWMNCRP_LAST = 3,
        }
        
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmEnableBlurBehindWindow(IntPtr hWnd, DWM_BLURBEHIND pBlurBehind);
        
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern bool DwmIsCompositionEnabled();
        
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmEnableComposition(bool bEnable);
        
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmGetColorizationColor(
            out int pcrColorization,
            [MarshalAs(UnmanagedType.Bool)]out bool pfOpaqueBlend
        );
        
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern IntPtr DwmRegisterThumbnail(IntPtr dest, IntPtr source);
        
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmUnregisterThumbnail(IntPtr hThumbnail);
        
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmUpdateThumbnailProperties(IntPtr hThumbnail, DWM_THUMBNAIL_PROPERTIES props);

        /// <summary>
        /// Apply glass effect to specified margins
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="pMargins"></param>
        /// <returns></returns>
        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMargins);

        #endregion

        #region Original method
        /// <summary>
        /// Apply glass effect to all client area
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static void DwmExtendIntoClientAll(IntPtr hWnd)
        {
            MARGINS margins = new MARGINS(-1, -1, -1, -1);
            DwmExtendFrameIntoClientArea(hWnd, ref margins);
        }
        #endregion
    }
}