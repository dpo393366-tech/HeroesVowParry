using System;
using UnityEngine;
using Fighters=Il2CppSystem.Collections.Generic.List<GKHLLOOEEFN>;
namespace Vow.Counters;

internal static partial class CombatTargetRecovery {
    static int areaChecks;
    static GKHLLOOEEFN lineActor;static KJFOPOCBHBM lineCard;static int lineRound,lineOrder;static bool lineRefreshed;
    static readonly System.Collections.Generic.HashSet<IntPtr> lineExpected=new();
    static bool LineActive(Fi_Controller f)=>f!=null && lineActor!=null && lineCard!=null && Fi_Controller.m_isInBattle && !Fi_Controller.m_isEnd && !Fi_Controller.m_isSpecialAttack &&
        Fi_Controller.m_curHero?.Pointer==lineActor.Pointer && Fi_Controller.m_curCard?.Pointer==lineCard.Pointer &&
        f.AHDOJFHBBFJ==lineRound && f.m_curOrder==lineOrder;
    // This entry point is used by automatic actors (including the protagonist),
    // not the player's manual click/drag targeting path. Recompute immediately
    // before LaunchAttack so an old plan cannot omit actors that have moved.
    static void AreaAttack(Fi_Controller __instance,ref Fighters __0,KJFOPOCBHBM __1,ref Vector3 __2){
        var actor=Fi_Controller.m_curHero;var card=__1;
        lineActor=null;lineCard=null;lineExpected.Clear();lineRefreshed=false;
        if(!Fi_Controller.m_isInBattle||Fi_Controller.m_isEnd||Fi_Controller.m_isSpecialAttack||actor==null||
           card==null||card.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack||card.CPIJBMOGBLA==KJFOPOCBHBM.JNDKGNOEKDN.Target)return;
        FNNCDIHJBEA plan=null;
        if(card.CPIJBMOGBLA==KJFOPOCBHBM.JNDKGNOEKDN.Line || card.CPIJBMOGBLA==KJFOPOCBHBM.JNDKGNOEKDN.TargetLine){
            lineActor=actor;lineCard=card;lineRound=__instance.AHDOJFHBBFJ;lineOrder=__instance.m_curOrder;
        }
        try{
            var a=actor.GetAttribute();if(a==null)return;
            // Use the game's own range/side/obstacle filters and shape solvers.
            // No custom radius, global target union, or extra damage invocation.
            var native=__instance.TargetListHelper(actor,a.BDFEGCOILNE(card,true),card.LBLIAONIBOP,card.OPBADODCHOD,true,true);
            if(native==null||native.Count<2)return;
            var legal=new Fighters();foreach(var t in native)legal.Add(t);
            plan=FNNCDIHJBEA.OCELGGJBGEN();
            switch(card.CPIJBMOGBLA){
                case KJFOPOCBHBM.JNDKGNOEKDN.Circle:plan.ICGKBKIGNEL(card,legal,legal);break;
                case KJFOPOCBHBM.JNDKGNOEKDN.Fan:plan.AGEHKPGJELA(actor,card,legal,legal);break;
                case KJFOPOCBHBM.JNDKGNOEKDN.Line:
                case KJFOPOCBHBM.JNDKGNOEKDN.TargetLine:plan.AMMJGMGFFHH(actor,card,legal,legal);break;
                default:return;
            }
            int before=__0?.Count??0;var best=__0;Vector3 point=__2;
            if(plan.JLGPKHPMEFF!=null)foreach(var group in plan.JLGPKHPMEFF){
                if(Finite(group.Key)&&Valid(group.Value,a.IBLKPJDLABN)&&UniqueCount(group.Value)>UniqueCount(best)){
                    best=group.Value;point=group.Key;
                }
            }
            if(best!=__0){
                var copy=new Fighters();foreach(var t in best)if(!copy.Contains(t))copy.Add(t);
                __0=copy;__2=point;
            }
            if(++areaChecks<=40||before!=(__0?.Count??0))Note($"area card={card.OJPECMNINNA} shape={card.CPIJBMOGBLA} legal={legal.Count} targets={before}->{__0?.Count??0}");
        }catch(Exception e){Fault(e);}finally{try{if(plan!=null)plan.Recycle();}catch(Exception e){Fault(e);}}
    }
    static int UniqueCount(Fighters list){
        if(list==null)return 0;
        var ids=new System.Collections.Generic.HashSet<IntPtr>();foreach(var t in list)if(t!=null)ids.Add(t.Pointer);
        return ids.Count;
    }
    // Recheck the SAME line aim once, before contact/parry/receipt capture. Never
    // optimize to a different direction after the beam is visible, and never
    // re-add victims that a later native damage iterator has already consumed.
    static void LineContact(Fi_Controller __instance,PKLOJNCAOND __0){
        if(__0==null || (!__0.EBHFFNHELFB&&!__0.GHEGCDIKHDI) || lineRefreshed || !LineActive(__instance))return;
        lineRefreshed=true;FNNCDIHJBEA plan=null;
        try{
            var a=lineActor.GetAttribute();var point=Fi_Controller.m_targetPosition;
            if(a==null || !Finite(point))return;
            var legal=__instance.TargetListHelper(lineActor,a.BDFEGCOILNE(lineCard,true),lineCard.LBLIAONIBOP,lineCard.OPBADODCHOD,true,true);
            if(legal==null || legal.Count==0)return;
            var candidates=new Fighters();foreach(var t in legal)if(t!=null&&!candidates.Contains(t))candidates.Add(t);
            plan=FNNCDIHJBEA.OCELGGJBGEN();plan.CCBPJEFJEEP(lineActor,lineCard,candidates,point);
            Fighters exact=null;
            if(plan.JLGPKHPMEFF!=null)foreach(var row in plan.JLGPKHPMEFF){
                if(row.Key.x==point.x&&row.Key.y==point.y&&row.Key.z==point.z&&Valid(row.Value,a.IBLKPJDLABN)){exact=row.Value;break;}
            }
            if(exact==null)return;
            var copy=new Fighters();foreach(var t in exact)if(t!=null&&!copy.Contains(t))copy.Add(t);
            int old=Fi_Controller.m_listTargetHero?.Count??0;
            Fi_Controller.m_listTargetHero=copy;
            foreach(var t in copy)lineExpected.Add(t.GetAttribute().Pointer);
            Note($"line-contact card={lineCard.OJPECMNINNA} targets={old}->{copy.Count} aim={point} candidates={DescribeTargets(candidates)} selected={DescribeTargets(copy)}");
        }catch(Exception e){Fault(e);}finally{try{plan?.Recycle();}catch(Exception e){Fault(e);}}
    }
    static void LineDamage(NFIDHHGKMDC __instance,int __0,int __result){
        if(lineRefreshed && lineExpected.Contains(__instance.Pointer) && LineActive(Fi_Controller.EBACHLGLNGD))
            Note($"line-damage card={lineCard.OJPECMNINNA} target={__instance.Pointer} requested={__0} result={__result} hp={__instance.MKEIPLCMANH}");
    }
    static string DescribeTargets(Fighters list){
        var rows=new System.Collections.Generic.List<string>();
        foreach(var t in list){if(rows.Count>=64)break;rows.Add($"{t.GetName()}:{t.GetAttribute().Pointer}@{t.GetTransform().position}");}
        return string.Join(";",rows);
    }
}
