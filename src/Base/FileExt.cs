// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Source77NW
{
    /// <summary>
    /// Extension classifier for a curated set of file extensions:
    /// <see cref="KindId"/> categorizes PROBABLE content and text vs
    /// binary, with an ignore-case binary-search lookup table and the
    /// nested <see cref="Filter"/> for selecting files by kind.
    /// </summary>
    /// <remarks>
    /// <see cref="From(string)"/> clips the dot-extension from the tail
    /// of the path; unknown extensions classify as
    /// <see cref="KindId.unknown"/>. <see cref="IsCompressed"/> marks
    /// formats that are inherently compressed (re-compression futile).
    /// </remarks>
    public struct FileExt : IComparable<FileExt>, IComparable<string>
    {
        /// <summary>
        /// Probable-content categories; ordering contract: text kinds
        /// precede <see cref="doc"/> (the first binary kind), with
        /// OS/app-dependent kinds last.
        /// </summary>
        public enum KindId : byte
        {
            // PROBABLE textual
            /// <summary>User/system/app text file.</summary>
            txt,
            /// <summary>Compile source (cs h c cpp ...).</summary>
            src,
            /// <summary>Anything web (html css js ts json).</summary>
            web,
            /// <summary>Anything cmd (bat cmd ps1 ps2).</summary>
            cmd,
            /// <summary>csv or tsv or lsv.</summary>
            csv,
            /// <summary>App startup ini text.</summary>
            ini,
            /// <summary>xml.</summary>
            xml,
            /// <summary>Windows Internet shortcut.</summary>
            url,
            /// <summary>Image/icon text.</summary>
            svg,

            // PROBABLE binary
            /// <summary>User app file.</summary>
            doc,
            /// <summary>System/app files.</summary>
            bin,
            /// <summary>Zip/archive files.</summary>
            zip,
            /// <summary>Windows/Apple binary links.</summary>
            link,
            /// <summary>Image files (including bmp).</summary>
            image,
            /// <summary>Video files.</summary>
            video,
            /// <summary>Audio files.</summary>
            audio,

            // EITHER - OS/APP dependent
            /// <summary>Backup file (content OS/app dependent).</summary>
            bak,
            /// <summary>Unclassified extension.</summary>
            unknown,
        }

        #region // STRUCT Filter
        /// <summary>
        /// A KindId set as a bit flag for fast file selection by kind.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size = 4)]
        public struct Filter
        {
            // KindId as a bit flag for faster "selection"

            private uint _FilterFlag;

            private static uint _Flag(KindId iKind) => (uint)(1 << (int)iKind);

            /// <summary>Clears all kinds.</summary>
            public void Clear() { _FilterFlag = 0; }

            /// <summary>True when any kind is selected.</summary>
            public bool HasKinds => _FilterFlag != 0;

            /// <summary>Selects every kind.</summary>
            public void AddAll()
            {
                int i = -1;
                while (++i < KindId_Count)
                    _FilterFlag |= (uint)1 << i;
            }

            /// <summary>A Filter selecting the given kinds.</summary>
            public static Filter GetFilter(params KindId[] theKinds)
            {
                Filter v = new Filter();
                int i = -1;
                while (++i < theKinds.Length)
                    v._FilterFlag |= (uint)1 << (int)theKinds[i];
                return v;
            }

            /// <summary>True when the kind is selected.</summary>
            public bool Selected(KindId theKind) => 0 != (_FilterFlag & _Flag(theKind));

            /// <summary>True when the FileExt's kind is selected.</summary>
            public bool Selected(FileExt theExt) => Selected(theExt.Kind);

            /// <summary>True when the file's kind is selected.</summary>
            public bool Selected(FileInfo theInfo) => Selected(From(theInfo).Kind);

            /// <summary>True when the path's kind is selected.</summary>
            public bool Selected(string thePath) => Selected(From(thePath).Kind);

            /// <summary>Enumerates the selected kinds; false at end (returnGroup = an invalid id).</summary>
            public bool GotNext(ref int cursor, out KindId returnGroup)
            {
                while (cursor >= 0 && cursor < KindId_Count)
                {
                    KindId iKind = (KindId)cursor;
                    int iFlag = 1 << cursor++;
                    if (0 != (iFlag & _FilterFlag))
                    {
                        returnGroup = iKind;
                        return true;
                    }
                }
                returnGroup = (KindId)KindId_Count; // INVALID ID
                return false;
            }

            /// <summary>The selected kind names, space separated.</summary>
            public override string ToString()
            {
                int cursor = 0;
                string s = string.Empty;
                while (GotNext(ref cursor, out KindId iId))
                {
                    if (s.Length > 0)
                        s += AS.SP + iId.ToString();
                    else
                        s = iId.ToString();
                }
                return s;
            }
        }
        #endregion

        private const ushort issueSource = 65235;
        private const char DOTchar = '.';

        private const KindId _Id_first_binary = KindId.doc;
        /// <summary>Count of KindId members.</summary>
        public const int KindId_Count = (int)KindId.unknown + 1;


        /// <summary>The FileExt for the path (clips the dot-extension from the tail; dots inside folder/volume names are ignored).</summary>
        public static FileExt From(string thePath) => _NewFileExt(thePath); // clips DotExt from tail end
        /// <summary>The FileExt for the file's extension.</summary>
        public static FileExt From(FileInfo theFileInfo) => _NewFileExt(theFileInfo.Extension);

        /// <summary>Parses a KindId member name; false when no match.</summary>
        public static bool ParsedKindId(string theText, out KindId returnId) => Enum.TryParse(theText, out returnId);
        /// <summary>Enumerates the classified-extension table; false at end.</summary>
        public static bool GotNextFileExt(ref int cursor, out FileExt returnExt)
        {
            if (cursor >= 0 && cursor < _items.Length)
            {
                returnExt = _items[cursor];
                cursor++;
                return true;
            }
            returnExt = default;
            return false;
        }


        // ======== INSTANCE ===========

        /// <summary>The dot-extension (".txt" form).</summary>
        public string DotExtension { get; private set; }

        /// <summary>The classified kind.</summary>
        public KindId Kind { get; private set; }

        /// <summary>Extra classification note for an extension.</summary>
        public enum NoteId : byte
        {
            /// <summary>No note.</summary>
            None = 0,
            /// <summary>The format is inherently compressed (re-compression futile).</summary>
            IsCompressed = 1,
            /// <summary>Classified src (TypeScript) but may be an MPEG transport stream video.</summary>
            Possible_MPEG_TS = 2,
            /// <summary>Classified VisualStudio solution.</summary>
            VisualStudioSolution = 3,
            /// <summary>Classified VisualStudio project.</summary>
            VisualStudioProject = 4,

        }

        private NoteId _Note;

        /// <summary>The extension's classification note.</summary>
        public NoteId Note => _Note;

        /// <summary>Orders by DotExtension, ordinal ignore-case.</summary>
        public int CompareTo(FileExt other) => string.Compare(DotExtension, other.DotExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>Orders against the extension (normalized to dot form first).</summary>
        public int CompareTo(string theExtension)
        {
            FileExt vExt = _NewFileExt(theExtension);
            return CompareTo(vExt);
        }

        /// <summary>
        /// True for a FileExt with the same DotExtension (ignore-case), or
        /// a string that IS a dot-extension matching it; a bare "txt"
        /// string never equals ".txt".
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is FileExt vFileExt)
                return string.Equals(DotExtension, vFileExt.DotExtension, StringComparison.OrdinalIgnoreCase);

            if (obj is string sExtension)
                if (sExtension.Length > 1 && sExtension[0] == DOTchar)
                    return string.Equals(DotExtension, sExtension, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        /// <summary>Ignore-case hash of the DotExtension (0 for a default instance), consistent with Equals.</summary>
        public override int GetHashCode() => DotExtension == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(DotExtension);

        /// <summary>The quoted DotExtension and kind name.</summary>
        public override string ToString() => AS.QUOTE + DotExtension + AS.QUOTE + AS.SP + Kind.ToString();

        /// <summary>True for the PROBABLE-textual kinds (bak/unknown count as NOT text).</summary>
        public bool IsText => Kind < _Id_first_binary;

        /// <summary>True when the kind is image.</summary>
        public bool IsImage => Kind == KindId.image;

        /// <summary>True when the kind is video.</summary>
        public bool IsVideo => Kind == KindId.video;

        /// <summary>True when the kind is audio.</summary>
        public bool IsAudio => Kind == KindId.audio;

        /// <summary>True when the format is inherently compressed (see <see cref="NoteId.IsCompressed"/>).</summary>
        public bool IsCompressed => _Note == NoteId.IsCompressed;

        /// <summary>True when the kind is any of the given kinds.</summary>
        public bool IsKind(params KindId[] ofKinds)
        {
            foreach (KindId iKind in ofKinds)
                if (Kind == iKind) return true;

            return false;
        }


        // STATIC MAPPING TABLE

        private static FileExt _NewFileExt(string theExtension)
        {
            if (theExtension == null) theExtension = String.Empty;
            int iDot = theExtension.LastIndexOf(DOTchar);
            int iSep = theExtension.LastIndexOf('\\');
            int i2 = theExtension.LastIndexOf('/');
            if (i2 > iSep) iSep = i2;
            i2 = theExtension.LastIndexOf(':');
            if (i2 > iSep) iSep = i2;
            if (iDot <= iSep) iDot = -1; // a dot inside a folder/volume name is not an extension
            if (iDot < 0)
                theExtension = DOTchar + theExtension;
            else if (iDot > 0)
                theExtension = theExtension.Substring(iDot);
            FileExt vItem = new FileExt()
            {
                DotExtension = theExtension
            };
            int index = Array.BinarySearch(_items, vItem); // compares ignoreCase
            if (index >= 0)
                return _items[index]; // return normalized DotExtention

            return _init(theExtension, KindId.unknown);
        }

        private static FileExt _init(string sDotExt, KindId iKind, NoteId iNote = 0)
        {
            return new FileExt()
            {
                DotExtension = sDotExt,
                Kind = iKind,
                _Note = iNote
            };
        }

#if DEBUG // VERIFY _items in sequence
        static FileExt()
        {
            FileExt vLast = _items[0];
            int i = 0;
            while (++i < _items.Length)
            {
                FileExt v1 = _items[i];
                int iCompare = vLast.CompareTo(v1);
                if (iCompare >= 0)
                {
                    throw Issue.Create(issueSource, 86
                        , typeof(FileExt).Name
                        , vLast.DotExtension
                        , v1.DotExtension
                        );
                }
                vLast = v1;
            }
        }
#endif

        private static readonly FileExt[] _items = new FileExt[] {
_init(".3gp", KindId.video),
_init(".7z", KindId.zip, NoteId.IsCompressed),
_init(".aac", KindId.audio, NoteId.IsCompressed),
_init(".ac3", KindId.audio, NoteId.IsCompressed),
_init(".aif", KindId.audio),
_init(".alias", KindId.link),
_init(".apk", KindId.zip, NoteId.IsCompressed),
_init(".apng", KindId.image),
_init(".ar", KindId.zip),
_init(".arc", KindId.zip, NoteId.IsCompressed),
_init(".avi", KindId.video, NoteId.IsCompressed),
_init(".avif", KindId.image, NoteId.IsCompressed),
_init(".avk", KindId.video),
_init(".bak", KindId.bak),
_init(".bas", KindId.src),
_init(".bat", KindId.cmd),
_init(".bin", KindId.bin),
_init(".bmp", KindId.image),
_init(".bz2", KindId.zip, NoteId.IsCompressed),
_init(".c", KindId.src),
_init(".c++", KindId.src),
_init(".cab", KindId.zip, NoteId.IsCompressed),
_init(".cda", KindId.audio),
_init(".cmd", KindId.cmd),
_init(".cpp", KindId.src),
_init(".cs", KindId.src),
_init(".csproj", KindId.ini, NoteId.VisualStudioProject),
_init(".css", KindId.web),
_init(".csv", KindId.csv),
_init(".cur", KindId.image),
_init(".dat", KindId.bin),         // mixed -> leave unflagged
_init(".db", KindId.bin),
_init(".dmg", KindId.zip, NoteId.IsCompressed),
_init(".doc", KindId.doc),
_init(".docm", KindId.doc),
_init(".docx", KindId.doc, NoteId.IsCompressed),
_init(".ear", KindId.zip, NoteId.IsCompressed),
_init(".epub", KindId.doc, NoteId.IsCompressed),  // ZIP container
_init(".exe", KindId.bin),
_init(".f4v", KindId.video, NoteId.IsCompressed),
_init(".flac", KindId.audio, NoteId.IsCompressed), // lossless but compressed
_init(".flv", KindId.video, NoteId.IsCompressed),
_init(".gif", KindId.image, NoteId.IsCompressed),
_init(".gz", KindId.zip, NoteId.IsCompressed),
_init(".h", KindId.src),
_init(".h264", KindId.video),
_init(".h265", KindId.video, NoteId.IsCompressed),
_init(".heic", KindId.image, NoteId.IsCompressed),
_init(".heif", KindId.image, NoteId.IsCompressed),
_init(".hevc", KindId.video, NoteId.IsCompressed),
_init(".htm", KindId.web),
_init(".html", KindId.web),
_init(".ico", KindId.image),
_init(".ini", KindId.ini),
_init(".iso", KindId.zip),         // not always compressed -> leave false
_init(".jar", KindId.zip, NoteId.IsCompressed),
_init(".jfif", KindId.image),
_init(".jif", KindId.image),
_init(".jp2", KindId.image, NoteId.IsCompressed), // JPEG 2000
_init(".jpe", KindId.image),
_init(".jpeg", KindId.image, NoteId.IsCompressed),
_init(".jpg", KindId.image, NoteId.IsCompressed),
_init(".js", KindId.web),
_init(".json", KindId.web),
_init(".jxl", KindId.image, NoteId.IsCompressed), // JPEG XL
_init(".lnk", KindId.link),
_init(".log", KindId.txt),
_init(".lsv", KindId.csv),
_init(".lz", KindId.zip, NoteId.IsCompressed),
_init(".lzma", KindId.zip, NoteId.IsCompressed),
_init(".m2ts", KindId.video, NoteId.IsCompressed),
_init(".m4a", KindId.audio, NoteId.IsCompressed),
_init(".m4p", KindId.audio, NoteId.IsCompressed),
_init(".m4v", KindId.video, NoteId.IsCompressed),
_init(".mid", KindId.audio),
_init(".midi", KindId.audio),
_init(".mka", KindId.audio, NoteId.IsCompressed),
_init(".mkv", KindId.video, NoteId.IsCompressed),
_init(".mov", KindId.video, NoteId.IsCompressed),
_init(".mp2", KindId.audio),
_init(".mp3", KindId.audio, NoteId.IsCompressed),
_init(".mp4", KindId.video, NoteId.IsCompressed),
_init(".mpe", KindId.video),
_init(".mpeg", KindId.video),
_init(".mpg", KindId.video),
_init(".mpv", KindId.video),
_init(".mts", KindId.video, NoteId.IsCompressed),
_init(".obj", KindId.bin),
_init(".odt", KindId.doc, NoteId.IsCompressed),
_init(".ogg", KindId.audio, NoteId.IsCompressed),
_init(".ogv", KindId.video),
_init(".opus", KindId.audio, NoteId.IsCompressed),
_init(".pak", KindId.zip, NoteId.IsCompressed),
_init(".pdf", KindId.doc, NoteId.IsCompressed),
_init(".png", KindId.image, NoteId.IsCompressed),
_init(".pps", KindId.doc),
_init(".ppt", KindId.doc),
_init(".pptx", KindId.doc, NoteId.IsCompressed),
_init(".ps1", KindId.cmd),
_init(".ps1xml", KindId.cmd),
_init(".ps2", KindId.cmd),
_init(".ps2xml", KindId.cmd),
_init(".qt", KindId.video),
_init(".rar", KindId.zip, NoteId.IsCompressed),
_init(".sln", KindId.ini, NoteId.VisualStudioSolution),
_init(".slnx", KindId.ini, NoteId.VisualStudioSolution),
_init(".svg", KindId.svg),
_init(".tar", KindId.zip),
_init(".taz", KindId.zip),
_init(".tbz2", KindId.zip, NoteId.IsCompressed),
_init(".tgz", KindId.zip, NoteId.IsCompressed),
_init(".tiff", KindId.image, NoteId.IsCompressed),
_init(".ts", KindId.src, NoteId.Possible_MPEG_TS),
_init(".tsv", KindId.csv),
_init(".txt", KindId.txt),
_init(".txz", KindId.zip, NoteId.IsCompressed),
_init(".url", KindId.url),
_init(".vb", KindId.src),
_init(".vbs", KindId.cmd),
_init(".vhd", KindId.bin, NoteId.IsCompressed),
_init(".vhdx", KindId.bin, NoteId.IsCompressed),
_init(".vmdk", KindId.bin, NoteId.IsCompressed),
_init(".vob", KindId.video),
_init(".war", KindId.zip, NoteId.IsCompressed),
_init(".wav", KindId.audio),
_init(".webm", KindId.video, NoteId.IsCompressed),
_init(".webp", KindId.image, NoteId.IsCompressed),
_init(".wma", KindId.audio, NoteId.IsCompressed),
_init(".wmv", KindId.video, NoteId.IsCompressed),
_init(".xls", KindId.doc),
_init(".xlsb", KindId.doc),
_init(".xlsm", KindId.doc),
_init(".xlsx", KindId.doc, NoteId.IsCompressed),
_init(".xml", KindId.xml),
_init(".xps", KindId.xml),
_init(".xz", KindId.zip, NoteId.IsCompressed),
_init(".zip", KindId.zip, NoteId.IsCompressed),
_init(".zst", KindId.zip, NoteId.IsCompressed),   // Zstandard
        };
    }
}
