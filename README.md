# TotkRSTB
 A simple CMD tool for editing RSTB/RESTBL files for TOTK.

## usage
Converting to yaml `TotkRSTB.exe [.yaml file]`  
Converting to RESTBL `TotkRSTB.exe [.rsizetable.zs file]`  
Merging `TotkRSTB.exe [--merge/-m] {Vanilla RSTB} {Modded RSTB} {Output RSTB Name}`  
Patching `TotkRSTB.exe [--patch/-p] {Vanilla RSTB} {RSTB Yaml patch} {Output RSTB Name}`  
Make Patch `TotkRSTB.exe [--makepatch/-mp] {Vanilla RSTB} {Modded RSTB}`

Note: TotkRSTB will always choose the entry with the highest value, removing entries will result in the program choosing the vanilla.

### Building
Requires my fork of [RstbLibrary](https://github.com/VelouriasMoon/RstbLibrary)
