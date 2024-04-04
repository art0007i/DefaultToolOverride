using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using System.Reflection.Emit;
using Elements.Core;

namespace DefaultToolOverride
{
    public class DefaultToolOverride : ResoniteMod
    {
        public override string Name => "DefaultToolOverride";
        public override string Author => "art0007i";
        public override string Version => "1.0.1";
        public override string Link => "https://github.com/art0007i/DefaultToolOverride/";

        public enum OverrideType
        {
            Fallback, // Use default behavior, so spawn the default resonite tools
            None, // Don't do anything at all, no tool will be equipped or dequipped
            Dequip, // Dequip and stash the current tool. same as pressing 1 by default
            URL, // Spawn a custom tool from a url
            ClassName // Spawn a tool by attaching the provided tool component to an empty slot
        }

        public struct ToolReplacement
        {
            public OverrideType Type => config.GetValue(type);
            public string Str => config.GetValue(str);

            Type _lastType = null;

            public Type ToolType
            {
                get
                {
                    var st = Str;
                    if (_lastType == null || !st.Contains(_lastType.Name))
                    {
                        _lastType = TypeHelper.FindType(st);
                    }
                    return _lastType;
                }
            }

            ModConfigurationKey<OverrideType> type;
            ModConfigurationKey<string> str;

            public ToolReplacement(ModConfigurationKey<OverrideType> type, ModConfigurationKey<string> str)
            {
                this.type = type;
                this.str = str;
            }

        }

        public static Dictionary<int, ToolReplacement> ConfigKeys = new();

        public static ModConfiguration config;

        private void BuildToolKey(ModConfigurationDefinitionBuilder builder, string name, int index)
        {
            var key_type = new ModConfigurationKey<OverrideType>($"tool{name}_type", $"Tool {name} will use this mode.", () => OverrideType.Fallback);
            var key_string = new ModConfigurationKey<string>($"tool{name}_string", $"String for Tool {name}. (Only for URL / ClassName)", () => null);
            builder.Key(key_type);
            builder.Key(key_string);
            ConfigKeys.Add(index, new(key_type, key_string));
        }


        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            base.DefineConfiguration(builder);
            for (int i = 1; i < 10; i++)
            {
                BuildToolKey(builder, i.ToString(), i);
            }
            BuildToolKey(builder, "0", 0);
            BuildToolKey(builder, "minus", 10);
        }

        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new Harmony("me.art0007i.DefaultToolOverride");
            harmony.PatchAll();

        }
        [HarmonyPatch(typeof(InteractionHandler), "OnInputUpdate")]
        class DefaultToolOverridePatch
        {
            public static bool ToolIntercept(int toolNum, InteractionHandler instance)
            {
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
                                instance.StartTask(async delegate
                                {
                                    await instance.SpawnAndEquip(uri);
                                });
                                return false;
                            }
                            else
                            {
                                Warn($"Invalid tool url '{pair.Str}' on tool {toolNum}.");
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
                                Warn($"Invalid tool TypeName '{pair.Str}' on tool {toolNum}.");
                                return true;
                            }
                    }
                }
                return true;
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                Label lastLabel = default;
                foreach (var code in codes)
                {
                    if (code.Branches(out var label))
                    {
                        if (label.HasValue) lastLabel = label.Value;
                    }
                    if (code.opcode == OpCodes.Switch && code.operand is Label[] arr && arr.Length > 6)
                    {
                        yield return new(OpCodes.Ldarg_0); // load object instance so we can access it in our intercept function
                        yield return new(OpCodes.Call, typeof(DefaultToolOverridePatch).GetMethod(nameof(DefaultToolOverridePatch.ToolIntercept)));
                        yield return new(OpCodes.Brfalse, lastLabel); // if our intercept says false skip remaining code
                        yield return new(OpCodes.Ldloc_S, (byte)12); // put the int back so the real switch statement can use it
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
                        NotificationMessage.SpawnTextMessage(value.Slot, "You don't have permission to equip this.", colorX.Red, 3f, 0.15f, 0.5f, 0.1f);
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
                    Error("Exception trying to spawn and equip tooltip:\n" + ex);
                    cleanup = true;
                }
                if (cleanup)
                {
                    toolSlot?.Destroy();
                }
            }
        }
    }
}
