using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;

namespace Vow.Counters;

// Never infer delegation from autoBattle: the protagonist may use automatic play.
// Read the real roster, without changing saved stats or caching actors across saves.
internal static class PlayerBattleRules
{
    static ConfigEntry<bool> alliedParry;
    internal static void Install(DuelCounters mod,Harmony harmony){
        alliedParry=mod.Config.Bind("PlayerBattle","AlliedManualParry",true,"Right-click guards actual allied targets only when the protagonist participates. Existing automatic rolls remain enabled.");
        foreach(var target in new[]{typeof(NFIDHHGKMDC),typeof(LEDJAIBAAJC)})
            Panel.Hook(harmony,target,"LEJICHHIEGH",typeof(PlayerBattleRules),after:nameof(Invalidate));
        Panel.Hook(harmony,typeof(Fi_Controller),"QuitBattle",typeof(PlayerBattleRules),after:nameof(Invalidate));
        Panel.Hook(harmony,typeof(De_Controller),"QuitDebate",typeof(PlayerBattleRules),after:nameof(Invalidate));
    }
    internal static void Settings(){Panel.Switch("주인공 참전 · 동행/아군 우클릭 패링",alliedParry);}
    static bool Friendly(FMHEOPMKFJB side)=>side==FMHEOPMKFJB.MySide || side==FMHEOPMKFJB.Ally;
    sealed class RosterCache {
        internal int frame=-1,count=-1;internal string player;
        internal bool participating;internal IntPtr roster;
        internal readonly HashSet<IntPtr> attributes=new();
    }
    static readonly RosterCache fightCache=new(),debateCache=new();
    static void Invalidate(){fightCache.frame=debateCache.frame=-1;}
    // Attack stats are queried repeatedly by native AI scoring and HUDs. Scan
    // the roster once per frame, never once per candidate/target/stat read.
    static RosterCache Roster(bool debate){
        var c=debate?debateCache:fightCache;
        string player=NEINKOOBJIH.POMNMBGDDIN?.OJPECMNINNA;
        bool active=debate?De_Controller.m_isInDebate&&De_Controller.EBACHLGLNGD&&!De_Controller.EBACHLGLNGD.m_isEnd:Fi_Controller.m_isInBattle&&!Fi_Controller.m_isEnd;
        int count=debate?(De_Controller.m_listHero?.Count??0):(Fi_Controller.m_listHero?.Count??0);
        if(!active||player==null){c.frame=-1;c.participating=false;c.attributes.Clear();return c;}
#if DuelCounters
        IntPtr rosterId=debate?(De_Controller.m_listHero?.Pointer??IntPtr.Zero):(Fi_Controller.m_listHero?.Pointer??IntPtr.Zero);
#else
        IntPtr rosterId=new IntPtr(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(debate?(object)De_Controller.m_listHero:Fi_Controller.m_listHero));
#endif
        if(c.frame==Time.frameCount&&c.count==count&&c.player==player&&c.roster==rosterId)return c;
        c.roster=rosterId;
        c.attributes.Clear();c.participating=false;c.frame=Time.frameCount;c.count=count;c.player=player;
        if(debate){
            var roster=De_Controller.m_listHero;if(roster==null)return c;
            foreach(var t in roster){if(!t)continue;var a=t.m_debateAttribute;if(a==null)continue;
                c.attributes.Add(a.Pointer);if(Friendly(a.IBLKPJDLABN)&&DuelCounters.IsPlayer(t.GetHero()))c.participating=true;}
        }else{
            var roster=Fi_Controller.m_listHero;if(roster==null)return c;
            foreach(var t in roster){if(t==null)continue;var a=t.GetAttribute();if(a==null)continue;
                c.attributes.Add(a.Pointer);if(Friendly(a.IBLKPJDLABN)&&DuelCounters.IsPlayer(t.GetHero()))c.participating=true;}
        }
        return c;
    }
    internal static bool Participating(bool debate)=>Roster(debate).participating;
    internal static bool Manual(bool debate,CLAGKDECFID hero,FMHEOPMKFJB side)=>Friendly(side) && hero!=null && Participating(debate) &&
        (DuelCounters.IsPlayer(hero) || alliedParry?.Value==true);
    internal static bool AutoRoll(bool debate,CLAGKDECFID hero)=>!DuelCounters.IsPlayer(hero) || (debate?De_Controller.m_autoDebate:Fi_Controller.m_autoBattle);
}
