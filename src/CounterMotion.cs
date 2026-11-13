using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Vow.Counters;

// A counter owns displacement for 0.30 seconds. Never Warp every frame while
// the native agent/waypoint mover/root motion still owns the same transform.
internal static class CounterMotion
{
    sealed class Motion
    {
        internal GKHLLOOEEFN actor;
        internal Transform root;
        internal NavMeshAgent agent;
        internal NavMeshObstacle obstacle;
        internal Animator animator;
        internal bool agentEnabled,obstacleEnabled,rootMotion;
        internal Vector3 origin,dest;
        internal float elapsed;
    }
    static readonly Dictionary<IntPtr,Motion> active=new();
    static readonly HashSet<IntPtr> displaced=new();
    static IntPtr watched;
    static int watchedOrder,watchedRound;
    static IntPtr watchedCard;
    static float bestDistance,idleSeconds;
    internal static bool Owns(GKHLLOOEEFN hero)=>hero!=null && active.ContainsKey(hero.Pointer);
    internal static bool AllowMove(NPCCtrl __instance)=>__instance==null || !active.ContainsKey(__instance.Pointer);
    static bool Usable(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    static void Attempt(Action action){try{action();}catch(Exception e){DuelCounters.Warn("패링 이동 정리: "+e.Message);}}
    internal static void Stop(GKHLLOOEEFN hero){
        if(hero==null)return;
        Attempt(()=>{var npc=hero.TryCast<NPCCtrl>();if(npc!=null)npc.m_listMovePos?.Clear();});
        Attempt(()=>{var agent=hero.GetNavAgent();
            if(agent && agent.enabled && agent.isOnNavMesh){agent.ResetPath();agent.velocity=Vector3.zero;}});
    }
    internal static void Begin(GKHLLOOEEFN hero,GKHLLOOEEFN defender){
        Complete(hero);
        AnimalDodgeSync.Cancel(hero);
        var root=hero?.GetTransform();var other=defender?.GetTransform();if(!root||!other)return;
        var m=new Motion{actor=hero,root=root,origin=root.position,agent=hero.GetNavAgent(),obstacle=hero.GetNavObstacle(),animator=hero.GetAnimator()};
        if(!Usable(m.origin))return;
        m.agentEnabled=m.agent && m.agent.enabled;m.obstacleEnabled=m.obstacle && m.obstacle.enabled;m.rootMotion=m.animator && m.animator.applyRootMotion;
        active[hero.Pointer]=m;displaced.Add(hero.Pointer);
        try{
            Stop(hero);
            if(m.agent)m.agent.enabled=false;
            if(m.obstacle)m.obstacle.enabled=false;
            if(m.animator)m.animator.applyRootMotion=false;
            // One native reset clears internal movement flags as well as NPC waypoints.
            hero.SetPosition(m.origin,false,false,false);
            m.dest=Destination(m,other.position);
        }catch{Complete(hero);throw;}
    }
    static Vector3 Destination(Motion m,Vector3 defender){
        var away=m.origin-defender;away.y=0;
        if(away.sqrMagnitude<.0001f){away=-m.root.forward;away.y=0;}
        if(away.sqrMagnitude<.0001f)return m.origin;
        away=away.normalized;int mask=m.agent?m.agent.areaMask:NavMesh.AllAreas;
        float radius=m.agent?Mathf.Max(.25f,m.agent.radius):.5f;
        if(!NavMesh.SamplePosition(m.origin,out var start,Mathf.Min(1.2f,radius+.35f),mask) || Mathf.Abs(start.position.y-m.origin.y)>.45f)return m.origin;
        foreach(float distance in new[]{1.55f,1f,.5f}){
            if(!NavMesh.SamplePosition(m.origin+away*distance,out var ground,.25f,mask))continue;
            var dest=ground.position;var offset=dest-m.origin;
            if(!Usable(dest)||Mathf.Abs(offset.y)>.45f||offset.magnitude>1.8f||Vector3.Dot(offset,away)<.2f)continue;
            if(NavMesh.Raycast(start.position,dest,out _,mask))continue;
            bool free=true;
            if(Fi_Controller.m_listHero!=null)foreach(var h in Fi_Controller.m_listHero){
                if(h==null||h.Pointer==m.actor.Pointer)continue;
                var t=h.GetTransform();if(!t)continue;
                var a=h.GetNavAgent();float separation=radius+(a?Mathf.Max(.25f,a.radius):.5f)+.15f;
                var delta=dest-t.position;delta.y=0;if(delta.sqrMagnitude<separation*separation){free=false;break;}
            }
            if(free)return dest;
        }
        // A wall, occupied landing point or missing mesh means an in-place recoil.
        // Never force a transform outside the reachable combat floor.
        return m.origin;
    }
    internal static void Tick(){
        if(active.Count==0)return;
        foreach(var m in new List<Motion>(active.Values)){
            if(!m.root||!Fi_Controller.m_isInBattle||Fi_Controller.m_isEnd){Complete(m.actor);continue;}
            if(!Application.isFocused||Time.timeScale<=0)continue;
            try{
                m.elapsed+=Time.unscaledDeltaTime;float t=Mathf.Min(1,m.elapsed/.30f);
                m.root.position=Vector3.Lerp(m.origin,m.dest,1-(1-t)*(1-t));
                if(t>=1)Complete(m.actor);
            }catch(Exception e){DuelCounters.Warn("패링 넉백 복구: "+e.Message);Complete(m.actor);}
        }
    }
    internal static void Complete(GKHLLOOEEFN hero){
        if(hero==null||!active.TryGetValue(hero.Pointer,out var m))return;
        active.Remove(hero.Pointer);
        // Restore each owner independently, even if one destroyed Unity object throws.
        // Transform interpolation did not update the native waypoint/rotation
        // state. Clear it at the LANDING point before returning ownership.
        Attempt(()=>{if(m.root)m.actor.SetPosition(m.root.position,false,false,false);});
        Attempt(()=>{if(m.root && m.agent){
            m.agent.enabled=m.agentEnabled;
            if(m.agentEnabled){
                // The obstacle is still disabled here: do not carve a hole under Warp.
                if(!m.agent.Warp(m.root.position)){m.root.position=m.origin;m.agent.Warp(m.origin);}
                if(m.agent.isOnNavMesh){m.agent.ResetPath();m.agent.velocity=Vector3.zero;}
            }
        }});
        Attempt(()=>{if(m.obstacle)m.obstacle.enabled=m.obstacleEnabled&&!m.agentEnabled;});
        Attempt(()=>{if(m.animator)m.animator.applyRootMotion=m.rootMotion;});
    }
    internal static bool Approach(Fi_Controller fight){
        var hero=Fi_Controller.m_curHero;
        if(hero==null || Fi_Controller.m_cardAfterMove==null ||
            (hero.GetAttribute().IBLKPJDLABN==FMHEOPMKFJB.MySide&&!Fi_Controller.m_autoBattle) || Fi_Controller.m_isSpecialAttack ||
            (Fi_Controller.m_curPhase!=Fi_Controller.NHCOMCJKABH.move && Fi_Controller.m_curPhase!=Fi_Controller.NHCOMCJKABH.additionMove)){
            watched=IntPtr.Zero;idleSeconds=0;return true;
        }
        if(Owns(hero))return false;
        if(!Application.isFocused||Time.timeScale<=0||Time.unscaledDeltaTime>1 ||
           UI_BattlePreviewUI.m_isOpen||FICFPEHAJNJ.ODIOKGGBKEI||(FICFPEHAJNJ.GGHHDIFNHOB?.Count??0)>0)return true;
        float distance=Vector3.Distance(hero.GetTransform().position,Fi_Controller.m_moveDes);
        if(watched!=hero.Pointer||watchedOrder!=fight.m_curOrder||watchedRound!=fight.AHDOJFHBBFJ||watchedCard!=Fi_Controller.m_cardAfterMove.Pointer){watched=hero.Pointer;watchedOrder=fight.m_curOrder;watchedRound=fight.AHDOJFHBBFJ;watchedCard=Fi_Controller.m_cardAfterMove.Pointer;bestDistance=distance;idleSeconds=0;}
        else if(distance<bestDistance-.10f){bestDistance=distance;idleSeconds=0;}
        else idleSeconds+=Time.unscaledDeltaTime;
        if(idleSeconds<3)return true;
        // Last resort only for a stalled automatic attack approach.
        // Walking normally keeps improving distance and never trips this guard.
        watched=IntPtr.Zero;idleSeconds=0;
        DuelCounters.StabilityNote($"Blocked automatic approach: actor={hero.Pointer} round={fight.AHDOJFHBBFJ} order={fight.m_curOrder} card={Fi_Controller.m_cardAfterMove?.OJPECMNINNA} distance={distance:F3} displaced={displaced.Contains(hero.Pointer)}");
        // Let the native mover clear its flags too, not just NavMesh's path.
        hero.SetPosition(hero.GetTransform().position,false,false,false);
        ImpactTiming.AbortApproach(fight);return false;
    }
    internal static void Reset(){foreach(var m in new List<Motion>(active.Values))Complete(m.actor);watched=IntPtr.Zero;idleSeconds=0;}
    internal static void ClearBattle(){Reset();displaced.Clear();}
}
