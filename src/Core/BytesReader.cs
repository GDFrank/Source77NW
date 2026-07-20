// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

#if NETCOREAPP && !NO_CORE
#define USE_CORE
#endif

using System;
using System.IO;

namespace Source77NW
{
    /// <summary>Contract for types that load their state from a BytesReader.</summary>
    public interface IBytesReader
    {
        /// <summary>Loads this instance's state from reader.</summary>
        void LoadBytes(BytesReader reader);
    }

    /// <summary>
    /// BinaryReader specialized for the house byte formats: 7-bit-encoded
    /// "Any" integers (paired with BytesWriter.WriteAny_*), length-prefixed
    /// byte blocks, Guids, plus in-stream decompression and decryption via
    /// its BytesReaderStream base stream.
    /// </summary>
    /// <remarks>
    /// NOT thread safe. Restrictions: decryption is not allowed while
    /// decompressing; decompression IS allowed while decrypting; only one
    /// decryption and one decompression may be active at a time.
    /// </remarks>
    public sealed class BytesReader : BinaryReader, IDisposable //, Exe.IDefaultCreationDisabled
    {
        private const ushort issueSource = 65001;

        /// <summary>BitConverter.IsLittleEndian.</summary>
        public static bool IsLittleEndian => BitConverter.IsLittleEndian;

        private readonly BytesReaderStream _BaseStream;

        /// <summary>The underlying BytesReaderStream.</summary>
        public override Stream BaseStream => _BaseStream;

        /// <summary>True with a reader over theFilePath, else false with the Issue.</summary>
        public static bool Created(string theFilePath, out BytesReader returnReader, out Issue returnIssue)
        {
            returnReader = Create_or_null(theFilePath, out returnIssue);

            return returnReader != null;
        }

        /// <summary>A reader over theFilePath, or null with the Issue.</summary>
        public static BytesReader Create_or_null(string theFilePath, out Issue returnIssue)
        {
            returnIssue = null;

            Stream xStream = FS.GetFileReader_or_null(theFilePath, out returnIssue);

            if (xStream == null)
                return null;

            return Create_or_null(xStream, out returnIssue);
        }

        /// <summary>A reader over theInputStream (wrapped in a BytesReaderStream unless it is one), or null with the Issue when the stream is null.</summary>
        public static BytesReader Create_or_null(Stream theInputStream, out Issue returnIssue)
        {
            returnIssue = null;

            if (theInputStream is BytesReaderStream x1)
                return new BytesReader(x1);

            if (theInputStream != null)
                return new BytesReader(new BytesReaderStream(theInputStream));

            returnIssue = Issue.Create(issueSource, 86, typeof(BytesReader).Name, IssueKind.NullReference);
            return null;
        }

        private BytesReader(BytesReaderStream theInputStream) : base(theInputStream)
        {
            _BaseStream = theInputStream;
        }

        /// <summary>Close then Dispose (Close exceptions swallowed).</summary>
        public void Close_and_Dispose()
        {
            try { Close(); } catch { };
            Dispose();
        }

        /// <summary>True while in-stream decompression is active.</summary>
        public bool IsDecompressing => _BaseStream.IsDecompressing;

        /// <summary>True while in-stream decryption is active.</summary>
        public bool IsDecrypting => _BaseStream.IsDecrypting;

        /// <summary>Starts in-stream decompression (see remarks for restrictions).</summary>
        public void StartDecompression() => _BaseStream.StartedDecompression();

        /// <summary>Stops in-stream decompression.</summary>
        public void StopDecompression() => _BaseStream.StoppedDecompression();

        /// <summary>Starts in-stream decryption with theKey; false with the Issue on failure (see remarks for restrictions).</summary>
        public bool StartedDecryption(CryptoKey theKey, out Issue returnIssue)
        {
            return _BaseStream.StartedDecryption(theKey, out returnIssue);
        }

        /// <summary>Stops in-stream decryption; false when none was active.</summary>
        public bool StoppedDecryption() => _BaseStream?.StoppedDecryption() ?? false;

        /// <summary>Reads a 7-bit-encoded Int32 (pairs with BytesWriter.WriteAny_Int32).</summary>
        public int ReadAny_Int32() => Read7BitEncodedInt();

        /// <summary>Reads a 7-bit-encoded UInt32 (pairs with BytesWriter.WriteAny_UInt32).</summary>
        public uint ReadAny_UInt32() => (uint)Read7BitEncodedInt();
        // VERIFIED 2026-07-08 vs BytesWriter.WriteAny_UInt32: both cast through
        // int and use the same 7-bit codec (raw bit pattern, no zigzag). Values
        // > Int32.MaxValue are a 5-byte sequence both ways (35 bits holds 32),
        // so the two's-complement roundtrip is exact for all uint values.


        /// <summary>Reads a 7-bit-encoded Int64 written as hi-int then lo-uint (pairs with BytesWriter.WriteAny_Int64).</summary>
        public long ReadAny_Int64()
        {
            long iHi = Read7BitEncodedInt();

            uint iLo = ReadAny_UInt32();

            return (iHi << 32) | iLo;

            // VERIFIED 2026-07-08 vs BytesWriter.WriteAny_Int64: writer emits
            // hi (sign-extended int) then lo (uint); read order matches, and
            // "long | uint" zero-extends iLo, so all Int64 values roundtrip.
        }

        /// <summary>Reads a 7-bit-encoded UInt64 (pairs with BytesWriter.WriteAny_UInt64).</summary>
        public ulong ReadAny_UInt64() => (ulong)ReadAny_Int64();

        /// <summary>Reads a length-prefixed byte block (pairs with BytesWriter.WriteAny_Bytes); a negative length yields null.</summary>
        public byte[] ReadAny_Bytes()
        {
            int iLen = ReadAny_Int32();
            if (iLen < 0)
                return null;
            return ReadBytes(iLen);
        }

        /// <summary>Reads a Guid (always 16 bytes).</summary>
        public Guid ReadGuid() => new Guid(ReadBytes(16)); // always 16 bytes

    }
}
