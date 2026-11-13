using System;
using System.Collections.Generic;
using Debaters=Il2CppSystem.Collections.Generic.List<NPCCtrl>;
namespace Vow.Counters;

internal static partial class CombatTargetRecovery {
    // Only a failed native choice can enter this recovery path. The native
    // candidate map still owns card eligibility, including status restrictions.
    static void RepairDebate(LEDJAIBAAJC a,ref MMGEDKCCMHH chosen,ref Debaters targets,ref float best){
        var f=De_Controller.EBACHLGLNGD;var actor=a.AFFCOKCHGKM;
        if(f==null||actor==null)return;int rebuilt=0;
        foreach(var row in a.PDMICGPKJKB){
            var card=row.Key;var plan=row.Value;
            if(card==null||plan==null||card.HFBAJIGHOAF!=MMGEDKCCMHH.GANKBONOKGD.Attack||
                a.EFDDHIBLEHF?.Contains(card)!=true||a.HIMGMJJKFFL(card,false)>a.IHOPPBMEAEN||
                !float.IsFinite(plan.DHLOPCACLKO)||plan.DHLOPCACLKO<0||plan.NOJLEOEGACM?.Count>0)continue;
            if(++rebuilt>8)break;
            // Identical target helper and grouping call used by the game's
            // KONOJDMOGNE selector. Never add arbitrary roster members.
            var legal=f.TargetListHelper(actor,card);if(legal==null||legal.Count==0)continue;
            float score=plan.DHLOPCACLKO;
            plan.LMINBCCJCIE(a.FMPMFGLKNJE(card),legal);
            if(plan.NOJLEOEGACM==null)continue;
            foreach(var group in plan.NOJLEOEGACM){
                if(!Valid(group,a.IBLKPJDLABN))continue;
                float value=Math.Max(.01f,score)*GroupValue(group);
                if(value<=best)continue;
                chosen=card;targets=group;best=value;
            }
        }
    }
    static void DebateSupport(LEDJAIBAAJC a,ref MMGEDKCCMHH chosen,ref Debaters targets,ref float best){
        foreach(var row in a.PDMICGPKJKB){
            var card=row.Key;var plan=row.Value;
            if(card==null||plan?.NOJLEOEGACM==null||card.HFBAJIGHOAF==MMGEDKCCMHH.GANKBONOKGD.Attack||
                !float.IsFinite(plan.DHLOPCACLKO)||plan.DHLOPCACLKO<=best||
                a.EFDDHIBLEHF?.Contains(card)!=true||a.HIMGMJJKFFL(card,false)>a.IHOPPBMEAEN)continue;
            foreach(var group in plan.NOJLEOEGACM){
                if(group==null||group.Count==0)continue;
                bool alive=true;foreach(var t in group)if(t==null||t.m_debateAttribute==null||t.m_debateAttribute.MKEIPLCMANH<=0){alive=false;break;}
                if(!alive)continue;
                chosen=card;targets=group;best=plan.DHLOPCACLKO;break;
            }
        }
    }
}
