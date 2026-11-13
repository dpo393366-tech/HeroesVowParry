using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
namespace Vow.Counters;
// Bounded coarse timings, no per-frame strings, heap walks or disk writes.
internal static class CombatCost{
    static readonly long[] ticks=new long[5],worst=new long[5];
    static readonly int[] calls=new int[5];
#if DuelCounters
    internal static void Install(HarmonyLib.Harmony h){
        foreach(var type in new[]{typeof(Fi_Controller),typeof(De_Controller)})
            h.Patch(HarmonyLib.AccessTools.DeclaredMethod(type,"Update"),prefix:new HarmonyLib.HarmonyMethod(typeof(CombatCost),nameof(NativeBegin)){priority=HarmonyLib.Priority.First},finalizer:new HarmonyLib.HarmonyMethod(typeof(CombatCost),nameof(NativeEnd)){priority=HarmonyLib.Priority.Last});
        h.Patch(HarmonyLib.AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"GOCFBMPJJKE"),prefix:new HarmonyLib.HarmonyMethod(typeof(CombatCost),nameof(NativeBegin)){priority=HarmonyLib.Priority.First},finalizer:new HarmonyLib.HarmonyMethod(typeof(CombatCost),nameof(DecisionEnd)){priority=HarmonyLib.Priority.Last});
    }
    static void NativeBegin(out long __state)=>__state=Start();
    static Exception NativeEnd(long __state,Exception __exception){if(__state!=0)End(3,__state);return __exception;}
    static Exception DecisionEnd(long __state,Exception __exception){if(__state!=0)End(4,__state);return __exception;}
#endif
    static double next;static int frames,lastFrame=-1,lastGc0,lastGc2;
    internal static long Start()=>Stopwatch.GetTimestamp();
    internal static void End(int group,long begin){
        long dt=Stopwatch.GetTimestamp()-begin;ticks[group]+=dt;calls[group]++;if(dt>worst[group])worst[group]=dt;
    }
    internal static void Frame(){
#if DuelCounters
        double now=Time.unscaledTimeAsDouble;
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;frames++;
        if(next==0){next=now+30;lastGc0=GC.CollectionCount(0);lastGc2=GC.CollectionCount(2);}
        if(now<next)return;next=now+30;
        try{
            if(Fi_Controller.m_isInBattle||De_Controller.m_isInDebate){
                double scale=1000d/Stopwatch.Frequency;
                string line=DateTime.Now.ToString("s")+$" focused={Application.isFocused} frames={frames} updateMs={ticks[0]*scale/Math.Max(1,frames):F3} lateHudMs={ticks[1]*scale/Math.Max(1,frames):F3} eventHookMs={ticks[2]*scale/Math.Max(1,frames):F3} worstUpdateMs={worst[0]*scale:F3} worstHudMs={worst[1]*scale:F3} worstEventMs={worst[2]*scale:F3} gc0={GC.CollectionCount(0)-lastGc0} gc2={GC.CollectionCount(2)-lastGc2}";
                line+=$" controllerUpdateMs={ticks[3]*scale/Math.Max(1,frames):F3} worstControllerMs={worst[3]*scale:F3} decisionMs={ticks[4]*scale/Math.Max(1,frames):F3} decisionCalls={calls[4]} worstDecisionMs={worst[4]*scale:F3}";
                string path=Path.Combine(BepInEx.Paths.BepInExRootPath,"DuelCounters-performance.log");
                CombatLog.Write(path,131072,line+Environment.NewLine);
            }
        }catch{}
        Array.Clear(ticks,0,ticks.Length);Array.Clear(worst,0,worst.Length);Array.Clear(calls,0,calls.Length);frames=0;lastGc0=GC.CollectionCount(0);lastGc2=GC.CollectionCount(2);
#endif
    }
}
