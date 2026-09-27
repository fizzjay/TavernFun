using System;
using System.Reflection;
using HarmonyLib;

// Optional developer-permission override. The Player > Dev Tools control toggles
// Enabled; when false these patches preserve the game's actual permission values.
//
// IsDeveloper  – auto-property on ShopHelper (Alta.Api.DataTransferModels)
// IsDev        – private-protected auto-property on the Player/Character class
//                (Root.Township), alongside PlayerInput / MetaMenu fields.
//
// Both are located at startup via reflection so the patch survives obfuscation
// and class renames without needing hard-coded type references.

[HarmonyPatch]
internal static class IsDeveloper_AlwaysTrue_Patch
{
    internal static bool Enabled;

    static MethodBase TargetMethod()
    {
        return FindGetter("IsDeveloper");
    }

    [HarmonyPostfix]
    static void Postfix(ref bool __result)
    {
        if (IsDeveloper_AlwaysTrue_Patch.Enabled)
            __result = true;
    }

    // Walk every loaded assembly to find a type that has a readable bool property
    // with the given name.  Returns null silently if not found (patch is skipped).
    internal static MethodBase FindGetter(string propertyName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch { continue; }

            foreach (var t in types)
            {
                var prop = t.GetProperty(propertyName,
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic);

                if (prop == null || prop.PropertyType != typeof(bool))
                    continue;

                var getter = prop.GetGetMethod(true);
                if (getter != null)
                    return getter;
            }
        }
        return null;
    }
}

[HarmonyPatch]
internal static class IsDev_AlwaysTrue_Patch
{
    static MethodBase TargetMethod()
    {
        return IsDeveloper_AlwaysTrue_Patch.FindGetter("IsDev");
    }

    [HarmonyPostfix]
    static void Postfix(ref bool __result)
    {
        if (IsDeveloper_AlwaysTrue_Patch.Enabled)
            __result = true;
    }
}
