using System;
using UnityEngine;
#if DuelCounters
using HarmonyLib;
#endif
namespace Vow.Counters;

// Observation only. Do not shorten native buffs, damage iterators or movement
// by guessing that an animation's tail is a disposable wait.
internal static class CardLatency {
#if DuelCounters
    sealed class Request {internal double requested,native;internal int holds;internal string card,reason;internal IntPtr actor;}
    static Request fight,debate;
    static double budgetAt;static int writes;
    internal static void Install(Harmony h){
        Panel.Hook(h,typeof(Fi_Controller),"LaunchAttack",typeof(CardLatency),before:nameof(FightRequest));
        Panel.Hook(h,typeof(De_Controller),"LaunchAttack",typeof(CardLatency),before:nameof(DebateRequest));
    }
    static void FightRequest(){if(Fi_Controller.m_isInBattle&&!Fi_Controller.m_isEnd)fight=new(){requested=Time.unscaledTimeAsDouble,card=Fi_Controller.m_curCard?.OJPECMNINNA,actor=Fi_Controller.m_curHero?.Pointer??IntPtr.Zero};}
    static void DebateRequest(){if(De_Controller.m_isInDebate)debate=new(){requested=Time.unscaledTimeAsDouble,card=De_Controller.m_curCard?.OJPECMNINNA,actor=De_Controller.m_curHero?.Pointer??IntPtr.Zero};}
    static Request Current(bool speech)=>speech?debate:fight;
    internal static void Hold(bool speech,string reason){var r=Current(speech);if(r!=null){r.holds++;r.reason=reason;}}
    internal static void Begin(ImpactTiming.Attack a){var r=Current(a.debate);if(r!=null&&r.native==0)r.native=Time.unscaledTimeAsDouble;}
    internal static void Loaded(ImpactTiming.Attack a){
        var r=Current(a.debate);if(r==null)return;
        double now=Time.unscaledTimeAsDouble;
        if(now-budgetAt>60){budgetAt=now;writes=0;}if(writes++>=120)return;
        // Match captured identity; special counterattacks may replace a request.
        if(r.actor!=(a.debate?a.debater.Pointer:a.fighter.Pointer)||r.card!=(a.debate?a.debateCard?.OJPECMNINNA:a.fightCard?.OJPECMNINNA))return;
        string line=$"{DateTime.Now:s} card={r.card} debate={a.debate} actor={r.actor} requestToLoadMs={(now-r.requested)*1000:F2} modGateMs={(r.native>0?r.native-r.requested:0)*1000:F2} nativeSetupMs={(r.native>0?now-r.native:0)*1000:F2} configuredLead={DuelCounters.AttackLead:F3} heldFrames={r.holds} reason={r.reason} scale={Time.timeScale:F2}";
        CombatLog.Write(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,"DuelCounters-card-latency.log"),131072,line+Environment.NewLine);
    }
#else
    internal static void Hold(bool speech,string reason){}
    internal static void Begin(ImpactTiming.Attack a){}
    internal static void Loaded(ImpactTiming.Attack a){}
#endif
}
