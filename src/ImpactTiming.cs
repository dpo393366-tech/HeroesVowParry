using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using NativeRoutine=Il2CppSystem.Collections.IEnumerator;

namespace Vow.Counters;

// Hook the iterator bodies, not only their named factories: the installed game
// also creates damage iterators directly inside three animation-event handlers.
internal static class ImpactTiming
{
    internal sealed class Attack
    {
#if DuelCounters
        internal StrengthPressure.State pressure;
#endif
        internal bool groggyCounted,groggyRelieved;
        internal bool debate,started,done,cancelled,finished,special,continuation,launchCompleted,parried;
        internal bool quickBasicChecked,quickBasic,quickContactPassed,animationLoaded;
        internal bool? archeryParry;
        internal float quickNativeLead=-1;
        internal int quickContactAnimation=-1;
        internal float quickContactTime;
        internal AttackClipClock.State clipClock;
        internal SingleCueClock singleCueClock;
        internal float singleCueLead=-1;
        internal readonly Dictionary<(int,float),RealtimeParry.CueWindow> cueWindows=new();
        internal float warningCap=float.PositiveInfinity,firstContactTime,requestedWarning;
        internal int firstContactAnimation=-1;
        internal bool warningPlanned;
        internal double firstCueAt=-1,lastCueAt=-1;
        internal double quickLoadedAt;
        internal PerHitParry.Plan strikes;
        internal bool preparing,launchEntered,motionFinished,pendingAfter,pendingSpecialEnd;
        internal double preparationAt,preparationElapsed;
        internal double finishWait;
        internal int finishFrame=-1;
        internal int order,round;
        internal Attack predecessor;
        internal GKHLLOOEEFN fighter;
        internal NPCCtrl debater;
        internal KJFOPOCBHBM fightCard;
        internal MMGEDKCCMHH debateCard;
        internal Fi_Controller fight;
        internal De_Controller speech;
        internal double heartbeat;
        internal bool Valid(){
            if(debate)return speech && !speech.m_isEnd && speech.m_curRound==round && speech.m_curOrder==order && De_Controller.m_curHero?.Pointer==debater?.Pointer && currentDebate==this;
            return fight && Fi_Controller.m_isInBattle && !Fi_Controller.m_isEnd && fight.AHDOJFHBBFJ==round &&
                fight.m_curOrder==order && Fi_Controller.m_curHero?.Pointer==fighter?.Pointer &&
                Fi_Controller.m_isSpecialAttack==special && (continuation?currentFight==predecessor:currentFight==this);
        }
    }
    static Attack currentFight,currentDebate;
    static Attack restoringSpecial;
    static bool nativeCleanup;
    static double lastTick;
    static readonly Dictionary<IntPtr,Attack> launches=new(),damages=new(),timers=new();
    internal static Attack Current(bool debate)=>debate?currentDebate:currentFight;
    // Damage iterator ownership, not merely whoever currently has the turn.
    internal static Attack BoundDamage(IntPtr iterator)=>damages.TryGetValue(iterator,out var a)&&a.Valid()?a:null;
    internal static void Install(Harmony h){
        AnimalDodgeSync.Install(h);
        TurnHandoff.Install(h);
        PerHitParry.Install(h);
        AttackPacing.Install(h);
        Hook(h,typeof(Fi_Controller.FMHMOHIGKCA),"MoveNext",nameof(FightLaunch));
        Hook(h,typeof(De_Controller.DKANHPKKGNC),"MoveNext",nameof(DebateLaunch));
        Hook(h,typeof(Fi_Controller.JFBIHELODLP),"MoveNext",nameof(FightImpact));
        Hook(h,typeof(De_Controller.BIODLKHOIFE),"MoveNext",nameof(DebateImpact));
        Hook(h,typeof(Fi_Controller.LDCPBCCLJOG),"MoveNext",nameof(FightTimer));
        Hook(h,typeof(Fi_Controller.GDGFEKIMOME),"MoveNext",nameof(FightNextTimer));
        Hook(h,typeof(Fi_Controller),"AfterAttack",nameof(AllowAfterAttack));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"EndSpecialAttack"),
            prefix:new HarmonyMethod(typeof(ImpactTiming),nameof(BeforeSpecialEnd)),
            finalizer:new HarmonyMethod(typeof(ImpactTiming),nameof(AfterSpecialEnd)));
        Hook(h,typeof(Fi_Controller),"SetPhase",nameof(AllowPhase));
        Hook(h,typeof(Fi_Controller),"SetRoundAndHero",nameof(RoundChanging));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetRoundAndHero"),postfix:new HarmonyMethod(typeof(ImpactTiming),nameof(RoundChanged)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetPhase"),postfix:new HarmonyMethod(typeof(ImpactTiming),nameof(PhaseChanged)));
        Hook(h,typeof(Fi_Controller),"HeroMovingAndStop",nameof(Approaching));
        Hook(h,typeof(NPCCtrl),"UpdateMove",nameof(AllowNpcMove));
        Hook(h,typeof(NPCCtrl),"SetDestination",nameof(AllowNpcMove));
        Hook(h,typeof(Fi_Controller),"Update",nameof(FightUpdate));
        Hook(h,typeof(De_Controller),"Update",nameof(DebateUpdate));
        foreach(var t in new[]{typeof(Fi_Controller.FMHMOHIGKCA),typeof(De_Controller.DKANHPKKGNC),typeof(Fi_Controller.JFBIHELODLP),typeof(De_Controller.BIODLKHOIFE),typeof(Fi_Controller.LDCPBCCLJOG),typeof(Fi_Controller.GDGFEKIMOME)})
            h.Patch(AccessTools.DeclaredMethod(t,"MoveNext"),postfix:new HarmonyMethod(typeof(ImpactTiming),nameof(Completed)));
    }
    static void Completed(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase __instance,bool __result){
        if(__result){
            // Read the attack data immediately after native launch setup yields,
            // before its first animation Update can deliver a very early hit.
            if((__instance is Fi_Controller.FMHMOHIGKCA) && launches.TryGetValue(__instance.Pointer,out var active))
                RealtimeParry.ObserveLoadedAttack(active);
            return;
        }
        if((__instance is Fi_Controller.FMHMOHIGKCA || __instance is De_Controller.DKANHPKKGNC) && launches.TryGetValue(__instance.Pointer,out var launch) && launch.Valid() && !launch.cancelled){
            launch.launchCompleted=true;
            if(launch.debate){PerHitParry.FinishMotion(launch);launch.motionFinished=true;AttackPacing.End(launch);ContactEffects.Finish(launch);}
        }
        if(timers.ContainsKey(__instance.Pointer))CombatTrace.Event("timer completed "+__instance.GetType().Name+" ptr="+__instance.Pointer);
        PerHitParry.Completed(__instance.Pointer);
        launches.Remove(__instance.Pointer);damages.Remove(__instance.Pointer);timers.Remove(__instance.Pointer);
    }
    static void Hook(Harmony h,Type type,string name,string before)=>h.Patch(AccessTools.DeclaredMethod(type,name),prefix:new HarmonyMethod(typeof(ImpactTiming),before));
    static Attack NewFight(Fi_Controller f)=>currentFight=new(){fight=f,fighter=Fi_Controller.m_curHero,fightCard=Fi_Controller.m_curCard??Fi_Controller.m_tmpCard,order=f.m_curOrder,round=f.AHDOJFHBBFJ,special=Fi_Controller.m_isSpecialAttack};
    static bool BeforeSpecialEnd(Fi_Controller __instance,out Attack __state){
        __state=restoringSpecial;
        if(PerHitParry.PendingSettlement(currentFight)){currentFight.pendingSpecialEnd=true;return false;}
        FinishAttackMotion(currentFight);
        restoringSpecial=currentFight?.special==true && currentFight.fight?.Pointer==__instance.Pointer?currentFight:null;
        CombatTrace.Event("special actor restoration begin");return true;
    }
    static Exception AfterSpecialEnd(Attack __state,Exception __exception){
        restoringSpecial=__state;
        CombatTrace.Event("special actor restoration returned");
        return __exception;
    }
    static Attack NextTimerOwner(){
        var old=restoringSpecial;var fight=old?.fight;
        // EndSpecialAttack restores the original actor; normal parry completion
        // resumes the same actor through AfterAttack. Both synchronously start
        // a NEW decision timer, which must not inherit the cancelled hit's latch.
        if(old!=null && fight && !Fi_Controller.m_isSpecialAttack &&
           Fi_Controller.m_curHero!=null && fight.AHDOJFHBBFJ==old.round && fight.m_curOrder==old.order){
            var owner=new Attack{fight=fight,fighter=Fi_Controller.m_curHero,order=fight.m_curOrder,
                round=fight.AHDOJFHBBFJ,continuation=true,predecessor=old};
            CombatTrace.Event($"restored decision timer owner={owner.fighter.Pointer} round={owner.round} order={owner.order}");
            return owner;
        }
        return currentFight;
    }
    static Attack NewDebate(De_Controller d)=>currentDebate=new(){debate=true,speech=d,debater=De_Controller.m_curHero,debateCard=De_Controller.m_curCard,order=d.m_curOrder,round=d.m_curRound};
    static bool Hold(Attack a)=>a!=null && a.Valid() && (AttackPacing.Preparing(a) || PerHitParry.Busy(a) || (a.started && (!a.done || (a.cancelled&&!a.finished))));
    static bool FightUpdate(){long started=CombatCost.Start();try{AttackPacing.ApplySpeed();LateParry.Tick();if(!AttackClipClock.AllowNative(currentFight)){RealtimeParry.PollInput();return false;}RealtimeParry.BeforeUpdate(currentFight);return !Hold(currentFight);}finally{CombatCost.End(2,started);}}
    static bool DebateUpdate(){long started=CombatCost.Start();try{AttackPacing.ApplySpeed();LateParry.Tick();RealtimeParry.BeforeUpdate(currentDebate);return !Hold(currentDebate);}finally{CombatCost.End(2,started);}}
    static bool CancelledTurn()=>!nativeCleanup && currentFight?.cancelled==true && currentFight.Valid();
    static bool AllowAfterAttack(){
        bool allow=!CancelledTurn() && !TurnHandoff.BlocksAction();
        if(allow && PerHitParry.PendingSettlement(currentFight)){currentFight.pendingAfter=true;return false;}
        if(allow)FinishAttackMotion(currentFight);
        return allow;
    }
    static void FinishAttackMotion(Attack a){
        if(a==null || a.motionFinished)return;
        PerHitParry.FinishMotion(a);a.motionFinished=true;a.pendingAfter=a.pendingSpecialEnd=false;
        AttackPacing.End(a);ContactEffects.Finish(a);
    }
    static bool AllowNpcMove(NPCCtrl __instance)=>CounterMotion.AllowMove(__instance);
    static bool Approaching(Fi_Controller __instance)=>CounterMotion.Approach(__instance);
    internal static void AbortApproach(Fi_Controller fight){var a=NewFight(fight);Cancel(a);Finish(a);}
    static bool AllowPhase(Fi_Controller.NHCOMCJKABH __0)=>!((CancelledTurn() && !currentFight.finished) || TurnHandoff.BlocksAction()) || __0==Fi_Controller.NHCOMCJKABH.end;
    static void PhaseChanged(){if(Fi_Controller.m_curPhase==Fi_Controller.NHCOMCJKABH.end)ParryStagger.Reset();}
    static void RoundChanging(){
        CombatTrace.Event("native turn selection requested");
    }
    static void RoundChanged(){
        // SetRoundAndHero has UI/animation early returns. A REQUEST is not a
        // completed handoff: retiring its attack here used to orphan the live turn.
        var a=currentFight;if(a==null||a.Valid())return;
        a.cancelled=true;a.done=true;DuelCounters.Release();
        CounterMotion.Reset();currentFight=null;
        // Keep existing iterator->attack bindings until those iterators finish.
        // Otherwise an old queued hit could be rebound to the following turn.
    }
    static bool Launch(Attack a,ref bool result){
        if(!a.Valid() || a.cancelled){CombatTrace.Event($"stop continuation ctx={a.GetHashCode()} valid={a.Valid()} cancelled={a.cancelled}");result=false;return false;}
        if(Hold(a) || PerHitParry.PendingSettlement(a)){result=true;return false;}
        return true;
    }
    static bool FightLaunch(Fi_Controller.FMHMOHIGKCA __instance,ref bool __result){
        if((CancelledTurn() && !currentFight.finished) || TurnHandoff.BlocksAction()){__result=false;__instance.__2__current=null;return false;}
        if(__instance.__1__state==0 && PerHitParry.PendingSettlement(currentFight)){
            CardLatency.Hold(false,"previous-damage-settlement");
            __result=true;__instance.__2__current=null;return false;
        }
        if(!launches.TryGetValue(__instance.Pointer,out var a) || (__instance.__1__state==0 && (!a.preparing || !a.Valid()))){
            AttackPacing.End(currentFight);launches[__instance.Pointer]=a=NewFight(__instance.__4__this);CombatTrace.Event("new launch ptr="+__instance.Pointer);
        }
        if(__instance.__1__state==0 && !AttackPacing.BeforeLaunch(a)){__result=true;__instance.__2__current=null;return false;}
        bool run=Launch(a,ref __result);if(!run)__instance.__2__current=null;
        return run;
    }
    static bool DebateLaunch(De_Controller.DKANHPKKGNC __instance,ref bool __result){
        if(__instance.__1__state==0 && PerHitParry.PendingSettlement(currentDebate)){
            CardLatency.Hold(true,"previous-damage-settlement");
            __result=true;__instance.__2__current=null;return false;
        }
        if(!launches.TryGetValue(__instance.Pointer,out var a) || (__instance.__1__state==0 && (!a.preparing || !a.Valid()))){
            AttackPacing.End(currentDebate);launches[__instance.Pointer]=a=NewDebate(__instance.__4__this);
        }
        if(__instance.__1__state==0 && !AttackPacing.BeforeLaunch(a)){__result=true;__instance.__2__current=null;return false;}
        bool run=Launch(a,ref __result);if(!run)__instance.__2__current=null;
        return run;
    }
    static bool FightTimer(Fi_Controller.LDCPBCCLJOG __instance,ref bool __result){
        if(__instance.__1__state==0 || !timers.TryGetValue(__instance.Pointer,out _)){timers[__instance.Pointer]=currentFight;CombatTrace.Event("bind after-attack timer ptr="+__instance.Pointer);}
        var a=timers[__instance.Pointer];
        if(a==null)return true;
        RepairCompletedLaunch(a);
        bool run=Launch(a,ref __result);if(!run)__instance.__2__current=null;
        return run;
    }
    static bool FightNextTimer(Fi_Controller.GDGFEKIMOME __instance,ref bool __result){
        if(__instance.__1__state==0 || !timers.TryGetValue(__instance.Pointer,out _)){timers[__instance.Pointer]=NextTimerOwner();CombatTrace.Event("bind next-action timer ptr="+__instance.Pointer);}
        var a=timers[__instance.Pointer];
        if(a==null)return true;
        bool run=Launch(a,ref __result);if(!run)__instance.__2__current=null;return run;
    }
    static bool FightImpact(Fi_Controller.JFBIHELODLP __instance,ref bool __result){
        if(TurnHandoff.BlocksAction()){__result=false;__instance.__2__current=null;return false;}
        var a=FindDamage(__instance.Pointer,__instance.__1__state,false,__instance.__4__this,null);
        bool run;
        if(PerHitParry.Enabled){run=PerHitParry.Ready(a,__instance.Pointer,__instance.__1__state);if(!run)__result=a.Valid()&&!a.cancelled;}
        else run=Impact(a,ref __result);
        if(!run)__instance.__2__current=null;
        return run;
    }
    static bool DebateImpact(De_Controller.BIODLKHOIFE __instance,ref bool __result){
        var a=FindDamage(__instance.Pointer,__instance.__1__state,true,null,__instance.__4__this);
        bool run;
        if(PerHitParry.Enabled){run=PerHitParry.Ready(a,__instance.Pointer,__instance.__1__state,__instance.targetHero);if(!run)__result=a.Valid()&&!a.cancelled;}
        else run=Impact(a,ref __result);
        if(!run)__instance.__2__current=null;
        return run;
    }
    static Attack FindDamage(IntPtr id,int step,bool debate,Fi_Controller f,De_Controller d){
        // The first hit can remain at state zero throughout a parry window;
        // reuse CURRENT attack here, so rebinding never restarts that window.
        var pending=PerHitParry.Enabled?PerHitParry.PendingOwner(id):null;
        if(pending!=null)return pending;
        if(step!=0 && damages.TryGetValue(id,out var known))return known;
        var a=Current(debate);
        if(a==null || !a.Valid())a=debate?NewDebate(d):NewFight(f);
        damages[id]=a;return a;
    }
    static void RepairCompletedLaunch(Attack a){
        var f=a.fight;
        // Support cards need no impact; damaging cards must have finished their
        // impact gate as well. Never release while a launch/damage/event is live.
        bool support=a.fightCard!=null&&a.fightCard.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack;
        bool ready=a.launchCompleted && a.Valid() && !a.cancelled && !a.special &&
            a.fightCard!=null && (support?!a.started:a.done) &&
            Fi_Controller.m_curPhase==Fi_Controller.NHCOMCJKABH.play && Fi_Controller.m_isLaunchingAttack &&
            f && f.gameObject.activeInHierarchy && f.m_listBattleAnimation!=null && f.m_curAnimationIndex>=f.m_listBattleAnimation.Count &&
            (f.m_listEventTime?.Count??0)==0 && (f.m_listEventIndex?.Count??0)==0 && (f.m_listPushing?.Count??0)==0 &&
            !Fi_Controller.m_hasSetMoveDes && !Fi_Controller.m_isPlayerMoving &&
            !UI_BattlePreviewUI.m_isOpen && !FICFPEHAJNJ.ODIOKGGBKEI &&
            (FICFPEHAJNJ.GGHHDIFNHOB?.Count??0)==0 && Fi_Controller.m_battleUI &&
            AOHIBKLOAEI.MOPAIPJCNJD==Fi_Controller.m_battleUI.gameObject;
        if(ready){foreach(var bound in launches.Values)if(bound==a){ready=false;break;}}
        if(ready){foreach(var bound in damages.Values)if(bound==a){ready=false;break;}}
        if(!ready || !Application.isFocused || Time.timeScale<=0 || Time.unscaledDeltaTime>1){a.finishWait=0;return;}
        if(a.finishFrame==Time.frameCount)return;
        a.finishFrame=Time.frameCount;
        a.finishWait+=Time.unscaledDeltaTime;if(a.finishWait<(support?3:10))return;
        a.finishWait=0;
        // Native AfterAttack timer already exists and is waiting for THIS latch.
        // Release only the proven orphan; never execute damage or advance a turn twice.
        DuelCounters.StabilityNote($"Completed card latch repaired: round={a.round} order={a.order} card={a.fightCard?.OJPECMNINNA} support={support}");
        Fi_Controller.m_isLaunchingAttack=false;
    }
    static bool Impact(Attack a,ref bool result){
        if(!a.Valid() || a.cancelled){result=false;return false;}
        if(!a.started){
            a.started=true;a.heartbeat=Time.unscaledTimeAsDouble;
            CombatTrace.Event("impact gate opened");
            try{Panel.Run(Gate(a));}
            catch(Exception e){DuelCounters.Warn("패링 시작 복구: "+e);a.done=true;DuelCounters.Release();}
        }
        if(!a.done){result=true;return false;}
        if(a.cancelled){result=false;return false;}
        return true;
    }
    static IEnumerator Resume(Attack a){a.done=true;yield break;}
    static IEnumerator Gate(Attack a){
        try{
            // The signal contains no native damage. Damage resumes through its own
            // iterator only after the shared counter decision has completed.
            var signal=Resume(a).WrapToIl2Cpp();
            if(a.debate)DuelCounters.Debate(a.speech,ref signal);else DuelCounters.Fight(a.fight,ref signal);
            yield return signal;
        }finally{a.done=true;CombatTrace.Event("impact gate closed");}
    }
    internal static IEnumerator Safe(IEnumerator counter,NativeRoutine fallback,Attack a){
        bool failed=false;
        try{
            while(true){
                bool more;object value;
                try{if(a!=null)a.heartbeat=Time.unscaledTimeAsDouble;more=counter.MoveNext();value=more?counter.Current:null;}
                catch(Exception e){DuelCounters.Warn("패링 판정 복구: "+e);failed=true;break;}
                if(!more)break;
                yield return value;
            }
        }finally{
            try{(counter as IDisposable)?.Dispose();}catch(Exception e){DuelCounters.Warn(e.Message);}
            if(failed)DuelCounters.Release();
        }
        if(failed){
            if(a?.cancelled==true)Finish(a);
            else if(a==null || a.Valid())yield return fallback;
        }
    }
    internal static void Cancel(bool debate,bool parried=false){var a=Current(debate);if(a!=null && parried)a.parried=true;Cancel(a);}
    static void Cancel(Attack a){
        if(a==null)return;
        bool ownsNative=a.Valid();
        a.cancelled=true;a.done=true;
        // A retired iterator can be observed after another actor has taken over.
        // Retiring its managed gate must not erase the new actor's native action.
        if(!a.debate && a.fight && ownsNative){
            a.fight.m_listEventTime?.Clear();a.fight.m_listEventIndex?.Clear();
            CounterMotion.Stop(a.fighter);
            // A special attack temporarily borrows m_curHero while its victim is
            // moving. EndSpecialAttack restores that mover and expects the same
            // destination, reserved card, targets and movement flags to survive.
            if(!a.special){
                Fi_Controller.m_cardAfterMove=null;Fi_Controller.m_listTargetAfterMove?.Clear();
                Fi_Controller.m_hasSetMoveDes=false;Fi_Controller.m_isPlayerMoving=false;
                Fi_Controller.m_isChoosingDes=false;Fi_Controller.m_isChoosingVictim=false;Fi_Controller.m_movingCache=0;
            }else{
                DuelCounters.StabilityNote($"Special parry preserves interrupted movement: round={a.round} order={a.order} actor={a.fighter?.Pointer} move={Fi_Controller.m_hasSetMoveDes}/{Fi_Controller.m_isPlayerMoving} reserved={Fi_Controller.m_cardAfterMove?.OJPECMNINNA}");
            }
            Fi_Controller.m_listEventMovingNPC?.Remove(a.fighter);a.fight.m_listPushing?.Remove(a.fighter);
            Fi_Controller.m_listSpecialAttacker?.Clear();
        }
    }
    // AfterAttack returns control to the native player UI / NPC decision timer.
    // Retain the cancelled attack as the owner of its old iterators. A new
    // continuation timer belongs to the resumed decision, never to those hits.
    internal static void ResumeFight(Fi_Controller fight){
        Fi_Controller.m_isLaunchingAttack=false;
        var previous=restoringSpecial;bool cleanup=nativeCleanup;
        restoringSpecial=currentFight;nativeCleanup=true;
        try{fight.AfterAttack();}finally{restoringSpecial=previous;nativeCleanup=cleanup;}
    }
    internal static void ResumeDebate(De_Controller debate){
        De_Controller.m_isLaunchingAttack=false;De_Controller.m_isAttacking=false;
        debate.m_isWaitingForAllAttack=false;debate.m_isWaitingForOneAttack=false;
        // Native play phase clears the spent selection, refreshes manual UI and
        // invokes the native decision for AI. Do not redraw/refill AP/end turn.
        debate.SetPhase(De_Controller.MOLBMLDLJEP.play,13);
    }
    static void ResumeParried(Attack a){
        a.finished=true;
        try{if(a.debate)ResumeDebate(a.speech);else ResumeFight(a.fight);}
        catch(Exception e){
            // A native callback can fail before handing control back. Permit a
            // bounded-delay retry only while this action still owns the state.
            // If a decision timer was already scheduled, never schedule it twice.
            bool scheduled=false;
            foreach(var timer in timers.Values)if(timer?.continuation==true && timer.predecessor==a){scheduled=true;break;}
            if(a.Valid() && !scheduled){a.finished=false;a.heartbeat=Time.unscaledTimeAsDouble;}
            DuelCounters.Warn("패링 후 다음 행동 복구: "+e.Message);
        }
    }
    internal static void Finish(bool debate)=>Finish(Current(debate));
    static void Finish(Attack a){
        if(a==null || a.finished || !a.Valid())return;
        if(a.debate){
            De_Controller.m_isLaunchingAttack=false;De_Controller.m_isAttacking=false;
            a.speech.m_isWaitingForAllAttack=false;a.speech.m_isWaitingForOneAttack=false;
            if(a.parried){ResumeParried(a);return;}
            a.speech.SetPhase(De_Controller.MOLBMLDLJEP.end,13);
        }else{
            CounterMotion.Complete(a.fighter);CounterMotion.Stop(a.fighter);
            Fi_Controller.m_isLaunchingAttack=false;
            if(a.special && Fi_Controller.m_isSpecialAttack){
                // Native special attacks temporarily replace the current actor.
                // Restore it through the native cleanup, never leave that latch set.
                nativeCleanup=true;
                try{a.fight.EndSpecialAttack();a.finished=true;}finally{nativeCleanup=false;}
                return;
            }
            if(a.parried){ResumeParried(a);return;}
            TurnHandoff.Begin(a.fight);
            a.fight.SetPhase(Fi_Controller.NHCOMCJKABH.end,300,true);
        }
        a.finished=true;
    }
    internal static void Tick(){
        AttackPacing.ApplySpeed();CombatTrace.Tick();ModelVisibilityTrace.Tick();
        double now=Time.unscaledTimeAsDouble, gap=Math.Max(0,now-lastTick);lastTick=now;
        TurnHandoff.Tick(gap,!Application.isFocused || Time.timeScale<=0 || gap>1);
        if(!Application.isFocused || Time.timeScale<=0 || gap>1){
            for(int slot=0;slot<2;slot++){var a=slot==0?currentFight:currentDebate;if(a!=null){a.heartbeat+=gap;a.preparationAt=now;if(a.firstCueAt>=0)a.firstCueAt+=gap;if(a.lastCueAt>=0)a.lastCueAt+=gap;foreach(var w in a.cueWindows.Values){w.start+=gap;w.deadline+=gap;}}}
            LateParry.Shift(gap);PerHitParry.Shift(gap);RealtimeParry.Shift(gap);return;
        }
        LateParry.Tick();ContactEffects.Flush();
        for(int slot=0;slot<2;slot++){var a=slot==0?currentFight:currentDebate;
            if(a==null)continue;
            if(a.Valid() && !PerHitParry.PendingSettlement(a)){
                if(a.pendingSpecialEnd){a.pendingSpecialEnd=false;a.fight.EndSpecialAttack();}
                else if(a.pendingAfter){a.pendingAfter=false;a.fight.AfterAttack();}
            }
            PerHitParry.Recover(a);
            if(!a.debate)RepairCompletedLaunch(a);
            if(!a.started)continue;
            if(a.cancelled){
                if(!a.finished && a.Valid() && Time.unscaledTimeAsDouble-a.heartbeat>2){DuelCounters.Release();Finish(a);}
                continue;
            }
            if(a.done)continue;
            if(!a.Valid()){Cancel(a);DuelCounters.Release();continue;}
            if(Time.unscaledTimeAsDouble-a.heartbeat<5)continue;
            if(a.strikes?.busy==true){DuelCounters.Warn("타격 패링 대기 해제 · 원래 공격 계속");PerHitParry.FailOpen(a.strikes);continue;}
            DuelCounters.Warn("중단된 패링 대기 복구");DuelCounters.Release();Cancel(a);Finish(a);
        }
    }
    internal static void Reset(){
#if DuelCounters
        StrengthPressure.Reset();
#endif
        AttackPacing.Reset();TurnHandoff.Reset();ParryStagger.Reset();PerHitParry.Reset();RealtimeParry.Reset();
        AnimalDodgeSync.Reset();CounterMotion.ClearBattle();
        if(currentFight!=null)currentFight.cancelled=true;
        if(currentDebate!=null)currentDebate.cancelled=true;
        currentFight=currentDebate=restoringSpecial=null;launches.Clear();damages.Clear();timers.Clear();lastTick=Time.unscaledTimeAsDouble;
    }
}
