using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Alta.QuickAccessActions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ── Patch 1 ────────────────────────────────────────────────────────────────────
// Remove the `!player2.IsLocalPlayer` guard in ShowNames.Update so the game
// creates a name-board above the local player exactly the same way it does for
// every remote player.
//
// The compiled IL for that branch looks like:
//   call     bool Player::get_IsLocalPlayer()   // or similar property name
//   brtrue   <skip_rest_of_loop_body>
//
// The transpiler finds that callvirt/call + brtrue pair and replaces both
// instructions with NOPs so the local-player path continues instead of jumping.
// ──────────────────────────────────────────────────────────────────────────────
[HarmonyPatch]
internal static class ShowNames_IncludeLocalPlayer_Patch
{
    static MethodBase TargetMethod()
    {
        // ShowNames.Update() is a private instance method on the ScriptableObject.
        return typeof(ShowNames).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count - 1; i++)
        {
            // Look for: call/callvirt to a method whose name contains "IsLocalPlayer"
            // followed immediately by a brtrue / brtrue.s (skip if true)
            var ci = codes[i];
            if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
                && ci.operand is MethodInfo mi
                && mi.Name.IndexOf("IsLocalPlayer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var next = codes[i + 1];
                if (next.opcode == OpCodes.Brtrue || next.opcode == OpCodes.Brtrue_S)
                {
                    // Replace both with NOPs so the local player is never skipped.
                    codes[i]     = new CodeInstruction(OpCodes.Nop);
                    codes[i + 1] = new CodeInstruction(OpCodes.Nop);
                    break;
                }
            }
        }

        return codes;
    }
}

// ── Patch 2 ────────────────────────────────────────────────────────────────────
// After NameBoard.Setup() runs, walk every Text and TextMeshPro component on the
// board's GameObject and force their color to black.  This affects every name
// that appears (other players and the newly-added local player).
// ──────────────────────────────────────────────────────────────────────────────
[HarmonyPatch]
internal static class NameBoard_BlackText_Patch
{
    static MethodBase TargetMethod()
    {
        return typeof(NameBoard).GetMethod(
            "Setup",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    [HarmonyPostfix]
    static void Postfix(NameBoard __instance)
    {
        if (__instance == null) return;
        ForceBlackText(__instance.gameObject);
    }

    internal static void ForceBlackText(GameObject go)
    {
        if (go == null) return;

        foreach (var t in go.GetComponentsInChildren<Text>(true))
            t.color = Color.black;

        foreach (var tmp in go.GetComponentsInChildren<TextMeshPro>(true))
            tmp.color = Color.black;

        foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>(true))
            tmp.color = Color.black;
    }
}
