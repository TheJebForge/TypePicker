using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace TypePicker
{
    public class TypePicker : NeosMod
    {
        public override string Name => "TypePicker";
        public override string Author => "TheJebForge";
        public override string Version => "1.0.0";

        public override void OnEngineInit() {
            Harmony harmony = new Harmony($"net.{Author}.{Name}");
            harmony.PatchAll();
        }

        public static void BuildComponentAttacherUI(ComponentAttacher componentAttacher, UIBuilder ui) {
            SyncRef<TextField> field = (SyncRef<TextField>)componentAttacher.GetSyncMember("_customGenericType");

            ReferenceField<IWorldElement> refField = componentAttacher.FindNearestParent<Slot>().AttachComponent<ReferenceField<IWorldElement>>();
            
            SyncMemberEditorBuilder.Build(refField.Reference, "Type picker", null, ui);
            ui.HorizontalLayout(8f);
            {
                ui.Button("Base type").LocalPressed += (button, data) => SetType(field, FindBaseType(refField));
                ui.Button("Inner type").LocalPressed += (button, data) => SetType(field, FindInnerType(refField));
            }
            ui.NestOut();
            
            ui.Text("Cast to:");
            
            ui.HorizontalLayout(8f);
            {
                ui.Button("SyncRef").LocalPressed += (button, data) => SetType(field, CastToSyncRef(refField));
                ui.Button("SyncRef Inner").LocalPressed += (button, data) => SetType(field, CastToSyncRefInner(refField));
                ui.Button("IField").LocalPressed += (button, data) => SetType(field, CastToIField(refField));
                ui.Button("IField Inner").LocalPressed += (button, data) => SetType(field, CastToIFieldInner(refField));
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

        static void SetType(SyncRef<TextField> field, Type type) {
            try {
                field.Target.Editor.Target.Text.Target.Text = type.FullName;
            }
            catch (Exception) {
                // ignored
            }
        }

        [HarmonyPatch(typeof(ComponentAttacher), "BuildUI")]
        class ComponentAttacher_BuildUI_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                int startIndex = -1;

                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

                for (int i = 0; i < codes.Count; i++) {
                    CodeInstruction instr = codes[i];
                    if (instr.opcode != OpCodes.Ldstr || !((string)instr.operand).Contains("Custom Generic Type Name")) continue;
                    Msg("Found!");
                    startIndex = i - 1;
                    break;
                }

                if (startIndex > -1) {
                    MethodInfo method = typeof(TypePicker).GetMethod("BuildComponentAttacherUI", BindingFlags.Public | BindingFlags.Static);
                    
                    codes.InsertRange(startIndex, new []
                    {
                       new CodeInstruction(OpCodes.Ldarg_0),
                       new CodeInstruction(OpCodes.Ldloc_0),
                       new CodeInstruction(OpCodes.Call, method)
                    });
                    Msg("Patched");
                }

                return codes.AsEnumerable();
            }
        }
    }
}