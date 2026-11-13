using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Fighters=Il2CppSystem.Collections.Generic.List<GKHLLOOEEFN>;
namespace Vow.Counters;

internal static partial class CombatTargetRecovery {
    // A positive native support score is required. Existing buffs/recovery with
    // no benefit must not be looped just to make the actor appear active.
    static void UsefulSupport(NFIDHHGKMDC a,ref KJFOPOCBHBM chosen,ref Fighters targets,ref Vector3 point,ref float best){
        foreach(var row in a.PDMICGPKJKB){var c=row.Key;var p=row.Value;
            if(c==null||p?.JLGPKHPMEFF==null||c.HFBAJIGHOAF==KJFOPOCBHBM.CCENIDMDCCN.Attack||
               !float.IsFinite(p.DHLOPCACLKO)||p.DHLOPCACLKO<=0||a.EFDDHIBLEHF?.Contains(c)!=true||
               a.HIMGMJJKFFL(c,false)>a.IHOPPBMEAEN||a.FMBHELDBDJP(c,false)>a.BCAIBCEODLJ)continue;
            foreach(var g in p.JLGPKHPMEFF){
                if(!Finite(g.Key)||g.Value==null||g.Value.Count==0)continue;
                bool alive=true;foreach(var t in g.Value)if(t?.GetAttribute()==null||t.GetAttribute().MKEIPLCMANH<=0){alive=false;break;}
                if(!alive||p.DHLOPCACLKO<=best)continue;
                chosen=c;targets=g.Value;point=g.Key;best=p.DHLOPCACLKO;
            }
        }
    }
    // Called only when the selector returned null, only for cards that survived
    // its native hand/cost/status eligibility filtering. Never widen range or
    // move a transform to turn an unreachable attack into a legal one.
    static void RepairAttack(NFIDHHGKMDC a,ref KJFOPOCBHBM chosen,ref Fighters targets,ref Vector3 point,ref float best){
        var actor=a.AFFCOKCHGKM;var f=Fi_Controller.EBACHLGLNGD;
        if(f==null||actor==null)return;int rebuilt=0;
        foreach(var row in a.PDMICGPKJKB){
            var card=row.Key;var old=row.Value;
            if(card==null||old==null||card.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack||
               !float.IsFinite(old.DHLOPCACLKO)||old.DHLOPCACLKO<0||a.EFDDHIBLEHF?.Contains(card)!=true||
               a.HIMGMJJKFFL(card,false)>a.IHOPPBMEAEN||a.FMBHELDBDJP(card,false)>a.BCAIBCEODLJ)continue;
            if(old.JLGPKHPMEFF?.Count>0)continue;
            if(++rebuilt>32)break;
            var legal=f.TargetListHelper(actor,a.BDFEGCOILNE(card,true),card.LBLIAONIBOP,card.OPBADODCHOD,true,true);
            if(legal==null||legal.Count==0)continue;
            FNNCDIHJBEA plan=null;
            try{
                plan=FNNCDIHJBEA.OCELGGJBGEN();
                switch(card.CPIJBMOGBLA){
                    case KJFOPOCBHBM.JNDKGNOEKDN.Target:plan.NJLGONIPOHM(legal);break;
                    case KJFOPOCBHBM.JNDKGNOEKDN.Circle:plan.ICGKBKIGNEL(card,legal,legal);break;
                    case KJFOPOCBHBM.JNDKGNOEKDN.Fan:plan.AGEHKPGJELA(actor,card,legal,legal);break;
                    case KJFOPOCBHBM.JNDKGNOEKDN.Line:
                    case KJFOPOCBHBM.JNDKGNOEKDN.TargetLine:plan.AMMJGMGFFHH(actor,card,legal,legal);break;
                    default:continue;
                }
                if(plan.JLGPKHPMEFF==null)continue;
                foreach(var group in plan.JLGPKHPMEFF){
                    if(!Finite(group.Key)||!Valid(group.Value,a.IBLKPJDLABN))continue;
                    float value=Math.Max(.01f,old.DHLOPCACLKO)*GroupValue(group.Value,actor);
                    if(value<=best)continue;
                    var copy=new Fighters();foreach(var t in group.Value)if(!copy.Contains(t))copy.Add(t);
                    chosen=card;targets=copy;point=group.Key;best=value;
                }
            }finally{plan?.Recycle();}
        }
    }
}
