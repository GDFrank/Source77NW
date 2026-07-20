// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

#if NETCOREAPP && !NO_CORE
#define USE_CORE
#endif


using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Source77NW
{
    /// <summary>Contract for types that save their state to a BytesWriter.</summary>
    public interface IBytesWriter
    {
        /// <summary>Saves this instance's state to writer.</summary>
        void SaveBytes(BytesWriter writer);
    }

    /// <summary>
    /// BinaryWriter specialized for the house byte formats: 7-bit-encoded
    /// "Any" integers (paired with BytesReader.ReadAny_*), length-prefixed
    /// byte blocks, Guids, stream copy-through, plus in-stream GZip
    /// compression and encryption layered via OutStream hot-swapping.
    /// </summary>
    /// <remarks>
    /// NOT thread safe. Restrictions mirror BytesReaderStream: encryption is
    /// not allowed while compressing; only one compression and one encryption
    /// may be active at a time. Dispose unwinds and disposes every layered
    /// stream (flushing any pending crypto final block) down to and including
    /// the base stream.
    /// </remarks>
    public sealed class BytesWriter : BinaryWriter, IDisposable
    {
        /// <summary>Largest byte[] WriteAny_Bytes accepts (int.MaxValue).</summary>
        public const int MaxAnyBytesLength = int.MaxValue;

        /// <summary>True with a writer over theFilePath, else false with the Issue.</summary>
        public static bool Created(string theFilePath, out BytesWriter returnWriter, out Issue returnIssue)
        {
            Stream xStream = FS.GetFileWriter_or_null(theFilePath, out returnIssue);
            if (xStream != null)
            {
                returnWriter = new BytesWriter(xStream);
                return true;
            }
            returnWriter = null;
            return false;
        }

        private const ushort issueSource = 65002;

        private static Issue _issue(issueId iErr) => Issue.Create(issueSource, ((byte)iErr), iErr.ToString(), IssueKind.BadOperation);

        private enum issueId : byte
        {
            None, // 0
            Cannot_WriteAny_Bytes_gt_Int32MaxSize,
            Already_compressing,
            Not_compressing,
            Current_stream_not_GZipStream,
            GZipStream_not_allowed,
            CryptoStream_not_allowed,
            Encryption_not_allowed_while_compressing,
            Already_encrypting,
            Not_encrypting,
            Current_stream_not_CryptoStream,
        }

        /// <summary>A write-something callback taking the writer.</summary>
        public delegate void WriteSomethingDO(BytesWriter theWriter);

        /// <summary>BitConverter.IsLittleEndian.</summary>
        public static bool IsLittleEndian => BitConverter.IsLittleEndian;

        /// <summary>Largest array length supported (int.MaxValue).</summary>
        public const int MaxArrayLength = int.MaxValue;

        private readonly Stream[] _Streams = new Stream[2];  // allows for GZipStream and CryptoStream

        private int _Streams_Top = 0;

        /// <summary>Creates a writer over a new MemoryStream of the given capacity.</summary>
        public BytesWriter(int theMemoryStreamCapacity)
        {
            OutStream = new MemoryStream(theMemoryStreamCapacity);
        }

        /// <summary>Creates a writer over theStream. Throws (Issue) when it is a GZipStream or CryptoStream - layers are added via the Start* methods, never pre-wrapped.</summary>
        public BytesWriter(Stream theStream)
        {
            OutStream = theStream;
            if (theStream is GZipStream) throw _issue(issueId.GZipStream_not_allowed);
            if (theStream is CryptoStream) throw _issue(issueId.CryptoStream_not_allowed);
        }

        /// <summary>Unwinds and disposes every layered stream (flushing any pending crypto final block) down to and including the base stream.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                while (true)
                {
                    if (OutStream == null)
                        break;

                    if (OutStream is GZipStream)
                    {
                        IsCompressing = false;
                    }
                    else if (OutStream is CryptoStream xCrypto)
                    {
                        if (!xCrypto.HasFlushedFinalBlock)
                        {
                            xCrypto.FlushFinalBlock();
                        }
                        IsEncrypting = false;
                    }

                    try { OutStream.Dispose(); } catch { };

                    if (_Streams_Top > 0)
                    {
                        OutStream = _Streams[--_Streams_Top];
                        continue;
                    }

                    OutStream = null;
                    break;
                }
            }

            // DO NOT CALL: base.Dispose(disposing);
        }

        /// <summary>Same as Dispose (BinaryWriter.Close forwards to Dispose(true)).</summary>
        public void Close_and_Dispose()
        {
            Dispose(); // Close() now just forwards to Dispose(true) via base BinaryWriter.Close()
        }

        /// <summary>True while GZip compression is layered in.</summary>
        public bool IsCompressing { get; private set; } = false;

        /// <summary>True while encryption is layered in.</summary>
        public bool IsEncrypting { get; private set; } = false;

        /// <summary>Layers GZip compression over the current stream. Throws (Issue) when already compressing.</summary>
        public void StartCompression()
        {
            if (IsCompressing) throw _issue(issueId.Already_compressing);
            GZipStream xZip = new GZipStream(BaseStream, CompressionLevel.Optimal, true);
            _Streams[_Streams_Top++] = OutStream;
            OutStream = xZip;
            IsCompressing = true;
        }

        /// <summary>Removes the compression layer, restoring the stream beneath. Throws (Issue) when not compressing.</summary>
        public void StopCompression()
        {
            if (!IsCompressing) throw _issue(issueId.Not_compressing);
            if (!(OutStream is GZipStream)) throw _issue(issueId.Current_stream_not_GZipStream);

            OutStream.Dispose(); // leaves BaseStream open
            OutStream = null;
            if (_Streams_Top > 0)
                OutStream = _Streams[--_Streams_Top];

            IsCompressing = false;
        }

        /// <summary>Layers encryption with theKey over the current stream. False with the Issue when already encrypting or compressing; false without one when the key yields no stream.</summary>
        public bool StartedEncryption(CryptoKey theKey, out Issue returnIssue)
        {
            if (IsEncrypting)
            {
                returnIssue = _issue(issueId.Already_encrypting);
                return false;
            }

            if (IsCompressing)
            {
                returnIssue = _issue(issueId.Encryption_not_allowed_while_compressing);
                return false;
            }

            returnIssue = null;
            CryptoStream xCrypto =  theKey.GetCryptoStream_or_null(OutStream, false);
            if (xCrypto == null)
                return false;

            // HOT SWAP
            _Streams[_Streams_Top++] = OutStream;
            OutStream = xCrypto;
            IsEncrypting = true;
            return true;
        }

        /// <summary>Flushes the crypto final block and removes the encryption layer, restoring the stream beneath. Throws (Issue) when not encrypting.</summary>
        public void StopEncryption()
        {
            if (!IsEncrypting)
                throw _issue(issueId.Not_encrypting);
            if (!(OutStream is CryptoStream xCrypto))
                throw _issue(issueId.Current_stream_not_CryptoStream);
            if (!xCrypto.HasFlushedFinalBlock)
                xCrypto.FlushFinalBlock();
            OutStream.Dispose(); // leaves BaseStream open
            OutStream = null;
            if (_Streams_Top > 0)
                OutStream = _Streams[--_Streams_Top];
            IsEncrypting = false;
        }

        /// <summary>Writes theInt32 7-bit-encoded (pairs with BytesReader.ReadAny_Int32).</summary>
        public void WriteAny_Int32(int theInt32) => Write7BitEncodedInt(theInt32);

        /// <summary>Writes theUInt32 7-bit-encoded (pairs with BytesReader.ReadAny_UInt32).</summary>
        public void WriteAny_UInt32(uint theUInt32) => Write7BitEncodedInt((int)theUInt32);
        // Roundtrip VERIFIED 2026-07-08 with ReadAny_UInt32: same raw-bit 7-bit
        // codec both ways (no zigzag); values > Int32.MaxValue travel as the
        // same 5-byte sequence, so every uint value roundtrips exactly.

        /// <summary>Writes theInt64 as hi-int then lo-uint, both 7-bit-encoded (pairs with BytesReader.ReadAny_Int64).</summary>
        public void WriteAny_Int64(long theInt64)
        {
            uint iLo = (uint)(theInt64 & uint.MaxValue);
            int iHi = (int)(theInt64 >> 32);
            Write7BitEncodedInt(iHi);
            WriteAny_UInt32(iLo);
        }

        /// <summary>Writes theUInt64 7-bit-encoded (pairs with BytesReader.ReadAny_UInt64).</summary>
        public void WriteAny_UInt64(ulong theUInt64) =>  WriteAny_Int64((long)theUInt64);

        /// <summary>Writes theBytes length-prefixed (null writes length -1; pairs with BytesReader.ReadAny_Bytes). Throws (Issue) above MaxAnyBytesLength.</summary>
        public void WriteAny_Bytes(byte[] theBytes)
        {
            if (theBytes == null)
            {
                // ReadAny_Bytes decodes a negative length as null, so write -1 for null.
                WriteAny_Int32(-1);
                return;
            }
            if (theBytes.LongLength > MaxAnyBytesLength)
                throw _issue(issueId.Cannot_WriteAny_Bytes_gt_Int32MaxSize);
            WriteAny_Int32(theBytes.Length);
            Write(theBytes);
        }

        /// <summary>Writes theGuid (always 16 bytes; pairs with BytesReader.ReadGuid).</summary>
        public void WriteGuid(Guid theGuid) => Write(theGuid.ToByteArray()); // always 16 bytes

        private const int _DefaultBufferSize = 81920; // Microsoft's choice

        /// <summary>Copies theSourceStream through to this writer using refBuffer (allocated at 81920 when null); returns total bytes written.</summary>
        public int ReadAndWrite(Stream theSourceStream, ref byte[] refBuffer)
        {
            int iTotalWritten = 0;
            if (refBuffer == null)
                refBuffer = new byte[_DefaultBufferSize];

            int iRead;
            while ((iRead = theSourceStream.Read(refBuffer, 0, refBuffer.Length)) > 0)
            {
                Write(refBuffer, 0, iRead);
                iTotalWritten += iRead;
            }

            return iTotalWritten;
        }

    }
}
