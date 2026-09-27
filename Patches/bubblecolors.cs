using HarmonyLib;
using UnityEngine;
using System.Reflection;
using Alta.QuickAccessActions;
using MelonLoader;

[HarmonyPatch(typeof(QuickAccessMenuBubble))]
internal static class QuickAccessMenuBubble_ColorPatch
{
    // richer, less "traffic-cone yellow" gold
    private static readonly Color GoldColor = new Color(0.83f, 0.68f, 0.21f); // ~ (212,175,55) metallic gold
    private static readonly Color GoldEmission = GoldColor * 0.15f;          // dim emission so it doesn't blow out

    private static FieldInfo _bubbleRendererField;
    private static FieldInfo _activePrefabField;

    [HarmonyPatch("UpdateContent")]
    [HarmonyPostfix]
    private static void UpdateContent_Postfix(QuickAccessMenuBubble __instance) => ApplyColors(__instance);

    [HarmonyPatch("UpdateActive")]
    [HarmonyPostfix]
    private static void UpdateActive_Postfix(QuickAccessMenuBubble __instance) => ApplyColors(__instance);

    private static void ApplyColors(QuickAccessMenuBubble instance)
    {
        var type = typeof(QuickAccessMenuBubble);
        if (_bubbleRendererField == null)
            _bubbleRendererField = type.GetField("bubbleRenderer", BindingFlags.Instance | BindingFlags.NonPublic);
        if (_activePrefabField == null)
            _activePrefabField = type.GetField("activePrefab", BindingFlags.Instance | BindingFlags.NonPublic);

        Renderer ring = _bubbleRendererField?.GetValue(instance) as Renderer;
        if (ring != null)
            ForceSetGold(ring);

        GameObject icon = _activePrefabField?.GetValue(instance) as GameObject;
        if (icon == null) return;

        var renderers = icon.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            ForceSetGold(r);
    }

    private static void ForceSetGold(Renderer r)
    {
        if (r == null || r.material == null) return;
        var mat = r.material;
        var shader = mat.shader;

        if (r.HasPropertyBlock())
            r.SetPropertyBlock(null);

        int count = shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color)
                continue;

            string propName = shader.GetPropertyName(i);

            // emission-type props get the dim version, everything else gets full gold
            bool isEmission = propName.IndexOf("Emission", System.StringComparison.OrdinalIgnoreCase) >= 0;
            mat.SetColor(propName, isEmission ? GoldEmission : GoldColor);
        }
    }
}