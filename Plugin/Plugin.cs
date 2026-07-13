using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using Renderite.Shared;

namespace DefaultToolOverride;

[ResonitePlugin(PluginMetadata.GUID, PluginMetadata.NAME, PluginMetadata.VERSION, PluginMetadata.AUTHORS,
    PluginMetadata.REPOSITORY_URL)]
[BepInDependency(BepInExResoniteShim.PluginMetadata.GUID, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
#nullable disable
    internal static new ManualLogSource Log;
    public static ConfigEntry<bool> InvertModifier;
    public static ConfigEntry<ModifierKey> Modifier;
#nullable enable


    public enum OverrideType
    {
        Fallback, // Use default behavior, so spawn the default resonite tools
        None, // Don't do anything at all, no tool will be equipped or dequipped
        Dequip, // Dequip and stash the current tool. same as pressing 1 by default
        URL, // Spawn a custom tool from a url
        ClassName // Spawn a tool by attaching the provided tool component to an empty slot
    }

    // Should I just use the FrooxEngine.Key enum?
    public enum ModifierKey
    {
        None,
        Shift,
        Alt,
        Control,
        Any
    }

    public struct ToolReplacement(ConfigEntry<OverrideType> type, ConfigEntry<string> str)
    {
        public OverrideType Type => type.Value;
        public string Str => str.Value;

        public Type ToolType
        {
            get
            {
                var st = Str;
                if (field == null || !st.Contains(field.Name))
                {
                    field = TypeHelper.FindType(st);
                }

                return field;
            }
        } = null;
    }

    public static Dictionary<int, ToolReplacement> ConfigKeys = new();

    private void BuildToolKey(string name, int index)
    {
        var key_type = Config.Bind("Tools", $"{name}_type", OverrideType.Fallback, $"Tool {name} will use this mode.");
        var key_string = Config.Bind("Tools", $"{name}_string", "",
            $"String for Tool {name}. (Only for URL / ClassName)");
        ConfigKeys.Add(index, new(key_type, key_string));
    }


    public override void Load()
    {
        Log = base.Log;

        InvertModifier = Config.Bind("General", "InvertModifier", false,
            "Reverses the behavior of modifier key. (If you hold the modifier tools will not be spawned)");
        Modifier = Config.Bind("General", "Modifier", ModifierKey.None,
            "Tool spawning will require holding this modifier key.");
        for (int i = 1; i < 10; i++)
        {
            BuildToolKey(i.ToString(), i);
        }

        BuildToolKey("0", 0);
        BuildToolKey("minus", 10);
        // = new("invert_modifier_key", "Reverses the behavior of modifier key. (If you hold the modifier tools will not be spawned)", () => false);
        // = new("modifier_key", "Tool spawning will require holding this modifier key.", () => ModifierKey.None);

        HarmonyInstance.PatchAll();
    }

    [HarmonyPatch(typeof(InteractionHandler), "OnInputUpdate")]
    class DefaultToolOverridePatch
    {
        public static bool ToolIntercept(int toolNum, InteractionHandler instance)
        {
            var shouldSpawn = true;
            var shiftHeld = instance.InputInterface.GetAnyKey(Key.Shift, Key.LeftShift, Key.RightShift);
            var altHeld = instance.InputInterface.GetAnyKey(Key.Alt, Key.LeftAlt, Key.RightAlt);
            var ctrlHeld = instance.InputInterface.GetAnyKey(Key.Control, Key.LeftControl, Key.RightControl);
            switch (Modifier.Value)
            {
                case ModifierKey.None:
                    break;
                case ModifierKey.Shift:
                    if (!shiftHeld) shouldSpawn = false;
                    break;
                case ModifierKey.Alt:
                    if (!altHeld) shouldSpawn = false;
                    break;
                case ModifierKey.Control:
                    if (!ctrlHeld) shouldSpawn = false;
                    break;
                case ModifierKey.Any:
                    if (!(shiftHeld || altHeld || ctrlHeld)) shouldSpawn = false;
                    break;
            }

            if (shouldSpawn == InvertModifier.Value) return false;

            if (ConfigKeys.TryGetValue(toolNum, out var pair))
            {
                switch (pair.Type)
                {
                    case OverrideType.Fallback:
                        return true;
                    case OverrideType.None:
                        return false;
                    case OverrideType.Dequip:
                        instance.StashCurrentToolOrDequip();
                        return false;
                    case OverrideType.URL:
                        if (Uri.TryCreate(pair.Str, UriKind.Absolute, out var uri))
                        {
                            instance.StartTask(async delegate { await instance.SpawnAndEquip(uri); });
                            return false;
                        }
                        else
                        {
                            Log.LogWarning($"Invalid tool url '{pair.Str}' on tool {toolNum}.");
                            return true;
                        }
                    case OverrideType.ClassName:
                        var toolType = pair.ToolType;
                        if (toolType != null && typeof(ITool).IsAssignableFrom(toolType))
                        {
                            // froox uses Uri as dictionary keys. why? I don't know, but you can shove any random url in and it will take it without issue.
                            SpawnEquipFunc(instance, new Uri("tool://tool" + toolNum), toolType);
                            return false;
                        }
                        else
                        {
                            Log.LogWarning($"Invalid tool TypeName '{pair.Str}' on tool {toolNum}.");
                            return true;
                        }
                }
            }

            return true;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes, MethodBase method)
        {
            // Hopefully this doesn't break...
            var localIndex = method.GetMethodBody().LocalVariables.Last((x) => x.LocalType == typeof(int)).LocalIndex;

            Log.LogDebug($"Found local at index {localIndex}");
            Label lastLabel = default;
            foreach (var code in codes)
            {
                if (code.Branches(out var label))
                {
                    if (label.HasValue) lastLabel = label.Value;
                }

                if (code.opcode == OpCodes.Switch && code.operand is Label[] arr && arr.Length > 6)
                {
                    yield return
                        new(OpCodes.Ldarg_0); // load object instance so we can access it in our intercept function
                    yield return new(OpCodes.Call,
                        typeof(DefaultToolOverridePatch).GetMethod(nameof(DefaultToolOverridePatch.ToolIntercept)));
                    yield return new(OpCodes.Brfalse, lastLabel); // if our intercept says false skip remaining code
                    yield return
                        new(OpCodes.Ldloc_S,
                            (byte)localIndex); // put the int back so the real switch statement can use it
                }

                yield return code;
            }
        }

        // Copy of InteractionHandler.SpawnAndEquip with minor tweaks
        public static void SpawnEquipFunc(InteractionHandler instance, Uri uri, Type toolType)
        {
            var t = Traverse.Create(instance);
            var uriField = t.Field<Uri>("_currentToolUri");
            if (uriField.Value == uri)
            {
                return;
            }

            Slot toolSlot = null;
            bool cleanup = false;
            try
            {
                var _stashedTools = t.Field<Dictionary<Uri, ITool>>("_stashedTools").Value;
                if (_stashedTools.TryGetValue(uri, out var value) && !value.IsRemoved)
                {
                    value.Slot.ActiveSelf = true;
                    toolSlot = value.Slot;
                }
                else
                {
                    // only piece of my code in this entire func lmao
                    toolSlot = instance.LocalUserRoot.Slot.AddSlot(toolType.Name);
                    toolSlot.AttachComponent(toolType);
                    value = toolSlot.GetComponentInChildren<ITool>();
                }

                _stashedTools.Remove(uri);
                if (value == null)
                {
                    cleanup = true;
                }
                else if (!instance.CanEquip(value))
                {
                    cleanup = true;
                    NotificationMessage.SpawnTextMessage(value.Slot, "You don't have permission to equip this.",
                        colorX.Red, 3f, 0.15f, 0.5f, 0.1f);
                }
                else
                {
                    instance.StashCurrentToolOrDequip();
                    instance.Equip(value, lockEquip: true);
                    uriField.Value = uri;
                }
            }
            catch (Exception ex)
            {
                Log.LogError("Exception trying to spawn and equip tooltip:\n" + ex);
                cleanup = true;
            }

            if (cleanup)
            {
                toolSlot?.Destroy();
            }
        }
    }
}