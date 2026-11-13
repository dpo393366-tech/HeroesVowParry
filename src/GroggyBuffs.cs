using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Vow.Counters;

// Remove native buff instances at a LateUpdate boundary, never while the
// native hit/buff dispatcher may be enumerating their lists.
internal static class GroggyBuffs
{
    static readonly List<BLFAANNDPHF> fight=new();
    static readonly List<PODFJIBHHDH> debate=new();
    static readonly HashSet<IntPtr> seen=new();
    static bool clearing;
    static double nextError;
    internal static void Install(Harmony h){
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"BIAIIMENKOJ"),postfix:new HarmonyMethod(typeof(GroggyBuffs),nameof(Invincible)){priority=Priority.Last});
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"MHJJJAACBFG"),prefix:new HarmonyMethod(typeof(GroggyBuffs),nameof(ApplyFight)){priority=Priority.First});
        h.Patch(AccessTools.DeclaredMethod(typeof(LEDJAIBAAJC),"MHJJJAACBFG"),prefix:new HarmonyMethod(typeof(GroggyBuffs),nameof(ApplyDebate)){priority=Priority.First});
    }
    // This check is immediate, including the interval before deferred cleanup.
    internal static void Invincible(NFIDHHGKMDC __instance,ref bool __result){if(EnemyGroggy.IsStunned(false,__instance.Pointer))__result=false;}
    internal static bool ApplyFight(NFIDHHGKMDC __instance,BHPMNBNGCFI __0)=>__0==null||!__0.ANOLJFGPCBM||!EnemyGroggy.IsStunned(false,__instance.Pointer);
    internal static bool ApplyDebate(LEDJAIBAAJC __instance,BIEPAJPFKON __0)=>__0==null||!__0.ANOLJFGPCBM||!EnemyGroggy.IsStunned(true,__instance.Pointer);
    static void Error(Exception ex){if(Time.unscaledTimeAsDouble<nextError)return;nextError=Time.unscaledTimeAsDouble+5;DuelCounters.Warn("그로기 버프 해제: "+ex.Message);}
    static void Collect(Il2CppSystem.Collections.Generic.List<BLFAANNDPHF> list){
        if(list==null)return;
        for(int i=0;i<list.Count;i++){var b=list[i];if(b!=null&&b.EBIOPDHEBOF?.ANOLJFGPCBM==true&&seen.Add(b.Pointer))fight.Add(b);}
    }
    static void Collect(Il2CppSystem.Collections.Generic.List<PODFJIBHHDH> list){
        if(list==null)return;
        for(int i=0;i<list.Count;i++){var b=list[i];if(b!=null&&b.EBIOPDHEBOF?.ANOLJFGPCBM==true&&seen.Add(b.Pointer))debate.Add(b);}
    }
    internal static void Purge(NFIDHHGKMDC attr){
        if(clearing||attr==null||!EnemyGroggy.IsStunned(false,attr.Pointer))return;
        // Native full removal accesses all three collections. Do not call it
        // against a partly constructed or already disposed combat attribute.
        if(attr.PGINGBMNPJG==null||attr.PIDAHHCCIJM==null||attr.GPLJJEGONFE==null)return;
        clearing=true;int removed=0;
        try{
            Collect(attr.PGINGBMNPJG);Collect(attr.PIDAHHCCIJM);Collect(attr.GPLJJEGONFE);
            foreach(var b in fight)try{
                if(b.AFFCOKCHGKM?.GetAttribute()?.Pointer!=attr.Pointer||b.EBIOPDHEBOF?.ANOLJFGPCBM!=true)continue;
                if(!attr.PGINGBMNPJG.Contains(b)&&!attr.PIDAHHCCIJM.Contains(b)&&!attr.GPLJJEGONFE.Contains(b))continue;
                // IBKGNHMPBOP removes from all collections, recycles the visual,
                // and refreshes icons. Unlike ordinary dispel, it does not honor
                // the definition's unDelete flag. Never edit that shared flag.
                b.IBKGNHMPBOP();removed++;
            }catch(Exception ex){Error(ex);}
            if(removed>0)DuelCounters.StabilityNote($"groggy dispel fight={attr.Pointer} removed={removed}");
        }catch(Exception ex){Error(ex);}
        finally{fight.Clear();seen.Clear();clearing=false;}
    }
    internal static void Purge(LEDJAIBAAJC attr){
        if(clearing||attr==null||!EnemyGroggy.IsStunned(true,attr.Pointer))return;
        if(attr.PGINGBMNPJG==null||attr.PIDAHHCCIJM==null||attr.GPLJJEGONFE==null)return;
        clearing=true;int removed=0;
        try{
            Collect(attr.PGINGBMNPJG);Collect(attr.PIDAHHCCIJM);Collect(attr.GPLJJEGONFE);
            foreach(var b in debate)try{
                if(b.AFFCOKCHGKM?.m_debateAttribute?.Pointer!=attr.Pointer||b.EBIOPDHEBOF?.ANOLJFGPCBM!=true)continue;
                if(!attr.PGINGBMNPJG.Contains(b)&&!attr.PIDAHHCCIJM.Contains(b)&&!attr.GPLJJEGONFE.Contains(b))continue;
                b.IBKGNHMPBOP();removed++;
            }catch(Exception ex){Error(ex);}
            if(removed>0)DuelCounters.StabilityNote($"groggy dispel debate={attr.Pointer} removed={removed}");
        }catch(Exception ex){Error(ex);}
        finally{debate.Clear();seen.Clear();clearing=false;}
    }
}
