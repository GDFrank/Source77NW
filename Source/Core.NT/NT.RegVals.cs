// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using Microsoft.Win32;

namespace Source77NW
{
    public static partial class NT
    {
        /// <summary>
        /// Thin wrapper over Win32 registry access for common system
        /// values: ProductName, ReleaseId, a desktop wallpaper folder
        /// helper, and a general GetValue that parses a full
        /// HKEY_xxx\path\key string. Assessed 2026-07-27 against
        /// NT.MediaDrive (pure WMI, no registry dependency) and the
        /// restored NT-PutInfoSys (keeps its own inline
        /// Registry.LocalMachine calls) - neither calls into RegVals, so
        /// every member here is curated as a standalone general-purpose
        /// surface rather than for a specific caller. Best-effort: a
        /// missing key/value yields the caller's default or null, never
        /// a throw.
        /// </summary>
        public static class RegVals
        {
            /// <summary>Registry.LocalMachine (HKEY_LOCAL_MACHINE).</summary>
            public static RegistryKey Key_LocalMachine => Registry.LocalMachine;

            /// <summary>The current user's desktop wallpaper path from HKCU\Control Panel\Desktop; null if the key or value is absent.</summary>
            public static string DesktopWallPaperFolder()
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    return key?.GetValue("Wallpaper") as string;
                }
            }

            /// <summary>The OS ProductName from HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion; empty string if absent.</summary>
            public static string ProductName()
            {
                return GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion", "ProductName", "");
            }

            /// <summary>The OS ReleaseId from HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion; empty string if absent.</summary>
            public static string ReleaseId()
            {
                return GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "");
            }

            /// <summary>True when Windows long-path support (LongPathsEnabled) is on, per HKLM\SYSTEM\CurrentControlSet\Control\FileSystem; false if the key/value is absent or off.</summary>
            public static bool LongPathsEnabled()
            {
                const string keyPath = @"SYSTEM\CurrentControlSet\Control\FileSystem";
                const string valueName = "LongPathsEnabled";
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue(valueName);
                    if (value is int intValue) return intValue == 1;
                    return false;
                }
            }

            /// <summary>Reads Field from the key at RegistryPath (a full "HKEY_xxx\sub\path" string; HKEY_CLASSES_ROOT/HKEY_CURRENT_USER/HKEY_LOCAL_MACHINE/HKEY_USERS/HKEY_CURRENT_CONFIG recognized, any case); DefaultValue on any failure - unrecognized hive, missing subkey/value, or exception (soft, never throws).</summary>
            public static string GetValue(string RegistryPath, string Field, string DefaultValue)
            {
                string rtn = DefaultValue;

                try
                {
                    string[] split_result = RegistryPath.Split('\\');

                    if (split_result.Length > 0)
                    {
                        split_result[0] = split_result[0].ToUpper();

                        RegistryKey OurKey = null;

                        if (split_result[0] == "HKEY_CLASSES_ROOT") OurKey = Registry.ClassesRoot;
                        else if (split_result[0] == "HKEY_CURRENT_USER") OurKey = Registry.CurrentUser;
                        else if (split_result[0] == "HKEY_LOCAL_MACHINE") OurKey = Registry.LocalMachine;
                        else if (split_result[0] == "HKEY_USERS") OurKey = Registry.Users;
                        else if (split_result[0] == "HKEY_CURRENT_CONFIG") OurKey = Registry.CurrentConfig;

                        if (OurKey != null)
                        {
                            string newRegistryPath = string.Join(@"\", split_result, 1, split_result.Length - 1);

                            if (newRegistryPath != "")
                            {
                                using (RegistryKey subKey = OurKey.OpenSubKey(newRegistryPath))
                                {
                                    if (subKey != null)
                                    {
                                        rtn = (string)subKey.GetValue(Field, DefaultValue);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                return rtn;
            }
        }
    }
}
