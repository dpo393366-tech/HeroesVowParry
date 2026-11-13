using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Vow.Counters;

// Observe the native action chain without choosing cards, ending turns or moving actors.
internal static class CombatTrace
{
#if DuelCounters
    static double next;
    static string stallKey;
    static int stableSamples;
    static Fi_Controller fight;
    internal static void Install(Harmony h){
        if(!DuelCounters.VerboseTrace)return;
        foreach(var name in new[]{"AfterAttack","SetPhase","SetRoundAndHero","ReceiveNPCSingleAttack","ReceiveNPCMultipleAttack"})
            h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),name),
                prefix:new HarmonyMethod(typeof(CombatTrace),nameof(Before)),
                postfix:new HarmonyMethod(typeof(CombatTrace),nameof(After)),
                finalizer:new HarmonyMethod(typeof(CombatTrace),nameof(Fault)));
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"GOCFBMPJJKE"),
            prefix:new HarmonyMethod(typeof(CombatTrace),nameof(DecisionBefore)),
            postfix:new HarmonyMethod(typeof(CombatTrace),nameof(DecisionAfter)),
            finalizer:new HarmonyMethod(typeof(CombatTrace),nameof(Fault)));
    }
    static void Before(Fi_Controller __instance,MethodBase __originalMethod){fight=__instance;Event("enter "+__originalMethod.Name);}
    static void After(MethodBase __originalMethod,bool __runOriginal)=>Event("leave "+__originalMethod.Name+" ran="+__runOriginal);
    static void DecisionBefore()=>Event("enter native decision");
    static void DecisionAfter(bool __runOriginal)=>Event("leave native decision ran="+__runOriginal);
    static Exception Fault(Exception __exception,MethodBase __originalMethod){if(__exception!=null)Event("exception "+__originalMethod.Name+" "+__exception);return __exception;}
    internal static void Event(string label){
        if(!DuelCounters.VerboseTrace)return;
        try{
            if(!Fi_Controller.m_isInBattle)return;
            var a=ImpactTiming.Current(false);var actor=Fi_Controller.m_curHero;var attr=actor?.GetAttribute();
            DuelCounters.StabilityNote($"TRACE {label}: round={fight?.AHDOJFHBBFJ} order={fight?.m_curOrder} phase={Fi_Controller.m_curPhase} actor={actor?.Pointer} AP={attr?.IHOPPBMEAEN} auto={Fi_Controller.m_autoBattle} launch={Fi_Controller.m_isLaunchingAttack} special={Fi_Controller.m_isSpecialAttack} move={Fi_Controller.m_hasSetMoveDes}/{Fi_Controller.m_isPlayerMoving} choice={Fi_Controller.m_isChoosingVictim}/{Fi_Controller.m_isChoosingDes} events={fight?.m_listEventTime?.Count}/{fight?.m_listEventIndex?.Count} context={(a==null?"none":$"{a.GetHashCode()} actor={a.fighter?.Pointer} order={a.order} valid={a.Valid()} started={a.started} done={a.done} cancel={a.cancelled} finished={a.finished}")}");
        }catch{/* Diagnostics must not alter battle flow. */}
    }
    internal static void Tick(){
        double now=Time.unscaledTimeAsDouble;if(now<next)return;next=now+1;
        try{
            if(!Fi_Controller.m_isInBattle||Fi_Controller.m_isEnd||!Application.isFocused||Time.timeScale<=0){stallKey=null;stableSamples=0;return;}
            fight=Fi_Controller.EBACHLGLNGD;
            var actor=Fi_Controller.m_curHero;var attr=actor?.GetAttribute();
            if(!fight||attr==null)return;
            // A manual player thinking/selecting a card is not a stalled AI turn.
            bool active=Fi_Controller.m_autoBattle||attr.IBLKPJDLABN!=FMHEOPMKFJB.MySide||Fi_Controller.m_isLaunchingAttack||Fi_Controller.m_curPhase==Fi_Controller.NHCOMCJKABH.end;
            string key=$"{fight.Pointer}/{fight.AHDOJFHBBFJ}/{fight.m_curOrder}/{actor.Pointer}/{Fi_Controller.m_curPhase}/{attr.IHOPPBMEAEN}/{Fi_Controller.m_curCard?.OJPECMNINNA}/{fight.m_curAnimationIndex}/{Fi_Controller.m_isLaunchingAttack}/{Fi_Controller.m_isSpecialAttack}";
            if(!active||key!=stallKey){stallKey=key;stableSamples=0;return;}
            stableSamples++;
            if(stableSamples==8||stableSamples%30==0){
                var a=ImpactTiming.Current(false);
                DuelCounters.StabilityNote($"STALL-SNAPSHOT unchanged={stableSamples}s state={key} position={actor.GetTransform()?.position} destination={Fi_Controller.m_moveDes} move={Fi_Controller.m_hasSetMoveDes}/{Fi_Controller.m_isPlayerMoving} reserved={Fi_Controller.m_cardAfterMove?.OJPECMNINNA} events={fight.m_listEventTime?.Count}/{fight.m_listEventIndex?.Count} pushes={fight.m_listPushing?.Count} modal={FICFPEHAJNJ.ODIOKGGBKEI} battleUI={AOHIBKLOAEI.MOPAIPJCNJD==Fi_Controller.m_battleUI?.gameObject} context={(a==null?"none":$"valid={a.Valid()} started={a.started} done={a.done} cancelled={a.cancelled} finished={a.finished} launchDone={a.launchCompleted}")}");
            }
            if(DuelCounters.VerboseTrace)Event("heartbeat");
        }catch{/* Sampling must never interrupt combat or require verbose hooks. */}
    }
#else
    internal static void Install(Harmony h){}
    internal static void Event(string label){}
    internal static void Tick(){}
#endif
}
