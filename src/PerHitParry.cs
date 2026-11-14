using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Vow.Counters;

// Observe the actual loaded animation (including workshop replacements).
// Do NOT use the card's damage multiplier as a strike count. Repeated event
// flags at the same animation/time are one contact, not extra attacks.
internal static class PerHitParry
{
    internal sealed class Step
    {
        internal int animation,index,latePending;
        internal readonly HashSet<IntPtr> damaged=new();
        internal float time;
        internal bool cueBound;internal double cueStart,cueDeadline;
        internal bool friendlySoundPlayed,enemySoundPlayed;
        internal NPCCtrl debateVictim;
        internal bool damage,queued,done,staggerCounted,groggyCounted,groggyRelieved,nativeOnly;
        internal readonly HashSet<IntPtr> blocked=new();
    }
    internal sealed class Plan
    {
        internal ImpactTiming.Attack attack;
        internal readonly List<Step> steps=new();
        internal readonly Dictionary<IntPtr,Step> events=new();
        internal readonly Queue<Step> pending=new();
        internal bool busy,aggregate,analysed,abandoned;
        internal bool groggyComboKnown,groggyCombo;
        internal Step activeStep;
        internal float inputRemaining=-1,inputBudget;
        internal GKHLLOOEEFN recoilVictim;
        internal bool recoilDone,cardCounted,cardParried;
        internal GKHLLOOEEFN firstGuard;
    }
    internal sealed class Receipt
    {
        internal Plan plan;
        internal Step step;
        internal bool ran;
        internal double createdAt;
        internal Il2CppSystem.Collections.Generic.List<GKHLLOOEEFN> targets;
    }
    static readonly Dictionary<IntPtr,Receipt> receipts=new();
    internal static Receipt running;
    static ImpactTiming.Attack nativeDamageOwner;
    internal static ImpactTiming.Attack DamageOwner=>running?.plan?.attack??nativeDamageOwner;
    internal struct NativeState {
        internal bool entered,swapped;internal Receipt previous,receipt;
        internal ImpactTiming.Attack previousOwner;
        internal Il2CppSystem.Collections.Generic.List<GKHLLOOEEFN> targets;
    }
    internal static bool Enabled=>DuelCounters.PerStrike || DuelCounters.NoDelay;
    internal static bool Busy(ImpactTiming.Attack a)=>a?.strikes?.busy==true && a.Valid();

    internal static void Install(Harmony h)
    {
        foreach(string name in new[]{"HIECIGKMAFE","KBIBAIIJAAP","DHMMNDHAEFF"})
            h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),name),prefix:new HarmonyMethod(typeof(PerHitParry),nameof(Event)),finalizer:new HarmonyMethod(typeof(PerHitParry),nameof(EventEnd)));
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"GOGFJJDCOOF"),prefix:new HarmonyMethod(typeof(PerHitParry),nameof(FightDamage)));
        h.Patch(AccessTools.DeclaredMethod(typeof(LEDJAIBAAJC),"GOGFJJDCOOF"),prefix:new HarmonyMethod(typeof(PerHitParry),nameof(DebateDamage)));
        ContactEffects.Install(h);
        foreach(var t in new[]{typeof(Fi_Controller.JFBIHELODLP),typeof(De_Controller.BIODLKHOIFE)})
            h.Patch(AccessTools.DeclaredMethod(t,"MoveNext"),prefix:new HarmonyMethod(typeof(PerHitParry),nameof(NativeScope)){priority=Priority.Last},
                finalizer:new HarmonyMethod(typeof(PerHitParry),nameof(NativeEnd)));
    }
    static Plan Get(ImpactTiming.Attack a)
    {
        if(a.strikes==null)a.strikes=new Plan{attack=a};
        return a.strikes;
    }
    // Count authored contacts, not the number of AOE victims/native callbacks.
    // Cache once per card. A final aggregate damage packet can still be a combo.
    internal static bool GroggyCombo(ImpactTiming.Attack a){
        if(a==null)return false;
        var p=Get(a);if(p.groggyComboKnown)return p.groggyCombo;
        if(!a.debate){
            Analyse(p);if(!p.analysed)return false;
            int contacts=0;foreach(var s in p.steps)if(!s.nativeOnly)contacts++;
            // Unknown native-only packets do not identify separate authored hits.
            if(contacts==0)return false;
            p.groggyCombo=contacts>1;
        }else{
            var events=a.speech?.m_debateAnimation?.DGBOCHKBFJG;
            if(events==null)return false;
            bool have=false;float first=0;
            for(int i=0;i<Math.Min(4096,events.Count);i++){
                var e=events[i];if(e==null||!e.EBHFFNHELFB||!float.IsFinite(e.DHBBBOOGECL))continue;
                if(!have){have=true;first=e.DHBBBOOGECL;}
                else if(!StrikeLedger.SameTime(first,e.DHBBBOOGECL)){p.groggyCombo=true;break;}
            }
            if(!have)return false;
        }
        p.groggyComboKnown=true;return p.groggyCombo;
    }
    static void Analyse(Plan p)
    {
        if(p.analysed || p.attack.debate)return;
        var animations=p.attack.fight?.m_listBattleAnimation;
        if(animations==null || animations.Count==0)return;
        p.analysed=true;
        if(animations.Count>256)return;
        int scanned=0;
        // Build transactionally: an incomplete/invalid list never replaces the
        // native packet fallback and cannot leave a wait for a nonexistent event.
        var steps=new List<Step>();var map=new Dictionary<IntPtr,Step>();
        for(int i=0;i<animations.Count;i++){
            var events=animations[i]?.DGBOCHKBFJG;if(events==null)continue;
            foreach(var e in events){
                if(++scanned>4096)return;
                if(e==null || !AttackClipClock.Contact(e.EBHFFNHELFB,e.GHEGCDIKHDI,e.GLBFOMGOIIO,e.MFAPFHKKMOK) || !float.IsFinite(e.DHBBBOOGECL))continue;
                Step step=null;
                foreach(var candidate in steps)if(candidate.animation==i && StrikeLedger.SameTime(candidate.time,e.DHBBBOOGECL)){step=candidate;break;}
                if(step==null){if(steps.Count>=128)return;step=new Step{animation=i,time=e.DHBBBOOGECL};steps.Add(step);}
                step.damage|=e.EBHFFNHELFB;map[e.Pointer]=step;
            }
        }
        steps.Sort((x,y)=>x.animation!=y.animation?x.animation.CompareTo(y.animation):x.time.CompareTo(y.time));
        int damageEvents=0,tailContacts=0;foreach(var s in steps)if(s.damage)damageEvents++;
        // A finishing reaction AFTER the sole damage packet does not invalidate
        // the preceding combo. Cataclysm has eleven contacts, one damage packet,
        // then a cosmetic launch. Previously that tail discarded all eleven.
        // Only split the contacts up to the packet: a trailing flourish cannot
        // retroactively parry damage that has already been committed.
        if(damageEvents==1){
            int lastDamage=steps.FindIndex(s=>s.damage);
            if(lastDamage+1<steps.Count){
                tailContacts=steps.Count-lastDamage-1;
                var tails=new HashSet<Step>(steps.GetRange(lastDamage+1,steps.Count-lastDamage-1));
                steps.RemoveRange(lastDamage+1,steps.Count-lastDamage-1);
                var discard=new List<IntPtr>();foreach(var e in map)if(tails.Contains(e.Value))discard.Add(e.Key);
                foreach(var key in discard)map.Remove(key);
            }
        }
        bool aggregate=steps.Count>1 && damageEvents==1;
        if(!aggregate){
            // Already-per-hit or ambiguous mixed layouts retain native packets.
            steps.RemoveAll(s=>!s.damage);
            var discard=new List<IntPtr>();foreach(var e in map)if(!e.Value.damage)discard.Add(e.Key);
            foreach(var key in discard)map.Remove(key);
        }
        if(steps.Count==0 || steps.Count>128)return;
        for(int i=0;i<steps.Count;i++)steps[i].index=i;
        p.steps.AddRange(steps);foreach(var e in map)p.events.Add(e.Key,e.Value);
        p.aggregate=aggregate;p.analysed=true;
        RealtimeParry.Audit(p.attack,"parry-plan",$"contacts={steps.Count} aggregate={aggregate} damageGroups={damageEvents} reactionTail={tailContacts}");
        CombatTrace.Event($"strike plan card={p.attack.fightCard?.OJPECMNINNA} contacts={steps.Count} aggregate={aggregate}");
    }
    // Every warning must refer to a contact the resolver can actually parry.
    // Cosmetic impacts after the final packet inherit its reaction result;
    // they do not erase earlier strike windows or open retroactive ones.
    internal static bool IsParryContact(ImpactTiming.Attack a,int animation,float time){
        if(a==null||!a.Valid()||a.cancelled)return false;
        if(a.debate)return true;
        if(!a.animationLoaded)return false;
        var p=Get(a);Analyse(p);
        foreach(var s in p.steps)if(!s.nativeOnly&&s.animation==animation&&StrikeLedger.SameTime(s.time,time))return true;
        return false;
    }
    internal static bool TrySingle(ImpactTiming.Attack a,out int animation,out float time){
        animation=-1;time=0;var p=Get(a);Analyse(p);
        if(p.steps.Count==0)return false;
        a.quickBasicChecked=true;
        if(p.steps.Count!=1)return false;
        animation=p.steps[0].animation;time=p.steps[0].time;return true;
    }
    static void Event(Fi_Controller __instance,PKLOJNCAOND __0,out ContactEffects.Context __state)
    {
        __state=ContactEffects.Begin();
        if(!Enabled || __0==null)return;
        var a=ImpactTiming.Current(false);
        if(a==null || !a.Valid() || a.cancelled || a.fight?.Pointer!=__instance.Pointer ||
           a.fightCard?.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack)return;
        if(IsParryContact(a,__instance.m_curAnimationIndex,__0.DHBBBOOGECL))AttackPacing.Contact(a);
        try{
            if(__0.EBHFFNHELFB || __0.GHEGCDIKHDI)RealtimeParry.ContactCue(a,__instance.m_curAnimationIndex,__0.DHBBBOOGECL);
            var p=Get(a);Analyse(p);
            if(p.events.TryGetValue(__0.Pointer,out var step)){Enqueue(p,step);ContactEffects.Set(p,step);}
            else ContactEffects.Nearest(p,__instance.m_curAnimationIndex,__0.DHBBBOOGECL);
        }catch(Exception e){DuelCounters.Warn("타격 시점 확인 실패: "+e.Message);}
    }
    static Exception EventEnd(ContactEffects.Context __state,Exception __exception)=>ContactEffects.EventEnd(__state,__exception);
    static void Enqueue(Plan p,Step step)
    {
        if(step.queued || p.abandoned)return;
        if(DuelCounters.NoDelay){
            // Resolve inside the native event call. No coroutine, yield, update
            // gate, speed change or extra frame is inserted into combat.
            step.queued=true;p.attack.started=true;
            try{
                var routine=Flatten(DuelCounters.Strike(p,step));
                try{if(routine.MoveNext())throw new InvalidOperationException("Realtime counter attempted to wait");}
                finally{(routine as IDisposable)?.Dispose();}
            }catch(Exception e){FailOpen(p);DuelCounters.Warn("실시간 패링 복구 · 원래 공격 계속: "+e.Message);}
            finally{step.done=step.latePending==0;p.attack.done=true;}
            return;
        }
        step.queued=true;p.pending.Enqueue(step);
        if(p.busy)return;
        p.busy=true;p.attack.started=true;p.attack.done=false;p.attack.heartbeat=Time.unscaledTimeAsDouble;
        try{Panel.Run(Drive(p));}
        catch(Exception e){FailOpen(p);DuelCounters.Warn("타격 패링 시작 복구: "+e.Message);}
    }
    // Keep nested managed enumerators inside Safe's exception boundary; Unity
    // otherwise owns their MoveNext and can abandon the outer gate on an error.
    static IEnumerator Flatten(IEnumerator routine){
        var stack=new Stack<IEnumerator>();stack.Push(routine);
        try{
            while(stack.Count>0){
                var current=stack.Peek();
                if(!current.MoveNext()){stack.Pop();(current as IDisposable)?.Dispose();continue;}
                if(current.Current is IEnumerator nested){stack.Push(nested);continue;}
                yield return current.Current;
            }
        }finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
    }
    static IEnumerator Drive(Plan p)
    {
        try{
            while(p.pending.Count>0 && p.attack.Valid() && !p.abandoned){
                var s=p.pending.Dequeue();p.activeStep=s;
                // Drive the guarded iterator here: no extra Unity coroutine/wrapper
                // between contacts, and automatic-only decisions finish in place.
                var guarded=ImpactTiming.Safe(Flatten(DuelCounters.Strike(p,s)),null,p.attack);
                try{while(guarded.MoveNext())yield return guarded.Current;}
                finally{(guarded as IDisposable)?.Dispose();}
                // Third counter still staggers, but never interrupts the middle
                // of an aggregate combo. Finish its contacts before displacing.
                if(p.recoilVictim!=null && !p.recoilDone &&
                   (s.nativeOnly || s.index==p.steps.Count-1) && p.attack.Valid()){
                    p.recoilDone=true;
                    bool reacted=false;
                    try{CounterEffects.Recoil(p.attack.fight,p.attack.fighter,p.recoilVictim);reacted=true;}
                    catch(Exception e){CounterMotion.Reset();DuelCounters.Warn("넉백 연출 복구 · 패링 판정 유지: "+e.Message);}
                    float elapsed=reacted?0:.34f;
                    while(elapsed<.34f && p.attack.Valid()){
                        yield return null;
                        p.attack.heartbeat=Time.unscaledTimeAsDouble;
                        if(Application.isFocused && Time.timeScale>0)elapsed+=Time.unscaledDeltaTime;
                    }
                }
                s.done=true;p.activeStep=null;
            }
        }finally{
            // Disposal/exception must not leave attack.done=true with an unfinished
            // receipt. Ready would otherwise yield forever and skip the watchdog.
            if(p.activeStep!=null || p.pending.Count>0)FailOpen(p);
            p.busy=false;p.attack.done=true;
        }
    }
    internal static bool Ready(ImpactTiming.Attack a,IntPtr id,int state,NPCCtrl debateVictim=null)
    {
        if(!a.Valid() || a.cancelled)return false;
        var p=Get(a);
        if(!receipts.TryGetValue(id,out var r) || r.plan!=p || (state==0 && r.ran)){
            Step step=null;
            if(!a.debate){
                Analyse(p);
                // The native damage coroutine is created synchronously in the
                // event handler. Bind to the latest queued damage contact.
                foreach(var s in p.steps)if(s.damage && s.queued && !s.nativeOnly)step=s;
            }
            if(step==null){
                if(a.debate)AttackPacing.Contact(a);
                step=new Step{damage=true,index=p.steps.Count,debateVictim=debateVictim,nativeOnly=true};p.steps.Add(step);
                // No synthetic repeats for timelines we cannot identify.
                p.aggregate=false;p.analysed=true;
            }
            receipts[id]=r=new Receipt{plan=p,step=step,createdAt=Time.unscaledTimeAsDouble};
            if(!a.debate && Fi_Controller.m_listTargetHero!=null){
                r.targets=new();foreach(var t in Fi_Controller.m_listTargetHero)r.targets.Add(t);
            }
            Enqueue(p,step);
        }
        LateParry.Tick();
        if(!p.abandoned){
            if(!r.step.done)return false;
            if(p.aggregate)foreach(var s in p.steps)if(s.queued && !s.done)return false;
        }
        return true;
    }
    static void NativeScope(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase __instance,out NativeState __state){
        __state=new NativeState{entered=true,previous=running,previousOwner=nativeDamageOwner,targets=Fi_Controller.m_listTargetHero};running=null;
        nativeDamageOwner=ImpactTiming.BoundDamage(__instance.Pointer);
        if(Enabled && receipts.TryGetValue(__instance.Pointer,out var r) && r.step.done && !r.plan.abandoned && r.plan.attack.Valid()){
            running=r;r.ran=true;r.createdAt=Time.unscaledTimeAsDouble;__state.receipt=r;
            // Normally use the original list so native removal/Clear persists.
            // A delayed packet uses its snapshot only if a later event has changed
            // the live targets; never erase that newer event's list on return.
            if(r.targets!=null && !SameTargets(r.targets,Fi_Controller.m_listTargetHero)){
                __state.swapped=true;Fi_Controller.m_listTargetHero=r.targets;
            }
        }
    }
    static Exception NativeEnd(NativeState __state,Exception __exception){
        // A different Harmony prefix can skip this scope. Its default state must
        // not pop an outer receipt or replace the native target list with null.
        if(__state.entered){
            var r=__state.receipt;
            if(r?.targets!=null){
                if(__state.swapped){if(Fi_Controller.m_listTargetHero?.Pointer==r.targets.Pointer)Fi_Controller.m_listTargetHero=__state.targets;}
                else if(Fi_Controller.m_listTargetHero?.Pointer==__state.targets?.Pointer){
                    r.targets.Clear();foreach(var t in __state.targets)r.targets.Add(t);
                }
            }
            running=__state.previous;
            nativeDamageOwner=__state.previousOwner;
        }
        return __exception;
    }
    static bool SameTargets(Il2CppSystem.Collections.Generic.List<GKHLLOOEEFN> a,Il2CppSystem.Collections.Generic.List<GKHLLOOEEFN> b){
        if(a==null || b==null || a.Count!=b.Count)return false;
        for(int i=0;i<a.Count;i++)if(a[i]?.Pointer!=b[i]?.Pointer)return false;
        return true;
    }
    internal static void Completed(IntPtr id)=>receipts.Remove(id);
    internal static ImpactTiming.Attack PendingOwner(IntPtr id)=>receipts.TryGetValue(id,out var r)&&!r.ran?r.plan.attack:null;
    internal static bool Valid(Plan p)
    {
        return !p.abandoned && !p.attack.cancelled && p.attack.Valid();
    }
    internal static float InputLimit(Plan p,Step s,float singleWindow,float comboBudget){
        if(p.inputRemaining<0){p.inputBudget=Math.Min(singleWindow,comboBudget);p.inputRemaining=p.inputBudget;}
        int contacts=s.nativeOnly?1:Math.Max(1,p.steps.Count);
        return Math.Max(0,Math.Min(p.inputRemaining,p.inputBudget/contacts));
    }
    internal static void ChargeInput(Plan p,float elapsed){p.inputRemaining=Math.Max(0,p.inputRemaining-Math.Max(0,elapsed));}
    internal static void Recover(ImpactTiming.Attack a){
        var p=a?.strikes;
        if(p==null || p.abandoned || a.cancelled || !a.Valid())return;
        if((p.busy && Time.unscaledTimeAsDouble-a.heartbeat>1.5) ||
           (!p.busy && (p.activeStep!=null || p.pending.Count>0))){
            DuelCounters.Warn("타격 패링 대기 복구 · 원래 공격 계속");FailOpen(p);
        }
    }
    internal static void FinishMotion(ImpactTiming.Attack a){
        var p=a?.strikes;
        if(p==null || p.abandoned || !a.Valid())return;
        if(DuelCounters.NoDelay && !p.cardCounted){
            p.cardCounted=true;
            if(!a.debate){
                if(p.cardParried){if(ParryStagger.Record(a.fight.Pointer,a.round,a.order,a.fighter.Pointer))p.recoilVictim=p.firstGuard;}
                else ParryStagger.Miss(a.fight.Pointer,a.round,a.order,a.fighter.Pointer);
            }
        }
        if(!DuelCounters.NoDelay || p.recoilDone || p.recoilVictim==null)return;
        p.recoilDone=true;
        try{CounterEffects.Recoil(a.fight,a.fighter,p.recoilVictim);}
        catch(Exception e){CounterMotion.Reset();DuelCounters.Warn("넉백 연출 복구: "+e.Message);}
    }
    internal static bool Block(Plan p,Step s,IntPtr attribute,GKHLLOOEEFN victim=null)
    {
        if(EnemyGroggy.IsStunned(p.attack.debate,attribute)||!s.blocked.Add(attribute))return false;
        EnemyGroggy.Record(p.attack,s);
        try{ParryVisuals.Success(p.attack,attribute,victim,s.debateVictim);}catch(Exception e){DuelCounters.Warn("패링 성공 연출: "+e.Message);}
        if(DuelCounters.NoDelay){p.cardParried=true;if(p.firstGuard==null)p.firstGuard=victim;return true;}
        // Compatibility for the explicitly selected legacy stopped-window mode.
        if(victim==null || s.staggerCounted)return true;
        s.staggerCounted=true;var a=p.attack;
        if(ParryStagger.Record(a.fight.Pointer,a.round,a.order,a.fighter.Pointer))p.recoilVictim=victim;
        return true;
    }
    internal static bool PendingSettlement(ImpactTiming.Attack a){
        if(a==null || !a.Valid() || !DuelCounters.NoDelay)return false;
        if(LateParry.Busy(a))return true;
        foreach(var r in receipts.Values)if(r.plan.attack==a && !r.plan.abandoned){
            if(Time.unscaledTimeAsDouble-r.createdAt<=(r.ran?10:DuelCounters.PostWindow+2))return true;
            // An external coroutine stop must never trap native AfterAttack.
            DuelCounters.Warn("중단된 피해 확정 대기 해제 · 원래 전투 계속");FailOpen(r.plan);
        }
        return false;
    }
    internal static bool Blocked(Receipt r,IntPtr attr){
        if(r==null || !Valid(r.plan) || EnemyGroggy.IsStunned(r.plan.attack.debate,attr))return false;
        if(!r.plan.aggregate)return r.step.blocked.Contains(attr);
        foreach(var s in r.plan.steps)if(!s.blocked.Contains(attr))return false;
        return r.plan.steps.Count>0;
    }
    static int Scale(IntPtr attribute,int total,bool debate)
    {
        var r=running;if(total<=0 || r==null || r.plan.attack.debate!=debate || !r.plan.attack.Valid() || r.plan.abandoned)return total;
        if(EnemyGroggy.IsStunned(debate,attribute))return total;
        var p=r.plan;
        if(!p.aggregate)return r.step.blocked.Contains(attribute)?0:total;
        int count=p.steps.Count;if(count==0)return total;
        int share=total/count,remainder=total%count;long blocked=0;
        // Bounded to 128 contacts; no allocation, callback or delay in damage math.
        for(int i=0;i<count;i++)if(p.steps[i].blocked.Contains(attribute))blocked+=share+(i<remainder?1:0);
        return total-(int)blocked;
    }
    static void FightDamage(NFIDHHGKMDC __instance,ref int __0){__0=Scale(__instance.Pointer,__0,false);}
    static void DebateDamage(LEDJAIBAAJC __instance,ref int __0){__0=Scale(__instance.Pointer,__0,true);}
    internal static void FailOpen(Plan p)
    {
        bool owns=p.attack.Valid();
        p.abandoned=true;p.pending.Clear();p.activeStep=null;p.busy=false;p.attack.done=true;
        foreach(var s in p.steps)if(s.queued)s.done=true;
        // Faults release only the optional gate. They never clear native events,
        // discard a card, invoke AfterAttack, or force the actor's turn to end.
        if(owns)DuelCounters.Release();
    }
    internal static void Shift(double gap){foreach(var r in receipts.Values)r.createdAt+=gap;}
    internal static void Reset(){LateParry.Reset();ContactEffects.Reset();receipts.Clear();running=null;nativeDamageOwner=null;}
}
