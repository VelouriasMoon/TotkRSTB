using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Buffers.Binary;
using ZstdSharp;
using System.Formats.Tar;
using SarcLibrary;

namespace TotkRSTB
{
    //Core code from https://github.com/EXKing-Editor/EXKing-Editor/blob/master/src/ExKingEditor.Core/Models/HashTable.cs
    public class HashTable
    {
        private static readonly Decompressor _commonDecompressor = new();
        private static readonly Compressor _commonCompressor = new(16);

        private static readonly Encoding _encoding = Encoding.UTF8;
        private static Dictionary<uint, string> _hashStringList { get; } = new();
        private static Dictionary<string, uint> _stringHashList { get; } = new();

        public Dictionary<uint, string> Strings => _hashStringList;
        public Dictionary<string, uint> Hashes => _stringHashList;

        static HashTable()
        {
            if (!File.Exists($"{Path.GetDirectoryName(Environment.ProcessPath)}\\HashTable.bin"))
            {
                Console.WriteLine("Unable to find HashTable.bin");
                Console.WriteLine("Press Any Key to Continue . . .");
                _ = Console.ReadKey();
                return;
            }

            FileStream fs = File.OpenRead($"{Path.GetDirectoryName(Environment.ProcessPath)}\\HashTable.bin");

            Span<byte> buffer = new byte[fs.Length];
            fs.Read(buffer);
            buffer = _commonDecompressor.Unwrap(buffer);

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(buffer[..4]);
            int offset = 4;

            for (int i = 0; i < count; i++)
            {
                uint hash = BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..(offset += 4)]);
                int size = buffer[offset++];
                string value = _encoding.GetString(buffer[offset..(offset += size)]);
                _hashStringList.Add(hash, value);
                _stringHashList.Add(value, hash);
            }
        }

        #region Compression

        public static byte[] DecompressFile(string file)
        {
            Span<byte> src = File.ReadAllBytes(file);
            return _commonDecompressor.Unwrap(src).ToArray();
        }

        public static byte[] CompressData(byte[] file)
        {
            Span<byte> src = file;
            return _commonCompressor.Wrap(src).ToArray();
        }
        #endregion
    }
}
