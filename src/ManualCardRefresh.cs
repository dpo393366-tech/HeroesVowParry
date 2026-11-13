using System;
using UnityEngine;
namespace Vow.Counters;
// A conservative UI refresh, never a phase change, AP refill or forced enable.
internal static class ManualCardRefresh {
#if DuelCounters
    static double next;static IntPtr actor;static int round,order;static bool stable;
    internal static void Tick(Fi_Controller __instance){
        double now=Time.unscaledTimeAsDouble;if(now<next)return;next=now+1;
        try{
            if(!Application.isFocused||Time.timeScale<=0||!Panel.InputAllowed||BattleModal.Visible||UI_LoadUI.m_isLoading||
                !Fi_Controller.m_isInBattle||Fi_Controller.m_isEnd||Fi_Controller.m_autoBattle||
                Fi_Controller.m_curPhase!=Fi_Controller.NHCOMCJKABH.play){stable=false;return;}
            // Native Update supplies the live controller. get_Instance is a
            // factory, not a passive lookup; never invoke it from a UI watchdog.
            var f=__instance;var ui=Fi_Controller.m_battleUI;var h=Fi_Controller.m_curHero;
            if(!f||!ui||h==null||!TurnHandoff.BattleReady(f)||
                Fi_Controller.m_isChoosingVictim||Fi_Controller.m_isChoosingDes||Fi_Controller.m_curCard!=null||
                Fi_Controller.m_hasSetMoveDes||Fi_Controller.m_isPlayerMoving||
                (f.m_listEventIndex?.Count??0)>0||(f.m_listEventTime?.Count??0)>0||(f.m_listPushing?.Count??0)>0||
                !h.GetAttribute().OFICDHGKHAF()||ui.m_npcCtrl?.Pointer!=h.Pointer||PerHitParry.PendingSettlement(ImpactTiming.Current(false))){stable=false;return;}
            if(!stable||actor!=h.Pointer||round!=f.AHDOJFHBBFJ||order!=f.m_curOrder){stable=true;actor=h.Pointer;round=f.AHDOJFHBBFJ;order=f.m_curOrder;return;}
            var cards=ui.m_listZCard;if(cards==null||cards.Count==0)return;
            int before=0;foreach(var c in cards)if(c&&c.m_button&&c.m_button.interactable)before++;
            if(before>0)return;
            // Native false branch recomputes AP/MP/status/skill restrictions.
            ui.RefreshAllCardInteractable(false);
            int after=0;foreach(var c in cards)if(c&&c.m_button&&c.m_button.interactable)after++;
            if(after>0){ui.RefreshButtonShow();ui.RefreshButtonCover();DuelCounters.StabilityNote($"Manual cards refreshed round={round} order={order} enabled={after}");}
        }catch(Exception e){next=now+10;DuelCounters.Warn("수동 카드 갱신: "+e.Message);}
    }
#else
    internal static void Tick(Fi_Controller f){}
#endif
}
