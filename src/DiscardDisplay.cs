using System;
using HarmonyLib;
namespace Vow.Counters;

internal static class DiscardDisplay
{
    [ThreadStatic] static int discarding;
    internal static void Install(Harmony h){
        foreach(var ui in new[]{typeof(Fi_BattleCtrlUI),typeof(De_DebateCtrlUI)}){
            h.Patch(AccessTools.DeclaredMethod(ui,"FxCastCard"),prefix:Patch(nameof(Begin)),finalizer:Patch(nameof(End)));
        }
        // Draw uses the real hand-card objects. Leave both native draw and its animation intact.
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_zCard),"GoingPool"),prefix:Patch(nameof(HideFight)));
        h.Patch(AccessTools.DeclaredMethod(typeof(De_zCard),"GoingPool"),prefix:Patch(nameof(HideDebate)));
    }
    static HarmonyMethod Patch(string name)=>new(typeof(DiscardDisplay),name);
    static void Begin(out int __state){__state=discarding;discarding++;}
    static void End(int __state){discarding=__state;}
    // GoingPool is owned by the global coroutine runner. It completes even with the card hidden.
    static void HideFight(Fi_zCard __instance,ref float __0){
        if(discarding==0||__instance==null)return;
        __instance.gameObject.SetActive(false);__0=0;
    }
    static void HideDebate(De_zCard __instance,ref float __0){
        if(discarding==0||__instance==null)return;
        __instance.gameObject.SetActive(false);__0=0;
    }
}
