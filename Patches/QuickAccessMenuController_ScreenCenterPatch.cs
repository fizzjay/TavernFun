using HarmonyLib;
using UnityEngine;
using Alta.QuickAccessActions;

[HarmonyPatch(typeof(QuickAccessMenuController))]
internal static class QuickAccessMenuController_ScreenCenterPatch
{
    private const float AnchorDistance = 0.8f; // meters in front of camera - keep menu out of player's face

    private static Transform _anchor;

    private static Transform GetScreenCenterAnchor()
    {
        if (_anchor == null)
        {
            _anchor = new GameObject("QAM_ScreenCenterAnchor").transform;
            Object.DontDestroyOnLoad(_anchor.gameObject);
        }
        Transform cam = PlayerController.Current.Camera.transform;
        _anchor.position = cam.position + cam.forward * AnchorDistance;
        return _anchor;
    }

    [HarmonyPatch("PositionMenuParent")]
    [HarmonyPrefix]
    private static void PositionMenuParent_Prefix(ref Transform controllerTransform)
    {
        controllerTransform = GetScreenCenterAnchor();
    }

    [HarmonyPatch("WaitForInput")]
    [HarmonyPrefix]
    private static void WaitForInput_Prefix(ref Transform controllerTransform)
    {
        controllerTransform = GetScreenCenterAnchor();
    }
}