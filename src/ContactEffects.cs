using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using NativeRoutine=Il2CppSystem.Collections.IEnumerator;

namespace Vow.Counters;

// Victim reactions are separate native calls from HP damage. Keep attacker
// animation/VFX intact; gate only the affected recipient's hostile effects.
internal static class ContactEffects
{
    internal sealed class Context { internal PerHitParry.Plan plan;internal PerHitParry.Step step;internal Context outer;internal bool scope; }
    sealed class Effect { internal Context contact;internal IntPtr attr;internal Action apply; }
    static Context current;
    static bool replaying;
    static readonly List<Effect> effects=new();
    internal static void Install(Harmony h){
        Hook(h,typeof(Fi_Controller),"Fly",nameof(Fly),new[]{typeof(GKHLLOOEEFN),typeof(float)});
        Hook(h,typeof(Fi_Controller),"PushAndPull",nameof(Push),new[]{typeof(GKHLLOOEEFN),typeof(bool),typeof(float),typeof(float)});
        Hook(h,typeof(Fi_Controller),"PlayBeHitAnimation",nameof(Hurt));
        Hook(h,typeof(NFIDHHGKMDC),"MHJJJAACBFG",nameof(FightStatus));
        Hook(h,typeof(NFIDHHGKMDC),"GGFCMDNNELG",nameof(FightStatus));
        Hook(h,typeof(LEDJAIBAAJC),"MHJJJAACBFG",nameof(DebateStatus));
        Hook(h,typeof(LEDJAIBAAJC),"GGFCMDNNELG",nameof(DebateStatus));
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"GOGFJJDCOOF"),postfix:new HarmonyMethod(typeof(ContactEffects),nameof(DamageDone)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"TriggerBuff"),postfix:new HarmonyMethod(typeof(ContactEffects),nameof(BuffRoutine)));
        h.Patch(AccessTools.DeclaredMethod(typeof(De_Controller),"LPCFCBBBIOH"),postfix:new HarmonyMethod(typeof(ContactEffects),nameof(BuffRoutine)));
    }
    static void Hook(Harmony h,Type t,string method,string patch,Type[] args=null)=>h.Patch(AccessTools.DeclaredMethod(t,method,args),prefix:new HarmonyMethod(typeof(ContactEffects),patch));
    internal static Context Begin(){var state=new Context{scope=true,outer=current};current=null;return state;}
    internal static void Set(PerHitParry.Plan p,PerHitParry.Step s){current=new Context{plan=p,step=s};}
    internal static void Nearest(PerHitParry.Plan p,int animation,float time){
        // Authored CC-only events may precede the contact. Bind them to the
        // nearest contact in this animation, without opening that contact early.
        PerHitParry.Step best=null;float distance=float.MaxValue;
        foreach(var s in p.steps)if(s.animation==animation && Math.Abs(s.time-time)<distance){best=s;distance=Math.Abs(s.time-time);}
        // Han Tian Bi (1002902): clip 0 launches, clip 1 deals damage,
        // clip 2 launches again. Cosmetic clips have no step of their own.
        // Bind a lead-in to the next actual contact, or a tail to the last one.
        // Never fabricate a hit or alter the attacker's authored timeline.
        if(best==null){
            foreach(var s in p.steps)if(!s.nativeOnly && s.animation>animation){best=s;break;}
            if(best==null)for(int i=p.steps.Count-1;i>=0;i--)if(!p.steps[i].nativeOnly && p.steps[i].animation<animation){best=p.steps[i];break;}
        }
        if(best!=null)Set(p,best);
    }
    internal static Exception EventEnd(Context __state,Exception __exception){if(__state?.scope==true)current=__state.outer;return __exception;}
    static bool Gate(GKHLLOOEEFN target,Action apply){
        var c=current;
        if(c==null && PerHitParry.running!=null)c=new Context{plan=PerHitParry.running.plan,step=PerHitParry.running.step};
        if(replaying || !DuelCounters.NoDelay || c==null || target==null || !PerHitParry.Valid(c.plan))return true;
        var actor=c.plan.attack.fighter;
        if(actor==null || !DuelCounters.Opposed(actor.GetAttribute().IBLKPJDLABN,target.GetAttribute().IBLKPJDLABN))return true;
        var attr=target.GetAttribute().Pointer;
        if(c.step.blocked.Contains(attr))return false;
        if(c.step.done && c.step.damaged.Contains(attr))return true;
        if(effects.Count>=2048){DuelCounters.Warn("피격 연출 보류 상한 · 추가 연출 생략");return false;}
        effects.Add(new Effect{contact=c,attr=attr,apply=apply});return false;
    }
    static bool Fly(Fi_Controller __instance,GKHLLOOEEFN __0,float __1)=>Gate(__0,()=>__instance.Fly(__0,__1));
    static bool Push(Fi_Controller __instance,GKHLLOOEEFN __0,bool __1,float __2,float __3)=>Gate(__0,()=>__instance.PushAndPull(__0,__1,__2,__3));
    static bool Hurt(Fi_Controller __instance,GKHLLOOEEFN __0,string __1,float __2,int __3)=>Gate(__0,()=>__instance.PlayBeHitAnimation(__0,__1,__2,__3));
    static bool FightStatus(NFIDHHGKMDC __instance)=>!PerHitParry.Blocked(PerHitParry.running,__instance.Pointer);
    static bool DebateStatus(LEDJAIBAAJC __instance)=>!PerHitParry.Blocked(PerHitParry.running,__instance.Pointer);
    static void DamageDone(NFIDHHGKMDC __instance,int __0,int __result){
        var r=PerHitParry.running;
        if(r==null || !PerHitParry.Valid(r.plan) || __result<=0 || __0<=0)return;
        var p=r.plan;var attr=__instance.Pointer;
        if(p.aggregate){foreach(var s in p.steps)if(s.queued && !s.blocked.Contains(attr))s.damaged.Add(attr);}
        else if(!r.step.blocked.Contains(attr))r.step.damaged.Add(attr);
        Flush();
    }
    internal static void Flush(){
        if(replaying)return;
        for(int i=0;i<effects.Count;){
            var e=effects[i];var c=e.contact;
            bool valid=PerHitParry.Valid(c.plan);
            if(valid && !c.step.blocked.Contains(e.attr) && !c.step.damaged.Contains(e.attr)){i++;continue;}
            effects.RemoveAt(i);
            if(!valid || c.step.blocked.Contains(e.attr))continue;
            replaying=true;
            try{e.apply();}catch(Exception ex){DuelCounters.Warn("피격 연출 복구: "+ex.Message);}finally{replaying=false;}
        }
    }
    internal static void Finish(ImpactTiming.Attack a){effects.RemoveAll(x=>x.contact.plan.attack==a);}
    static void BuffRoutine(ref NativeRoutine __result){
        var r=PerHitParry.running;
        if(!DuelCounters.NoDelay || r==null || __result==null)return;
        __result=Scoped(__result,r).WrapToIl2Cpp();
    }
    static IEnumerator Scoped(NativeRoutine routine,PerHitParry.Receipt receipt){
        while(true){
            bool more;object value;var previous=PerHitParry.running;
            try{
                PerHitParry.running=PerHitParry.Valid(receipt.plan)?receipt:null;
                more=routine.MoveNext();value=more?routine.Current:null;
            }finally{PerHitParry.running=previous;}
            if(!more)yield break;
            if(value is NativeRoutine nested)yield return Scoped(nested,receipt).WrapToIl2Cpp();else yield return value;
        }
    }
    internal static void Reset(){effects.Clear();current=null;replaying=false;}
}
