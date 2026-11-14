using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vow.Counters;

// Pending outcomes, not animation waits. Damage iterators retain their original
// owner until these short, bounded decisions have settled. No HP rollback.
internal static class LateParry
{
    sealed class Pending {
        internal PerHitParry.Plan plan; internal PerHitParry.Step step;
        internal IntPtr attribute; internal Action success; internal Action pose;
        internal double contact,deadline,actualContact;internal bool cueMode;
    }
    static readonly List<Pending> pending=new();
    static bool updating;
    internal static void Add(PerHitParry.Plan p,PerHitParry.Step s,IntPtr attr,Action pose,Action success){
        if(EnemyGroggy.IsStunned(p.attack.debate,attr) || !RealtimeParry.Allowed || (!s.cueBound && DuelCounters.PostWindow<=DuelCounters.TimingAdvance))return;
        if(pending.Count>=128){DuelCounters.Warn("사후 패링 대기 상한 · 원래 타격 유지");return;}
        double now=Time.unscaledTimeAsDouble;
        double begin=s.cueBound?s.cueStart:now-DuelCounters.TimingAdvance;
        double end=s.cueBound?RealtimeParry.DefenderDeadline(p.attack,attr,s.cueStart,s.cueDeadline):now+DuelCounters.PostWindow-DuelCounters.TimingAdvance;
        if(end<now)return;
        pending.Add(new Pending{cueMode=s.cueBound,actualContact=now,plan=p,step=s,attribute=attr,pose=pose,success=success,contact=begin,deadline=end});
        s.done=false;s.latePending++;
    }
    internal static bool Target(ImpactTiming.Attack a,out IntPtr attr,out Action pose){
        foreach(var x in pending)if(x.plan.attack==a && PerHitParry.Valid(x.plan) && Time.unscaledTimeAsDouble<=x.deadline){attr=x.attribute;pose=x.pose;return true;}
        attr=IntPtr.Zero;pose=null;return false;
    }
    internal static void Collect(ImpactTiming.Attack a,Dictionary<IntPtr,Action> targets){
        foreach(var x in pending)if(x.plan.attack==a && PerHitParry.Valid(x.plan) && Time.unscaledTimeAsDouble<=x.deadline)
            targets[x.attribute]=x.pose;
    }
    internal static bool Busy(ImpactTiming.Attack a){
        if(!DuelCounters.NoDelay)return false;
        foreach(var x in pending)if(x.plan.attack==a && PerHitParry.Valid(x.plan))return true;
        return false;
    }
    internal static void Tick(){
        if(updating || !Application.isFocused || Time.timeScale<=0)return;updating=true;
        try{
            RealtimeParry.PollInput();
            for(int i=0;i<pending.Count;){
                var x=pending[i];bool valid=PerHitParry.Valid(x.plan)&&PlayerBattleRules.ManualCombat(x.plan.attack.debate)&&!EnemyGroggy.IsStunned(x.plan.attack.debate,x.attribute);
                bool won=valid && (x.cueMode?RealtimeParry.MatchesCue(x.plan.attack,x.attribute,x.contact,x.deadline,x.actualContact):RealtimeParry.Matches(x.plan.attack,x.attribute,x.contact,x.deadline));
                if(valid && !won && Time.unscaledTimeAsDouble<=x.deadline){i++;continue;}
                pending.RemoveAt(i);
                RealtimeParry.Audit(x.plan.attack,"late-final",$"step={x.step.index} manual={won} valid={valid} deadline={x.deadline:F4} victim={x.attribute}");
                try{if(won){RealtimeParry.Success(x.plan.attack,x.attribute);x.success();}}
                catch(Exception e){DuelCounters.Warn("사후 패링 판정 복구: "+e.Message);}
                finally{x.step.latePending--;if(x.step.latePending==0)x.step.done=true;}
            }
        }finally{updating=false;}
    }
    internal static void Shift(double gap){foreach(var x in pending){x.contact+=gap;x.deadline+=gap;x.actualContact+=gap;}}
    internal static void Reset(){foreach(var x in pending){x.step.latePending=0;x.step.done=true;}pending.Clear();updating=false;}
}
