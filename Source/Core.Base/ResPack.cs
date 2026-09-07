// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Source77NW
{
    /// <summary>
    /// ResPack - a binary ".pack" of resource streams, embedded in an
    /// assembly and served to <see cref="ResCode"/> on demand. Packs are
    /// built (by <see cref="ResPackBuilder"/>, development-time; same file,
    /// below) into a
    /// RES folder as "&lt;kind&gt;.&lt;codeName&gt;.pack" and included as
    /// EmbeddedResource; at runtime <see cref="Loaded"/> scans an
    /// assembly's manifest resources for "*.RES.*.pack" names, indexes
    /// each pack, and registers this class's stream/value suppliers with
    /// <see cref="ResCode"/> (streams on the first pack found, lingo
    /// values on the first lingo pack found).
    /// </summary>
    /// <remarks>
    /// Pack layout: ASCII header, the concatenated item streams, then a
    /// sorted manifest tail (see <see cref="ItemInfo"/>; tail format is
    /// documented at the write site in ResPackBuilder._TryBuild and read
    /// here in _GotReader).
    /// </remarks>
    public sealed class ResPack : IComparable<ResPack>
    {
        static ResPack() { }

        private const ushort issueSource = 65123;
        private const string DOT = AS.DOT;
        private const string s_pack = "pack";
        private const string s_DOT_RES_DOT = ".RES.";
        private const string s_ResPack = "ResPack";
        private const string s_Issue = "Issue";

        /// <summary>The pack file extension, ".pack".</summary>
        public const string DOT_pack = DOT + s_pack;

        /// <summary>The <see cref="FileExt.KindId"/> a pack of theId holds (icon -&gt; image, echo -&gt; audio; else unknown = unrestricted).</summary>
        public static FileExt.KindId KindOf(ResKind theId)
        {
            switch (theId)
            {
                case ResKind.icon: return FileExt.KindId.image;
                case ResKind.echo: return FileExt.KindId.audio;
            }
            return FileExt.KindId.unknown;
        }

        private static bool _GotReader(ResKind theKind, string theName, out BytesReader returnReader)
        {
            returnReader = null;

            int i1 = -1;
            while (++i1 < _ResPacks.Count)
            {
                ResPack xPack = _ResPacks[i1];

                if (!xPack.Kind.Equals(theKind))
                    continue;

                Stream xStream = null;
                BytesReader xReader = null;

                try
                {
                    xStream = xPack.Assembly.GetManifestResourceStream(xPack.ResourceName);
                    xReader = BytesReader.Create_or_null(xStream, out _);
                }
                catch
                {
                    xReader = null;
                }

                if (xReader == null)
                {
                    if (xStream != null)
                        xStream.Dispose();
                    continue;
                }

                if (xPack._ItemInfos == null)
                {
                    xStream.Seek(xStream.Length - 1, SeekOrigin.Begin);
                    int iByte = xStream.ReadByte(); // tail: byte length of manifest-pos field
                    if (iByte > 0)
                    {
                        xStream.Seek(xStream.Length - (iByte + 1), SeekOrigin.Begin);
                        long xBotInfos = xReader.ReadAny_Int64();
                        xStream.Seek(xBotInfos, SeekOrigin.Begin);
                        int iCount = xReader.ReadAny_Int32();
                        xPack._ItemInfos = new ItemStack<ItemInfo>(iCount);
                        int i4 = -1;
                        while (++i4 < iCount)
                        {
                            ItemInfo vInfo = new ItemInfo();
                            vInfo.LoadBytes(xReader);
                            xPack._ItemInfos.Push(vInfo);
                        }
                    }
                }

                if (xPack._ItemInfos == null)
                {
                    xReader.Dispose();
                    continue;
                }

                int i2 = xPack.IndexOfItem(theName);

                if (i2 < 0)
                {
                    xReader.Dispose();
                    continue;
                }

                ItemInfo vFound = xPack._ItemInfos[i2];
                xStream.Seek(vFound.Position, SeekOrigin.Begin);
                returnReader = xReader;
                return true;
            }

            return false;
        }

        private static bool _GotValue(ResCode theCode, CodeValId theId, out Chars returnValue)
        {
            // YET_TODO

            returnValue = default;
            return false;
        }

        /// <summary>
        /// Loads every "*.RES.*.pack" embedded resource of theAssembly (true
        /// immediately when the assembly is already loaded), registering this
        /// class's stream supplier with <see cref="ResCode"/> when any stream
        /// pack was found and its value supplier when any lingo pack was found.
        /// False with returnIssue only when the assembly's manifest resource
        /// names cannot be accessed at all.
        /// </summary>
        public static bool Loaded(Assembly theAssembly, out Issue returnIssue)
        {
            returnIssue = null;
            int i = -1;
            while (++i < _ResPacks.Count)
                if (_ResPacks[i].Assembly.Equals(theAssembly)) return true;

            try
            {
                int iStreamCount = 0; 
                int iLingoCount = 0;
                string[] xResourceNames = theAssembly.GetManifestResourceNames();

                int i1 = -1;
                while (++i1 < xResourceNames.Length)
                {
                    string sResourceName = xResourceNames[i1];
                    //[1]"Source77NW.RES.icon.77.pack"
                    int iBot = sResourceName.IndexOf(s_DOT_RES_DOT, StringComparison.OrdinalIgnoreCase);
                    if (iBot < 0 || !sResourceName.EndsWith(DOT_pack, StringComparison.OrdinalIgnoreCase))
                        continue;
                    int iTop = sResourceName.Length - DOT_pack.Length;
                    iBot += s_DOT_RES_DOT.Length;
                    if (iTop <= iBot)
                        continue;
                    // icon.77

                    ResPack xPack = new ResPack();
                    xPack.Assembly = theAssembly;
                    xPack.ResourceName = sResourceName;
                    xPack.PackNameOnly = sResourceName.Substring(iBot, iTop - iBot);
                    string sResKind = xPack.PackNameOnly;
                    int iSub = sResKind.IndexOf(DOT);
                    if (iSub >= 0)
                        sResKind = sResKind.Substring(0, iSub);
                    int iCode = ResCode.ResKinds.IndexOf(sResKind);
                    if (iCode < 0)
                        continue;
                    xPack.Kind = (ResKind)ResCode.ResKinds.Code(iCode);
                    _ResPacks.Push(xPack);
                    if (ResCode.IsLingo(xPack.Kind))
                        iLingoCount++;
                    else
                        iStreamCount++;
                }

                if (iStreamCount > 0)
                    ResCode.Register(_GotReader);

                if (iLingoCount > 0)
                    ResCode.Register(_GotValue);

                return true;
            }
            catch (Exception e)
            {
                returnIssue = Issue.Create(issueSource, 33
                    , "ResPack Cannot access ManifestResourceNames", e
                    , IssueKind.ProgramIssue);
                return false;
            }
        }

        /// <summary>
        /// One pack item's manifest entry: name, original timestamp, and where
        /// its bytes live in the pack. The shared read/write contract between
        /// ResPack (reads, _GotReader) and <see cref="ResPackBuilder"/>
        /// (writes; same file). Ordered and equated by NameDotExt,
        /// OrdinalIgnoreCase.
        /// </summary>
        public struct ItemInfo : IComparable<ItemInfo>
        {
            /// <summary>The item's file name with extension - the lookup key (OrdinalIgnoreCase).</summary>
            public string NameDotExt;
            /// <summary>The source file's LastWriteTime when packed.</summary>
            public DateTime LastWrite;
            /// <summary>The item's byte position within the pack.</summary>
            public long Position;
            /// <summary>The original file length in bytes.</summary>
            public int FileLength;
            /// <summary>The item's byte length within the pack (differs from FileLength when compressed).</summary>
            public int StreamLength;

            /// <summary>True when the packed bytes differ in length from the original file (compressed).</summary>
            public bool IsCompressed => FileLength != StreamLength;
            /// <summary>The item's FileExt, derived from NameDotExt.</summary>
            public FileExt FileExt => FileExt.From(NameDotExt);

            /// <inheritdoc/>
            public int CompareTo(ItemInfo other) => string.Compare(NameDotExt, other.NameDotExt, StringComparison.OrdinalIgnoreCase);
            /// <inheritdoc/>
            public override bool Equals(object obj) => (obj is ItemInfo vItem) && 0 == CompareTo(vItem);
            /// <inheritdoc/>
            public override int GetHashCode() => NameDotExt == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(NameDotExt); // FIX: match OrdinalIgnoreCase CompareTo
            /// <summary>Loads this entry from reader (field order fixed: Position, StreamLength, FileLength, LastWrite ticks, NameDotExt).</summary>
            public void LoadBytes(BytesReader reader)
            {
                Position = reader.ReadAny_Int64();
                StreamLength = reader.ReadAny_Int32();
                FileLength = reader.ReadAny_Int32();
                LastWrite = new DateTime(reader.ReadAny_Int64());
                NameDotExt = reader.ReadString();
            }
            /// <summary>Saves this entry to writer (same fixed field order as LoadBytes).</summary>
            public void SaveBytes(BytesWriter writer)
            {
                writer.WriteAny_Int64(Position);
                writer.WriteAny_Int32(StreamLength);
                writer.WriteAny_Int32(FileLength);
                writer.WriteAny_Int64(LastWrite.Ticks);
                writer.Write(NameDotExt);
            }
            /// <inheritdoc/>
            public override string ToString()
            {
                return NameDotExt + AS.SP + LastWrite.ToString(AS.STAMP_date_HHmm_ss)
                    + AS.SP + Position.ToString() + AS.SP + StreamLength.ToString() + AS.SP + FileLength.ToString();
            }

        }

        // ==== STATIC HEAP ======================

        // YET_TODO R9: _ResPacks and the lazy _ItemInfos loads are unsynchronized;
        // lookups racing Loaded() is an open item (same class as Alloc<T> statics).
        private static ItemStack<ResPack> _ResPacks = new ItemStack<ResPack>(4);


        // ==== ResPack INSTANCE HEAP ======================

        // YET_TODO R8: GotStreamDO gives the caller no StreamLength, so the item
        // end is unknowable from the returned reader. Needs a bounded reader or
        // a length out param before public exposure.
        private ItemStack<ItemInfo> _ItemInfos; // lazy-set in _GotReader

        /// <summary>The assembly this pack is embedded in.</summary>
        public Assembly Assembly { get; private set; }
        /// <summary>The pack's full manifest resource name (e.g. "Source77NW.RES.icon.77.pack").</summary>
        public string ResourceName { get; private set; }
        /// <summary>The pack name between ".RES." and ".pack" (e.g. "icon.77").</summary>
        public string PackNameOnly { get; private set;  } // extracted from ResourceName
        /// <summary>The pack's ResKind, parsed from the first PackNameOnly token.</summary>
        public ResKind Kind { get; private set; }

        /// <inheritdoc/>
        public int CompareTo(ResPack other)
        {
            int i = Kind.CompareTo(other.Kind);
            if (i == 0) i = PackNameOnly.CompareTo(other.PackNameOnly);
            return i;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is ResPack xPack)
            {
                return 0 == CompareTo(xPack);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return ((int)Kind * 397) ^ (PackNameOnly == null ? 0 : PackNameOnly.GetHashCode());
        }

        private ResPack() { }

        /// <summary>The manifest index of theNameDotExt (BinarySearch over the sorted manifest), else -1 (also -1 for null/empty names or a missing manifest).</summary>
        public int IndexOfItem(string theNameDotExt)
        {
            if (string.IsNullOrEmpty(theNameDotExt)) return -1;
            if (_ItemInfos == null || _ItemInfos.Count == 0) return -1;
            ItemInfo vInfo = new ItemInfo() { NameDotExt = theNameDotExt };
            int i = _ItemInfos.BinarySearch(vInfo);
            return i;
        }


    } // ResPack

    /// <summary>
    /// Builds ResPack ".pack" files (development-time tooling): writes an
    /// ASCII header, concatenates asset files from a kind-named assets
    /// folder, and appends the sorted <see cref="ResPack.ItemInfo"/>
    /// manifest tail that <see cref="ResPack"/> reads back at runtime.
    /// Create one per pack via <see cref="Create"/>, then
    /// <see cref="TryBuild"/> with file/folder names relative to
    /// <see cref="AssetsFolderPath"/>. The built pack lands at
    /// <see cref="PackFilePath"/>, to be included as EmbeddedResource.
    /// </summary>
    /// <remarks>
    /// Curated 2026-07-26 from AppLab's nested ResPack.Builder: now a
    /// peer class in this same file - reader and maker share one file,
    /// one namespace. The AppLab #if DEV gate did not travel: the maker
    /// ships in Base, since a library consumer needs it to build packs
    /// in the first place; release apps simply never call it.
    /// </remarks>
    public sealed class ResPackBuilder
    {
        private const ushort issueSource = 65007;

        private static Issue _Issue(byte spot, params object[] items)
            => Issue.Create(issueSource, spot, items);

        private ResPackBuilder() { }

        /// <summary>A builder for one pack: theKind + theCodeName name the pack file; theResFolderPath is where it will be written (and holds the kind-named assets subfolder).</summary>
        public static ResPackBuilder Create(string theResFolderPath, ResKind theKind, string theCodeName)
        {
            return new ResPackBuilder()
            {
                ResFolderPath = theResFolderPath,
                PackKind = theKind,
                CodeName = theCodeName
            };
        }

        /// <summary>The RES folder the pack is built into.</summary>
        public string ResFolderPath { get; private set; }
        /// <summary>The ResKind this pack holds.</summary>
        public ResKind PackKind { get; private set; }
        /// <summary>The pack's code name (the "&lt;codeName&gt;" in "&lt;kind&gt;.&lt;codeName&gt;.pack").</summary>
        public string CodeName { get; private set; }

        /// <summary>The kind-named assets subfolder items are read from ("&lt;ResFolderPath&gt;&lt;kind&gt;\").</summary>
        public string AssetsFolderPath => ResFolderPath + EnumCodes.EnumAsToken(PackKind) + FS.DSep;
        /// <summary>The pack file name, "&lt;kind&gt;.&lt;codeName&gt;.pack".</summary>
        public string PackFileName => EnumCodes.EnumAsToken(PackKind) + AS.DOT + CodeName + ResPack.DOT_pack;
        /// <summary>The pack file's full path (ResFolderPath + PackFileName).</summary>
        public string PackFilePath => ResFolderPath + PackFileName;

        /// <summary>The build stamp written into the pack header (creation time of this builder).</summary>
        public string PackStamp { get; private set; } = DateTime.Now.ToString(AS.STAMP_date_HHmm);

        /// <summary>The only FileExt.KindId admitted into this pack (ResPack.KindOf(PackKind); unknown = unrestricted).</summary>
        public FileExt.KindId OnlyKind => ResPack.KindOf(PackKind);

        /// <summary>
        /// Builds the pack from the named items (file names, or folder names
        /// recursed in full, all relative to <see cref="AssetsFolderPath"/>);
        /// null when built, else the Issue. Items that are absent, wrong-kind,
        /// oversize, or duplicate-named are silently ignored; a failed build
        /// deletes the partial pack file (best effort).
        /// </summary>
        public Issue TryBuild(params object[] theAssetsFolder_filesnames_or_foldersnames)
        {
            return _TryBuild(theAssetsFolder_filesnames_or_foldersnames);
        }

        private Issue _TryBuild(object[] xItems)
        {
            // ------------------------------------------------------------
            // Local helper (C# 7.3 safe - no static local functions)
            // ------------------------------------------------------------
            bool addedFile(
                BytesWriter xWriter,
                ItemStack<ResPack.ItemInfo> xManifest,
                ref byte[] xBuffer,
                string sFilePath)
            {
                FileExt vExt = FileExt.From(sFilePath);
                if (OnlyKind != FileExt.KindId.unknown && vExt.Kind != OnlyKind)
                    return false;

                using (var xStream1 = FS.GetFileReader_or_null(sFilePath, out Issue xIssue))
                {
                    if (xIssue != null)
                        return false;

                    FileInfo xInfo = new FileInfo(sFilePath);
                    if (xInfo.Length > int.MaxValue)
                        return false;

                    int iDup = -1;
                    while (++iDup < xManifest.Count)
                        if (0 == string.Compare(xManifest[iDup].NameDotExt, xInfo.Name, StringComparison.OrdinalIgnoreCase))
                            return false;

                    ResPack.ItemInfo vInfo = new ResPack.ItemInfo
                    {
                        FileLength = (int)xInfo.Length,
                        NameDotExt = xInfo.Name,
                        LastWrite = xInfo.LastWriteTime,
                        Position = xWriter.BaseStream.Position
                    };

                    xWriter.ReadAndWrite(xStream1, ref xBuffer);

                    vInfo.StreamLength = (int)(xWriter.BaseStream.Position - vInfo.Position);
                    xManifest.Push(vInfo);
                }

                return true;
            }

            // ------------------------------------------------------------
            // Begin main writer logic
            // ------------------------------------------------------------
            int iIgnoreCount = 0;

            try
            {
                using (var xStream2 = FS.GetFileWriter_or_null(PackFilePath, out Issue xIssue))
                {
                    if (xIssue != null)
                        return xIssue;

                    using (var xWriter = new BytesWriter(xStream2))
                    {
                        byte[] xBuffer = null;

                        // Write header
                        string sHeader = AS.VBAR + PackFileName + AS.VBAR + PackStamp + AS.VBAR;
                        xWriter.WriteAny_Bytes(Encoding.ASCII.GetBytes(sHeader));

                        var xManifest = new ItemStack<ResPack.ItemInfo>();

                        // ----------------------------------------------------
                        // Add files
                        // ----------------------------------------------------
                        if (xItems != null)
                        {
                            foreach (object xItem in xItems)
                            {
                                if (xItem == null) // FIX: null item NRE
                                {
                                    iIgnoreCount++;
                                    continue;
                                }

                                string sItem = xItem.ToString();
                                string sPath = AssetsFolderPath + sItem;

                                if (Directory.Exists(sPath))
                                {
                                    string[] xFiles = Directory.GetFiles(sPath, AS.STAR_DOT_STAR, SearchOption.AllDirectories);
                                    foreach (string sFilePath in xFiles)
                                        if (!addedFile(xWriter, xManifest, ref xBuffer, sFilePath))
                                            iIgnoreCount++;
                                }
                                else if (File.Exists(sPath))
                                {
                                    if (!addedFile(xWriter, xManifest, ref xBuffer, sPath))
                                        iIgnoreCount++;
                                }
                                else
                                {
                                    iIgnoreCount++;
                                }
                            }
                        }

                        // ----------------------------------------------------
                        // Write manifest tail - ALWAYS, even for empty/null
                        // items. TAIL FORMAT (single source of truth, read by
                        // ResPack._GotReader):
                        //   [count Int32][ItemInfos][manifestPos Int64][lenByte]
                        // where manifestPos = position of count, and lenByte =
                        // byte length of the manifestPos field (last byte).
                        // ----------------------------------------------------
                        xManifest.Sort();

                        long iManifestPos = xStream2.Position;

                        xWriter.WriteAny_Int32(xManifest.Count);

                        foreach (var item in xManifest)
                            item.SaveBytes(xWriter);

                        long iPosPosBot = xStream2.Position;

                        xWriter.WriteAny_Int64(iManifestPos);

                        byte iPosPosLen = (byte)(xStream2.Position - iPosPosBot);

                        xWriter.Write(iPosPosLen);
                    }

                    return null;
                }
            }
            catch (Exception e)
            {
                // best-effort cleanup of partial pack (streams already
                // disposed by using-unwind before we get here)
                try { if (File.Exists(PackFilePath)) File.Delete(PackFilePath); } catch { }
                return _Issue(11, "ResPack build failed", PackFilePath, e);
            }
        }

    } // ResPackBuilder
} // namespace
