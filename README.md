# AssetRipper Guid Patcher
 UnityEditor tool that patches AssetRipper generated guids with the correct ones from the installed packages on your project.

This tool will try to fix incorrect GUID values in prefabs and scenes. Aditionally it will also try to match shader names in materials and assign the correct shaders also.

I made this tool to make Lethal Company modding easier, as we often use in-game prefabs to get a proper reference of how the game is actually set-up for our mods. But this tool can also be used for other games.

# How To (Guide)
The use of this script assumes that you have at least some basic understanding of UnityEngine assets, and modding.

1. Make sure you have the [Lethal Company: Unity Template](https://github.com/EvaisaDev/LethalCompanyUnityTemplate/tree/main#readme) and your project is setup properly.

2. Download [AssetRipper](https://github.com/AssetRipper/AssetRipper/releases/latest)
    - Open asset ripper and set the following settings:
      - **Script Export Format:** `Decompiled`
      - **Script Content Level:** `Level 1`
    - Export your game files to a location that you might remember.
    - Do not place these files in your project *yet*.

3. Open your unity project, and import this tool using the latest [Release](https://github.com/ChrisFeline/AssetRipperGuidPatcher/releases/latest).
    - You can copy this script into your assets folder or import the Unity package.

4. Then go to the top bar and run:
    - `Kittenji` **/** `AssetRipper Guid Patch`
5. An open folder dialog will open, locate your game's exported directory and select the generated folder called: `ExportedProject`
6. After the tool is done processing your files, you can now import the prefabs and other assets to your project. Enjoy.