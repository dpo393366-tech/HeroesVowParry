using System;
using UnityEngine;

namespace Vow.Counters;

// The native controller reads the CURRENT Attack1/2 clock even during CrossFade.
// A new card can reuse Attack1 while the old Attack1 is still blending out.
// Keep the new event list pending until Unity has evaluated the new state.
// No Animator.Update/Play, event-time edits, coroutine waits or damage replay.
internal static class AttackClipClock
{
    internal sealed class State
    {
        internal int index,loadedFrame,checkedFrame=-1;
        internal double elapsed;
        internal bool ready,allowed,logged;
    }
    internal static void Loaded(ImpactTiming.Attack a){
        if(a==null||a.debate)return;
        a.clipClock=new State{index=a.fight.m_curAnimationIndex,loadedFrame=Time.frameCount};
    }
    internal static bool AllowNative(ImpactTiming.Attack a){
        var c=a?.clipClock;
        if(c==null||c.ready||a.debate||!a.quickBasic||a.quickContactPassed||
           !DuelCounters.NoDelay||!DuelCounters.QuickBasic||!a.Valid()||a.cancelled||a.motionFinished)return true;
        if(c.index!=a.fight.m_curAnimationIndex || c.index!=a.quickContactAnimation)return true;
        if(c.checkedFrame==Time.frameCount)return c.allowed;
        c.checkedFrame=Time.frameCount;
        if(Time.timeScale<=0)return c.allowed=false;
        // Bound even a broken/custom controller. Paused frames do not expire it.
        c.elapsed+=Math.Min(.25,Math.Max(0,Time.unscaledDeltaTime));
        if(c.elapsed>=2){c.ready=true;RealtimeParry.Audit(a,"clip-clock-fallback","bounded native resume");return c.allowed=true;}
        try{
            var animator=a.fighter.GetAnimator();
            if(!animator||!animator.isActiveAndEnabled){c.ready=true;return c.allowed=true;}
            string expected=(c.index&1)==0?"Attack1":"Attack2";
            bool matches=false,transition=false;
            for(int i=0;i<Math.Min(8,animator.layerCount);i++){
                var state=animator.GetCurrentAnimatorStateInfo(i);
                bool blending=animator.IsInTransition(i);
                bool next=blending && animator.GetNextAnimatorStateInfo(i).IsName(expected);
                if(state.IsName(expected)||next){matches=true;transition|=blending;}
            }
            // Native LoadAttackAnim may run AFTER this frame's Animator update.
            // Two frame boundaries prevent accepting the unchanged old state.
            c.allowed=Ready(Time.frameCount-c.loadedFrame,matches,transition);
            if(c.allowed){c.ready=true;RealtimeParry.Audit(a,"clip-clock-ready",$"index={c.index} elapsed={c.elapsed:F4}");}
            else if(!c.logged){c.logged=true;RealtimeParry.Audit(a,"clip-clock-pending",$"index={c.index} matching={matches} transition={transition}");}
            return c.allowed;
        }catch(Exception e){c.ready=true;DuelCounters.Warn("공격 동작 시계 확인: "+e.Message);return c.allowed=true;}
    }
    internal static bool Ready(int frames,bool matches,bool transition)=>frames>=2 && matches && !transition;
    // Native HIECIGKMAFE uses modes 0/1 to move the target toward the attacker;
    // 2/3 push it away, 4 launches it. A non-damaging setup pull is not a strike.
    internal static bool Contact(bool damage,bool reaction,bool movement,int mode)=>
        damage || (reaction && !(movement && (mode==0||mode==1)));
}
