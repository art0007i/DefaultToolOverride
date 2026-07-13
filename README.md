# DefaultToolOverride
[![Thunderstore Badge](https://modding.resonite.net/assets/available-on-thunderstore.svg)](https://thunderstore.io/c/resonite/)

A [Resonite](https://resonite.com/) mod that gives you full control over the desktop number row tool keybinds.

This mod allows rebinding the 0-9 keys (and minus) in five different ways:
- Fallback: The key will behave exactly the same as it would without the mod.
- None: The key will do nothing. Can be useful if you want to create a multitool for desktop mode.
- Dequip: Dequips and stashes the currently held tool. aka the default action of the `1` key.
- URL: Spawns a tool from the record / asset URL specified inside the "string" config key.
- ClassName: Spawns an empty slot with the tool component specified in the "string" config key. This is useful because a lot of the tools will generate a basic visual when attached. Examples can be found in the [DefaultClassNames.cfg](./DefaultClassNames.cfg) file which contains all the default tools, but as ClassNames.

This is a workaround for [#772](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/772)


## Installation (Manual)
1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader) for Resonite.
2. Download the latest release ZIP file (e.g., `art0007i-DefaultToolOverride-1.0.0.zip`) from the [Releases](https://github.com/art0007i/DefaultToolOverride/releases) page.
3. Extract the ZIP and copy the `plugins` folder to your BepInEx folder in your Resonite installation directory:
   - **Default location:** `C:\Program Files (x86)\Steam\steamapps\common\Resonite\BepInEx\`
4. Start the game. If you want to verify that the mod is working you can check your BepInEx logs.
