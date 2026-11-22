using System;
using HarmonyLib;
using UnityEngine;

namespace Vow.Counters;

// Observe the clock write itself rather than detouring GlobalUpdateCtrl.Update,
// whose entry is also used by the external editor's main-thread dispatcher.
internal static class AttackPacing
{
    static ImpactTiming.Attack owner;
    static float previousScale=1,appliedScale=1;
    static bool adjusted;
    [ThreadStatic] static bool handlingScaleWrite;
    internal static string LastMeasurement="아직 측정된 공격 없음";
    internal static void Install(Harmony h){
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"LoadAttackAnim"),postfix:new HarmonyMethod(typeof(AttackPacing),nameof(AnimationLoaded)));
        h.Patch(AccessTools.DeclaredMethod(typeof(De_Controller),"LoadAttackAnim"),postfix:new HarmonyMethod(typeof(AttackPacing),nameof(DebateAnimationLoaded)));
        h.Patch(AccessTools.PropertySetter(typeof(Time),nameof(Time.timeScale)),postfix:new HarmonyMethod(typeof(AttackPacing),nameof(AfterTimeScaleWrite)));
        foreach(string name in new[]{"DGKHPLBKLJB","LDKGPFEBOBD"})
            h.Patch(AccessTools.DeclaredMethod(typeof(GlobalUpdateCtrl),name),postfix:new HarmonyMethod(typeof(AttackPacing),nameof(ApplySpeed)));
    }
    static void AfterTimeScaleWrite(){
        if(owner==null||handlingScaleWrite)return;
        handlingScaleWrite=true;
        try{ApplySpeed();}finally{handlingScaleWrite=false;}
    }
    internal static bool Preparing(ImpactTiming.Attack a)=>a!=null && a.Valid() && a.preparing && !a.launchEntered;
    internal static bool BeforeLaunch(ImpactTiming.Attack a){
        if(!DuelCounters.NoDelay){a.preparing=false;a.launchEntered=true;return true;}
        if(a.launchEntered)return true;
        bool attack=a.debate?a.debateCard?.HFBAJIGHOAF==MMGEDKCCMHH.GANKBONOKGD.Attack:a.fightCard?.HFBAJIGHOAF==KJFOPOCBHBM.CCENIDMDCCN.Attack;
        if(!attack){a.launchEntered=true;CardLatency.Begin(a);return true;}
        if(!a.preparing){a.preparing=true;a.preparationAt=Time.unscaledTimeAsDouble;}
        double now=Time.unscaledTimeAsDouble,gap=Math.Max(0,now-a.preparationAt);a.preparationAt=now;
        if(Application.isFocused && Time.timeScale>0 && gap<=1)a.preparationElapsed+=gap;
        if(a.preparationElapsed+1e-6<DuelCounters.AttackLead){CardLatency.Hold(a.debate,"configured-attack-lead");return false;}
        a.preparing=false;a.launchEntered=true;CardLatency.Begin(a);
        End(owner);owner=a;previousScale=Time.timeScale;ApplySpeed();
        return true;
    }
    internal static void ApplySpeed(){
        if(owner==null)return;
        if(!owner.Valid() || owner.cancelled || owner.motionFinished || !DuelCounters.NoDelay){End(owner);return;}
        // Preserve native pause/slow motion and the user's speed-button choice.
        // Normal attack playback and its VFX share one clock, so contacts stay synced.
        // Only classified incoming singles are speed-limited. Warning settings,
        // clip offsets, lead time and multi-hit attacks never change playback.
        if(!owner.quickBasic || !DuelCounters.QuickBasic || owner.quickContactPassed)return;
        // A pull/charge clip before the actual single strike keeps native speed.
        // Only the clip containing the damaging strike receives this cap.
        if(!owner.debate && owner.fight.m_curAnimationIndex!=owner.quickContactAnimation){RestoreScale(owner);return;}
        float cap=SingleCap(owner);
        if(Time.timeScale>cap){if(!adjusted || !Mathf.Approximately(Time.timeScale,appliedScale))previousScale=Time.timeScale;appliedScale=cap;adjusted=true;Time.timeScale=cap;}
        else if(adjusted && Mathf.Approximately(Time.timeScale,appliedScale) && !Mathf.Approximately(appliedScale,cap)){
            appliedScale=Math.Min(previousScale,cap);Time.timeScale=appliedScale;
        }
    }
    // Native launch setup has installed this card's event list. Classify from
    // contacts, not rarity, animal/human type, clip length or Animator state name.
    internal static void ObserveSingle(ImpactTiming.Attack a){
        if(!DuelCounters.QuickBasic || a==null || a.debate || a.preparing || !a.launchEntered || !a.animationLoaded || a.quickContactPassed || a.quickBasicChecked || !a.Valid())return;
        var animations=a.fight.m_listBattleAnimation;
        if(!Fi_Controller.m_isLaunchingAttack || animations==null || animations.Count==0 || (a.fight.m_listEventIndex?.Count??0)==0)return;
        if(animations.Count>256)return;
        a.quickBasic=PerHitParry.TrySingle(a,out int contactAnimation,out float contact);
        if(!a.quickBasic)return;
        a.quickContactAnimation=contactAnimation;a.quickContactTime=contact;
        if(a.quickBasic){
            try{
                var animation=animations[contactAnimation];
                var clip=HILPKINAKPL.NGFCCDLMGAN(animation.HIENOINGBCN,0);
                var animator=a.fighter.GetAnimator();
                float rate=animator?Math.Abs(animator.speed):1;
                if(clip && rate>.0001f)a.quickNativeLead=Math.Max(0,contact-animation.PIJKBOHAMDK)*clip.length/rate;
            }catch(Exception e){DuelCounters.Warn("단타 준비 모션 길이 확인: "+e.Message);}
            ApplySpeed();
            // Loading a clip is not playback: skills can have a preceding wind-up
            // or a later contact clip. Emit cues only from the sampled Animator
            // clock (or the native contact fallback), never this duration estimate.
        }
    }
    // Prepare once from the native-loaded clip, not the old Animator state.
    // Never alter event timestamps, replay a skill, or hold a damage coroutine.
    internal static void PlanWarning(ImpactTiming.Attack a){
        if(a==null || a.warningPlanned || a.quickContactPassed || !a.animationLoaded || DuelCounters.WarningLead<=0)return;
        if(a.debate){
            var anim=a.speech.m_debateAnimation;var events=anim?.DGBOCHKBFJG;if(events==null)return;
            float earliest=float.PositiveInfinity;
            foreach(var e in events)if(e!=null && e.EBHFFNHELFB && float.IsFinite(e.DHBBBOOGECL))earliest=Math.Min(earliest,e.DHBBBOOGECL);
            if(!float.IsFinite(earliest))return;
            a.warningPlanned=true;a.firstContactAnimation=a.speech.m_curAnimationIndex;a.firstContactTime=earliest;a.requestedWarning=DuelCounters.WarningLead;
            return;
        }
        var list=a.fight.m_listBattleAnimation;if(list==null || list.Count==0)return;
        for(int i=0;i<Math.Min(256,list.Count);i++){
            var anim=list[i];var events=anim?.DGBOCHKBFJG;if(events==null)continue;
            float earliest=float.PositiveInfinity;
            foreach(var e in events)if(e!=null && PerHitParry.IsParryContact(a,i,e.DHBBBOOGECL) && float.IsFinite(e.DHBBBOOGECL))earliest=Math.Min(earliest,e.DHBBBOOGECL);
            if(!float.IsFinite(earliest))continue;
            a.warningPlanned=true;a.firstContactAnimation=i;a.firstContactTime=earliest;a.requestedWarning=DuelCounters.WarningLead;
            return;
        }
    }
    // remaining is native seconds at timeScale=1. Recheck the actual Animator
    // clock before native event dispatch, including state speed multipliers.
    internal static void PaceWarning(ImpactTiming.Attack a,int index,float contact,float remaining){
        if(a==null || !a.animationLoaded || a.quickContactPassed || DuelCounters.WarningLead<=0 || !float.IsFinite(remaining))return;
        if(!a.warningPlanned){a.warningPlanned=true;a.firstContactAnimation=index;a.firstContactTime=contact;a.requestedWarning=DuelCounters.WarningLead;}
        if(a.firstContactAnimation!=index || !StrikeLedger.SameTime(contact,a.firstContactTime))return;
        if(remaining<=0){return;} // fail open for a contact already at/past start
        float natural=a.quickBasic&&DuelCounters.QuickBasic?SingleCap(a):Time.timeScale;
        natural=Math.Min(natural,Time.timeScale);
        if(natural<=0)return;
        if(a.firstCueAt<0 && remaining/natural<=a.requestedWarning+1e-6){
            RealtimeParry.ContactCue(a,index,contact);
        }
    }
    internal static float SingleCap(ImpactTiming.Attack a)=>DuelCounters.QuickBasicSpeed;
    // The launch iterator can yield for buffs before installing new animation
    // data. Only this native loader establishes that the list belongs to this card.
    static void AnimationLoaded(Fi_Controller __instance,bool __result){
        var a=ImpactTiming.Current(false);
        if(!__result || a==null || !a.Valid() || a.cancelled || a.fight?.Pointer!=__instance.Pointer)return;
        a.animationLoaded=true;a.quickLoadedAt=Time.unscaledTimeAsDouble;CardLatency.Loaded(a);
        AttackClipClock.Loaded(a);
        RealtimeParry.ObserveLoadedAttack(a);
        Trace(a,"loaded");
    }
    static void DebateAnimationLoaded(De_Controller __instance){
        var a=ImpactTiming.Current(true);
        if(a==null || !a.Valid() || a.cancelled || a.speech?.Pointer!=__instance.Pointer)return;
        a.animationLoaded=true;a.quickLoadedAt=Time.unscaledTimeAsDouble;CardLatency.Loaded(a);
        AttackClipClock.Loaded(a);
        RealtimeParry.ObserveLoadedAttack(a);
    }
    static void Trace(ImpactTiming.Attack a,string phase){
#if DuelCounters
        try{
            if(!a.animationLoaded)return;
            double now=Time.unscaledTimeAsDouble;
            if(now-traceAt>=60){traceAt=now;traceCount=0;}
            if(traceCount++>=120)return;
            string path=System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,"DuelCounters-pacing.log");
            
            CombatLog.Write(path,262144,DateTime.Now.ToString("s")+$" {phase} actor={a.fighter?.GetName()} card={a.fightCard?.OJPECMNINNA} single={a.quickBasic} checked={a.quickBasicChecked} lead={a.quickNativeLead:F4} cap={SingleCap(a):F4} scale={Time.timeScale:F3} elapsed={now-a.quickLoadedAt:F4} clip={a.fight?.m_curAnimationIndex} strikeClip={a.quickContactAnimation} strikeTime={a.quickContactTime:F4} clockReady={a.clipClock?.ready} targets={Fi_Controller.m_listTargetHero?.Count} passed={a.quickContactPassed}"+Environment.NewLine);
        }catch{}
#endif
    }
#if DuelCounters
    static double traceAt;static int traceCount;
#endif
    // End only this attack's extra pacing owner at actual contact, regardless
    // of whether parry succeeds, misses, or awaits post-contact input settlement.
    // Never wait for the recovery animation or for delayed HP application.
    internal static void Contact(ImpactTiming.Attack a){
        if(a==null || !a.Valid() || a.cancelled)return;
        if(!a.quickContactPassed && a.requestedWarning>0){
            LastMeasurement=a.firstCueAt<0?"첫 타격 전 예고 누락 · 로그 확인 필요":$"예고 목표 {a.requestedWarning:0.00}초 / 실제 간격 {Time.unscaledTimeAsDouble-a.firstCueAt:0.00}초";
        }
        if(!a.quickContactPassed)RealtimeParry.Audit(a,"first-contact",$"requested={a.requestedWarning:F4} actualLead={(a.firstCueAt<0?-1:Time.unscaledTimeAsDouble-a.firstCueAt):F4}");
        bool first=!a.quickContactPassed;
        a.quickContactPassed=true;a.warningCap=float.PositiveInfinity;
        if(a.quickBasic)End(a);else ApplySpeed();
        if(first)Trace(a,"contact");
    }
    internal static void End(ImpactTiming.Attack a){
#if DuelCounters
        StrengthPressure.Release(a);
#endif
        if(a==null || owner!=a)return;
        owner=null;
        RestoreScale(a);
    }
    static void RestoreScale(ImpactTiming.Attack a){
        if(adjusted && Mathf.Approximately(Time.timeScale,appliedScale)){
            bool fast=a.debate?De_DebateCtrlUI.m_isAccelerate:Fi_BattleCtrlUI.m_isAccelerate;
            Time.timeScale=fast?previousScale:1;
        }
        adjusted=false;
    }
    internal static void Reset(){End(owner);}
}
