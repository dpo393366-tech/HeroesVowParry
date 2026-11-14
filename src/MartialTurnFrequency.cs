using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace Vow.Counters;

// A separate schedule, never duplicate native actors in m_listHero: that list is
// also used for targeting, death, cards, round effects, icons and scene cleanup.
internal static class MartialTurnFrequency
{
    static ConfigEntry<float> frequency;
    static List<IntPtr> schedule;
    static int cursor,depth;
    static IntPtr controller;
    static readonly Dictionary<IntPtr,int> turnRemainders=new();
    internal static void Install(BasePlugin plugin,Harmony h){
        float previous=1.2f;
        string path=System.IO.Path.Combine(Paths.ConfigPath,"local.heroesvow.enemytempo.cfg");
        if(System.IO.File.Exists(path)){
            var legacy=new ConfigFile(path,false){SaveOnConfigSet=false};
            bool active=legacy.Bind("EnemyTurns","DoubleFrequency",true,"").Value;
            float saved=legacy.Bind("EnemyTurns","TurnsPerRound",2f,"").Value;
            previous=active && float.IsFinite(saved)?Math.Clamp(saved,1,4):1f;
        }
        frequency=plugin.Config.Bind("EnemyTurns","TurnsPerRound",previous,new ConfigDescription("Enemy martial turns per round in 0.1 steps. 1 is native. Allies and debates unchanged; takes effect next round.",new AcceptableValueRange<float>(1,4)));
        var before=new HarmonyMethod(typeof(MartialTurnFrequency),nameof(Before)){priority=Priority.Last};
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetRoundAndHero"),prefix:before,finalizer:new HarmonyMethod(typeof(MartialTurnFrequency),nameof(Final)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"RenewHeroActionOrder"),postfix:new HarmonyMethod(typeof(MartialTurnFrequency),nameof(Renew)));
        Panel.Hook(h,typeof(Fi_Controller),"QuitBattle",typeof(MartialTurnFrequency),before:nameof(Reset));
        Panel.Hook(h,typeof(Fi_Controller),"StartBattle",typeof(MartialTurnFrequency),before:nameof(Reset));
    }
    internal static List<T> Plan<T>(IReadOnlyList<T> units,Func<T,bool> enemy,int repetitions=2){
        repetitions=Math.Clamp(repetitions,1,4);
        int n=units.Count;var slots=new List<(T unit,int time,int extra,int order)>();
        for(int i=0;i<n;i++){
            slots.Add((units[i],i*repetitions,0,i));
            if(enemy(units[i]))for(int repeat=1;repeat<repetitions;repeat++)
                slots.Add((units[i],(i*repetitions+repeat*n)%(n*repetitions),repeat,i));
        }
        return slots.OrderBy(x=>x.time).ThenBy(x=>x.extra).ThenBy(x=>x.order).Select(x=>x.unit).ToList();
    }
    internal static int NextCount(float frequency,ref int remainder){
        int tenths=(int)Math.Round(Math.Clamp(float.IsFinite(frequency)?frequency:2f,1,4)*10,MidpointRounding.AwayFromZero);
        int credit=Math.Clamp(remainder,0,9)+tenths;
        remainder=credit%10;return credit/10;
    }
    internal static List<T> PlanFractional<T>(IReadOnlyList<T> units,Func<T,bool> enemy,float frequency,Dictionary<T,int> remainders){
        int n=units.Count;var slots=new List<(T unit,int time,int extra,int order)>();
        for(int i=0;i<n;i++){
            int count=1;
            if(enemy(units[i])){remainders.TryGetValue(units[i],out int credit);count=NextCount(frequency,ref credit);remainders[units[i]]=credit;}
            // Twelve is divisible by every allowed whole turn count (1..4).
            // Integer time keys preserve ordering without floating-point ties.
            slots.Add((units[i],i*12,0,i));
            for(int repeat=1;repeat<count;repeat++)slots.Add((units[i],(i*12+repeat*n*(12/count))%(n*12),repeat,i));
        }
        return slots.OrderBy(x=>x.time).ThenBy(x=>x.extra).ThenBy(x=>x.order).Select(x=>x.unit).ToList();
    }
    static void Reset(){schedule=null;controller=IntPtr.Zero;cursor=0;turnRemainders.Clear();}
    static void Renew(Fi_Controller __instance){
        if(frequency.Value<=1f){schedule=null;turnRemainders.Clear();return;}
        // Native mid-round sorting invalidates the schedule, not earned credit.
        if(depth==0){schedule=null;return;}
        var units=Fi_Controller.m_listHero;if(units==null||units.Count<2){schedule=null;return;}
        var order=new List<IntPtr>();var enemies=new HashSet<IntPtr>();
        foreach(var unit in units){if(unit==null){schedule=null;return;}order.Add(unit.Pointer);if(unit.GetAttribute()?.IBLKPJDLABN==FMHEOPMKFJB.Enemy)enemies.Add(unit.Pointer);}
        foreach(var stale in turnRemainders.Keys.Where(id=>!enemies.Contains(id)).ToArray())turnRemainders.Remove(stale);
        schedule=PlanFractional(order,id=>enemies.Contains(id),frequency.Value,turnRemainders);controller=__instance.Pointer;
        // The native round boundary itself selects physical slot zero immediately
        // after Renew. It remains the first slot, including equal-time ties.
        cursor=1;
    }
    static void Before(Fi_Controller __instance,out bool __state){
        depth++;__state=true;
        var units=Fi_Controller.m_listHero;
        if(units==null||units.Count<2||Fi_Controller.CJKNCEOMJNL>0||!Fi_Controller.m_isInBattle||Fi_Controller.m_isEnd)return;
        if(controller!=__instance.Pointer){schedule=null;return;}
        if(schedule==null)return;
        // Preserve the completed round's plan when switched off midway. A new
        // round re-reads the option, preventing either skipped allies or duplicates.
        while(cursor<schedule.Count){
            var id=schedule[cursor++];
            for(int i=0;i<units.Count;i++)if(units[i]?.Pointer==id){
                __instance.m_curOrder=i-1;return;
            }
        }
        __instance.m_curOrder=units.Count-1;
        schedule=null; // let native round effects, sorting and cleanup run once
    }
    static Exception Final(Exception __exception,bool __state){
        if(__state)depth=Math.Max(0,depth-1);
        if(__exception!=null)schedule=null;
        return __exception;
    }
}
