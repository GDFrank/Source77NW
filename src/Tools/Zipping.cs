// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.IO.Compression;

namespace Source77NW
{
    /// <summary>
    /// Static zip/unzip helpers over System.IO.Compression with a
    /// soft Try* surface: each operation returns null on success or
    /// the <see cref="Issue"/> describing the failure.
    /// </summary>
    /// <remarks>
    /// Requires references to System.IO.Compression and
    /// System.IO.Compression.FileSystem (netstandard on Framework).
    /// </remarks>
    public static class Zipping
    {
        private const ushort issueSource = 65220;

        private static Issue _Issue(byte spot, object reason, object regarding, IssueKind kind)
        {
            string sHeader = ResCode.Caption_or_empty(WordId.Zipping)
                + AS.SP + ResCode.Caption_or_empty(WordId.Issue) + AS.COLON + FS.LSep;
            string sCaption = string.Empty;
            string sInfo = string.Empty;
            if (reason is Enum iEnum)
            {
                sCaption = ResCode.Caption_or_empty(iEnum);
            }
            else
            {
                sCaption = regarding?.ToString();
            }
            if (regarding is FileInfo xFileInfo)
            {
                sInfo = AS.Quoted(xFileInfo.FullName);
            }
            else
            {
                sInfo = regarding?.ToString(); // FIX: was 'sInfo?.ToString()' (self-assign of empty) - non-FileInfo detail was silently dropped from every Issue
            }

            return Issue.Create(issueSource, spot, sHeader + sCaption, sInfo, kind);
        }

        private static Issue _Issue_bad_params(byte spot) => _Issue(spot, FS.IssueId.Invalid_parameters, null, IssueKind.BadParam);

        /// <summary>
        /// Creates (never appends to) the target zip from the given
        /// file(s): a string path, a FileInfo, or an array of either.
        /// Invalid/missing source paths are silently skipped;
        /// returnCount is the number of entries written. Null on
        /// success, else the Issue.
        /// </summary>
        public static Issue TryZippingFiles(object theFileInfos_or_FilePaths, FileInfo theTargetZipFilePath, out int returnCount)
        {
            bool bArray = false;
            bool bString = false;
            int iCount = 0;
            string sFilePath = null;
            FileInfo[] xFileInfos = null;
            string[] xFilePaths = null;
            returnCount = 0;

            if (theFileInfos_or_FilePaths == null || theTargetZipFilePath == null)
            {
                return _Issue_bad_params(31);
            }

            if (theFileInfos_or_FilePaths is string s1)
            {
                sFilePath = s1; bString = true; iCount = 1;
            }
            else if (theFileInfos_or_FilePaths is FileInfo xInfo)
            {
                sFilePath = xInfo.FullName; bString = false; iCount = 1;
            }
            else if (theFileInfos_or_FilePaths is FileInfo[] xxInfo)
            {
                xFileInfos = xxInfo; bArray = true; iCount = xxInfo.Length;
            }
            else if (theFileInfos_or_FilePaths is string[] xstrings)
            {
                xFilePaths = xstrings; bArray = true; iCount = xstrings.Length;
            }
            else
            {
                return _Issue_bad_params(32);
            }

            try
            {
                using (ZipArchive xZip = ZipFile.Open(theTargetZipFilePath.FullName, ZipArchiveMode.Create))
                {
                    int i = -1;
                    while (++i < iCount)
                    {
                        if (bArray)
                        {
                            if (bString)
                            {
                                sFilePath = xFilePaths[i];
                            }
                            else
                            {
                                sFilePath = xFileInfos[i].FullName;
                            }
                        }
                        string sGoodPath = FS.ValidFilePath_or_null(sFilePath, out _);
                        string sGoodName = Path.GetFileName(sGoodPath);
                        if (sGoodPath != null)
                        {
                            xZip.CreateEntryFromFile(sGoodPath, sGoodName);
                            returnCount++;
                        }
                    }
                }
                return null;

            }
            catch (Exception e)
            {
                string sInfo = e.Message
                + FS.LSep + AS.Quoted(sFilePath)
                + FS.LSep + AS.Quoted(FS.LSep + theTargetZipFilePath.FullName);
                return _Issue(29, FS.IssueId.Operation_failed, sInfo, Issue.KindOf(e));
            }
        }

        /// <summary>
        /// Zips theSourceFolder to '&lt;name&gt;.zip' (optionally
        /// '&lt;name&gt;.&lt;stamp&gt;.zip') in the target folder, or in the
        /// source's parent when the target is null; refuses root
        /// folders and existing undeletable targets; a partial output
        /// is deleted on failure. Null on success, else the Issue.
        /// </summary>
        public static Issue TryZippingFolder(DirectoryInfo theSourceFolder
            ,out FileInfo returnTargetFileInfo, bool asTimeStamped = false
            ,DirectoryInfo theTargetFolder_or_null_for_parent_of_theSourceFolder = null)
        {
            returnTargetFileInfo = null;

            if (theSourceFolder == null)
                return _Issue_bad_params(21);
            if (theSourceFolder.Parent == null)
                return _Issue(22, FS.IssueId.Root_folder_operation_not_supported, null, IssueKind.NotSupported);

            try
            {
                if (!theSourceFolder.Exists)
                    return _Issue(23, FS.IssueId.Folder_not_found, theSourceFolder, IssueKind.NoSuch);

                string sTargetFileName = theSourceFolder.Name +
                    ( asTimeStamped
                    ? AS.DOT + DateTime.Now.ToString(AS.STAMP_date_HHmm) + AS.DOT_zip
                    : AS.DOT_zip );

                if (theTargetFolder_or_null_for_parent_of_theSourceFolder == null)
                    returnTargetFileInfo = new FileInfo(FS.EnsureDSepTail(theSourceFolder.Parent.FullName) + sTargetFileName);
                else
                    returnTargetFileInfo = new FileInfo(FS.EnsureDSepTail(theTargetFolder_or_null_for_parent_of_theSourceFolder.FullName) + sTargetFileName);

                if (!FS.FileAbsent_or_Deleted(returnTargetFileInfo.FullName, out Issue xIssue))
                    return _Issue(24, FS.IssueId.Unable_to_replace_file, returnTargetFileInfo, xIssue.Kind);

                ZipFile.CreateFromDirectory
                        ( theSourceFolder.FullName
                        , returnTargetFileInfo.FullName
                        , CompressionLevel.Optimal
                        , false
                        , System.Text.Encoding.ASCII); // only used in meta data

                return null;
            }
            catch (Exception e)
            {
                string sInfo = e.Message
                    + AS.Quoted(FS.LSep + theSourceFolder?.FullName)
                    + AS.Quoted(FS.LSep + returnTargetFileInfo?.FullName);

                // try to delete any partial and invalid output generated.
                FS.FileAbsent_or_Deleted(returnTargetFileInfo?.FullName, out _); // FIX: '?.' - a pre-assignment throw would NRE here, masking the real Issue (adjacent line already null-safe)

                return _Issue(49, FS.IssueId.Operation_failed, sInfo, Issue.KindOf(e));
            }
        }

        /// <summary>
        /// Extracts theSourceFile into a NEW folder named after the
        /// file (sans extension) inside the target folder, or inside
        /// the file's own folder when the target is null; the folder
        /// must not already exist; a partial output folder is deleted
        /// on failure. Null on success, else the Issue.
        /// </summary>
        public static Issue TryUnzipFile(FileInfo theSourceFile
            , DirectoryInfo toTargetFolder_or_null_for_parent_of_theFile
            , out DirectoryInfo returnTargetFolderInfo
            )
        {
            returnTargetFolderInfo = null;

            if (theSourceFile == null)
                return _Issue_bad_params(70); // FIX: was spot 71, duplicating the File_not_found raise below - renumbered for (source, spot) uniqueness

            if (!theSourceFile.Exists)
            {
                return _Issue(71, FS.IssueId.File_not_found, theSourceFile, IssueKind.NoSuch);
            }

            try
            {
                string sSourceNameOnly = FS.FileNameOnly(theSourceFile);

                if (toTargetFolder_or_null_for_parent_of_theFile == null)
                {
                    returnTargetFolderInfo = new DirectoryInfo(FS.EnsureDSepTail(theSourceFile.Directory.FullName) + sSourceNameOnly + FS.DSep);
                }
                else
                {
                    returnTargetFolderInfo = new DirectoryInfo(FS.EnsureDSepTail(toTargetFolder_or_null_for_parent_of_theFile.FullName) + sSourceNameOnly + FS.DSep);
                }

                if (returnTargetFolderInfo.Exists)
                {
                    return _Issue(72, FS.IssueId.Folder_must_NOT_exist,
                        AS.Quoted(returnTargetFolderInfo.FullName), IssueKind.AlreadyExists);
                }

                Directory.CreateDirectory(returnTargetFolderInfo.FullName);

                ZipFile.ExtractToDirectory(theSourceFile.FullName, returnTargetFolderInfo.FullName);

                return null;
            }
            catch (Exception e)
            {
                string sInfo = AS.Quoted(theSourceFile?.FullName)
                   + FS.LSep + AS.Quoted(returnTargetFolderInfo?.FullName);

                // try to delete any partial and invalid output generated.
                FS.FolderAbsent_or_Deleted(returnTargetFolderInfo?.FullName, out _);

                return _Issue(79, FS.IssueId.Operation_failed, sInfo, Issue.KindOf(e));
            }
        }
    }
}
