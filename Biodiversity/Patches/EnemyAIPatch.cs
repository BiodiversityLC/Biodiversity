using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Biodiversity.General;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace Biodiversity.Patches;

[HarmonyPatch(typeof(EnemyAI))]
static class EnemyAIPatch {
	
	// TODO: basically extract most of this into some helper classes that let you patch in a yield return anywhere. i am just too lazy
	// FIXME: broken
    
	//[HarmonyPatch(nameof(EnemyAI.CurrentSearchCoroutine), MethodType.Enumerator), HarmonyTranspiler]
	static IEnumerable<CodeInstruction> AllowSearchRoutineDelay(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod) {
		Type coroutineType = __originalMethod.DeclaringType;

		CodeMatcher matcher = new CodeMatcher(instructions, generator).MatchForward(true, new CodeMatch());
		CodeInstruction latest = null;
		
		// this eventually should automatically chose the correct opcode / operand, but yeah
		while (matcher.IsValid) {
			matcher = matcher.MatchForward(false,
				new CodeMatch(OpCodes.Ldarg_0),
				new CodeMatch(), // actual magic number
				new CodeMatch(OpCodes.Stfld, AccessTools.Field(coroutineType, "<>1__state")),
				new CodeMatch(OpCodes.Ldc_I4_1),
				new CodeMatch(OpCodes.Ret)
			).Advance(1);
			
			if (matcher.IsValid) latest = matcher.Instruction;
		}

		if (latest == null) throw new InvalidProgramException("EnemyAI::CurrentSearchCoroutine doesn't have any yield return statements?? what?");
		
		CodeInstruction magicNumber;
		if (latest.opcode == OpCodes.Ldc_I4_6) { // method hasn't been changed
			magicNumber = new CodeInstruction(OpCodes.Ldc_I4_7);
		} else {
			throw new InvalidProgramException("EnemyAI::CurrentSearchCoroutine has a new number of yield return statements.");
		}
		// ----
        
		matcher = matcher.Start()
						 .MatchForward(false,
							 // this.searchCoroutine != null
							 new CodeMatch(OpCodes.Ldloc_1),
							 new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(EnemyAI), nameof(EnemyAI.searchCoroutine))),
							 new CodeMatch(OpCodes.Brfalse),
							 // base.IsOwner
							 new CodeMatch(OpCodes.Ldloc_1),
							 new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(NetworkBehaviour), nameof(NetworkBehaviour.IsOwner))),
							 new CodeMatch(OpCodes.Brtrue),

							 // !base.IsOwner
							 new CodeMatch(OpCodes.Ldloc_1),
							 new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(NetworkBehaviour), nameof(NetworkBehaviour.IsOwner))),
							 new CodeMatch(OpCodes.Brtrue)
						 )
						 .ThrowIfInvalid("Failed to find 'Destination Reached' code!")
						 .CreateLabel(out Label whileLoop)
						 .InsertAndAdvance(
							 // check if we are a BiodiverseAI instance
							 new CodeInstruction(OpCodes.Ldloc_1),
							 new CodeInstruction(OpCodes.Isinst, typeof(BiodiverseAI)),
							 new CodeInstruction(OpCodes.Brfalse, whileLoop), // just ignore what we are doing
							 
							 new CodeInstruction(OpCodes.Ldarg_0),
							 new CodeInstruction(OpCodes.Ldloc_1),
							 new CodeInstruction(OpCodes.Castclass, typeof(BiodiverseAI)),
							 new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(BiodiverseAI), nameof(BiodiverseAI.GetDelayBeforeContinueSearch))), // call function to get time
							 new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(WaitForSeconds), [typeof(float)])),
							 new CodeInstruction(OpCodes.Stfld, AccessTools.Field(coroutineType, "<>2__current")), // oh god help us all

							 new CodeInstruction(OpCodes.Ldarg_0),
							 magicNumber,
							 new CodeInstruction(OpCodes.Stfld, AccessTools.Field(coroutineType, "<>1__state")),
							 new CodeInstruction(OpCodes.Ldc_I4_1),
							 new CodeInstruction(OpCodes.Ret),

							 new CodeInstruction(OpCodes.Ldarg_0),
							 new CodeInstruction(OpCodes.Ldc_I4_M1),
							 new CodeInstruction(OpCodes.Stfld, AccessTools.Field(coroutineType, "<>1__state"))
						 )
						 .Advance(-3)
						 .CreateLabel(out Label stateRestore)
						 .Start()
						 .MatchForward(true,
							 new CodeMatch(OpCodes.Switch)
						 )
						 .SetOperandAndAdvance(((Label[])matcher.Instruction.operand).AddItem(stateRestore).ToArray())
						 .Advance(-1);

		return matcher.InstructionEnumeration();
	}
}