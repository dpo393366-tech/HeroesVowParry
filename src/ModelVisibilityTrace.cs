using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Vow.Counters;

// Read-only evidence. Inactive parts, off-camera objects and dying actors can be
// intentional. Never force them visible or alter bones/materials to mask a symptom.
internal static class ModelVisibilityTrace
{
#if DuelCounters
    static double next,budgetAt;
    static IntPtr battle;
    static int writes,cursor;
    sealed class Sample {internal int flags=-1,total,enabled,forced,visible,active;internal Vector3 center,offset;internal double logged;}
    static readonly Dictionary<IntPtr,Sample> samples=new();
    internal static void Tick(){
        if(!DuelCounters.VisibilityTrace || !Fi_Controller.m_isInBattle || Fi_Controller.m_isEnd){samples.Clear();battle=IntPtr.Zero;return;}
        double now=Time.unscaledTimeAsDouble;if(now<next || !Application.isFocused || Time.timeScale<=0)return;next=now+.25;
        var fight=Fi_Controller.EBACHLGLNGD;if(!fight)return;
        if(battle!=fight.Pointer){battle=fight.Pointer;samples.Clear();writes=0;budgetAt=now;}
        if(now-budgetAt>=60){writes=0;budgetAt=now;}
        if(writes>=128)return;
        try{
            var actors=Fi_Controller.m_listHero;if(actors==null)return;
            // Spread read-only diagnostics across frames instead of rescanning
            // every renderer of every actor in one burst four times per second.
            int count=Math.Min(128,actors.Count);if(count==0)return;
            for(int i=0;i<Math.Min(2,count);i++){if(cursor>=count)cursor=0;Observe(actors[cursor++],fight,now);}
        }catch(Exception e){Write("sample-failed "+e.GetType().Name+": "+e.Message);}
    }
    static void Observe(GKHLLOOEEFN actor,Fi_Controller fight,double now){
        if(actor==null)return;var root=actor.GetTransform();if(!root)return;
        if(!samples.TryGetValue(actor.Pointer,out var sample)){sample=new();samples[actor.Pointer]=sample;}
        var animal=actor.TryCast<AnimalCtrl>();var animalRenderers=animal?.m_listRenderer;
        var renderers=actor.GetListRenderer();int total=0,enabled=0,forced=0,visible=0,active=0;Vector3 center=root.position,extent=Vector3.zero;
        void Add(Renderer r){
            if(!r)return;total++;if(r.enabled)enabled++;if(r.forceRenderingOff)forced++;if(r.isVisible)visible++;if(r.gameObject.activeInHierarchy)active++;
            var bounds=r.bounds;if(total==1||bounds.extents.sqrMagnitude>extent.sqrMagnitude){center=bounds.center;extent=bounds.extents;}
        }
        if(animalRenderers!=null&&animalRenderers.Count>0){for(int i=0;i<Math.Min(64,animalRenderers.Count);i++)Add(animalRenderers[i]);}
        else if(renderers!=null)for(int i=0;i<Math.Min(64,renderers.Count);i++)Add(renderers[i]);
        var scale=root.lossyScale;bool tiny=scale.sqrMagnitude<.000001f,far=(center-root.position).sqrMagnitude>100;
        int flags=(root.gameObject.activeInHierarchy?1:0)|(total>0?2:0)|(enabled>0?4:0)|(forced==total&&total>0?8:0)|(visible>0?16:0)|(active>0?32:0)|(tiny?64:0)|(far?128:0);
        bool changed=flags!=sample.flags || total!=sample.total || enabled!=sample.enabled || forced!=sample.forced || visible!=sample.visible || active!=sample.active || (center-sample.center).sqrMagnitude>100 || (center-root.position-sample.offset).sqrMagnitude>4;
        if(!changed || now-sample.logged<.5)return;
        sample.flags=flags;sample.total=total;sample.enabled=enabled;sample.forced=forced;sample.visible=visible;sample.active=active;sample.center=center;sample.offset=center-root.position;sample.logged=now;
        var animator=actor.GetAnimator();string state="none";
        if(animator && animator.layerCount>0){var a=animator.GetCurrentAnimatorStateInfo(0);state=$"{a.fullPathHash}/{a.normalizedTime:F3} next={(animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0).fullPathHash:0)} speed={animator.speed} enabled={animator.enabled} cull={animator.cullingMode}";}
        var attr=actor.GetAttribute();
        Write($"actor={actor.Pointer} name={actor.GetName()} hp={attr?.MKEIPLCMANH} side={attr?.IBLKPJDLABN} turn={fight.AHDOJFHBBFJ}/{fight.m_curOrder} current={Fi_Controller.m_curHero?.Pointer} phase={Fi_Controller.m_curPhase} launching={Fi_Controller.m_isLaunchingAttack} special={Fi_Controller.m_isSpecialAttack} flags={flags} renderers={total} enabled={enabled} forcedOff={forced} active={active} visible={visible} root={root.position} local={root.localPosition} scale={scale} bodyCenter={center} extent={extent} animalDodge={animal?.EDJIDCFHLOP} dodgeHome={animal?.OBGCHDBMLBJ} dodgeDest={animal?.HPNHCDEJGKK} animation={state}");
    }
    static void Write(string line){
        if(writes++>=128)return;
        try{var path=Path.Combine(BepInEx.Paths.BepInExRootPath,"DuelCounters-visibility.log");CombatLog.Write(path,262144,DateTime.Now.ToString("s")+" "+line+Environment.NewLine);}catch{}
    }
#else
    internal static void Tick(){}
#endif
}
