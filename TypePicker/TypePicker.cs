using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace TypePicker
{
    public class TypePicker : ResoniteMod
    {
        public override string Name => "TypePicker";
        public override string Author => "TheJebForge"; // Ported by art0007i :)
        public override string Version => "2.0.2";
        public override string Link => "https://github.com/TheJebForge/TypePicker/";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"net.{Author}.{Name}");
            harmony.PatchAll();
        }

        public static void BuildComponentSelectorUI(ComponentSelector selector, object uiDisplayClass)
        {

            var ui = Traverse.Create(uiDisplayClass).Field<UIBuilder>("ui").Value;
            var fields = (SyncRefList<TextField>)selector.GetSyncMember("_customGenericArguments");

            ReferenceField<IWorldElement> refField = selector.FindNearestParent<Slot>().GetComponentOrAttach<ReferenceField<IWorldElement>>();

            // max amount of generic params is ushort.MaxValue (I checked this myself)
            var paramIndex = selector.FindNearestParent<Slot>().GetComponentOrAttach<ValueField<ushort>>().Value;
            paramIndex.Value = 0;

            // Make the Generic Param Name clickable to select which one you put into.
            selector.RunInUpdates(1, () => {
                // if there's only a single there's no point in generating this
                if (fields.Count <= 1) return;
                for (int i = 0; i < fields.Count; i++)
                {
                    var f = fields[i];
                    var textSlot = f.Slot.Parent.Parent[0][0];
                    var btn = textSlot.AttachComponent<Button>();
                    var text = textSlot.GetComponent<Text>();
                    btn.SetupBackgroundColor(text.Color);
                    btn.ColorDrivers.FirstOrDefault().DisabledColor.Value = RadiantUI_Constants.Hero.GREEN;
                    var radio = textSlot.AttachComponent<ValueRadio<ushort>>();
                    radio.OptionValue.Value = (ushort)i;
                    radio.TargetValue.Target = paramIndex;
                    var bvd = textSlot.AttachComponent<BooleanValueDriver<bool>>();
                    bvd.FalseValue.Value = true;
                    bvd.TrueValue.Value = false;
                    bvd.TargetField.Target = btn.EnabledField;
                    radio.CheckVisual.Target = bvd.State;
                }
            });

            SyncMemberEditorBuilder.Build(refField.Reference, "Type picker", null, ui);
            ui.HorizontalLayout(8f);
            {
                ui.Button("Base type").LocalPressed += (button, data) => SetType(fields[paramIndex.Value], FindBaseType(refField));
                ui.Button("Inner type").LocalPressed += (button, data) => SetType(fields[paramIndex.Value], FindInnerType(refField));
            }
            ui.NestOut();

            ui.Text("Cast to:");

            ui.HorizontalLayout(8f);
            {
                ui.Button("SyncRef").LocalPressed += (button, data) => SetType(fields[paramIndex.Value], CastToSyncRef(refField));
                ui.Button("SyncRef Inner").LocalPressed += (button, data) => SetType(fields[paramIndex.Value], CastToSyncRefInner(refField));
                ui.Button("IField").LocalPressed += (button, data) => SetType(fields[paramIndex.Value], CastToIField(refField));
                ui.Button("IField Inner").LocalPressed += (button, data) => SetType(fields[paramIndex.Value], CastToIFieldInner(refField));
            }
            ui.NestOut();
        }

        static Type FindBaseType(ReferenceField<IWorldElement> refField) {
            try {
                return refField.Reference.Target.GetType();
            } 
            catch (Exception) {
                return null;
            }
        }

        static Type FindInnerType(ReferenceField<IWorldElement> refField) {
            try {
                return FindBaseType(refField).GenericTypeArguments[0];
            }
            catch (Exception) {
                return null;
            }
        }

        static Type CastToSyncRef(ReferenceField<IWorldElement> refField) {
            try {
                return typeof(SyncRef<>).MakeGenericType(((ISyncRef)refField.Reference.Target).TargetType);
            }
            catch (Exception) {
                return null;
            }
        }

        static Type CastToSyncRefInner(ReferenceField<IWorldElement> refField) {
            try {
                return CastToSyncRef(refField).GenericTypeArguments[0];
            }
            catch (Exception) {
                return null;
            }
        }

        static Type CastToIField(ReferenceField<IWorldElement> refField) {
            try {
                return typeof(IField<>).MakeGenericType(((IField)refField.Reference.Target).ValueType);
            }
            catch (Exception) {
                return null;
            }
        }

        static Type CastToIFieldInner(ReferenceField<IWorldElement> refField) {
            try {
                return CastToIField(refField).GenericTypeArguments[0];
            }
            catch (Exception) {
                return null;
            }
        }

        static void SetType(TextField field, Type type) {
            try {
                field.Editor.Target.Text.Target.Text = type.FullName;
            }
            catch (Exception) {
                // ignored
            }
        }

        [HarmonyPatch(typeof(ComponentSelector), "BuildUI")]
        class ComponentSelector_BuildUI_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int startIndex = -1;

                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

                for (int i = 0; i < codes.Count; i++)
                {
                    CodeInstruction instr = codes[i];
                    //this string appears twice, we need the first occurance in this case
                    if (instr.opcode != OpCodes.Ldstr || !((string)instr.operand).Contains("ComponentSelector.CustomGenericArguments")) continue;
                    Debug("Found code at index " + i);
                    startIndex = i - 1;
                    break;
                }

                if (startIndex > -1)
                {
                    MethodInfo method = typeof(TypePicker).GetMethod("BuildComponentSelectorUI", BindingFlags.Public | BindingFlags.Static);

                    codes.InsertRange(startIndex, new[]
                    {
                       new CodeInstruction(OpCodes.Ldarg_0),
                       new CodeInstruction(OpCodes.Ldloc_0),
                       new CodeInstruction(OpCodes.Call, method)
                    });
                    Debug("Patched");
                }
                else
                {
                    Warn("Could not find patch target! This means the mod won't do anything.");
                }

                return codes.AsEnumerable();
            }
        }
    }
}