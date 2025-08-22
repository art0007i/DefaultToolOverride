# DefaultToolOverride

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that gives you full control over the desktop number row tool keybinds.

This mod allows rebinding the 0-9 keys (and minus) in five different ways:
- Fallback: The key will behave exactly the same as it would without the mod.
- None: The key will do nothing. Can be useful if you want to create a multitool for desktop mode.
- Dequip: Dequips and stashes the currently held tool. aka the default action of the `1` key.
- URL: Spawns a tool from the record / asset URL specified inside the "string" config key.
- ClassName: Spawns an empty slot with the tool component specified in the "string" config key. This is useful because a lot of the tools will generate a basic visual when attached. Examples can be found in the [DefaultClassNames.json](./DefaultClassNames.json) file which contains all the default tools, but as ClassNames.

This is a workaround for [#772](https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/772)

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Place [DefaultToolOverride.dll](https://github.com/art0007i/DefaultToolOverride/releases/latest/download/DefaultToolOverride.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
1. Start the game. If you want to verify that the mod is working you can check your Resonite logs.
