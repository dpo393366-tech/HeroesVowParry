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
    static ConfigEntry<bool> alliedParry,enemyAttack;
    static ConfigEntry<float> multiplier;
    internal static void Install(DuelCounters mod,Harmony harmony){
        alliedParry=mod.Config.Bind("PlayerBattle","AlliedManualParry",true,"Right-click/Q guards actual friendly targets in any active martial/debate battle, including story/event battles without the protagonist. Existing automatic rolls remain enabled.");
        enemyAttack=mod.Config.Bind("PlayerBattle","EnemyAttackEnabled",true,"Scale enemy attack stats only in protagonist-participating duels/debates; delegated battles are unchanged.");
        multiplier=mod.Config.Bind("PlayerBattle","EnemyAttackMultiplier",1.2f,new ConfigDescription("Enemy attack stat multiplier, not an additional final damage multiplier.",new AcceptableValueRange<float>(.5f,5f)));
        var balanced=mod.Config.Bind("Migration","AttackBalance183Applied",false,"Legacy migration marker; current default is 1.2, existing user adjustments are preserved.");
        if(!balanced.Value){balanced.Value=true;}
        foreach(var target in new[]{typeof(NFIDHHGKMDC),typeof(LEDJAIBAAJC)})
            Panel.Hook(harmony,target,"LEJICHHIEGH",typeof(PlayerBattleRules),after:nameof(Invalidate));
        Panel.Hook(harmony,typeof(Fi_Controller),"QuitBattle",typeof(PlayerBattleRules),after:nameof(Invalidate));
        Panel.Hook(harmony,typeof(De_Controller),"QuitDebate",typeof(PlayerBattleRules),after:nameof(Invalidate));
        // NFID uses either a live human/animal getter OR a scenario stat override.
        // Scale its final attack getter once, covering both paths and additive buffs.
        Panel.Hook(harmony,typeof(NFIDHHGKMDC),"MIPCJKHEHFO",typeof(PlayerBattleRules),after:nameof(FightAttack));
        // Native debate damage = skill coefficient * MIPCJKHEHFO(attacker)
        // / (FCJJDMAMNHA(defender) + constant). Do not scale wisdom globally.
        Panel.Hook(harmony,typeof(LEDJAIBAAJC),"MIPCJKHEHFO",typeof(PlayerBattleRules),after:nameof(DebateAttack));
    }
    internal static void Settings(){
        Panel.Switch("이벤트 전투 포함 · 동행/아군 우클릭·Q 패링",alliedParry);
        Panel.Switch("주인공 참전 · 적 공격력 배율 사용",enemyAttack);
        Panel.Slider("적 공격력 배율 · 위임 전투 제외",multiplier,.5f,5f,.1f);
    }
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
    // Manual/event combat uses the live native battle, not the save protagonist.
    // Keep Participating separate: the legacy enemy attack multiplier still has its own scope.
    internal static bool ManualCombat(bool debate)=>debate
        ? De_Controller.m_isInDebate && De_Controller.EBACHLGLNGD && !De_Controller.EBACHLGLNGD.m_isEnd && De_Controller.m_listHero!=null
        : Fi_Controller.m_isInBattle && Fi_Controller.EBACHLGLNGD && !Fi_Controller.m_isEnd && Fi_Controller.m_listHero!=null;
    internal static bool Manual(bool debate,CLAGKDECFID hero,FMHEOPMKFJB side)=>Friendly(side) && hero!=null && ManualCombat(debate) &&
        (DuelCounters.IsPlayer(hero) || alliedParry?.Value==true);
    internal static bool AutoRoll(bool debate,CLAGKDECFID hero)=>!DuelCounters.IsPlayer(hero) || (debate?De_Controller.m_autoDebate:Fi_Controller.m_autoBattle);
    internal static int Scale(int value,float factor){
        if(value<=0 || !float.IsFinite(factor))return value;
        factor=Math.Clamp(factor,.5f,5f);
        return (int)Math.Clamp(Math.Round(value*(double)factor,MidpointRounding.AwayFromZero),1d,int.MaxValue);
    }
    static void FightAttack(NFIDHHGKMDC __instance,ref int __result){
        if(enemyAttack?.Value!=true || __instance.IBLKPJDLABN!=FMHEOPMKFJB.Enemy || !Participating(false))return;
        if(Roster(false).attributes.Contains(__instance.Pointer))__result=Scale(__result,multiplier.Value);
    }
    static void DebateAttack(LEDJAIBAAJC __instance,ref int __result){
        if(enemyAttack?.Value!=true || __instance.IBLKPJDLABN!=FMHEOPMKFJB.Enemy || !Participating(true))return;
        if(Roster(true).attributes.Contains(__instance.Pointer))__result=Scale(__result,multiplier.Value);
    }
}
