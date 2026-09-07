// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;

namespace Source77NW
{
    public static partial class NT
    {
        /// <summary>
        /// Dynamic access to System.Management (WMI), reflection-loaded so
        /// the assembly is never compile-time referenced (dynamic +
        /// Microsoft.CSharp RuntimeBinder only). Best-effort throughout:
        /// an unavailable/off-Windows System.Management, or a failed
        /// query, returns null/false/empty rather than throwing.
        /// <see cref="NT.MediaDrive"/> shares this searcher-type cache
        /// (the CLR loads System.Management once per AppDomain either way).
        /// </summary>
        public static class Mgmt
        {
            /// <summary>Class-name filter for <see cref="GetClassList(ClassId, string)"/>.</summary>
            public enum ClassId : byte
            {
                /// <summary>Not a prefix filter - GetClassList returns this literal name unchanged.</summary>
                Win32_OperatingSystem,
                /// <summary>Classes whose name starts with "Win32".</summary>
                Win32,
                /// <summary>Classes whose name starts with "MSFT".</summary>
                MSFT,
                /// <summary>Classes whose name starts with "CIM".</summary>
                CIM,
                /// <summary>Every class in the namespace, unfiltered.</summary>
                All,
            }

            private const ushort issueSource = 65333;

            /// <summary>Sorted, newline-separated list of WMI class names in theNamespacePath matching theParam's prefix (Win32_OperatingSystem is a literal, not a prefix); null when WMI is unavailable.</summary>
            public static string GetClassList(ClassId theParam, string theNamespacePath)
            {
                string sStartsWith = null;

                switch (theParam)
                {
                    case ClassId.Win32:
                        sStartsWith = "Win32";
                        break;
                    case ClassId.MSFT:
                        sStartsWith = "MSFT";
                        break;
                    case ClassId.CIM:
                        sStartsWith = "CIM";
                        break;
                    case ClassId.All:
                        sStartsWith = null;
                        break;
                    default:
                        // ClassId.Win32_OperatingSystem is intentionally unhandled here;
                        // it falls through to return its name as a class filter string.
                        return theParam.ToString();
                }

                if (!GotSearcherType(out Type xType))
                {
                    return null;
                }

                using (var xStringStack = Heap.New_ItemStack<string>(0))
                {
                    try
                    {
                        using (dynamic searcher = CreateSearcher(xType, theNamespacePath, @"SELECT * FROM meta_class"))
                        using (var xResults = searcher.Get())
                        {
                            foreach (var xResult in xResults)
                            {
                                try
                                {
                                    string sName = xResult["__CLASS"].ToString();

                                    if (sStartsWith != null && !sName.StartsWith(sStartsWith, AS.IgnoreCase))
                                        continue;

                                    xStringStack.Push(sName);
                                }
                                finally
                                {
                                    if (xResult != null)
                                        System.Runtime.InteropServices.Marshal.ReleaseComObject(xResult);
                                }
                            }
                        }
                    }
                    catch { }

                    xStringStack.Sort();

                    return xStringStack.ToString_and_Dispose();
                }
            }

            /// <summary>GetClassList defaulting theNamespacePath to root\CIMV2.</summary>
            public static string GetClassList(ClassId theParam)
            {
                return GetClassList(theParam, @"root\CIMV2");
            }

            /// <summary>Sorted, newline-separated tree of WMI namespaces rooted at theRootNamespace (defaults to "root" when null/empty); null when WMI is unavailable.</summary>
            public static string GetNamespaceTree(string theRootNamespace)
            {
                if (!GotSearcherType(out Type xType))
                    return null;

                if (string.IsNullOrEmpty(theRootNamespace))
                    theRootNamespace = "root";

                using (var xStringStack = Heap.New_ItemStack<string>(0))
                {
                    xStringStack.Push(theRootNamespace);
                    try
                    {
                        _CollectNamespacesRecursive(xStringStack, xType, theRootNamespace);
                    }
                    catch { }

                    xStringStack.Sort();
                    return xStringStack.ToString_and_Dispose();
                }
            }

            private static void _CollectNamespacesRecursive(ItemStack<string> theStack, Type theSearcherType, string theParentNamespace)
            {
                try
                {
                    using (dynamic searcher = CreateSearcher(theSearcherType, theParentNamespace, @"SELECT * FROM __namespace"))
                    using (var xResults = searcher.Get())
                    {
                        foreach (var xResult in xResults)
                        {
                            try
                            {
                                string sChildName = xResult.Name as string;
                                if (string.IsNullOrEmpty(sChildName))
                                {
                                    continue;
                                }

                                string sFull = theParentNamespace + "\\" + sChildName;

                                theStack.Push(sFull);

                                _CollectNamespacesRecursive(theStack, theSearcherType, sFull);
                            }
                            finally
                            {
                                if (xResult != null)
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(xResult);
                            }
                        }
                    }
                }
                catch
                {
                    // best-effort: ignore failures for individual namespace branches
                }
            }

            /// <summary>Dumps every WMI class in theClassNamesList (a newline-separated string, or a ClassId to resolve via GetClassList) from theNamespacePath to theWriter_or_FolderPath - a TextWriter, a folder DirectoryInfo, or a folder path string (one ClassName.txt file per class when writing to a folder). Returns the class count; per-class query/property failures are swallowed and noted in returnLog rather than thrown.</summary>
            public static int PutManagementData
                ( object theWriter_or_FolderPath
                , object theClassNamesList
                , string theNamespacePath
                , out string returnLog
                , out Issue returnIssue)
            {
                returnLog = string.Empty;
                returnIssue = null;

                string sFolderPath = null;
                TextWriter xWriter = null;

                if (theWriter_or_FolderPath is TextWriter xWriter1)
                {
                    xWriter = xWriter1;
                }
                else if (theWriter_or_FolderPath is DirectoryInfo xDir1)
                {
                    sFolderPath = xDir1.FullName + FS.DSep;
                }
                else if (theWriter_or_FolderPath is string sDir1)
                {
                    sFolderPath = FS.ValidFolderPath_or_null(sDir1, out returnIssue);

                    if (sFolderPath == null)
                        return 0;
                }
                else
                {
                    returnIssue = Issue.Create(issueSource, 3, "Invalid param", "Output: " + theWriter_or_FolderPath.ToString());
                    return 0;
                }

                bool bSavingToFiles = sFolderPath != null;

                if (bSavingToFiles)
                {
                    if (!FS.FolderExists_or_Created(sFolderPath, out returnIssue))
                        return 0;
                }

                string sClassNames = null;

                if (theClassNamesList is ClassId iParm)
                {
                    sClassNames = GetClassList(iParm, theNamespacePath ?? @"root\CIMV2");
                }
                else
                {
                    sClassNames = theClassNamesList.ToString();
                }

                int iClassCount = 0;

                string sClassName = null;

                Chars vClasses = new Chars(sClassNames);

                if (!GotSearcherType(out Type xType))
                    return 0;

                while (vClasses.PluckedLine(out Chars vClass))
                {
                    if (vClass.IsEmpty)
                        continue;

                    sClassName = vClass.ToString();

                    try
                    {
                        using (dynamic searcher = CreateSearcher(xType, theNamespacePath, @"SELECT * FROM " + sClassName))
                        using (var xProps = searcher.Get())
                        {
                            int iPropCount = 0;

                            foreach (var xProp in xProps)
                            {
                                try
                                {
                                    if (xProp == null || xProp.Properties == null)
                                        continue;

                                    if (iPropCount == 0)
                                    {
                                        if (bSavingToFiles)
                                        {
                                            string sClassPath = sFolderPath + sClassName + AS.DOT_txt;

                                            xWriter = FS.GetTextWriter_or_null(sClassPath, out Issue xIssue);

                                            if (xWriter == null)
                                            {
                                                returnLog += "Error " + sClassName + AS.SP + xIssue.Message + FS.LSep;
                                                break;
                                            }
                                        }
                                    }

                                    foreach (var prop in xProp.Properties)
                                    {
                                        string sType = prop.Type.ToString();
                                        string sValue;

                                        if (prop.Value == null)
                                        {
                                            sValue = string.Empty;
                                        }
                                        else if (prop.Value is Array arr)
                                        {
                                            string[] parts = new string[arr.Length];
                                            for (int k = 0; k < arr.Length; k++)
                                                parts[k] = arr.GetValue(k)?.ToString() ?? string.Empty;
                                            sValue = string.Join(",", parts);
                                        }
                                        else
                                        {
                                            sValue = prop.Value.ToString();
                                        }

                                        try
                                        {
                                            xWriter.WriteLine(AS.DOT + prop.Name + AS.SP + sType + AS.SP + sValue);
                                        }
                                        catch (Exception e)
                                        {
                                            returnLog += "Error " + sClassName + AS.SP + e.Message + FS.LSep;
                                            break;
                                        }

                                        iPropCount++;
                                    }
                                }
                                finally
                                {
                                    if (xProp != null)
                                        System.Runtime.InteropServices.Marshal.ReleaseComObject(xProp);
                                }
                            }

                            if (bSavingToFiles)
                            {
                                if (xWriter != null)
                                {
                                    try
                                    {
                                        xWriter.Close();
                                        xWriter.Dispose();
                                        xWriter = null;
                                    }
                                    catch { }
                                }
                            }

                            if (iPropCount == 0)
                            {
                                returnLog += "Empty " + sClassName + FS.LSep;
                            }
                        }
                    }
                    catch { }

                    iClassCount++;
                }

                return iClassCount;
            }

            /// <summary>PutManagementData defaulting theNamespacePath to root\CIMV2.</summary>
            public static int PutManagementData
                ( object theWriter_or_FolderPath
                , object theClassNamesList
                , out string returnLog
                , out Issue returnIssue)
            {
                return PutManagementData(theWriter_or_FolderPath, theClassNamesList, @"root\CIMV2", out returnLog, out returnIssue);
            }

            private static Assembly _Assembly = null;

            private static Type _SearcherType = null;

            private static Exception _LastException = null;
            /// <summary>The most recent load/type-resolution failure, if any (see <see cref="GotSearcherType"/>).</summary>
            public static Exception LastException => _LastException;

            private static readonly object _Lock = new object();

            /// <summary>Clears the cached assembly, searcher type, and LastException; a later call retries the System.Management load from scratch.</summary>
            public static void Reset()
            {
                lock (_Lock)
                {
                    _Assembly = null;
                    _SearcherType = null;
                    _LastException = null;
                }
            }

            private static bool _GotAssembly(out Assembly returnAssembly)
            {
                if (_Assembly != null)
                {
                    returnAssembly = _Assembly;
                    return true;
                }

                try
                {
                    // .NET Framework: System.Management is a GAC assembly, a single
                    // strong-name load is sufficient and preferred. .NET 8/10: resolved
                    // from the shared/runtime pack when present; fails (caught below)
                    // off-Windows or when the pack is absent.
                    _Assembly = Assembly.Load("System.Management, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

                    if (_Assembly == null)
                    {
                        throw new FileNotFoundException("System.Management assembly returned null from Assembly.Load.");
                    }
                }
                catch (Exception ex)
                {
                    returnAssembly = null;
                    _LastException = ex;
                    return false;
                }

                returnAssembly = _Assembly;
                return true;
            }

            /// <summary>True with the ManagementObjectSearcher Type (cached after the first success) when System.Management is loadable; false (see <see cref="LastException"/>) off-Windows or when the assembly/type is absent.</summary>
            public static bool GotSearcherType(out Type returnSearcherType)
            {
                if (_SearcherType != null)
                {
                    returnSearcherType = _SearcherType;
                    return true;
                }

                returnSearcherType = null;

                lock (_Lock)
                {
                    if (_SearcherType != null)
                    {
                        returnSearcherType = _SearcherType;
                        return true;
                    }

                    if (!_GotAssembly(out Assembly xAssembly))
                        return false;

                    try
                    {
                        _SearcherType = xAssembly.GetType("System.Management.ManagementObjectSearcher");

                        if (_SearcherType == null)
                        {
                            _LastException = new TypeLoadException("ManagementObjectSearcher type not found in System.Management assembly");
                            return false;
                        }

                        returnSearcherType = _SearcherType;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        returnSearcherType = null;
                        _LastException = ex;
                        return false;
                    }
                }
            }

            /// <summary>Creates a ManagementObjectSearcher dynamically: the single-argument (query-only) constructor when theNamespacePath is null/empty (default WMI namespace), else the (scope, query) constructor.</summary>
            public static dynamic CreateSearcher(Type theSearcherType, string theNamespacePath, string theQuery)
            {
                if (string.IsNullOrEmpty(theNamespacePath))
                {
                    return Activator.CreateInstance(theSearcherType, new object[] { theQuery });
                }

                return Activator.CreateInstance(theSearcherType, new object[] { theNamespacePath, theQuery });
            }
        }
    }
}
