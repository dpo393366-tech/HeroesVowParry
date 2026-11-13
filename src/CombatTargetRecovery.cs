using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using Fighters=Il2CppSystem.Collections.Generic.List<GKHLLOOEEFN>;
using Debaters=Il2CppSystem.Collections.Generic.List<NPCCtrl>;

namespace Vow.Counters;

internal static partial class CombatTargetRecovery
{
    [ThreadStatic] static IntPtr fightDecision,debateDecision;
    static int decisions,changes,empty,faults;
    internal static void Install(Harmony h){
            h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"GOCFBMPJJKE"),prefix:new HarmonyMethod(typeof(CombatTargetRecovery),nameof(FightBegin)),finalizer:new HarmonyMethod(typeof(CombatTargetRecovery),nameof(FightEnd)));
            h.Patch(AccessTools.DeclaredMethod(typeof(LEDJAIBAAJC),"GOCFBMPJJKE"),prefix:new HarmonyMethod(typeof(CombatTargetRecovery),nameof(DebateBegin)),finalizer:new HarmonyMethod(typeof(CombatTargetRecovery),nameof(DebateEnd)));
            Panel.Hook(h,typeof(NFIDHHGKMDC),"KONOJDMOGNE",typeof(CombatTargetRecovery),after:nameof(FightChoice));
            Panel.Hook(h,typeof(LEDJAIBAAJC),"KONOJDMOGNE",typeof(CombatTargetRecovery),after:nameof(DebateChoice));
            h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"ReceiveNPCMultipleAttack"),prefix:new HarmonyMethod(typeof(CombatTargetRecovery),nameof(AreaAttack)));
            foreach(string name in new[]{"HIECIGKMAFE","KBIBAIIJAAP","DHMMNDHAEFF"})
                h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),name),prefix:new HarmonyMethod(typeof(CombatTargetRecovery),nameof(LineContact)){priority=Priority.First});
            h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"GOGFJJDCOOF"),postfix:new HarmonyMethod(typeof(CombatTargetRecovery),nameof(LineDamage)));
    }
    static void FightBegin(NFIDHHGKMDC __instance,out IntPtr __state){
        __state=fightDecision;fightDecision=IntPtr.Zero;
        try{if(Fi_Controller.m_isInBattle && !Fi_Controller.m_isEnd && !Fi_Controller.m_isSpecialAttack && __instance.IBLKPJDLABN==FMHEOPMKFJB.Enemy && Fi_Controller.m_curHero?.Pointer==__instance.AFFCOKCHGKM?.Pointer)fightDecision=__instance.Pointer;}
        catch(Exception e){Fault(e);}
    }
    static Exception FightEnd(Exception __exception,IntPtr __state){fightDecision=__state;return __exception;}
    static void DebateBegin(LEDJAIBAAJC __instance,out IntPtr __state){
        __state=debateDecision;debateDecision=IntPtr.Zero;
        try{if(De_Controller.m_isInDebate && De_Controller.EBACHLGLNGD!=null && !De_Controller.EBACHLGLNGD.m_isEnd && __instance.IBLKPJDLABN==FMHEOPMKFJB.Enemy && De_Controller.m_curHero?.Pointer==__instance.AFFCOKCHGKM?.Pointer)debateDecision=__instance.Pointer;}
        catch(Exception e){Fault(e);}
    }
    static Exception DebateEnd(Exception __exception,IntPtr __state){debateDecision=__state;return __exception;}

    // Called only after the native selector has filled its legal card/target maps.
    // Fill a null selection only; a non-null native decision is always preserved.
    // Return the recovered card and targets to the SAME native caller;
    // never launch a second action, run a coroutine, or advance a phase here.
    static void FightChoice(NFIDHHGKMDC __instance,bool __0,bool __1,ref KJFOPOCBHBM __result){
        if(fightDecision!=__instance.Pointer || __0 || __1 || __result!=null)return;
        try{
            var a=__instance;var actor=a.AFFCOKCHGKM;var candidates=a.PDMICGPKJKB;
            if(actor==null || candidates==null)return;
            var original=__result;
            // Do not replace emergency native recovery with reckless offense.
            KJFOPOCBHBM chosen=null;Fighters targets=null;Vector3 point=default;float best=0;
            foreach(var row in candidates){
                var card=row.Key;var plan=row.Value;
                if(card==null||plan?.JLGPKHPMEFF==null||card.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack)continue;
                if(a.EFDDHIBLEHF?.Contains(card)!=true || a.HIMGMJJKFFL(card,false)>a.IHOPPBMEAEN || a.FMBHELDBDJP(card,false)>a.BCAIBCEODLJ)continue;
                if(!float.IsFinite(plan.DHLOPCACLKO)||plan.DHLOPCACLKO<0)continue;
                foreach(var group in plan.JLGPKHPMEFF){
                    if(!Finite(group.Key)||!Valid(group.Value,a.IBLKPJDLABN))continue;
                    // Native expected-damage scoring truncates small values to zero.
                    // Zero utility is not an illegal attack: keep a tiny floor only
                    // AFTER costs, native target groups, living/hostile checks pass.
                    float value=Math.Max(.01f,plan.DHLOPCACLKO)*GroupValue(group.Value,actor);
                    if(value>best){best=value;chosen=card;targets=group.Value;point=group.Key;}
                }
            }
            if(chosen==null && original==null)RepairAttack(a,ref chosen,ref targets,ref point,ref best);
            if(chosen==null && original==null)UsefulSupport(a,ref chosen,ref targets,ref point,ref best);
            bool replace=chosen!=null;
            if(replace && chosen!=null){
                // Allocate and validate before committing any native field.
                var copy=new Fighters();foreach(var t in targets)copy.Add(t);
                var previousTargets=a.PGOOOOLNPLF;var previousPoint=a.EPKIPIDBDGC;
                try{a.PGOOOOLNPLF=copy;a.EPKIPIDBDGC=point;__result=chosen;}
                catch{a.PGOOOOLNPLF=previousTargets;a.EPKIPIDBDGC=previousPoint;throw;}
                changes++;
            }
            Report("martial",a.IHOPPBMEAEN,a.EFDDHIBLEHF?.Count??0,candidates.Count,original?.OJPECMNINNA,__result?.OJPECMNINNA);
        }catch(Exception e){Fault(e);}
    }
    static void DebateChoice(LEDJAIBAAJC __instance,bool __0,ref MMGEDKCCMHH __result){
        if(debateDecision!=__instance.Pointer || __0 || __result!=null)return;
        try{
            var a=__instance;var candidates=a.PDMICGPKJKB;
            if(candidates==null)return;
            var original=__result;
            MMGEDKCCMHH chosen=null;Debaters targets=null;float best=0;
            foreach(var row in candidates){
                var card=row.Key;var plan=row.Value;
                if(card==null||plan?.NOJLEOEGACM==null||card.HFBAJIGHOAF!=MMGEDKCCMHH.GANKBONOKGD.Attack)continue;
                if(a.EFDDHIBLEHF?.Contains(card)!=true || a.HIMGMJJKFFL(card,false)>a.IHOPPBMEAEN)continue;
                if(!float.IsFinite(plan.DHLOPCACLKO)||plan.DHLOPCACLKO<0)continue;
                foreach(var group in plan.NOJLEOEGACM){
                    if(!Valid(group,a.IBLKPJDLABN))continue;
                    float value=Math.Max(.01f,plan.DHLOPCACLKO)*GroupValue(group);
                    if(value>best){best=value;chosen=card;targets=group;}
                }
            }
            if(chosen==null&&original==null)RepairDebate(a,ref chosen,ref targets,ref best);
            if(chosen==null&&original==null)DebateSupport(a,ref chosen,ref targets,ref best);
            if(chosen!=null){
                var copy=new Debaters();foreach(var t in targets)copy.Add(t);
                a.PGOOOOLNPLF=copy;__result=chosen;changes++;
            }
            Report("debate",a.IHOPPBMEAEN,a.EFDDHIBLEHF?.Count??0,candidates.Count,original?.OJPECMNINNA,__result?.OJPECMNINNA);
        }catch(Exception e){Fault(e);}
    }
    static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    static bool Hostile(FMHEOPMKFJB a,FMHEOPMKFJB b)=>AOHIBKLOAEI.BFAFNFOHJKJ(a,b);
    static bool Valid(Fighters group,FMHEOPMKFJB side){
        if(group==null||group.Count==0)return false;
        foreach(var t in group){var a=t?.GetAttribute();if(t==null||a==null||a.MKEIPLCMANH<=0||!Hostile(side,a.IBLKPJDLABN))return false;}
        return true;
    }
    static bool Valid(Debaters group,FMHEOPMKFJB side){
        if(group==null||group.Count==0)return false;
        foreach(var t in group){var a=t?.m_debateAttribute;if(!t||a==null||a.MKEIPLCMANH<=0||!Hostile(side,a.IBLKPJDLABN))return false;}
        return true;
    }
    static float GroupValue(Fighters targets,GKHLLOOEEFN actor){
        float value=0;var origin=actor.GetTransform().position;
        foreach(var t in targets){var a=t.GetAttribute();float health=Math.Clamp((float)a.MKEIPLCMANH/Math.Max(1,t.GetBattleHPLimit(1)),0,1);
            value+=1+.2f*(1-health);}
        // Small preference for closer valid targets; never invent a destination.
        float distance=(targets[0].GetTransform().position-origin).magnitude;
        return Math.Min(1.35f,1+.08f*(targets.Count-1)+.1f*(value/targets.Count-1))/(1+Math.Min(50,distance)*.003f);
    }
    static float GroupValue(Debaters targets){
        float weak=0;foreach(var t in targets){var a=t.m_debateAttribute;weak+=1-Math.Clamp((float)a.MKEIPLCMANH/Math.Max(1,a.ONGKCKHBLIB),0,1);}
        return Math.Min(1.35f,1+.08f*(targets.Count-1)+.1f*weak/targets.Count);
    }
    static void Report(string mode,int ap,int cards,int candidates,string before,string after){
        decisions++;if(after==null)empty++;
        if(decisions<=60 || decisions%40==0)Note($"{mode} AP={ap} hand={cards} legal={candidates} choice={before??"none"}->{after??"none"}; checked={decisions} changed={changes} empty={empty}");
    }
    static void Fault(Exception e){if(++faults<=3)Note("Kept native decision: "+e);}
    static void Note(string text){
        try{var path=Path.Combine(Paths.BepInExRootPath,"CombatTargetRecovery.log");
            CombatLog.Write(path,1048576,DateTime.Now.ToString("s")+" "+text+Environment.NewLine);
        }catch{/* Diagnostic I/O must not interrupt combat. */}
    }
}
