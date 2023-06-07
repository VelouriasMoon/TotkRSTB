using RstbLibrary;
using RstbLibrary.Calculations;
using RstbLibrary.Core;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ZstdSharp;

namespace TotkRSTB
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 0 || args[0].ToLower() == "-h" || args[0].ToLower() == "--help")
            {
                Console.WriteLine("\nTotkRSTB.exe [option] {arguments}");
                Console.WriteLine("A simple CMD tool for editing RSTB/RESTBL files for TOTK.");
                Console.WriteLine("-------------------------------------------------------------------------------------------------------------");
                Console.WriteLine("  [.yaml file]                                                      Converts a yaml file to RESTBL");
                Console.WriteLine("  [.rsizetable.zs file]                                             Converts a RESTBL file to yaml");
                Console.WriteLine("  [--merge/-m] {Vanilla RSTB} {Modded RSTB} {Output RSTB Name}      Merges 2 RESTBL files into one");
                Console.WriteLine("  [--patch/-p] {Vanilla RSTB} {RSTB Yaml patch} {Output RSTB Name}  Patches a RESTBL file with a yaml file");
                Console.WriteLine("  [--makepatch/-mp] {Vanilla RSTB} {Modded RSTB}                    Create a yaml patch file with just the modded entries");
                Console.WriteLine("\nNote: TotkRSTB will always choose the entry with the highest value, \nremoving entries will result in the program choosing the vanilla.");
                return;
            }

            if (Path.GetExtension(args[0]).ToLower() == ".yaml" || Path.GetExtension(args[0]).ToLower() == ".yml")
                CreateRSTB(args[0]);
            else if (args[0].ToLower().EndsWith(".rsizetable.zs"))
                ExtractRSTB(args[0]);
            else if (args[0].ToLower() == "--makepatch" || args[0].ToLower() == "-mp")
                MakeRSTBPatch(args[1], args[2]);
            else if (args[0].ToLower() == "--merge" || args[0].ToLower() == "-m")
                MergeRSTB(args[1], args[2], args[3]);
            else if (args[0].ToLower() == "--patch" || args[0].ToLower() == "-p")
                PatchRSTB(args[1], args[2], args[3]);
        }

        static void ExtractRSTB(string infile)
        {
            //File.WriteAllBytes(Path.ChangeExtension(infile, null), HashTable.DecompressFile(infile));
            RSTB rstb = RSTB.FromBinary(HashTable.DecompressFile(infile), Endianness.Little);
            HashTable hashtable = new HashTable();

            StringBuilder output = new StringBuilder();
            IEnumerable<KeyValuePair<string, uint>> files = rstb.NameMap
                .Concat(rstb.CrcMap.Select(
                    x => new KeyValuePair<string, uint>(hashtable.Strings.TryGetValue(x.Key, out string? name) ? name : $"0x{x.Key:X2}", x.Value)))
                .OrderBy(x => x.Key);

            foreach (KeyValuePair<string, uint> entry in files)
            {
                output.Append($"{entry.Key}: {entry.Value}\r\n");
            }

            File.WriteAllText(Path.ChangeExtension(infile, ".yaml"), output.ToString());
        }

        static void CreateRSTB(string infile)
        {
            string[] yaml = File.ReadAllLines(infile);
            RSTB rstb = new RSTB();
            HashTable hashtable = new HashTable();

            foreach (string line in yaml)
            {
                if (uint.TryParse(line.Split(':')[0].Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
                {
                    rstb.CrcMap.Add(hash, uint.Parse(line.Split(':')[1].Trim()));
                }
                else if (hashtable.Hashes.ContainsKey(line.Split(':')[0]))
                {
                    rstb.CrcMap.Add(hashtable.Hashes[line.Split(':')[0]], uint.Parse(line.Split(':')[1].Trim()));
                }
                else
                {
                    rstb.NameMap.Add(line.Split(':')[0], uint.Parse(line.Split(':')[1].Trim()));
                }
            }

            //File.WriteAllBytes(Path.ChangeExtension(infile, null), rstb.ToBinary().ToArray());
            File.WriteAllBytes(Path.ChangeExtension(infile, ".zs"), HashTable.CompressData(rstb.ToBinary().ToArray()));
        }

        static void MakeRSTBPatch(string vanilla, string modded)
        {
            RSTB VanillaRSTB = RSTB.FromBinary(HashTable.DecompressFile(vanilla), Endianness.Little);
            RSTB ModdedRSTB = RSTB.FromBinary(HashTable.DecompressFile(modded), Endianness.Little);
            HashTable hashtable = new HashTable();

            Dictionary<string, uint> Vanillafiles = VanillaRSTB.NameMap
                .Concat(VanillaRSTB.CrcMap.Select(
                    x => new KeyValuePair<string, uint>(hashtable.Strings.TryGetValue(x.Key, out string? name) ? name : $"0x{x.Key:X2}", x.Value)))
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value);

            Dictionary<string, uint> Moddedfiles = ModdedRSTB.NameMap
                .Concat(ModdedRSTB.CrcMap.Select(
                    x => new KeyValuePair<string, uint>(hashtable.Strings.TryGetValue(x.Key, out string? name) ? name : $"0x{x.Key:X2}", x.Value)))
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value);

            List<KeyValuePair<string, uint>> Patchfiles = new List<KeyValuePair<string, uint>>(
                Moddedfiles.Where(
                    x => !Vanillafiles.ContainsKey(x.Key) || x.Value > Vanillafiles[x.Key])
                .Concat(Vanillafiles.Where(
                    x => !Moddedfiles.ContainsKey(x.Key)))
                .ToList());

            //Create YAML file from full name map
            StringBuilder output = new StringBuilder();
            foreach (KeyValuePair<string, uint> entry in Patchfiles)
            {
                output.Append($"{entry.Key}: {entry.Value}\r\n");
            }

            File.WriteAllText(Path.ChangeExtension(modded, "_patch.yaml"), output.ToString());
        }

        static void MergeRSTB(string vanilla, string modded, string outname = "ResourceSizeTable.Product.112.rsizetable.zs")
        {
            RSTB VanillaRSTB = RSTB.FromBinary(HashTable.DecompressFile(vanilla), Endianness.Little);
            RSTB ModdedRSTB = RSTB.FromBinary(HashTable.DecompressFile(modded), Endianness.Little);
            RSTB rstb = new RSTB();
            HashTable hashtable = new HashTable();

            rstb.CrcMap = new SortedDictionary<uint, uint>(
                VanillaRSTB.CrcMap
                .Select(x => new KeyValuePair<uint, uint>(x.Key, x.Value <= ModdedRSTB.CrcMap[x.Key] ? ModdedRSTB.CrcMap[x.Key] : x.Value))
                .ToDictionary(x => x.Key, x => x.Value));

            rstb.NameMap = new SortedDictionary<string, uint>(
                VanillaRSTB.NameMap
                .Select(x => new KeyValuePair<string, uint>(x.Key, x.Value <= ModdedRSTB.NameMap[x.Key] ? ModdedRSTB.NameMap[x.Key] : x.Value))
                .ToDictionary(x => x.Key, x => x.Value));

            File.WriteAllBytes(outname, HashTable.CompressData(rstb.ToBinary().ToArray()));
        }

        static void PatchRSTB(string Restbl, string patch, string outname = "ResourceSizeTable.Product.112.rsizetable.zs")
        {
            RSTB VanillaRSTB = RSTB.FromBinary(HashTable.DecompressFile(Restbl), Endianness.Little);
            string[] yaml = File.ReadAllLines(patch);
            RSTB ModdedRSTB = new RSTB();
            RSTB rstb = new RSTB();
            HashTable hashtable = new HashTable();

            foreach (string line in yaml)
            {
                if (uint.TryParse(line.Split(':')[0].Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash))
                    ModdedRSTB.CrcMap.Add(hash, uint.Parse(line.Split(':')[1].Trim()));
                else if (hashtable.Hashes.ContainsKey(line.Split(':')[0]))
                    ModdedRSTB.CrcMap.Add(hashtable.Hashes[line.Split(':')[0]], uint.Parse(line.Split(':')[1].Trim()));
                else
                    ModdedRSTB.NameMap.Add(line.Split(':')[0], uint.Parse(line.Split(':')[1].Trim()));
            }

            rstb.CrcMap = new SortedDictionary<uint, uint>(
                VanillaRSTB.CrcMap
                .Select(x => new KeyValuePair<uint, uint>(x.Key, ModdedRSTB.CrcMap.TryGetValue(x.Key, out uint value) && value > x.Value ? value : x.Value))
                .ToDictionary(x => x.Key, x => x.Value));

            rstb.NameMap = new SortedDictionary<string, uint>(
                VanillaRSTB.NameMap
                .Select(x => new KeyValuePair<string, uint>(x.Key, ModdedRSTB.NameMap.TryGetValue(x.Key, out uint value) && value > x.Value ? value : x.Value))
                .ToDictionary(x => x.Key, x => x.Value));

            File.WriteAllBytes(outname, HashTable.CompressData(rstb.ToBinary().ToArray()));
        }
    }
}