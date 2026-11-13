using System;
using System.Collections.Generic;
using UnityEngine;
#if DuelCounters
using HarmonyLib;
using UnityEngine.AI;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
#endif
namespace Vow.Counters;

// AnimalCtrl's native dodge owns two DOMove tweens and five delayed Invoke
// callbacks. SetPosition/StopAllCoroutines do NOT cancel those owners.
internal static class AnimalDodgeSync
{
#if DuelCounters
    sealed class Motion {
        internal AnimalCtrl actor; internal Transform root; internal Vector3 origin;
        internal NavMeshAgent agent; internal Animator animator;
        internal bool updatePosition,updateRotation,stopped,rootMotion;
        internal float speed,remaining; internal Tween tween;
    }
    static readonly Dictionary<IntPtr,Motion> active=new();
    static readonly string[] callbacks={"EILMLCEIOIE","LKGKEGKCOLE","GMCBAHMAMPK","PJPIFIHGPNE","JMLNJGBGOPG"};
    static Motion capturing;
    static int notes;
    static bool installed;
    static bool Battle=>Fi_Controller.m_isInBattle&&!Fi_Controller.m_isEnd;
    internal static void Install(Harmony h){
        try{
        h.Patch(AccessTools.DeclaredMethod(typeof(AnimalCtrl),"StartDodge"),prefix:new HarmonyMethod(typeof(AnimalDodgeSync),nameof(Start)),finalizer:new HarmonyMethod(typeof(AnimalDodgeSync),nameof(StartFailed)));
        foreach(string name in callbacks){
            h.Patch(AccessTools.DeclaredMethod(typeof(AnimalCtrl),name),prefix:new HarmonyMethod(typeof(AnimalDodgeSync),nameof(Stage)),finalizer:new HarmonyMethod(typeof(AnimalDodgeSync),nameof(StageDone)));
        }
        h.Patch(AccessTools.DeclaredMethod(typeof(AnimalCtrl),"JMLNJGBGOPG"),postfix:new HarmonyMethod(typeof(AnimalDodgeSync),nameof(Ended)));
        h.Patch(AccessTools.DeclaredMethod(typeof(ShortcutExtensions),"DOMove",new[]{typeof(Transform),typeof(Vector3),typeof(float),typeof(bool)}),postfix:new HarmonyMethod(typeof(AnimalDodgeSync),nameof(CaptureTween)));
        foreach(string name in new[]{"SetPosition","Recycle","PlayDeathAnimation"})
            h.Patch(AccessTools.DeclaredMethod(typeof(AnimalCtrl),name),prefix:new HarmonyMethod(typeof(AnimalDodgeSync),nameof(Interrupt)));
        installed=true;
        DuelCounters.StabilityNote("animal-dodge hooks installed: native callback chain + owned DOMove capture");
        }catch(Exception e){installed=false;DuelCounters.Warn("짐승 회피 보정 연결 실패 (원래 회피 유지): "+e.Message);}
    }
    static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    static void Note(Motion m,string reason){
        if(notes++>=128)return;
        DuelCounters.StabilityNote($"animal-dodge {reason} actor={m.actor.Pointer} root={m.root?.position} home={m.origin} nativeHome={m.actor.OBGCHDBMLBJ} destination={m.actor.HPNHCDEJGKK} nav={(m.agent&&m.agent.enabled&&m.agent.isOnNavMesh?m.agent.nextPosition:Vector3.zero)}");
    }
    static void Try(Action a){try{a();}catch(Exception e){DuelCounters.Warn("짐승 회피 위치 정리: "+e.Message);}}
    static void Kill(Motion m){var tween=m.tween;m.tween=null;if(tween!=null&&TweenExtensions.IsActive(tween))TweenExtensions.Kill(tween,false);}
    static bool Start(AnimalCtrl __instance,ref Vector3 __1,float __2,float __3,float __4){
        if(!installed||!Battle||!__instance)return true;
        // A parry recoil already owns position. Retain the dodge result but do
        // not launch a second physical displacement against that owner.
        if(CounterMotion.Owns(__instance.Cast<GKHLLOOEEFN>()))return false;
        Finish(__instance,"replaced");
        var root=__instance.GetTransform();if(!root||!Finite(root.position))return true;
        var m=new Motion{actor=__instance,root=root,origin=root.position,agent=__instance.GetNavAgent(),animator=__instance.GetAnimator()};
        m.remaining=Mathf.Clamp((float.IsFinite(__2)?Mathf.Max(0,__2):0)+(float.IsFinite(__3)?Mathf.Max(0,__3):0)+(float.IsFinite(__4)?Mathf.Max(0,__4):0)+1,1,30);
        active[__instance.Pointer]=m;
        try{
            if(m.agent){m.updatePosition=m.agent.updatePosition;m.updateRotation=m.agent.updateRotation;
                if(m.agent.enabled&&m.agent.isOnNavMesh){m.stopped=m.agent.isStopped;m.agent.isStopped=true;}
                m.agent.updatePosition=false;m.agent.updateRotation=false;}
            if(m.animator){m.rootMotion=m.animator.applyRootMotion;m.speed=m.animator.speed;m.animator.applyRootMotion=false;}
            // Save home before native StartDodge: repeats must never accumulate
            // an intermediate sidestep as the next origin.
            __instance.EDJIDCFHLOP=false;__instance.OBGCHDBMLBJ=m.origin;
            var delta=__1-m.origin;delta.y=0;
            float limit=Mathf.Clamp(m.agent?m.agent.radius*2:1.5f,1,2.5f);
            if(!Finite(__1))__1=m.origin;
            else if(delta.magnitude>limit)__1=m.origin+delta.normalized*limit;
            int mask=m.agent?m.agent.areaMask:NavMesh.AllAreas;
            if(NavMesh.SamplePosition(m.origin,out var home,.5f,mask)&&NavMesh.SamplePosition(__1,out var land,.3f,mask)&&Mathf.Abs(land.position.y-m.origin.y)<.5f&&!NavMesh.Raycast(home.position,land.position,out _,mask))__1=land.position;
            else __1=m.origin;
            Note(m,"begin");
        }catch{Finish(__instance,"start-error");throw;}
        return true;
    }
    static Exception StartFailed(AnimalCtrl __instance,Exception __exception){if(__exception!=null)Finish(__instance,"native-start-error");return __exception;}
    static bool Stage(AnimalCtrl __instance,out Motion __state){
        __state=capturing;
        if(!installed)return true;
        if(!active.TryGetValue(__instance.Pointer,out var m))return !Battle;
        if(!Battle){Finish(__instance,"late-exit-callback");return false;}
        capturing=m;return true;
    }
    static Exception StageDone(Motion __state,Exception __exception){var m=capturing;capturing=__state;if(__exception!=null&&m!=null)Finish(m.actor,"native-stage-error");return __exception;}
    static void CaptureTween(Transform __0,TweenerCore<Vector3,Vector3,VectorOptions> __result){
        var m=capturing;if(m==null||__result==null||!m.root||__0.Pointer!=m.root.Pointer)return;
        Kill(m);__result.isRecyclable=false;m.tween=__result;
    }
    static void Ended(AnimalCtrl __instance)=>Finish(__instance,"complete");
    static void Interrupt(AnimalCtrl __instance)=>Finish(__instance,"position/death/recycle");
    static void Finish(AnimalCtrl actor,string reason){
        if(actor==null||!active.TryGetValue(actor.Pointer,out var m))return;
        active.Remove(actor.Pointer);
        Try(()=>Note(m,reason));
        foreach(string callback in callbacks)Try(()=>{if(actor)actor.CancelInvoke(callback);});
        Try(()=>Kill(m));
        Try(()=>{if(actor){actor.EDJIDCFHLOP=false;actor.OBGCHDBMLBJ=m.origin;actor.HPNHCDEJGKK=m.origin;}});
        // Only the captured dodge root, never skeleton bones or other actors.
        Try(()=>{if(m.root)m.root.position=m.origin;});
        Try(()=>{if(m.agent&&m.agent.enabled&&m.agent.isOnNavMesh){bool ok=m.agent.Warp(m.origin);if(!ok)Note(m,"nav-warp-failed");m.agent.isStopped=m.stopped;}});
        Try(()=>{if(m.agent){m.agent.updatePosition=m.updatePosition;m.agent.updateRotation=m.updateRotation;}});
        Try(()=>{if(m.animator){m.animator.applyRootMotion=m.rootMotion;m.animator.speed=m.speed;}});
    }
    internal static void Cancel(GKHLLOOEEFN hero){var animal=hero?.TryCast<AnimalCtrl>();if(animal!=null)Finish(animal,"parry-takeover");}
    internal static void Tick(){
        if(active.Count==0)return;
        foreach(var m in new List<Motion>(active.Values)){
            if(!m.actor||!m.root||!Battle){Finish(m.actor,"battle-exit");continue;}
            if(!Application.isFocused||Time.timeScale<=0)continue;
            m.remaining-=Time.deltaTime;
            if(m.remaining<=0||!m.actor.EDJIDCFHLOP)Finish(m.actor,"completion-guard");
        }
    }
    internal static void Reset(){foreach(var m in new List<Motion>(active.Values))Finish(m.actor,"reset");capturing=null;notes=0;}
#else
    internal static void Install(HarmonyLib.Harmony h){}
    internal static void Cancel(GKHLLOOEEFN hero){}
    internal static void Tick(){}
    internal static void Reset(){}
#endif
}

