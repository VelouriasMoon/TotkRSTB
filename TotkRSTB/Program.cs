using RstbLibrary;
using RstbLibrary.Calculations;
using RstbLibrary.Core;
using System.Collections;
using System.Collections.Concurrent;
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

            foreach (string line in yaml)
            {
                uint keyHash = Crc32.Compute(line.Split(':')[0]);

                if (uint.TryParse(line.Split(':')[0].Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hash)) // Use Hash
                    ModdedRSTB.CrcMap.Add(hash, uint.Parse(line.Split(':')[1].Trim()));
                else if (VanillaRSTB.CrcMap.ContainsKey(keyHash)) // Convert string to hash
                    ModdedRSTB.CrcMap.Add(keyHash, uint.Parse(line.Split(':')[1].Trim()));
                else
                    ModdedRSTB.CrcMap.Add(keyHash, uint.Parse(line.Split(':')[1].Trim()));
            }

            // This code is combining two dictionaries (VanillaRSTB.CrcMap and ModdedRSTB.CrcMap) into a new SortedDictionary.
            // For keys that exist in both dictionaries, it takes the larger value from ModdedRSTB.CrcMap.
            // For keys that exist only in ModdedRSTB.CrcMap, it adds them to the result.
            // The result is a SortedDictionary with the updated or merged key-value pairs.
            var resultCrcDict = new ConcurrentDictionary<uint, uint>();

            // Process the keys that exist in both dictionaries in parallel
            Parallel.ForEach(VanillaRSTB.CrcMap, kvp =>
            {
                if (ModdedRSTB.CrcMap.TryGetValue(kvp.Key, out uint value) && value > kvp.Value)
                {
                    resultCrcDict.TryAdd(kvp.Key, value);
                }
                else
                {
                    resultCrcDict.TryAdd(kvp.Key, kvp.Value);
                }
            });

            // Process the keys that exist only in ModdedRSTB.CrcMap in parallel
            Parallel.ForEach(ModdedRSTB.CrcMap, kvp =>
            {
                if (!VanillaRSTB.CrcMap.ContainsKey(kvp.Key))
                {
                    resultCrcDict.TryAdd(kvp.Key, kvp.Value);
                }
            });

            // Create a SortedDictionary from the ConcurrentDictionary
            var sortedCrcDict = new SortedDictionary<uint, uint>(resultCrcDict);
            rstb.CrcMap = sortedCrcDict;

            // This code is combining two dictionaries (VanillaRSTB.NameMap and ModdedRSTB.NameMap) into a new SortedDictionary.
            // For keys that exist in both dictionaries, it takes the larger value from ModdedRSTB.NameMap.
            // For keys that exist only in ModdedRSTB.NameMap, it adds them to the result.
            // The result is a SortedDictionary with the updated or merged key-value pairs.
            var resultNameMapDict = new ConcurrentDictionary<string, uint>();

            // Process the keys that exist in both dictionaries in parallel
            Parallel.ForEach(VanillaRSTB.NameMap, kvp =>
            {
                if (ModdedRSTB.NameMap.TryGetValue(kvp.Key, out uint value) && value > kvp.Value)
                {
                    resultNameMapDict.TryAdd(kvp.Key, value);
                }
                else
                {
                    resultNameMapDict.TryAdd(kvp.Key, kvp.Value);
                }
            });

            // Process the keys that exist only in ModdedRSTB.NameMap in parallel
            Parallel.ForEach(ModdedRSTB.NameMap, kvp =>
            {
                if (!VanillaRSTB.NameMap.ContainsKey(kvp.Key))
                {
                    resultNameMapDict.TryAdd(kvp.Key, kvp.Value);
                }
            });

            // Create a SortedDictionary from the ConcurrentDictionary
            var sortedNameMapDict = new SortedDictionary<string, uint>(resultNameMapDict);
            rstb.NameMap = sortedNameMapDict;


            File.WriteAllBytes(outname, HashTable.CompressData(rstb.ToBinary().ToArray()));
        }
    }
}