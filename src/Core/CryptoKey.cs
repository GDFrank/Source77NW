// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Security.Cryptography;

namespace Source77NW
{
    /// <summary>
    /// Identifies the cipher behind a <see cref="CryptoKey"/>. Values below
    /// <see cref="CryptoKind.Aes"/> (None, XOR, Jumble) are EMBEDDED kinds:
    /// fixed singleton keys compiled into the executable, identified by a
    /// synthetic Guid. Aes and above are generated kinds carrying random key
    /// material and a fresh Guid. Persisted as a single byte (see
    /// <see cref="CryptoKey.SaveBytes"/>); values are a storage contract -
    /// do not renumber.
    /// </summary>
    public enum CryptoKind : byte
    {
        /// <summary>No encryption; a None key yields no CryptoStream (pass-through).</summary>
        None = 0,

        /// <summary>Byte-wise complement transform. Obfuscation only - confuses
        /// casual inspection, provides zero security.</summary>
        XOR = 1,

        /// <summary>AES with a fixed key/IV compiled into the executable.
        /// Not secure when the executable is available.</summary>
        Jumble = 2,

        /// <summary>AES with generated key material (Rijndael deprecated).</summary>
        Aes = 3,

        /// <summary>TripleDES with generated key material and assigned Guid.</summary>
        TripleDES = 4,

        /// <summary>DES with generated key material and assigned Guid (legacy).</summary>
        DES = 5,
    }

    /// <summary>
    /// A symmetric-cipher key: a <see cref="CryptoKind"/>, a Guid identity,
    /// and (for generated kinds) key material. Instances come only from the
    /// three embedded singletons (<see cref="None"/>/<see cref="XOR"/>/
    /// <see cref="Jumble"/>) or the <see cref="Create(CryptoKind)"/>
    /// factories - all constructors are private, hence sealed.
    /// <see cref="GetCryptoStream_or_null"/> wraps any Stream for transparent
    /// encryption or decryption.
    /// </summary>
    /// <remarks>
    /// Persistence contract: <see cref="SaveBytes"/>/<see cref="LoadBytes"/>
    /// store Kind + Guid ONLY - key material is never written. The Guid acts
    /// as a lookup handle; the caller's key store resolves it back to key
    /// material. A loaded generated-kind key cannot produce a working
    /// CryptoStream until that resolution happens.
    /// </remarks>
    public sealed class CryptoKey : IDisposable
    {
        const ushort issueSource = 65032;

        const CryptoKind firstNonEmbeddedKind = CryptoKind.Aes;

        const byte firstNonEmbeddedIndex = (byte)firstNonEmbeddedKind;

        /// <summary>True when <paramref name="theKind"/> is an embedded kind
        /// (None, XOR, Jumble) backed by a compiled-in singleton.</summary>
        public static bool IsEmbeddedKind(CryptoKind theKind) => theKind < firstNonEmbeddedKind;

        /// <summary>True when <paramref name="theId"/> is the synthetic Guid
        /// of one of the embedded singleton keys.</summary>
        public static bool IsEmbeddedGuid(Guid theId) => GotEmbedded(theId, out _);

        /// <summary>The embedded no-encryption key (pass-through).</summary>
        public static CryptoKey None { get { return embeddedKeys[0]; } }

        /// <summary>The embedded XOR obfuscation key.</summary>
        public static CryptoKey XOR { get { return embeddedKeys[1]; } }

        /// <summary>The embedded fixed-AES (Jumble) key.</summary>
        public static CryptoKey Jumble { get { return embeddedKeys[2]; } }

        static CryptoKey[] embeddedKeys = new CryptoKey[]
            { new CryptoKey(0)
            , new CryptoKey(1)
            , new CryptoKey(2)
            };

        private CryptoKey(byte theEmbeddedKind_only) // for embedded only
        {
            // INITIAL
            _Guid = new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, theEmbeddedKind_only);
            _Kind = (CryptoKind)theEmbeddedKind_only;
            if (theEmbeddedKind_only == 2)
            {
                key = new byte[] { 0x83, 0xd1, 0x5a, 0x04, 0x18, 0x13, 0x44, 0x6d, 0x1b, 0x5c, 0x7e, 0x9b, 0xbd, 0x50, 0xf8, 0x22, 0x9d, 0xb6, 0x9f, 0x7d, 0x74, 0xa5, 0xa2, 0x31, 0xd1, 0x63, 0xd6, 0xf1, 0xf7, 0xf4, 0xa0, 0x88 };
                iv = new byte[] { 0x6c, 0xc2, 0x44, 0x1c, 0x13, 0x0d, 0x92, 0x33, 0xbb, 0x5e, 0x88, 0xfe, 0xcf, 0x50, 0xd0, 0xfb };
            }
            else
            {
                key = null;
                iv = null;
            }
        }

        private CryptoKey() // for Create methods
        {
            _Guid = Guid.Empty;
            _Kind = CryptoKind.None;
            key = null;
            iv = null;
        }

        /// <summary>Cursor-style enumeration over the embedded singleton keys.
        /// Start with <paramref name="cursor"/> = 0; returns false when
        /// exhausted (returnCryptoKey then = <see cref="None"/>).</summary>
        public static bool GotNextEmbedded(ref int cursor, out CryptoKey returnCryptoKey)
        {
            if (cursor >= 0 && cursor < firstNonEmbeddedIndex)
            {
                returnCryptoKey = embeddedKeys[cursor];
                cursor++;
                return true;
            }
            returnCryptoKey = None;
            return false;
        }

        /// <summary>Soft lookup of an embedded singleton by kind. False (with
        /// returnCryptoKey = <see cref="None"/>) when <paramref name="theKind"/>
        /// is not an embedded kind.</summary>
        public static bool GotEmbedded(CryptoKind theKind, out CryptoKey returnCryptoKey)
        {
            byte iKind = (byte)theKind;
            if (iKind < firstNonEmbeddedIndex)
            {
                returnCryptoKey = embeddedKeys[iKind];
                return true;
            }
            returnCryptoKey = None;
            return false;
        }

        /// <summary>Soft lookup of an embedded singleton by its synthetic Guid.
        /// False (with returnCryptoKey = <see cref="None"/>) when
        /// <paramref name="theKeyId"/> matches no embedded key.</summary>
        public static bool GotEmbedded(Guid theKeyId, out CryptoKey returnCryptoKey)
        {
            for (int i = 0; i < firstNonEmbeddedIndex; i++)
            {
                if (theKeyId == embeddedKeys[i]._Guid)
                {
                    returnCryptoKey = embeddedKeys[i];
                    return true;
                }
            }
            returnCryptoKey = None;
            return false;
        }

        /// <summary>Creates a key of <paramref name="theKind"/> using the
        /// largest legal key size. Embedded kinds return their shared
        /// singleton; generated kinds get fresh random key material and a
        /// new Guid.</summary>
        /// <exception cref="Issue">BadOperation on an unknown kind.</exception>
        public static CryptoKey Create(CryptoKind theKind) => Create(theKind, true);

        /// <summary>Creates a key of <paramref name="theKind"/>. Embedded kinds
        /// return their shared singleton (size flag ignored); generated kinds
        /// get fresh random key material at the largest or smallest legal key
        /// size and a new Guid.</summary>
        /// <param name="theKind">The crypto kind to create.</param>
        /// <param name="as_biggest_key_else_smallest">True = largest legal key
        /// size; false = smallest.</param>
        /// <exception cref="Issue">BadOperation on an unknown kind.</exception>
        public static CryptoKey Create(CryptoKind theKind, bool as_biggest_key_else_smallest)
        {
            switch (theKind)
            {
                case CryptoKind.Aes:
                    return _create_sym_algo(Aes.Create(), theKind, as_biggest_key_else_smallest);
                case CryptoKind.Jumble:
                    return embeddedKeys[2];
                case CryptoKind.XOR:
                    return embeddedKeys[1];
                case CryptoKind.TripleDES:
                    return _create_sym_algo(TripleDES.Create()
                        , theKind, as_biggest_key_else_smallest);
                case CryptoKind.DES:
                    return _create_sym_algo(DES.Create()
                        , theKind, as_biggest_key_else_smallest);
                case CryptoKind.None:
                    return embeddedKeys[0];
            }
            throw Issue.Create(issueSource, 3, typeof(CryptoKey), IssueKind.BadOperation);
        }

        /// <summary>Wraps <paramref name="theBaseStream"/> in a CryptoStream
        /// that decrypts on read or encrypts on write using this key. Returns
        /// null for a <see cref="CryptoKind.None"/> key - a valid result
        /// meaning "use the base stream directly".</summary>
        /// <param name="theBaseStream">The stream to wrap.</param>
        /// <param name="as_reader_else_writer">True = decrypting read stream;
        /// false = encrypting write stream.</param>
        /// <exception cref="Issue">ProgramIssue when the base stream cannot be
        /// read/written as requested, or the kind is unimplemented.</exception>
        public CryptoStream GetCryptoStream_or_null(Stream theBaseStream, bool as_reader_else_writer)
        {
            switch (_Kind)
            {
                case CryptoKind.Aes:
                    {
                        return _stream_sym(Aes.Create()
                            , theBaseStream, as_reader_else_writer);
                    }
                case CryptoKind.Jumble:
                    {
                        return _stream_sym(Aes.Create()
                            , theBaseStream, as_reader_else_writer);
                    }
                case CryptoKind.XOR:
                    {
                        return _stream_sym(XORTransform.Uno
                            , theBaseStream, as_reader_else_writer);
                    }
                case CryptoKind.TripleDES:
                    {
                        return _stream_sym(TripleDES.Create()
                            , theBaseStream, as_reader_else_writer);
                    }
                case CryptoKind.DES:
                    {
                        return _stream_sym(DES.Create()
                            , theBaseStream, as_reader_else_writer);
                    }
                case CryptoKind.None:
                    {
                    }
                    return null; // is valid return
            }

            throw Issue.Create(issueSource, 86, this, new NotImplementedException(_Kind.ToString()), IssueKind.ProgramIssue);
        }

        private static CryptoKey _create_sym_algo(SymmetricAlgorithm xAlgorithm, CryptoKind kind, bool biggest)
        {
            CryptoKey xKey = new CryptoKey()
            {
                _Kind = kind,
                _Guid = Guid.NewGuid()
            };
            int minSize = int.MaxValue;
            int maxSize = 0;
            for (int i = 0; i < xAlgorithm.LegalKeySizes.Length; i++)
            {
                if (minSize > xAlgorithm.LegalKeySizes[i].MinSize)
                    minSize = xAlgorithm.LegalKeySizes[i].MinSize;
                if (maxSize < xAlgorithm.LegalKeySizes[i].MaxSize)
                    maxSize = xAlgorithm.LegalKeySizes[i].MaxSize;
            }
            xAlgorithm.KeySize = biggest ? maxSize : minSize;
            xAlgorithm.GenerateIV();
            xAlgorithm.GenerateKey();

            xKey.key = xAlgorithm.Key;
            xKey.iv = xAlgorithm.IV;
            xAlgorithm.Dispose();
            return xKey;
        }

        private CryptoStream _stream_sym(SymmetricAlgorithm x, Stream baseStream, bool as_reader_else_writer)
        {
            CryptoStreamMode mode;
            if (as_reader_else_writer)
            {
                mode = CryptoStreamMode.Read;
                if (!baseStream.CanRead)
                    throw Issue.Create(issueSource, 100, this, IssueKind.ProgramIssue);
            }
            else
            {
                mode = CryptoStreamMode.Write;
                if (!baseStream.CanWrite)
                    throw Issue.Create(issueSource, 101, this, IssueKind.ProgramIssue);
            }
            if (key != null) // null-guard — embedded XOR/None keys have key/iv == null; SymmetricAlgorithm setters throw ArgumentNullException
                x.Key = key;

            if (iv != null) // null-guard (see key above)
                x.IV = iv;

            var xTrans = as_reader_else_writer ? x.CreateDecryptor() : x.CreateEncryptor();

            var cryptoStream = new CryptoStream(baseStream, xTrans, mode);

            if (!ReferenceEquals(x, XORTransform.Uno)) // never dispose the shared XORTransform.Uno singleton
                x.Dispose(); // Dispose SymmetricAlgorithm after CryptoStream creation

            return cryptoStream;
        }

        private Guid _Guid;

        private byte[] key;

        private byte[] iv;

        private CryptoKind _Kind;

        private bool _disposed = false;

        /// <summary>"CryptoKey.{Kind}" plus " Id={Guid}" for non-None kinds.</summary>
        public override string ToString()
        {
            return "CryptoKey."
                + _Kind.ToString()
                + (_Kind != CryptoKind.None
                  ? " Id=" + _Guid.ToString()
                  : string.Empty
                  );
        }

        /// <summary>Clears key material and marks the key disposed. No-op for
        /// the embedded singletons - they are shared and never die.</summary>
        public void Dispose()
        {
            if (_disposed || IsEmbedded) return; // FIX: Create(None/XOR/Jumble) return shared singletons — never mark them disposed
            Clear();
            _disposed = true;
        }

        /// <summary>The key's identity. Generated kinds carry a random Guid;
        /// embedded kinds a synthetic one; None reports the None singleton's
        /// Guid regardless of instance state.</summary>
        public Guid Guid => _Kind == CryptoKind.None ? embeddedKeys[0]._Guid : _Guid;

        /// <summary>The cipher kind of this key.</summary>
        public CryptoKind Kind => _Kind;

        /// <summary>True when this key is one of the embedded kinds
        /// (None, XOR, Jumble).</summary>
        public bool IsEmbedded => _Kind < firstNonEmbeddedKind;

        /// <summary>True when this key is the no-encryption kind.</summary>
        public bool IsNone => _Kind == CryptoKind.None;

        private static void _Array_Set(byte[] theBytes, byte toValue)
        {
            if (null != theBytes)
                for (int i = 0; i < theBytes.Length; i++)
                    theBytes[i] = toValue;
        }

        /// <summary>Scrubs key material (overwritten with 0xFF, then dropped)
        /// and resets the key to None state. No-op for embedded singletons.</summary>
        public void Clear()
        {
            if (IsEmbedded) return;
            _Array_Set(key, 0xFF);
            _Array_Set(iv, 0xFF);
            key = iv = null;
            _Kind = CryptoKind.None;
            _Guid = new Guid();
        }

        /// <summary>Persists Kind (one byte) plus, for non-embedded kinds, the
        /// Guid. Key material is NEVER written - the Guid is the lookup handle
        /// for the caller's key store (see class remarks).</summary>
        public void SaveBytes(BytesWriter writer)
        {
            writer.Write((byte)_Kind);

            if (!IsEmbedded)
            {
                byte[] xGuid = _Guid.ToByteArray();

                writer.WriteAny_Int32(xGuid.Length);

                writer.Write(xGuid);
            }
        }

        /// <summary>Restores Kind and (for non-embedded kinds) the Guid written
        /// by <see cref="SaveBytes"/>. Key material is NOT restored - the
        /// caller must resolve the Guid back to key material via its key store
        /// before any <see cref="GetCryptoStream_or_null"/> use (see class
        /// remarks).</summary>
        public void LoadBytes(BytesReader reader)
        {
            _Kind = (CryptoKind)reader.ReadByte();

            if (IsEmbedded) return;

            int iLen = reader.ReadAny_Int32();

            _Guid = new Guid(reader.ReadBytes(iLen));

            // NOTE: key/iv stay null here by design - Guid -> key-material
            // resolution is the caller's key-store concern. Using this key
            // for crypto before that resolution fails (see ISSUES LOG,
            // JOB.CS1: no in-library resolution path exists).
        }

        internal sealed class XORTransform
            : SymmetricAlgorithm
            , ICryptoTransform
        {
            public static readonly XORTransform Uno = new XORTransform();
            XORTransform() { }

            public override ICryptoTransform CreateDecryptor()
            { return this as ICryptoTransform; }

            public override ICryptoTransform CreateDecryptor
                (byte[] rgbKey, byte[] rgbIV)
            { return this as ICryptoTransform; }

            public override ICryptoTransform CreateEncryptor()
            { return this as ICryptoTransform; }

            public override ICryptoTransform CreateEncryptor
                (byte[] rgbKey, byte[] rgbIV)
            { return this as ICryptoTransform; }

            public override void GenerateIV() { }

            public override void GenerateKey() { }

            public bool CanReuseTransform { get { return true; } }

            public bool CanTransformMultipleBlocks { get { return true; } }

            public int InputBlockSize { get { return 32; } }

            public int OutputBlockSize { get { return 32; } }
            //            const byte xor = 0;

            public int TransformBlock
                (byte[] inputBuffer
                , int inputOffset
                , int inputCount
                , byte[] outputBuffer
                , int outputOffset
                )
            {
                int i = inputCount;
                while (i-- > 0)
                    outputBuffer[outputOffset++]
                        = (byte)~inputBuffer[inputOffset++];
                return inputCount;
            }

            public byte[] TransformFinalBlock
                (byte[] inputBuffer
                , int inputOffset
                , int inputCount
                )
            {
                int outputOffset = 0;
                byte[] outputBuffer = new byte[inputCount];
                while (inputCount-- > 0)
                    outputBuffer[outputOffset++]
                        = (byte)~inputBuffer[inputOffset++];
                return outputBuffer;
            }
        }
    }
}
