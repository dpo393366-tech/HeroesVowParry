using UnityEngine;
namespace Vow.Counters;

// RMB is also native "open fighter details". A failed/unavailable guard during
// an incoming attack must not turn that same click into a modal window request.
// Own only this button gesture, through release; never change pause/time/phase.
internal static class GuardInfoInput {
    static int frame=-1,release=-1,notes;
    static bool owned;
    static bool Incoming(){
        if(Fi_Controller.m_isInBattle&&!Fi_Controller.m_isEnd){
            var a=ImpactTiming.Current(false);
            return (Fi_Controller.m_isLaunchingAttack||(a!=null&&a.Valid()&&!a.motionFinished&&!a.cancelled)) &&
                Fi_Controller.m_curHero?.GetAttribute()?.IBLKPJDLABN==FMHEOPMKFJB.Enemy &&
                Fi_Controller.m_curCard?.HFBAJIGHOAF==KJFOPOCBHBM.CCENIDMDCCN.Attack;
        }
        if(De_Controller.m_isInDebate){
            var a=ImpactTiming.Current(true);
            return (De_Controller.m_isLaunchingAttack||(a!=null&&a.Valid()&&!a.motionFinished&&!a.cancelled)) &&
                De_Controller.m_curHero?.m_debateAttribute?.IBLKPJDLABN==FMHEOPMKFJB.Enemy &&
                De_Controller.m_curCard?.HFBAJIGHOAF==MMGEDKCCMHH.GANKBONOKGD.Attack;
        }
        return false;
    }
    internal static void Observe(){
        if(frame==Time.frameCount)return;frame=Time.frameCount;
        if(!Fi_Controller.m_isInBattle&&!De_Controller.m_isInDebate){owned=false;release=-1;return;}
        bool down=Input.GetMouseButtonDown(1),held=Input.GetMouseButton(1),up=Input.GetMouseButtonUp(1);
        if(down){
            owned=false;release=-1;
            if(Panel.InputAllowed&&Application.isFocused&&Time.timeScale>0&&!FICFPEHAJNJ.ODIOKGGBKEI&&!UI_BattlePreviewUI.m_isOpen&&!BattleModal.Visible){
                try{owned=Incoming();}catch{owned=false;}
                if(owned&&(++notes<=32||notes%60==0))DuelCounters.StabilityNote("Guard RMB reserved through release; native detail popup suppressed for incoming attack.");
            }
        }
        if(up&&owned)release=Time.frameCount;
        if(!down&&!held&&!up&&release<Time.frameCount){owned=false;release=-1;}
    }
    internal static bool Suppress(){Observe();return owned&&(Input.GetMouseButtonDown(1)||Input.GetMouseButton(1)||Input.GetMouseButtonUp(1));}
}
