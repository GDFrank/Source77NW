// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Source77NW
{
    // NT-PutInfoSys - system-information reporting as direct NT functions.
    // Curated 2026-07-26 from AppLab 3.Core\NT-PutInfoSys.cs, made
    // self-contained for Source77NW.NT; WMI fidelity restored 2026-07-27
    // once NT.Mgmt/NT.MediaDrive existed to curate against; class renamed
    // from Sys back to NT 2026-07-30 (see NT.cs remarks):
    //   - OS identity: registry CurrentVersion stays primary (ProductName,
    //     DisplayVersion, BuildNumber, EditionID); ProductType is WMI
    //     (Win32_OperatingSystem, best-effort via NT.Mgmt).
    //   - Memory totals: kernel32 GlobalMemoryStatusEx (bytes) - NOT
    //     reverted to the AppLab WMI figures (which were KB).
    //   - Processor: registry CentralProcessor\0 (Name/Model/Vendor/
    //     RatedMHz) stays the always-available baseline; PhysicalCores/
    //     ProcessorId/Manufacturer are WMI (Win32_Processor, best-effort
    //     via NT.Mgmt, first socket only).
    //   - RegVals did not travel: Registry.LocalMachine used inline
    //     (NT.RegVals exists as a general surface but nothing here
    //     calls into it - see NT.RegVals's own curation note).
    //   - Drives: DriveInfo summary (Format/TotalSize/FreeSpace) kept
    //     from the self-contained curation, PLUS the MediaDrive hardware
    //     block (serial, bus, media type, ...) restored via
    //     NT.MediaDrive.FromDriveLetter + SaveAsLsvFormat.
    //   - UT.PutDictionarySorted did not travel: private
    //     _PutDictionarySorted stands in.
    //   - PutInfoFonts / _Fonts / _FontFamilies did NOT travel
    //     (System.Drawing dependency) - still the only gap; UI-flavor
    //     candidate.
    public static partial class NT
    {
        /// <summary>
        /// Writes a full system-information report to theWriter: OS/system
        /// identity, processor, motherboard/BIOS, drives, installed .NET
        /// Framework versions, and environment variables - one blank line
        /// between sections. Each PutInfoSys_* section is also
        /// independently callable. Registry/OS-dependent values are
        /// best-effort: unavailable sections write little or nothing
        /// rather than throw.
        /// </summary>
        public static void PutInfoSys(TextWriter theWriter)
        {
            PutInfoSys_System(theWriter);
            theWriter.WriteLine();

            PutInfoSys_Processor(theWriter);
            theWriter.WriteLine();

            PutInfoSys_Motherboard(theWriter);
            theWriter.WriteLine();

            PutInfoSys_Drives(theWriter);
            theWriter.WriteLine();

            PutInfoSys_DOTNET(theWriter);
            theWriter.WriteLine();

            PutInfoSys_EnvVars(theWriter);
            theWriter.WriteLine();
        }

        /// <summary>Writes the System section: machine/user identity, OS identity (registry CurrentVersion: ProductName, DisplayVersion, BuildNumber, EditionID; ProductType via WMI, best-effort), bitness, page size, culture, CLR version, system paths, and physical/virtual memory totals (kernel32, bytes).</summary>
        public static void PutInfoSys_System(TextWriter theWriter)
        {
            string sProductName = null;
            string sDisplayVersion = null;
            string sBuildNumber = null;
            string sEditionID = null;

            try
            {
                using (RegistryKey xKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (xKey != null)
                    {
                        sProductName    = xKey.GetValue("ProductName")?.ToString();
                        sDisplayVersion = xKey.GetValue("DisplayVersion")?.ToString();
                        sBuildNumber    = xKey.GetValue("CurrentBuildNumber")?.ToString();
                        sEditionID      = xKey.GetValue("EditionID")?.ToString();
                    }
                }
            }
            catch { }

            // WMI (best-effort): Win32_OperatingSystem.ProductType (1=Workstation,
            // 2=Domain Controller, 3=Server) - not available from the registry,
            // registry stays primary for everything else in this section.
            string sProductType = null;

            if (Mgmt.GotSearcherType(out Type xOsType))
            {
                try
                {
                    using (dynamic searcher = Mgmt.CreateSearcher(xOsType, null, "SELECT ProductType FROM Win32_OperatingSystem"))
                    using (var xResults = searcher.Get())
                    {
                        foreach (var xResult in xResults)
                        {
                            sProductType = xResult["ProductType"]?.ToString();
                            break; // only one instance exists
                        }
                    }
                }
                catch { }
            }

            theWriter.WriteLine("System:");
            theWriter.WriteLine("MachineName: "    + AS.Quoted(Environment.MachineName));
            theWriter.WriteLine("UserDomainName: " + AS.Quoted(Environment.UserDomainName));
            theWriter.WriteLine("UserName: "       + AS.Quoted(Environment.UserName));

            theWriter.WriteLine("ProductName: "    + AS.Quoted(sProductName));
            theWriter.WriteLine("Version: "        + AS.Quoted(sDisplayVersion));
            theWriter.WriteLine("BuildNumber: "    + AS.Quoted(sBuildNumber));
            theWriter.WriteLine("EditionID: "      + AS.Quoted(sEditionID));
            theWriter.WriteLine("ProductType: "    + AS.Quoted(sProductType));

            theWriter.WriteLine("Is64BitOS: "      + Environment.Is64BitOperatingSystem);

            theWriter.WriteLine("PageSize: "       + Environment.SystemPageSize);
            theWriter.WriteLine("CultureName: "    + CultureInfo.InstalledUICulture.Parent.Name);
            theWriter.WriteLine("CommonLanguageRTVersion: " + Environment.Version);
            theWriter.WriteLine("IsRightToLeft: "  + CultureInfo.InstalledUICulture.TextInfo.IsRightToLeft);
            theWriter.WriteLine("SystemDirectory: " + Environment.SystemDirectory);
            theWriter.WriteLine("SystemDrive: "    + Environment.GetEnvironmentVariable("SystemDrive"));

            string s = Environment.GetEnvironmentVariable("DataDrive");
            if (!string.IsNullOrEmpty(s))
                theWriter.WriteLine("DataDrive: " + s);

            // kernel32 memory totals (bytes) - replaces the AppLab WMI
            // TotalVisibleMemorySize/TotalVirtualMemorySize (which were KB).
            // ullTotalPageFile = the commit limit, the WMI "virtual" analog.
            if (_GotMemoryStatus(out MEMORYSTATUSEX vMem))
            {
                theWriter.WriteLine("VirtualMemory: " + vMem.ullTotalPageFile.ToString()
                    + AS.SP + AS.PAR1 + AS.Format_KB_MB_GB_TB_PB(vMem.ullTotalPageFile, true) + AS.PAR2);
                theWriter.WriteLine("PhysicalMemory: " + vMem.ullTotalPhys.ToString()
                    + AS.SP + AS.PAR1 + AS.Format_KB_MB_GB_TB_PB(vMem.ullTotalPhys, true) + AS.PAR2);
            }
        }

        /// <summary>Writes the Processor section: Environment.ProcessorCount as the logical count, then WMI (Win32_Processor: PhysicalCores/ProcessorId/Manufacturer, best-effort via Sys.Mgmt, first socket only), then the registry CentralProcessor\0 baseline (name, model identifier, vendor, rated MHz - always attempted regardless of WMI success).</summary>
        public static void PutInfoSys_Processor(TextWriter theWriter)
        {
            theWriter.WriteLine("Processor:");
            theWriter.WriteLine("LogicalProcessors: " + Environment.ProcessorCount);

            // WMI (best-effort): physical core count, processor id, and
            // manufacturer - WMI-only in AppLab and dropped by the initial
            // self-contained curation; restored 2026-07-27 via Sys.Mgmt.
            // First socket only (matches the single CentralProcessor\0
            // registry baseline below - multi-socket systems only get
            // socket 0 detail from either source).
            if (Mgmt.GotSearcherType(out Type xCpuType))
            {
                try
                {
                    using (dynamic searcher = Mgmt.CreateSearcher(xCpuType, null, "SELECT NumberOfCores, ProcessorId, Manufacturer FROM Win32_Processor"))
                    using (var xResults = searcher.Get())
                    {
                        foreach (var cpu in xResults)
                        {
                            theWriter.WriteLine("PhysicalCores: " + (cpu["NumberOfCores"]?.ToString() ?? string.Empty));
                            theWriter.WriteLine("ProcessorId: " + (cpu["ProcessorId"]?.ToString() ?? string.Empty));
                            theWriter.WriteLine("Manufacturer: " + (cpu["Manufacturer"]?.ToString() ?? string.Empty));
                            break;
                        }
                    }
                }
                catch { }
            }

            try
            {
                using (RegistryKey xKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (xKey != null)
                    {
                        object xVal; string sVal; string s1;

                        xVal = xKey.GetValue("ProcessorNameString");
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine("Name" + AS.COLON_SP + sVal.Trim());

                        xVal = xKey.GetValue("Identifier");
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine("Model" + AS.COLON_SP + sVal);

                        s1 = "VendorIdentifier";
                        xVal = xKey.GetValue(s1);
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine(s1 + AS.COLON_SP + sVal);

                        xVal = xKey.GetValue("~MHz");
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine("RatedMHz" + AS.COLON_SP + sVal);
                    }
                }
            }
            catch { }
        }

        /// <summary>Writes the Motherboard section from the registry BIOS description keys (product name, BIOS release date/version/vendor, system identifier, multifunction adapters).</summary>
        public static void PutInfoSys_Motherboard(TextWriter theWriter)
        {
            theWriter.WriteLine("Motherboard:");

            const string s_HARDWARE_DESCRIPTION_System_ = @"HARDWARE\DESCRIPTION\System\";
            const string s_Identifier = "Identifier";

            try
            {
                object xVal; string sVal; string sKeySuffix;

                using (RegistryKey xKey = Registry.LocalMachine.OpenSubKey(s_HARDWARE_DESCRIPTION_System_ + "BIOS"))
                {
                    if (xKey != null)
                    {
                        sKeySuffix = "SystemProductName";
                        xVal = xKey.GetValue(sKeySuffix);
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine(sKeySuffix + AS.COLON_SP + sVal);

                        sKeySuffix = "BIOSReleaseDate";
                        xVal = xKey.GetValue(sKeySuffix);
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine(sKeySuffix + AS.COLON_SP + sVal);

                        sKeySuffix = "BIOSVersion";
                        xVal = xKey.GetValue(sKeySuffix);
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine(sKeySuffix + AS.COLON_SP + sVal);

                        sKeySuffix = "BIOSVendor";
                        xVal = xKey.GetValue(sKeySuffix);
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine(sKeySuffix + AS.COLON_SP + sVal);
                    }
                }

                using (RegistryKey xKey = Registry.LocalMachine.OpenSubKey(s_HARDWARE_DESCRIPTION_System_))
                {
                    if (xKey != null)
                    {
                        sKeySuffix = "SystemIdentifier";
                        xVal = xKey.GetValue(s_Identifier);
                        sVal = xVal?.ToString() ?? string.Empty;
                        theWriter.WriteLine(sKeySuffix + AS.COLON_SP + sVal);
                    }
                }

                sKeySuffix = "MultiFunctionAdapter";
                for (int i = 0; i <= 8; i++)
                {
                    using (RegistryKey xKey = Registry.LocalMachine.OpenSubKey(s_HARDWARE_DESCRIPTION_System_ + sKeySuffix + AS.BSLASH + i.ToString()))
                    {
                        if (xKey == null) break;

                        xVal = xKey.GetValue(s_Identifier);
                        if (xVal == null) break;

                        sVal = xVal.ToString();
                        theWriter.WriteLine(sKeySuffix + AS.PAR1 + i.ToString() + AS.PAR2 + AS.EQ + sVal);
                    }
                }
            }
            catch { }
        }

        /// <summary>Writes one Drive block per DriveInfo: name, label, type, format, and total/free sizes, followed (for ready drives) by the MediaDrive hardware block - serial, bus, media type, etc. - as an LSV chain (best-effort WMI via Sys.Mgmt/Sys.MediaDrive; absent WMI leaves the LSV chain header-only).</summary>
        public static void PutInfoSys_Drives(TextWriter theWriter)
        {
            DriveInfo[] xItems = DriveInfo.GetDrives();

            int i = -1;

            while (++i < xItems.Length)
            {
                DriveInfo x = xItems[i];

                string sName = x.Name;

                if (x.IsReady)
                {
                    theWriter.WriteLine("Drive"
                        + AS.SP + sName
                        + AS.SP + x.VolumeLabel
                        + AS.SP + AS.PAR1 + x.DriveType.ToString() + AS.PAR2
                        + AS.COLON
                        );
                    theWriter.WriteLine("Format: " + x.DriveFormat);
                    theWriter.WriteLine("TotalSize: " + x.TotalSize.ToString()
                        + AS.SP + AS.PAR1 + AS.Format_KB_MB_GB_TB_PB((ulong)x.TotalSize, true) + AS.PAR2);
                    theWriter.WriteLine("FreeSpace: " + x.AvailableFreeSpace.ToString()
                        + AS.SP + AS.PAR1 + AS.Format_KB_MB_GB_TB_PB((ulong)x.AvailableFreeSpace, true) + AS.PAR2);

                    // MediaDrive hardware block (WMI chain, best-effort) -
                    // restored 2026-07-27 via NT.MediaDrive, per AppLab's
                    // original PutInfoSys_Drives (which wrote only this LSV
                    // chain; the Format/TotalSize/FreeSpace lines above are
                    // a self-contained-curation addition, kept alongside it).
                    MediaDrive vSig = MediaDrive.FromDriveLetter(sName[0]);
                    vSig.SaveAsLsvFormat(theWriter);
                }
                else
                {
                    theWriter.WriteLine("Drive"
                        + AS.SP + sName
                        + AS.SP + "not loaded");
                }

                if (i != (xItems.Length - 1))
                {
                    theWriter.WriteLine();
                }
            }
        }

        /// <summary>Writes the sorted environment variables of the User, Machine, and Process targets, one "name=value" line each.</summary>
        public static void PutInfoSys_EnvVars(TextWriter theWriter)
        {
            theWriter.WriteLine("EnvironmentVariables.User:");
            IDictionary x = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
            _PutDictionarySorted(theWriter, x);
            theWriter.WriteLine();

            theWriter.WriteLine("EnvironmentVariables.Machine:");
            x = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            _PutDictionarySorted(theWriter, x);

            theWriter.WriteLine();
            theWriter.WriteLine("EnvironmentVariables.Process:");
            x = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
            _PutDictionarySorted(theWriter, x);
        }

        /// <summary>Writes every Environment.SpecialFolder as 'NN=Name="path"' (empty path when the folder is undefined on this system).</summary>
        public static void PutInfoSys_SpecialFolders(TextWriter theWriter)
        {
            Array xVals = Enum.GetValues(typeof(Environment.SpecialFolder));
            string[] xNames = Enum.GetNames(typeof(Environment.SpecialFolder));
            theWriter.WriteLine("SpecialFolders:");
            for (int i = 0; i < xVals.Length; i++)
            {
                string sName = xNames[i];
                Environment.SpecialFolder iVal = (Environment.SpecialFolder)xVals.GetValue(i);
                int iInt = (int)iVal;
                theWriter.Write(iInt.ToString("00"));
                theWriter.Write(AS.EQ);
                theWriter.Write(sName);
                theWriter.Write(AS.EQ);
                theWriter.Write(AS.QUOTE);
                theWriter.Write(FS.FolderPath(iVal));
                theWriter.WriteLine(AS.QUOTE);
            }
        }

        /// <summary>Writes the installed .NET FRAMEWORK versions from the registry NDP enumeration (v-keys with Version/SP/Install; this is the classic Framework list - .NET Core/5+ runtimes are not registry-registered and are not reported).</summary>
        public static void PutInfoSys_DOTNET(TextWriter theWriter)
        {
            const string s_DotNet_ = "DotNet.";

            theWriter.WriteLine("DotNet:");

            try
            {
                using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                {
                    if (ndpKey == null) return;

                    foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                    {
                        if (versionKeyName.StartsWith("v"))
                        {
                            using (RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName))
                            {
                                string name    = (string)versionKey.GetValue("Version", "");
                                string sp      = versionKey.GetValue("SP", "").ToString();
                                string install = versionKey.GetValue("Install", "").ToString();
                                if (install == "")
                                    theWriter.WriteLine(s_DotNet_ + versionKeyName + "  " + name);
                                else
                                {
                                    if (sp != "" && install == "1")
                                    {
                                        theWriter.WriteLine(s_DotNet_ + versionKeyName + "  " + name + "  SP" + sp);
                                    }
                                }
                                if (name != "")
                                {
                                    continue;
                                }
                                foreach (string subKeyName in versionKey.GetSubKeyNames())
                                {
                                    using (RegistryKey subKey = versionKey.OpenSubKey(subKeyName))
                                    {
                                        name = (string)subKey.GetValue("Version", "");
                                        if (name != "")
                                            sp = subKey.GetValue("SP", "").ToString();
                                        install = subKey.GetValue("Install", "").ToString();
                                        if (install == "")
                                            theWriter.WriteLine(s_DotNet_ + versionKeyName + "  " + name);
                                        else
                                        {
                                            if (sp != "" && install == "1")
                                            {
                                                theWriter.WriteLine(s_DotNet_ + versionKeyName + "  " + subKeyName + "  " + name + "  SP" + sp);
                                            }
                                            else if (install == "1")
                                            {
                                                theWriter.WriteLine(s_DotNet_ + versionKeyName + "  " + subKeyName + "  " + name);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                theWriter.WriteLine("Processing error: " + e.GetType().Name);
            }
        }

        // ------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------

        // Inline stand-in for AppLab UT.PutDictionarySorted (UT is not
        // curated): "name=value" lines, keys sorted OrdinalIgnoreCase.
        private static void _PutDictionarySorted(TextWriter theWriter, IDictionary theItems)
        {
            if (theItems == null || theItems.Count == 0) return;

            string[] xKeys = new string[theItems.Count];
            int i = 0;
            foreach (object xKey in theItems.Keys)
                xKeys[i++] = xKey?.ToString() ?? string.Empty;

            Array.Sort(xKeys, StringComparer.OrdinalIgnoreCase);

            for (i = 0; i < xKeys.Length; i++)
            {
                object xVal = theItems[xKeys[i]];
                theWriter.WriteLine(xKeys[i] + AS.EQ + (xVal?.ToString() ?? string.Empty));
            }
        }

        // kernel32 GlobalMemoryStatusEx - false off-Windows or on failure.
        private static bool _GotMemoryStatus(out MEMORYSTATUSEX returnStatus)
        {
            returnStatus = new MEMORYSTATUSEX();
            returnStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            try
            {
                return GlobalMemoryStatusEx(ref returnStatus);
            }
            catch
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    } // Sys
} // namespace
