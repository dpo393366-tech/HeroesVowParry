using System;
using HarmonyLib;
namespace Vow.Counters;

internal static class DiscardDisplay
{
    [ThreadStatic] static int discarding;
    [ThreadStatic] static int drawing;
    internal static void Install(Harmony h){
        foreach(var ui in new[]{typeof(Fi_BattleCtrlUI),typeof(De_DebateCtrlUI)}){
            h.Patch(AccessTools.DeclaredMethod(ui,"FxCastCard"),prefix:Patch(nameof(Begin)),finalizer:Patch(nameof(End)));
        }
        foreach(var ui in new[]{typeof(Fi_BattleCtrlUI),typeof(De_DebateCtrlUI)})
            h.Patch(AccessTools.DeclaredMethod(ui,"FxDrawCard"),prefix:Patch(nameof(BeginDraw)),finalizer:Patch(nameof(EndDraw)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_zCard),"GetOne"),postfix:Patch(nameof(DrawnFight)));
        h.Patch(AccessTools.DeclaredMethod(typeof(De_zCard),"GetOne"),postfix:Patch(nameof(DrawnDebate)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_zCard),"GoingPool"),prefix:Patch(nameof(HideFight)));
        h.Patch(AccessTools.DeclaredMethod(typeof(De_zCard),"GoingPool"),prefix:Patch(nameof(HideDebate)));
    }
    static HarmonyMethod Patch(string name)=>new(typeof(DiscardDisplay),name);
    static void Begin(out int __state){__state=discarding;discarding++;}
    static void End(int __state){discarding=__state;}
    static void BeginDraw(out int __state){__state=drawing;drawing++;}
    static void EndDraw(int __state){drawing=__state;}
    // FxDrawCard also updates counts and owns native card-pool bookkeeping: never skip it.
    // Only its newly spawned presentation cards are hidden. Native GetOne activates reused cards.
    static void DrawnFight(Fi_zCard __result){if(drawing>0&&__result!=null)__result.gameObject.SetActive(false);}
    static void DrawnDebate(De_zCard __result){if(drawing>0&&__result!=null)__result.gameObject.SetActive(false);}
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
