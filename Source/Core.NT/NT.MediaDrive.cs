// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.IO;

namespace Source77NW
{
    public static partial class NT
    {
        /// <summary>
        /// Drive identity and media information for a given drive letter,
        /// walking the WMI chain (via <see cref="NT.Mgmt"/>):
        /// Win32_LogicalDisk -> Win32_LogicalDiskToPartition (association)
        /// -> Win32_DiskDriveToDiskPartition (association) ->
        /// MSFT_PhysicalDisk (root\Microsoft\Windows\Storage). Results are
        /// stored in a fixed ValId-indexed string array and emitted as LSV
        /// via <see cref="SaveAsLsvFormat"/>/<see cref="ToLsv"/>. Each
        /// stage is independent and best-effort: a failure at any stage
        /// leaves later ValIds unset rather than throwing.
        /// </summary>
        public struct MediaDrive
        {
            /// <summary>The WMI stage a ValId's data comes from (packed into the value's high 3 bits; see <see cref="SrcOf"/>).</summary>
            public enum ValSrc : byte
            {
                /// <summary>Set directly from the FromDriveLetter parameter, not WMI.</summary>
                Param = 0 << 5,
                /// <summary>0x20 - Win32_LogicalDisk.</summary>
                LogicalDisk = 1 << 5,
                /// <summary>0x40 - Win32_DiskPartition.</summary>
                DiskPartition = 2 << 5,
                /// <summary>0x60 - Win32_DiskDrive.</summary>
                DiskDrive = 3 << 5,
                /// <summary>0x80 - MSFT_PhysicalDisk.</summary>
                PhysicalDisk = 4 << 5,
                                       // room for 6<<5 and 7<<5
            }

            /// <summary>A drive value slot: low 5 bits are the array index (see <see cref="IndexOf"/>), high 3 bits are its <see cref="ValSrc"/> (see <see cref="SrcOf"/>).</summary>
            public enum ValId : byte
            {
                /// <summary>The drive letter itself, set from the FromDriveLetter parameter.</summary>
                VolumeLetter = 0 | (byte)ValSrc.Param,
                /// <summary>Physical drive index (from Win32_DiskDrive.Index).</summary>
                VolumeIndex = 1 | (byte)ValSrc.LogicalDisk,
                /// <summary>Volume label.</summary>
                VolumeName = 2 | (byte)ValSrc.LogicalDisk,
                /// <summary>2=Removable, 3=Fixed, 4=Network, 5=CDROM, 6=RAM.</summary>
                VolumeType = 3 | (byte)ValSrc.LogicalDisk,
                /// <summary>"NTFS", "FAT32", etc.</summary>
                FileSystem = 4 | (byte)ValSrc.LogicalDisk,
                /// <summary>Logical (volume) size in bytes.</summary>
                DataSize = 5 | (byte)ValSrc.LogicalDisk,
                /// <summary>Partition size in bytes.</summary>
                PartitionSize = 6 | (byte)ValSrc.DiskPartition,
                /// <summary>Physical (formatted) size in bytes.</summary>
                FormattedSize = 7 | (byte)ValSrc.DiskDrive,
                /// <summary>Physical media size in bytes.</summary>
                MediaSize = 8 | (byte)ValSrc.PhysicalDisk,
                /// <summary>Real hardware serial number.</summary>
                MediaSerialNumber = 9 | (byte)ValSrc.PhysicalDisk,
                /// <summary>Health state.</summary>
                HealthStatus = 10 | (byte)ValSrc.PhysicalDisk,
                /// <summary>3=HDD, 4=SSD, 5=SCM.</summary>
                MediaType = 11 | (byte)ValSrc.PhysicalDisk,
                /// <summary>SATA, NVMe, USB, SAS, RAID, etc.</summary>
                BusType = 12 | (byte)ValSrc.PhysicalDisk,
                /// <summary>User/display name.</summary>
                Model = 13 | (byte)ValSrc.PhysicalDisk,
                /// <summary>Firmware version.</summary>
                FirmwareVersion = 14 | (byte)ValSrc.PhysicalDisk,
                /// <summary>RPM (HDD only; 0 for SSD).</summary>
                SpindleSpeed = 15 | (byte)ValSrc.PhysicalDisk,
                /// <summary>Physical sector size.</summary>
                MediaSectorSize = 16 | (byte)ValSrc.PhysicalDisk,
                /// <summary>Logical sector size.</summary>
                VolumeSectorSize = 17 | (byte)ValSrc.LogicalDisk,
            }

            /// <summary>True when theId's value is a raw digit run that <see cref="Desc_or_null"/> re-renders (byte size, or a coded lookup); false for text values.</summary>
            public static bool IsDigitsValue(ValId theId)
            {
                switch (theId)
                {
                    case ValId.DataSize: return true;
                    case ValId.FormattedSize: return true;
                    case ValId.PartitionSize: return true;
                    case ValId.MediaSize: return true;
                    case ValId.VolumeType: return true;
                    case ValId.HealthStatus: return true;
                    case ValId.MediaType: return true;
                    case ValId.BusType: return true;
                }

                return false;
            }

            /// <summary>A parenthesized human-readable rendering of theId's value - a size abbreviation for the byte-size ValIds, a coded-lookup name for VolumeType/HealthStatus/MediaType/BusType; null for text ValIds or an unparseable value.</summary>
            public string Desc_or_null(ValId theId)
            {
                string sValue = Value(theId);
                switch (theId)
                {
                    case ValId.DataSize: return _SizeAbbr(sValue);
                    case ValId.FormattedSize: return _SizeAbbr(sValue);
                    case ValId.PartitionSize: return _SizeAbbr(sValue);
                    case ValId.MediaSize: return _SizeAbbr(sValue);
                    case ValId.VolumeType: return _Desc_of_value(sValue, _VolumeTypes);
                    case ValId.HealthStatus: return _Desc_of_value(sValue, _HealthStatus);
                    case ValId.MediaType: return _Desc_of_value(sValue, _MediaType);
                    case ValId.BusType: return _Desc_of_value(sValue, _BusType);
                }
                return null;
            }

            private static string _SizeAbbr(string theValue)
            {
                ulong iVal = AS.ParseUInt64(theValue, out bool bSuccess);
                if (bSuccess)
                {
                    return AS.PAR1 + AS.Format_KB_MB_GB_TB_PB(iVal, true) + AS.PAR2;
                }
                return null;
            }

            private static string _Desc_of_value(string theValue, string theList)
            {
                // WARNING: raw function assumes correct args and list values
                string sKey = AS.LF + theValue + AS.SP;
                int iBot = theList.IndexOf(sKey);
                if (iBot >= 0)
                {
                    iBot += sKey.Length;
                    int iTop = iBot;
                    while (char.IsLetterOrDigit(theList[iTop])) iTop++;
                    return AS.PAR1 + theList.Substring(iBot, iTop - iBot) + AS.PAR2;
                }
                return null;
            }

            private const string _VolumeTypes = @"
0 Unknown
1 NoRootDirectory
2 Removable
3 Fixed
4 Network
5 CDROM
6 RAM
";
            private const string _HealthStatus = @"
0 Healthy
1 Warning
2 Unhealthy
5 Unknown;
";

            private const string _MediaType = @"
0 Unspecified
3 HDD
4 SSD
5 SCM
";
            private const string _BusType = @"
0 Unknown
1 SCSI
2 ATAPI
3 ATA
4 IEEE1394
5 SSA
6 FibreChannel
7 USB
8 RAID
9 iSCSI
10 SAS
11 SATA
12 SD
13 MMC
14 Virtual
15 FileBackedVirtual
16 StorageSpaces
17 NVMe
18 MicrosoftReserved
";

            /// <summary>The LSV record-name marker written by <see cref="SaveAsLsvFormat"/> ("MediaDrive").</summary>
            public const string RecordName = @"MediaDrive";
            private const byte _IndexMask = 0x1F; // low 5 bits
            private const byte _SrcMask = 0xE0; // high 3 bits

            private const string _RecordMark = ":";
            private const string _ValMark = ".";

            private string SP => AS.SP;

            /// <summary>The array-slot index packed into id's low 5 bits.</summary>
            public static int IndexOf(ValId id)
            {
                return ((int)id) & _IndexMask;
            }

            /// <summary>The WMI stage packed into id's high 3 bits.</summary>
            public static ValSrc SrcOf(ValId id)
            {
                return (ValSrc)(((int)id) & _SrcMask);
            }

            private static readonly ValId[] _IdsByIndex; // ValId values, ordered by index

            /// <summary>Count of distinct ValId slots (the length of the internal value array).</summary>
            public static int ValCount { get; private set; }

            static MediaDrive()
            {
                var vals = (ValId[])Enum.GetValues(typeof(ValId));
                Array.Sort(vals, (a, b) => IndexOf(a).CompareTo(IndexOf(b)));
                _IdsByIndex = vals;
                ValCount = _IdsByIndex.Length;
            }

            /// <summary>True with the ValId whose array index is index; false when no ValId uses that index.</summary>
            public static bool GotId(int index, out ValId id)
            {
                if (index < 0 || index > _IndexMask)
                {
                    id = 0;
                    return false;
                }

                foreach (ValId v in _IdsByIndex)
                {
                    if (IndexOf(v) == index)
                    {
                        id = v;
                        return true;
                    }
                }

                id = 0;
                return false;
            }

            /// <summary>Walks the WMI chain for driveLetter (best-effort, each stage independent) and returns the populated MediaDrive.</summary>
            public static MediaDrive FromDriveLetter(char driveLetter)
            {
                var vSig = new MediaDrive();

                vSig._Values = new string[ValCount];

                _FromDriveLetter(ref vSig, driveLetter);

                return vSig;
            }

            private static void _FromDriveLetter(ref MediaDrive vInfo, char cLetter)
            {
                // Walk the WMI chain: LogicalDisk -> DiskPartition -> DiskDrive
                // -> PhysicalMedia (root\CIMV2) -> MSFT_PhysicalDisk (root\Microsoft\Windows\Storage).
                // Best-effort: each stage is independent; failures are silently skipped.

                if (!Mgmt.GotSearcherType(out Type xType))
                {
                    return;
                }

                vInfo.Set(ValId.VolumeLetter, cLetter.ToString());

                string sDrivePath = char.ToUpper(cLetter) + ":";  // e.g. "C:"

                // ----------------------------------------------------------------
                // Stage 1: Win32_LogicalDisk
                // ----------------------------------------------------------------

                string sDiskIndex = null;  // Win32_DiskDrive.Index for MSFT join

                try
                {
                    string sQ = @"SELECT FileSystem,VolumeName,Size,DriveType FROM Win32_LogicalDisk WHERE DeviceID='" + sDrivePath + "'";

                    using (dynamic xSearcher = Mgmt.CreateSearcher(xType, @"root\CIMV2", sQ))
                    using (dynamic xResults = xSearcher.Get())
                    {
                        foreach (dynamic xObj in xResults)
                        {
                            try
                            {
                                _Set(ref vInfo, ValId.FileSystem, xObj, "FileSystem");
                                _Set(ref vInfo, ValId.VolumeName, xObj, "VolumeName");
                                _Set(ref vInfo, ValId.DataSize, xObj, "Size");
                                _Set(ref vInfo, ValId.VolumeType, xObj, "DriveType");
                                break;
                            }
                            finally
                            {
                                if (xObj != null)
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(xObj);
                            }
                        }
                    }
                }
                catch { }

                // ----------------------------------------------------------------
                // Stage 2: Win32_LogicalDiskToPartition (association)
                // ----------------------------------------------------------------

                string sPartPath = null;  // __PATH of the associated DiskPartition

                try
                {
                    string sQ = @"ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + sDrivePath + "'} WHERE AssocClass=Win32_LogicalDiskToPartition";

                    using (dynamic xSearcher = Mgmt.CreateSearcher(xType, @"root\CIMV2", sQ))
                    using (dynamic xResults = xSearcher.Get())
                    {
                        foreach (dynamic xObj in xResults)
                        {
                            try
                            {
                                _Set(ref vInfo, ValId.PartitionSize, xObj, "Size");

                                // Capture the __PATH for the next association query
                                try { sPartPath = xObj["__PATH"].ToString(); } catch { }

                                break;
                            }
                            finally
                            {
                                if (xObj != null)
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(xObj);
                            }
                        }
                    }
                }
                catch { }

                // ----------------------------------------------------------------
                // Stage 3: Win32_DiskDriveToDiskPartition (association)
                // ----------------------------------------------------------------

                if (sPartPath != null)
                {
                    try
                    {
                        string sQ = @"ASSOCIATORS OF {" + sPartPath + @"} WHERE AssocClass=Win32_DiskDriveToDiskPartition";

                        using (dynamic xSearcher = Mgmt.CreateSearcher(xType, @"root\CIMV2", sQ))
                        using (dynamic xResults = xSearcher.Get())
                        {
                            foreach (dynamic xObj in xResults)
                            {
                                try
                                {
                                    _Set(ref vInfo, ValId.FormattedSize, xObj, "Size");

                                    // Capture disk index (e.g. "0") for MSFT join and VolumeNbr
                                    try
                                    {
                                        sDiskIndex = xObj["Index"].ToString();
                                        vInfo.Set(ValId.VolumeIndex, sDiskIndex);
                                    }
                                    catch { }

                                    break;
                                }
                                finally
                                {
                                    if (xObj != null)
                                        System.Runtime.InteropServices.Marshal.ReleaseComObject(xObj);
                                }
                            }
                        }
                    }
                    catch { }
                }

                // ----------------------------------------------------------------
                // Stage 4: MSFT_PhysicalDisk (most accurate serial, NVMe-safe)
                // Namespace: root\Microsoft\Windows\Storage
                // Join on DiskNumber (== Win32_DiskDrive.Index).
                // ----------------------------------------------------------------

                if (sDiskIndex != null)
                {
                    try
                    {
                        string sQ = @"SELECT * FROM MSFT_PhysicalDisk WHERE DeviceId='" + sDiskIndex + "'";

                        using (dynamic xSearcher = Mgmt.CreateSearcher(xType, @"root\Microsoft\Windows\Storage", sQ))
                        using (dynamic xResults = xSearcher.Get())
                        {
                            foreach (dynamic xObj in xResults)
                            {
                                try
                                {
                                    _Set(ref vInfo, ValId.MediaSerialNumber, xObj, "SerialNumber");
                                    _Set(ref vInfo, ValId.MediaType, xObj, "MediaType");
                                    _Set(ref vInfo, ValId.BusType, xObj, "BusType");
                                    _Set(ref vInfo, ValId.Model, xObj, "Model");
                                    _Set(ref vInfo, ValId.FirmwareVersion, xObj, "FirmwareVersion");
                                    _Set(ref vInfo, ValId.MediaSize, xObj, "Size");
                                    _Set(ref vInfo, ValId.VolumeSectorSize, xObj, "LogicalSectorSize");
                                    _Set(ref vInfo, ValId.MediaSectorSize, xObj, "PhysicalSectorSize");
                                    _Set(ref vInfo, ValId.HealthStatus, xObj, "HealthStatus");
                                    _Set(ref vInfo, ValId.SpindleSpeed, xObj, "SpindleSpeed");
                                    break;
                                }
                                finally
                                {
                                    if (xObj != null)
                                        System.Runtime.InteropServices.Marshal.ReleaseComObject(xObj);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            private static void _Set(ref MediaDrive vInfo, ValId theId, dynamic theObj, string theProp)
            {
                // Helper: safely extract a WMI property string and store it via Set().
                try
                {
                    object xVal = theObj[theProp];

                    if (xVal != null)
                    {
                        vInfo.Set(theId, xVal.ToString());
                    }
                }
                catch { }
            }


            //******* INSTANCE METHODS ****************

            private string[] _Values;

            /// <summary>The stored value for theId (empty string when unset by any stage); null when theId's index is out of range.</summary>
            public string Value(ValId theId)
            {
                int i1 = IndexOf(theId);

                if (i1 >= 0 && i1 < _Values.Length)
                {
                    string sVal = _Values[i1];

                    return sVal ?? string.Empty;
                }

                return null;
            }

            /// <summary>Stores theValue for theId (trimmed; digit-valued ids are normalized to their leading digit run, "0" when none).</summary>
            public void Set(ValId theId, string theValue)
            {
                int iValue = IndexOf(theId);

                if (iValue >= 0 && iValue < _Values.Length)
                {
                    theValue = theValue == null ? string.Empty : theValue.Trim();

                    if (IsDigitsValue(theId))
                    {
                        int iTop = 0;

                        while (iTop < theValue.Length && char.IsDigit(theValue[iTop])) iTop++;

                        if (iTop == 0)
                        {
                            theValue = AS.DIGIT0;
                        }
                        else
                        {
                            if (iTop < theValue.Length)
                            {
                                theValue = theValue.Substring(0, iTop);
                            }

                            theValue = AS.ParseUInt64(theValue, out _).ToString(); // normalize "000x" to "x"
                        }
                    }

                }

                _Values[iValue] = theValue;
            }

            /// <summary>
            /// Writes this drive's values to theWriter as an LSV record: a
            /// ":MediaDrive" record marker, then one ".&lt;ValId&gt; value
            /// [desc]" line per set value (unset values skipped).
            /// </summary>
            /// <remarks>Renamed from the AppLab SaveLsv 2026-07-27, per the
            /// *AsLsvFormat naming precedent set by EnumVals: it serializes a
            /// format, it does not perform file I/O.</remarks>
            public void SaveAsLsvFormat(TextWriter x2)
            {
                x2.WriteLine(_RecordMark + RecordName);

                int i = -1;

                while (++i < _IdsByIndex.Length)
                {
                    ValId iId = _IdsByIndex[i];
                    string sVal = Value(iId);
                    if (sVal == null) continue;
                    x2.Write(_ValMark);
                    x2.Write(iId.ToString());
                    x2.Write(SP);
                    x2.Write(sVal);

                    string sDesc = Desc_or_null(iId);
                    if (sDesc != null)
                    {
                        x2.Write(SP);
                        x2.Write(sDesc);
                    }
                    x2.WriteLine();
                }
            }

            /// <summary>SaveAsLsvFormat rendered as a string; null when this MediaDrive was never opened via FromDriveLetter.</summary>
            public string ToLsv()
            {
                if (_Values == null) return null;
                using (var x2 = Heap.New_TextBuilder(0))
                {
                    SaveAsLsvFormat(x2);
                    return x2.ToString_and_Dispose();
                }
            }

            // YET_TODO FromLsv(string theLsvText)

        }
    }
}
