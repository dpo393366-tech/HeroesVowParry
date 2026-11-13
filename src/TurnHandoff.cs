using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Vow.Counters;

// SetPhase(end) only starts a coroutine. It does NOT acknowledge the next turn.
// Keep the handoff alive separately from ImpactTiming's attack until the
// selected actor/round actually changes, including native early-return cases.
internal static class TurnHandoff
{
    sealed class Pending
    {
        internal Fi_Controller fight;
        internal IntPtr actor;
        internal int round,order,attempts;
        internal double readyTime;
        internal bool closed;
        internal bool SameTurn()=>fight && Fi_Controller.m_isInBattle && !Fi_Controller.m_isEnd &&
            fight.AHDOJFHBBFJ==round && fight.m_curOrder==order && Fi_Controller.m_curHero?.Pointer==actor;
    }
    sealed class Timer
    {
        internal IntPtr controller;
        internal Pending owner;
        internal bool stale;
    }
    static Pending pending;
    static readonly Dictionary<IntPtr,Timer> timers=new();
    internal static bool BlocksAction()=>pending!=null && pending.SameTurn() && Fi_Controller.m_curPhase==Fi_Controller.NHCOMCJKABH.end;

    internal static void Install(Harmony h)
    {
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetPhase"),
            prefix:new HarmonyMethod(typeof(TurnHandoff),nameof(PhaseRequested)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller.NADLILCLLGH),"MoveNext"),
            prefix:new HarmonyMethod(typeof(TurnHandoff),nameof(BeforeTimer)),
            postfix:new HarmonyMethod(typeof(TurnHandoff),nameof(AfterTimer)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetRoundAndHero"),
            postfix:new HarmonyMethod(typeof(TurnHandoff),nameof(ObserveRound)));
    }
    static void PhaseRequested(Fi_Controller __instance,Fi_Controller.NHCOMCJKABH __0){
        // Cover natural end-of-turn paths as well as parry/path cancellation.
        // This only observes end; it never decides to finish an active turn.
        if(__0==Fi_Controller.NHCOMCJKABH.end && Fi_Controller.m_isInBattle && !Fi_Controller.m_isEnd)
            Begin(__instance);
    }
    internal static void Begin(Fi_Controller fight)
    {
        if(pending!=null && !pending.closed && pending.fight?.Pointer==fight.Pointer && pending.SameTurn())return;
        Close("replaced");
        // A timer started by the interrupted card must not later advance the
        // newly selected actor. New end-phase timers are bound below instead.
        foreach(var t in timers.Values)if(t.controller==fight.Pointer)t.stale=true;
        pending=new Pending{fight=fight,actor=Fi_Controller.m_curHero?.Pointer??IntPtr.Zero,
            round=fight.AHDOJFHBBFJ,order=fight.m_curOrder};
        DuelCounters.StabilityNote($"Parry end requested: round={pending.round}, order={pending.order}, actor={pending.actor}");
    }
    static bool BeforeTimer(Fi_Controller.NADLILCLLGH __instance,ref bool __result)
    {
        // state zero also replaces an entry if native GC reused its address.
        if(__instance.__1__state==0 || !timers.TryGetValue(__instance.Pointer,out _))
        {
            var f=__instance.__4__this;
            var owner=pending;
            if(owner==null || owner.closed || owner.fight?.Pointer!=f?.Pointer || !owner.SameTurn() || Fi_Controller.m_curPhase!=Fi_Controller.NHCOMCJKABH.end)owner=null;
            timers[__instance.Pointer]=new Timer{controller=f?.Pointer??IntPtr.Zero,owner=owner};
        }
        var t=timers[__instance.Pointer];
        if(!t.stale && (t.owner==null || (!t.owner.closed && t.owner.SameTurn() && Fi_Controller.m_curPhase==Fi_Controller.NHCOMCJKABH.end)))return true;
        __instance.__2__current=null;__result=false;return false;
    }
    static void AfterTimer(Fi_Controller.NADLILCLLGH __instance,bool __result)
    {
        if(!__result)timers.Remove(__instance.Pointer);
    }
    static void ObserveRound()
    {
        if(pending!=null && (!pending.SameTurn() || Fi_Controller.m_curPhase!=Fi_Controller.NHCOMCJKABH.end))Close("advanced");
    }
    static void Close(string reason)
    {
        if(pending==null)return;
        pending.closed=true;
        DuelCounters.StabilityNote($"Parry end {reason}: round={pending.round}, order={pending.order}, retries={pending.attempts}");
        pending=null;
    }
    static bool Ready(Pending p)
        =>BattleReady(p.fight);
    internal static bool BattleReady(Fi_Controller fight)
    {
        // These are the native phaseTimer's UI gates plus SetRoundAndHero's
        // early-return gates. Never turn an open menu or an end battle into a skip.
        return fight && fight.gameObject.activeInHierarchy && Fi_Controller.m_battleUI &&
            !UI_BattlePreviewUI.m_isOpen && !FICFPEHAJNJ.ODIOKGGBKEI &&
            (FICFPEHAJNJ.GGHHDIFNHOB==null || FICFPEHAJNJ.GGHHDIFNHOB.Count==0) &&
            AOHIBKLOAEI.MOPAIPJCNJD==Fi_Controller.m_battleUI.gameObject &&
            Fi_Controller.m_listHero!=null && Fi_Controller.m_listHero.Count>1 &&
            Fi_Controller.CJKNCEOMJNL<=0 && !Fi_Controller.m_isSpecialAttack && !Fi_Controller.m_isLaunchingAttack;
    }
    internal static void Tick(double delta,bool paused)
    {
        var p=pending;if(p==null)return;
        if(!p.SameTurn() || Fi_Controller.m_curPhase!=Fi_Controller.NHCOMCJKABH.end){Close("left end phase");return;}
        if(paused || !Ready(p)){p.readyTime=0;return;}
        p.readyTime+=delta;
        if(p.readyTime<2)return;
        p.readyTime=0;
        // Retire only this parry's end timers BEFORE retrying. A delayed original
        // coroutine must never advance the following actor a second time.
        foreach(var t in timers.Values)if(t.owner==p)t.stale=true;
        p.attempts++;
        DuelCounters.StabilityNote($"Parry end retry {p.attempts}: round={p.round}, order={p.order}, phase={Fi_Controller.m_curPhase}");
        try{p.fight.SetRoundAndHero(6);ObserveRound();}
        catch(Exception e){DuelCounters.Warn("패링 턴 전환 재시도: "+e);}
    }
    internal static void Reset()
    {
        Close("battle reset");timers.Clear();
    }
}
