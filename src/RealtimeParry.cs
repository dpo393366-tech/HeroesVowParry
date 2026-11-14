using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vow.Counters;

// Input history is scoped to one card and defender. A successful guard has a
// fixed expiry from its input; hits never extend it or carry it to the next card.
internal static class RealtimeParry
{
    // Native aryWeapon reader stores the weapon-key dictionary in ALAMACECLLD.
    // Key 5 is archery (e.g. 속사 1002405); never infer it from the character's
    // equipped weapon because cross-weapon cards can be equipped independently.
    internal static float RangedGrace(ImpactTiming.Attack a){
        if(a==null || a.debate || a.fightCard==null || a.fightCard.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack)return 0;
        if(!a.archeryParry.HasValue){
            var weapons=a.fightCard.ALAMACECLLD;
            a.archeryParry=weapons!=null && weapons.Count==1 && weapons.ContainsKey(5);
        }
        return a.archeryParry.Value?DuelCounters.RangedGrace:0;
    }
    internal static double DefenderDeadline(ImpactTiming.Attack a,IntPtr victim,double start,double deadline){
#if DuelCounters
        return Math.Min(deadline,start+StrengthPressure.Window(a,victim)+RangedGrace(a));
#else
        return deadline;
#endif
    }
    static float RepeatGuard(ImpactTiming.Attack a,IntPtr victim){
        float seconds=DuelCounters.GuardWindow+RangedGrace(a);
#if DuelCounters
        return StrengthPressure.RepeatWindow(a,victim,seconds);
#else
        return seconds;
#endif
    }
    internal sealed class CueWindow{internal double start,deadline;}
    static ImpactTiming.Attack pressedAttack,cueAttack;
    static readonly Dictionary<IntPtr,Action> pressedVictims=new();
    static readonly HashSet<IntPtr> successfulVictims=new();
    static double pressedAt=-1,readyAt,cueAt;
    static int polledFrame=-1,cueFrame=-1;
    static bool consumedPress;
    // Commit an in-window input when it arrives, not when a later animation
    // event happens to run. A subsequent press cannot erase a valid earlier hit.
    static readonly Dictionary<(ImpactTiming.Attack,IntPtr,double),double> acceptedCues=new();
    static double matchedPress=-1;
    static bool AlreadyGuarded(ImpactTiming.Attack a,IntPtr victim,double now){
        if(!DuelCounters.CueInput)return false;bool found=false;
        foreach(var w in a.cueWindows.Values){
            if(now<w.start||now>DefenderDeadline(a,victim,w.start,w.deadline))continue;
            if(!acceptedCues.ContainsKey((a,victim,w.start)))return false;
            found=true;
        }
        return found;
    }
    static void LatchCueInput(ImpactTiming.Attack a){
        if(!DuelCounters.CueInput||a==null||pressedAttack!=a||pressedAt<0)return;
        foreach(var w in a.cueWindows.Values){
            if(pressedAt<w.start-DuelCounters.EarlyCueWindow-1e-7||pressedAt>w.deadline+1e-7)continue;
            foreach(var victim in pressedVictims.Keys){
                if(pressedAt>DefenderDeadline(a,victim,w.start,w.deadline)+1e-7)continue;
                var key=(a,victim,w.start);if(acceptedCues.ContainsKey(key))continue;
                acceptedCues[key]=pressedAt;successfulVictims.Add(victim);
                readyAt=Time.unscaledTimeAsDouble+DuelCounters.SuccessCooldown;
                Audit(a,"cue-input-accepted",$"start={w.start:F4} deadline={DefenderDeadline(a,victim,w.start,w.deadline):F4} sharedDeadline={w.deadline:F4} victim={victim}");
            }
        }
    }
    static CounterGlow cue;
    static readonly HashSet<IntPtr> debateWarnings=new();
    static readonly HashSet<(int,float)> fightWarnings=new();
    internal static bool Allowed=>DuelCounters.NoDelay && Panel.InputAllowed && Application.isFocused && Time.timeScale>0 &&
        !FICFPEHAJNJ.ODIOKGGBKEI && !UI_BattlePreviewUI.m_isOpen && !BattleModal.Visible;

    static bool Eligible(ImpactTiming.Attack a,out IntPtr victim,out Action guard,bool pose=false,bool early=false){
        victim=IntPtr.Zero;guard=null;
        if(a==null || !a.Valid() || a.cancelled || !PlayerBattleRules.ManualCombat(a.debate))return false;
        // The animation can finish before an owned late-input deadline.
        // Its pending victim still owns that window until settlement completes.
        if(LateParry.Target(a,out victim,out guard))return true;
        if((!early && a.preparing) || a.motionFinished || (!early && !a.debate && !a.animationLoaded))return false;
        if(a.debate){
            if(a.debateCard?.HFBAJIGHOAF!=MMGEDKCCMHH.GANKBONOKGD.Attack ||
               a.debater.m_debateAttribute.IBLKPJDLABN!=FMHEOPMKFJB.Enemy ||
               (!early && !De_Controller.m_isLaunchingAttack && (a.speech.m_listEventIndex?.Count??0)==0))return false;
            var targets=a.speech.m_listTarget;if(targets==null)return false;
            foreach(var t in targets)if(t && PlayerBattleRules.Manual(true,t.GetHero(),t.m_debateAttribute.IBLKPJDLABN) && DuelCounters.Opposed(FMHEOPMKFJB.Enemy,t.m_debateAttribute.IBLKPJDLABN)){
                victim=t.m_debateAttribute.Pointer;if(pose)guard=()=>CounterEffects.Rebut(t);return true;
            }
        }else{
            if(a.fightCard?.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack ||
               a.fighter.GetAttribute().IBLKPJDLABN!=FMHEOPMKFJB.Enemy ||
               (!early && !Fi_Controller.m_isLaunchingAttack && (a.fight.m_listEventIndex?.Count??0)==0))return false;
            var targets=Fi_Controller.m_listTargetHero;if(targets==null)return false;
            foreach(var t in targets)if(t!=null && PlayerBattleRules.Manual(false,t.GetHero(),t.GetAttribute().IBLKPJDLABN) && DuelCounters.Opposed(FMHEOPMKFJB.Enemy,t.GetAttribute().IBLKPJDLABN)){
                victim=t.GetAttribute().Pointer;if(pose)guard=()=>CounterEffects.Guard(t);return true;
            }
        }
        return false;
    }
    // Snapshot only the current card's actual victims; never guard a distant
    // protagonist or transfer this input to a later retargeted card.
    static void CollectTargets(ImpactTiming.Attack a){
        LateParry.Collect(a,pressedVictims);
        if(a.debate){
            var targets=a.speech.m_listTarget;if(targets==null)return;
            foreach(var t in targets)if(t && PlayerBattleRules.Manual(true,t.GetHero(),t.m_debateAttribute.IBLKPJDLABN))
                pressedVictims[t.m_debateAttribute.Pointer]=()=>CounterEffects.Rebut(t);
        }else{
            var targets=Fi_Controller.m_listTargetHero;if(targets==null)return;
            foreach(var t in targets)if(t!=null && PlayerBattleRules.Manual(false,t.GetHero(),t.GetAttribute().IBLKPJDLABN))
                pressedVictims[t.GetAttribute().Pointer]=()=>CounterEffects.Guard(t);
        }
    }
    internal static void PollInput(){
        GuardInfoInput.Observe();
        if(!(Fi_Controller.m_isInBattle?PlayerBattleRules.ManualCombat(false):De_Controller.m_isInDebate&&PlayerBattleRules.ManualCombat(true))){
            pressedAttack=null;pressedAt=-1;consumedPress=false;pressedVictims.Clear();successfulVictims.Clear();acceptedCues.Clear();ClearCue();return;
        }
        if(!Allowed){pressedAttack=null;pressedAt=-1;ClearCue();return;}
        if(polledFrame==Time.frameCount)return;
        polledFrame=Time.frameCount;
        if(!DuelCounters.GuardPressed)return;
        consumedPress=false;
        try{
            var a=ImpactTiming.Current(Fi_Controller.m_isInBattle?false:true);
            if(!Eligible(a,out var victim,out var guard,true,DuelCounters.CueInput && DuelCounters.EarlyCueWindow>0))return;
            LatchCueInput(a);
            consumedPress=true;
            double now=Time.unscaledTimeAsDouble;
            // Repeated input for a cue already guarded neither extends guard
            // duration nor creates a failure cooldown for the next contact.
            if(AlreadyGuarded(a,victim,now))return;
            if(now<readyAt){
                if(DuelCounters.AntiSpam && successfulVictims.Count==0){pressedAttack=null;pressedAt=-1;pressedVictims.Clear();successfulVictims.Clear();readyAt=now+DuelCounters.FailedCooldown;Audit(a,"spam-rejected",$"ready={readyAt:F4}");}
                return;
            }
            if(pressedAttack!=a)acceptedCues.Clear();
            pressedAttack=a;pressedVictims.Clear();successfulVictims.Clear();
            pressedVictims[victim]=guard;CollectTargets(a);pressedAt=now;
            // Optimistically reserve the failure cooldown. A successful contact
            // removes it immediately; another press cannot replace a failed one.
            readyAt=now+DuelCounters.FailedCooldown;Audit(a,"input",$"ready={readyAt:F4}");
            LatchCueInput(a);
            if(a.quickBasic)foreach(var w in a.cueWindows.Values)
                Audit(a,"single-input-window",$"sinceCue={now-w.start:F4} effectiveDeadline={DefenderDeadline(a,victim,w.start,w.deadline):F4} remaining={DefenderDeadline(a,victim,w.start,w.deadline)-now:F4}");
            foreach(var pose in pressedVictims.Values)try{pose?.Invoke();}catch(Exception e){DuelCounters.Warn("패링 자세: "+e.Message);}
        }catch(Exception e){pressedAttack=null;DuelCounters.Warn("패링 입력: "+e.Message);}
    }
    internal static bool Consume(ImpactTiming.Attack a,IntPtr victim){
        // Unity Update ordering is unspecified. Read the same input edge before
        // native damage too; the frame guard prevents duplicate consumption.
        PollInput();
        if(!Allowed || pressedAttack!=a || !pressedVictims.ContainsKey(victim) || !a.Valid() || a.cancelled || !PlayerBattleRules.ManualCombat(a.debate))return false;
        double age=Time.unscaledTimeAsDouble-DuelCounters.TimingAdvance-pressedAt;
        return age>=-DuelCounters.PostWindow-1e-7 && age<=(successfulVictims.Contains(victim)?RepeatGuard(a,victim):DuelCounters.TimingWindow+RangedGrace(a))+1e-7;
    }
    internal static bool Matches(ImpactTiming.Attack a,IntPtr victim,double contact,double deadline){
        if(!Allowed || pressedAttack!=a || !pressedVictims.ContainsKey(victim) || !a.Valid() || a.cancelled || !PlayerBattleRules.ManualCombat(a.debate))return false;
        // A late click can cover more than one already-arrived hit, but only
        // while each hit's own post deadline is still open. No retroactive HP undo.
        double delta=pressedAt-contact;
        return pressedAt<=deadline+1e-7 && (delta>=0 || -delta<=(successfulVictims.Contains(victim)?RepeatGuard(a,victim):DuelCounters.TimingWindow+RangedGrace(a))+1e-7);
    }
    internal static void Success(ImpactTiming.Attack a,IntPtr victim){
        if(pressedAttack==a && pressedVictims.ContainsKey(victim)){
            if(DuelCounters.CueInput&&matchedPress>=0&&Math.Abs(matchedPress-pressedAt)>1e-7)return;
            if(successfulVictims.Count==0)readyAt=Time.unscaledTimeAsDouble+DuelCounters.SuccessCooldown;
            successfulVictims.Add(victim);
        }
    }
    static readonly List<KeyValuePair<(ImpactTiming.Attack,IntPtr,double),double>> shiftCues=new();
    internal static void Shift(double gap){
        if(pressedAt>=0)pressedAt+=gap;readyAt+=gap;cueAt+=gap;if(matchedPress>=0)matchedPress+=gap;
        if(acceptedCues.Count==0)return;
        shiftCues.Clear();shiftCues.AddRange(acceptedCues);acceptedCues.Clear();
        foreach(var kv in shiftCues)acceptedCues[(kv.Key.Item1,kv.Key.Item2,kv.Key.Item3+gap)]=kv.Value+gap;
        shiftCues.Clear();
    }
    internal static bool SuppressInfo(){
        PollInput();
        return consumedPress && (Input.GetMouseButtonDown(1)||Input.GetMouseButton(1)||Input.GetMouseButtonUp(1));
    }
    internal static void ObserveLoadedAttack(ImpactTiming.Attack a){
        try{
#if DuelCounters
            StrengthPressure.Prepare(a);
#endif
            if(Allowed && Eligible(a,out _,out _)){AttackPacing.ObserveSingle(a);AttackPacing.PlanWarning(a);}}
        catch(Exception e){DuelCounters.Warn("단타 공격 속도 확인: "+e.Message);}
    }
    internal static void BeforeUpdate(ImpactTiming.Attack a){
        if(!DuelCounters.NoDelay)return;
        PollInput();
        if(!Allowed || a==null || !a.Valid() || a.cancelled){ClearCue();return;}
        try{
            if(cueAttack!=a){ClearCue();cueAttack=a;debateWarnings.Clear();fightWarnings.Clear();}
#if DuelCounters
            StrengthPressure.Motion(a);
#endif
            if(cue!=null){cue.Pulse((float)(Time.unscaledTimeAsDouble-cueAt));if(Time.unscaledTimeAsDouble-cueAt>cue.Duration)ClearCue();}
            ObserveLoadedAttack(a);
            if(cueFrame==Time.frameCount || !Eligible(a,out _,out _))return;
            cueFrame=Time.frameCount;
            var animator=a.debate?a.debater.GetAnimator():a.fighter.GetAnimator();
            if(!animator)return;
            int index=a.debate?a.speech.m_curAnimationIndex:a.fight.m_curAnimationIndex;
            string stateName=a.debate || (index&1)==0?"Attack1":"Attack2";
            // Native Update dispatches events against current-state normalized
            // time, not wall-clock countdowns. Use the same Attack1/2 states.
            for(int layer=0;layer<Math.Min(8,animator.layerCount);layer++){
                var state=animator.GetCurrentAnimatorStateInfo(layer);
                // A blending-out Attack1/2 may belong to the previous clip.
                // The next state is not yet the native event-dispatch clock.
                if(animator.IsInTransition(layer))continue;
                if(!state.IsName(stateName))continue;
                // Attack clips are one-shot. Modulo would hide a pending contact
                // when a long frame advances the state past normalizedTime=1.
                float normalized=state.normalizedTime;
                float rate=Math.Abs(animator.speed*state.speed*state.speedMultiplier*Time.timeScale);
                if(rate<=.0001f || state.length<=0)return;
                if(a.debate){
                    var animation=a.speech.m_debateAnimation;var indices=a.speech.m_listEventIndex;
                    if(animation?.DGBOCHKBFJG==null || indices==null)return;
                    for(int i=0;i<Math.Min(128,indices.Count);i++){
                        int j=indices[i];if(j<0 || j>=animation.DGBOCHKBFJG.Count)continue;
                        var e=animation.DGBOCHKBFJG[j];if(e==null || !e.EBHFFNHELFB)continue;
                        AttackPacing.PaceWarning(a,index,e.DHBBBOOGECL,(e.DHBBBOOGECL-normalized)*state.length/Math.Abs(animator.speed*state.speed*state.speedMultiplier));
                        if(WarningDue(e.DHBBBOOGECL,normalized,state.length,rate)){
                            debateWarnings.Add(e.Pointer);if(!a.cueWindows.ContainsKey((index,e.DHBBBOOGECL)))Audit(a,"cue-clock",$"clip={index} layer={layer} normalized={normalized:F4} contact={e.DHBBBOOGECL:F4} length={state.length:F4} rate={rate:F4}");ContactCue(a,index,e.DHBBBOOGECL);return;
                        }
                    }
                }else{
                    // Do not cache an attack plan before native loading finishes.
                    // Read only events that native Update still has pending.
                    var animations=a.fight.m_listBattleAnimation;var indices=a.fight.m_listEventIndex;
                    if(animations==null || index<0 || index>=animations.Count || indices==null)return;
                    var events=animations[index]?.DGBOCHKBFJG;if(events==null)return;
                    double observedRate=0;
                    bool sampled=a.quickBasic && a.singleCueClock.Sample(index,state.fullPathHash,normalized,Time.unscaledTimeAsDouble,out observedRate);
                    for(int i=0;i<Math.Min(128,indices.Count);i++){
                        int j=indices[i];if(j<0 || j>=events.Count)continue;
                        var e=events[j];if(e==null || !PerHitParry.IsParryContact(a,index,e.DHBBBOOGECL))continue;
                        if(!a.quickBasic)AttackPacing.PaceWarning(a,index,e.DHBBBOOGECL,(e.DHBBBOOGECL-normalized)*state.length/Math.Abs(animator.speed*state.speed*state.speedMultiplier));
                        bool due=a.quickBasic?sampled&&SingleCueClock.Due(e.DHBBBOOGECL,normalized,observedRate,SingleCueLead(a)):WarningDue(e.DHBBBOOGECL,normalized,state.length,rate);
                        if(due){
                            if(!a.cueWindows.ContainsKey((index,e.DHBBBOOGECL)))Audit(a,"cue-clock",$"clip={index} layer={layer} normalized={normalized:F4} contact={e.DHBBBOOGECL:F4} length={state.length:F4} rate={rate:F4} observedNormalizedRate={observedRate:F5} singleLead={a.singleCueLead:F4}");ContactCue(a,index,e.DHBBBOOGECL);return;
                        }
                    }
                }
                return;
            }
        }catch(Exception e){ClearCue();DuelCounters.Warn("패링 예고 확인: "+e.Message);}
    }
    // Only pending native events call this. A frame crossing the entire warning
    // window still emits its cue once instead of silently losing it.
    static bool WarningDue(float contact,float normalized,float length,float rate)=>
        (contact-normalized)*length/rate<=(DuelCounters.WarningLead>0?DuelCounters.WarningLead:DuelCounters.TimingAdvance+DuelCounters.TimingWindow)+1e-7;
    static float SingleCueLead(ImpactTiming.Attack a){
        if(DuelCounters.WarningLead>0)return DuelCounters.WarningLead;
        if(a.singleCueLead>=0)return a.singleCueLead;
        float seconds=EffectiveCueWindow(a)+RangedGrace(a);
        var targets=Fi_Controller.m_listTargetHero;
        if(targets!=null)for(int i=0;i<Math.Min(128,targets.Count);i++){
            var t=targets[i];if(t==null)continue;
            var attr=t.GetAttribute();
            if(attr!=null && PlayerBattleRules.Manual(false,t.GetHero(),attr.IBLKPJDLABN))
                seconds=Math.Min(seconds,(float)DefenderDeadline(a,attr.Pointer,0,seconds));
        }
        return a.singleCueLead=Math.Max(.01f,seconds);
    }
    internal static void ContactCue(ImpactTiming.Attack a,int animation,float time){
        if(!Allowed || a==null || !Eligible(a,out _,out _) || !PerHitParry.IsParryContact(a,animation,time))return;
        try{
            if(cueAttack!=a){ClearCue();cueAttack=a;debateWarnings.Clear();fightWarnings.Clear();}
            if(fightWarnings.Add((animation,time))){RecordCue(a,animation,time);Show(a);}
        }catch(Exception e){DuelCounters.Warn("패링 타격 예고: "+e.Message);}
    }
    static void RecordCue(ImpactTiming.Attack a,int animation,float time){
        if(a.cueWindows.Count>=256 || a.cueWindows.ContainsKey((animation,time)))return;
        double now=Time.unscaledTimeAsDouble;
        a.cueWindows[(animation,time)]=new CueWindow{start=now,deadline=now+EffectiveCueWindow(a)+RangedGrace(a)};
    }
    static float EffectiveCueWindow(ImpactTiming.Attack a){
#if DuelCounters
        return StrengthPressure.Window(a);
#else
        return DuelCounters.CueWindow;
#endif
    }
    internal static void BindCue(ImpactTiming.Attack a,PerHitParry.Step s){
        if(s.cueBound)return;
        CueWindow w=null;
        if(!s.nativeOnly)a.cueWindows.TryGetValue((s.animation,s.time),out w);
        else if(a.debate){
            // Native debate captures/removes the target before damage creation.
            // Bind the latest emitted event in this animation, never a prior card.
            foreach(var row in a.cueWindows)if(row.Key.Item1==a.speech.m_curAnimationIndex && (w==null||row.Value.start>w.start))w=row.Value;
        }
        if(w==null){
            // A too-short/unreadable animation still MUST provide its full input
            // window. Only settlement waits, never animation or a repeated skill.
            double now=Time.unscaledTimeAsDouble;w=new CueWindow{start=now,deadline=now+EffectiveCueWindow(a)+RangedGrace(a)};
            if(!s.nativeOnly)a.cueWindows[(s.animation,s.time)]=w;
            Show(a);
        }
        s.cueBound=true;s.cueStart=w.start;s.cueDeadline=w.deadline;
        Audit(a,"cue-window",$"step={s.index} start={w.start:F4} deadline={w.deadline:F4} rangedGrace={RangedGrace(a):F3}");
    }
    internal static bool ConsumeCue(ImpactTiming.Attack a,IntPtr victim,double start,double deadline,double contact){
        PollInput();return MatchesCue(a,victim,start,deadline,contact);
    }
    internal static bool MatchesCue(ImpactTiming.Attack a,IntPtr victim,double start,double deadline,double contact){
        if(!Allowed || !a.Valid() || a.cancelled || !PlayerBattleRules.ManualCombat(a.debate))return false;
        deadline=DefenderDeadline(a,victim,start,deadline);
        LatchCueInput(a);matchedPress=-1;
        if(acceptedCues.TryGetValue((a,victim,start),out var committed)){matchedPress=committed;return true;}
        if(pressedAttack!=a || !pressedVictims.ContainsKey(victim))return false;
        matchedPress=pressedAt;
        if(pressedAt>=start-DuelCounters.EarlyCueWindow-1e-7 && pressedAt<=deadline+1e-7)return true;
        return successfulVictims.Contains(victim) && contact>=pressedAt && contact-pressedAt<=RepeatGuard(a,victim)+1e-7 && pressedAt<=deadline+1e-7;
    }
    static bool Near(float contact,float normalized,float length,float rate){
        float seconds=(contact-normalized)*length/rate;
        return seconds>=Math.Max(0,DuelCounters.TimingAdvance-DuelCounters.PostWindow)-1e-7 && seconds<=DuelCounters.TimingAdvance+DuelCounters.TimingWindow+1e-7;
    }
    static void Show(ImpactTiming.Attack a){
        ClearCue();cueAt=Time.unscaledTimeAsDouble;a.lastCueAt=cueAt;if(!a.quickContactPassed && a.firstCueAt<0)a.firstCueAt=cueAt;Audit(a,"cue",$"requested={DuelCounters.WarningLead:F4}");
        if(DuelCounters.FlashEnabled)try{cue=a.debate?new CounterGlow(a.debater.GetTransform(),a.debater.GetListRenderer(),a.debater.GetHighlighter(),ParryVisuals.Cost(a)):new CounterGlow(a.fighter.GetTransform(),a.fighter.GetListRenderer(),a.fighter.GetHighlighter(),ParryVisuals.Cost(a));}catch(Exception e){DuelCounters.Warn("패링 섬광: "+e.Message);}
        if(DuelCounters.SoundEnabled)try{CounterEffects.AttackWarning();}catch(Exception e){DuelCounters.Warn("패링 예고음: "+e.Message);}
    }
    internal static void Audit(ImpactTiming.Attack a,string phase,string detail){
#if DuelCounters
        try{
            double now=Time.unscaledTimeAsDouble;
            if(now-auditAt>=60){auditAt=now;auditCount=0;}
            if(auditCount++>=240)return;
            string path=System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,"DuelCounters-timing.log");
            
            CombatLog.Write(path,262144,DateTime.Now.ToString("s")+$" t={now:F4} {phase} attack={a?.GetHashCode()} card={a?.fightCard?.OJPECMNINNA} advance={DuelCounters.TimingAdvance:F3} pre={DuelCounters.TimingWindow:F3} post={DuelCounters.PostWindow:F3} press={pressedAt:F4} cue={a?.lastCueAt:F4} scale={Time.timeScale:F4} {detail}"+Environment.NewLine);
        }catch{}
#endif
    }
#if DuelCounters
    static double auditAt;static int auditCount;
#endif
    static void ClearCue(){var old=cue;cue=null;old?.Dispose();}
    internal static void Marker(){if(Allowed && cueAttack?.Valid()==true)cue?.Marker();}
    internal static void Reset(){ClearCue();pressedAttack=cueAttack=null;pressedVictims.Clear();successfulVictims.Clear();acceptedCues.Clear();matchedPress=-1;pressedAt=-1;readyAt=0;polledFrame=cueFrame=-1;consumedPress=false;debateWarnings.Clear();fightWarnings.Clear();}
}
