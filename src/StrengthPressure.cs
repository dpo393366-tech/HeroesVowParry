using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace Vow.Counters;

// Relative matchup estimate, not a win probability. Never invokes AI selection,
// damage, RNG, buff execution or card draws to simulate a fight.
internal static class StrengthPressure
{
    internal static ConfigEntry<float> maxSpeed,minWindow;
    internal sealed class State {
        internal float pressure,speed=1,window;
        internal IntPtr attacker,defender;
        internal Animator animator;internal float original,applied;
        internal bool owns;internal int clipIndex=-1;internal float lastContact=-1;
    }
    sealed class Profile {
        internal double attack,defence,efficiency=1,souls,quality=1,intellect,debateSoul;
        internal bool martial;
        internal IntPtr hero;
    }
    static readonly Dictionary<(bool,IntPtr),Profile> profiles=new();
    static readonly HashSet<string> seen=new();
    static readonly Dictionary<(bool,IntPtr,IntPtr),(float pressure,float speed,float window)> frozen=new();
    // Incoming attacker -> enemy defender: the reverse of the speed matchup.
    static readonly Dictionary<(bool,IntPtr,IntPtr),float> frozenEnemyOdds=new();
    static float fightWindow,debateWindow;
    static bool fightFrozen,debateFrozen;
    static double nextError;static int logs;
    internal static void Install(DuelCounters mod,Harmony h){
        maxSpeed=mod.Config.Bind("StrengthPressure","MaximumAttackSpeed",1.2f,new ConfigDescription("Relative multiplier at overwhelming matchup strength. Intermediate opponents interpolate from 1. Only attack animation through last contact is changed. 1 disables acceleration.",new AcceptableValueRange<float>(1,2)));
        minWindow=mod.Config.Bind("StrengthPressure","MinimumParrySeconds",.25f,new ConfigDescription("Real seconds after cue at overwhelming strength. Intermediate opponents interpolate from the regular cue window. Cannot exceed that regular window; equal values disable shortening.",new AcceptableValueRange<float>(.05f,2)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetRoundAndHero"),prefix:new HarmonyMethod(typeof(StrengthPressure),nameof(FreezeFight)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetPhase"),prefix:new HarmonyMethod(typeof(StrengthPressure),nameof(FreezeFight)));
        h.Patch(AccessTools.DeclaredMethod(typeof(De_Controller),"SetPhase"),prefix:new HarmonyMethod(typeof(StrengthPressure),nameof(FreezeDebate)));
        Panel.Hook(h,typeof(Fi_Controller),"StartBattle",typeof(StrengthPressure),before:nameof(Reset));
        Panel.Hook(h,typeof(De_Controller),"StartDebate",typeof(StrengthPressure),before:nameof(Reset));

    }
    internal static float Pressure(double ratio){
        if(!double.IsFinite(ratio)||ratio<=1.1)return 0;
        if(ratio<1.3)return (float)((ratio-1.1)/.2*.3);
        if(ratio<1.7)return .3f+(float)((ratio-1.3)/.4*.3);
        return Math.Clamp(.6f+(float)((ratio-1.7)/.8*.4),0,1);
    }
    internal static (float speed,float window) Limits(float pressure,float maximum,float minimum,float normal){
        pressure=Math.Clamp(pressure,0,1);maximum=Math.Clamp(maximum,1,2);normal=Math.Clamp(normal,.05f,2);
        minimum=Math.Clamp(minimum,.05f,normal);
        return (1+(maximum-1)*pressure,normal+(minimum-normal)*pressure);
    }
    static double Ratio(double a,double b)=>Math.Clamp((1+a)/(1+b),.05,20);
    static double Compare(Profile a,Profile b){
        return Vow.Shared.StrengthMetric.Compare(Score(a),Score(b));
    }
    static double Score(Profile p)=>p.martial
        ?Vow.Shared.StrengthMetric.MartialScore(p.attack,p.defence,p.efficiency,p.souls,p.debateSoul,p.quality,p.intellect)
        :Vow.Shared.StrengthMetric.Score(p.attack,p.defence,p.efficiency,p.souls,p.quality);
    internal static float ChanceFromRatio(double defenderOverAttacker,float baseline,float ceiling){
        if(!double.IsFinite(defenderOverAttacker)||defenderOverAttacker<=0)defenderOverAttacker=1;
        // Retain the old +/-60 percentage-point response and configured cap,
        // replacing its stat/three-soul sum with the shared composite rating.
        double relative=(defenderOverAttacker-1)/(defenderOverAttacker+1);
        return (float)Math.Clamp(baseline+60*relative,0,Math.Clamp(ceiling,0,100))*.01f;
    }
    internal static float EnemyOdds(bool debate,IntPtr incoming,IntPtr defender){
        if(frozenEnemyOdds.TryGetValue((debate,incoming,defender),out float odds))return odds;
        // Unknown/late actors get neutral odds, never a mid-battle rerating or
        // a fallback to the obsolete HP/three-soul/temporary-stat calculation.
        return ChanceFromRatio(1,DuelCounters.NeutralAutoPercent,DuelCounters.EnemyAutoCeiling);
    }
    // Native soul array order: war=0, martial=1, debate=2. Read the raw
    // relevant soul only; do not invoke the native martial-to-debate override.
    static double Soul(CLAGKDECFID hero,bool debate){
        var p=hero?.LMPMNLLGOEC?.GKAPBPDPDJI;int index=debate?2:1;
        return p==null||p.Length<=index?0:Math.Max(0,(double)p[index]);
    }
    static void Quality(Profile p,EIFBNDCHLML s,bool debate){
        if(s==null)return;
        var active=debate?s.HLIIGOFFOMG:s.IDPNHIGMEPD;var passive=debate?s.ODHEOJDJMIH:s.EJDJDDPLJFG;
        double sum=0;int count=0;
        if(active!=null)for(int i=0;i<Math.Min(64,active.Length);i++){
            var id=active[i];if(string.IsNullOrEmpty(id))continue;
            if(debate){if(AEHAGJAKNEK.HNKEGCKHIHN.TryGetValue(id,out var c)){sum+=1+(int)c.HDMOEKGEPFE;count++;}}
            else if(AEHAGJAKNEK.KOANIABOBNM.TryGetValue(id,out var c)){sum+=1+(int)c.HDMOEKGEPFE;count++;}
        }
        if(passive!=null)for(int i=0;i<Math.Min(64,passive.Length);i++){
            var id=passive[i];if(string.IsNullOrEmpty(id))continue;
            if(debate){if(AEHAGJAKNEK.CEFOHJIHNLC.TryGetValue(id,out var c)){sum+=1+(int)c.HDMOEKGEPFE;count++;}}
            else if(AEHAGJAKNEK.GDENCMFPPOE.TryGetValue(id,out var c)){sum+=1+(int)c.HDMOEKGEPFE;count++;}
        }
        p.quality=count>0?sum/count:1;
    }
    static Profile FightProfile(GKHLLOOEEFN actor){
        var attr=actor.GetAttribute();var hero=actor.GetHero();var key=(false,attr.Pointer);
        if(profiles.TryGetValue(key,out var old)&&old.hero==(hero?.Pointer??IntPtr.Zero))return old;
        var p=new Profile{hero=hero?.Pointer??IntPtr.Zero,attack=Math.Max(1,attr.MIPCJKHEHFO(false)),defence=Math.Max(0,attr.FCJJDMAMNHA(false)),souls=Soul(hero,false),debateSoul=Soul(hero,true),intellect=Math.Max(0,actor.GetWise()),martial=true};
        double total=0;int count=0;seen.Clear();
        void Card(KJFOPOCBHBM c){
            if(c==null||c.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack||!seen.Add(c.OJPECMNINNA)||count>=64)return;
            int cost=Math.Max(1,attr.HIMGMJJKFFL(c,false));
            // Authored card coefficient. Do not execute custom skill scripts or
            // sample current target buffs merely to evaluate a character.
            double power=c.MJKIDEMJEOG;if(!double.IsFinite(power)||power<=0)return;
            total+=power/cost;count++;
        }
        var slots=hero?.LMPMNLLGOEC?.IDPNHIGMEPD;
        if(slots!=null)for(int i=0;i<Math.Min(64,slots.Length);i++)if(!string.IsNullOrEmpty(slots[i])&&AEHAGJAKNEK.KOANIABOBNM.TryGetValue(slots[i],out var c))Card(c);
        // No current-hand sampling: draw RNG must not change the prebattle rating.
        // Beasts without equipment use their native attack/defence baseline.
        if(count>0)p.efficiency=Math.Clamp(total/count*Math.Max(1,Math.Min(actor.GetBattleAPRecovery(),actor.GetBattleDrawCardNum()*2)),.1,20);
        Quality(p,hero?.LMPMNLLGOEC,false);profiles[key]=p;return p;
    }
    static Profile DebateProfile(NPCCtrl actor){
        var a=actor.m_debateAttribute;var hero=actor.GetHero();var key=(true,a.Pointer);
        if(profiles.TryGetValue(key,out var old)&&old.hero==(hero?.Pointer??IntPtr.Zero))return old;
        var p=new Profile{hero=hero?.Pointer??IntPtr.Zero,attack=Math.Max(1,a.MIPCJKHEHFO(false)),defence=Math.Max(0,a.FCJJDMAMNHA(false)),souls=Soul(hero,true)};
        var slots=hero?.LMPMNLLGOEC?.HLIIGOFFOMG;double power=0;int count=0;
        if(slots!=null)for(int i=0;i<Math.Min(64,slots.Length);i++)if(!string.IsNullOrEmpty(slots[i])&&AEHAGJAKNEK.HNKEGCKHIHN.TryGetValue(slots[i],out var c)&&c.HFBAJIGHOAF==MMGEDKCCMHH.GANKBONOKGD.Attack&&float.IsFinite(c.MJKIDEMJEOG)&&c.MJKIDEMJEOG>0){power+=c.MJKIDEMJEOG/Math.Max(1,a.HIMGMJJKFFL(c,false));count++;}
        if(count>0)p.efficiency=Math.Clamp(power/count*Math.Max(1,a.NPEECFFDLAF(false)),.1,20);
        Quality(p,hero?.LMPMNLLGOEC,true);profiles[key]=p;return p;
    }
    static void FreezeFight(){Freeze(false);}
    static void FreezeDebate(De_Controller.MOLBMLDLJEP __0){
        if(__0==De_Controller.MOLBMLDLJEP.begin||__0==De_Controller.MOLBMLDLJEP.draw||__0==De_Controller.MOLBMLDLJEP.play)Freeze(true);
    }
    // Run before the first native action selection, not when cards/hits execute.
    // No rerating for HP loss, buffs, groggy, drawn cards, round or option changes.
    static void Freeze(bool debate){
        if(debate?debateFrozen:fightFrozen)return;
        if(!PlayerBattleRules.Participating(debate))return;
        try{
            var actors=new List<(IntPtr id,FMHEOPMKFJB side,Profile profile)>();
            if(debate){
                var roster=De_Controller.m_listHero;if(roster==null)return;
                foreach(var actor in roster)if(actor&&actor.m_debateAttribute!=null)
                    actors.Add((actor.m_debateAttribute.Pointer,actor.m_debateAttribute.IBLKPJDLABN,DebateProfile(actor)));
                if(actors.Count==0)return;
                debateWindow=DuelCounters.CueWindow;
                debateFrozen=true;
            }else{
                var roster=Fi_Controller.m_listHero;if(roster==null)return;
                foreach(var actor in roster)if(actor!=null&&actor.GetAttribute()!=null)
                    actors.Add((actor.GetAttribute().Pointer,actor.GetAttribute().IBLKPJDLABN,FightProfile(actor)));
                if(actors.Count==0)return;
                fightWindow=DuelCounters.CueWindow;
                fightFrozen=true;
            }
            // Build opposed enemy/defender pairs once. Controlling a different
            // allied officer never substitutes the protagonist's rating.
            foreach(var attacker in actors)if(attacker.side==FMHEOPMKFJB.Enemy)
                foreach(var defender in actors)if(DuelCounters.Opposed(attacker.side,defender.side))
                    Store(debate,attacker.id,defender.id,attacker.profile,defender.profile);
        }catch(Exception ex){
            // A failed preflight must not fall back to mid-combat resampling.
            if(debate)debateFrozen=true;else fightFrozen=true;
            if(Time.unscaledTimeAsDouble>=nextError){nextError=Time.unscaledTimeAsDouble+30;DuelCounters.Warn("전투 전 강적 평가 보류: "+ex.Message);}
        }
    }
    static void Store(bool debate,IntPtr id,IntPtr target,Profile attacker,Profile defender){
        double ratio=Compare(attacker,defender);float pressure=Pressure(ratio);
        var limit=Limits(pressure,maxSpeed.Value,minWindow.Value,DuelCounters.CueWindow);
        frozen[(debate,id,target)]=(pressure,limit.speed,limit.window);
        float odds=ChanceFromRatio(ratio,DuelCounters.NeutralAutoPercent,DuelCounters.EnemyAutoCeiling);
        frozenEnemyOdds[(debate,target,id)]=odds;
        if(logs++<64)DuelCounters.StabilityNote($"strength prebattle frozen enemy={id} defender={target} debate={debate} ratio={ratio:F3} pressure={pressure:F3} speed={limit.speed:F3} cueSeconds={limit.window:F3} enemyAutoParry={odds:P1} souls={attacker.souls:F0}/{defender.souls:F0} quality={attacker.quality:F2}/{defender.quality:F2} intellect={attacker.intellect:F0}/{defender.intellect:F0} debateSoul={attacker.debateSoul:F0}/{defender.debateSoul:F0}");
    }
    static float BaseWindow(bool debate)=>(debate?debateWindow:fightWindow)>0?(debate?debateWindow:fightWindow):DuelCounters.CueWindow;
    static IntPtr Attacker(ImpactTiming.Attack a)=>a.pressure?.attacker??(a.debate?a.debater.m_debateAttribute.Pointer:a.fighter.GetAttribute().Pointer);
    static IntPtr PrimaryTarget(ImpactTiming.Attack a){
        if(a.debate){
            var targets=a.speech.m_listTarget;
            if(targets!=null)foreach(var t in targets)if(t&&DuelCounters.Opposed(a.debater.m_debateAttribute.IBLKPJDLABN,t.m_debateAttribute.IBLKPJDLABN))return t.m_debateAttribute.Pointer;
        }else{
            var targets=Fi_Controller.m_listTargetHero;
            if(targets!=null)foreach(var t in targets)if(t!=null&&DuelCounters.Opposed(a.fighter.GetAttribute().IBLKPJDLABN,t.GetAttribute().IBLKPJDLABN))return t.GetAttribute().Pointer;
        }
        return IntPtr.Zero;
    }
    internal static void Prepare(ImpactTiming.Attack a){
        if(a==null||a.pressure!=null||!a.animationLoaded||!a.Valid())return;
        // Only copy the precomputed result. No stats/gear/HP/hand reads here.
        bool attack=a.debate?a.debateCard?.HFBAJIGHOAF==MMGEDKCCMHH.GANKBONOKGD.Attack:a.fightCard?.HFBAJIGHOAF==KJFOPOCBHBM.CCENIDMDCCN.Attack;
        if(!attack)return;
        var target=PrimaryTarget(a);if(target==IntPtr.Zero)return;
        var s=a.pressure=new State{window=BaseWindow(a.debate),attacker=Attacker(a),defender=target};
        if(frozen.TryGetValue((a.debate,s.attacker,target),out var result)){s.pressure=result.pressure;s.speed=result.speed;s.window=result.window;}
    }
    // Shared cue timestamps use the widest base window. Every actual defender
    // is narrowed independently, including accepted input and late settlement.
    internal static float Window(ImpactTiming.Attack a)=>a==null?DuelCounters.CueWindow:BaseWindow(a.debate);
    internal static float Window(ImpactTiming.Attack a,IntPtr victim){
        if(a!=null&&frozen.TryGetValue((a.debate,Attacker(a),victim),out var result))return result.window;
        return Window(a);
    }
    internal static float RepeatWindow(ImpactTiming.Attack a,IntPtr victim,float seconds){
        float speed=1;
        if(a!=null&&frozen.TryGetValue((a.debate,Attacker(a),victim),out var result))speed=result.speed;
        return seconds/(float.IsFinite(speed)?Math.Clamp(speed,1,2):1);
    }
    internal static void Motion(ImpactTiming.Attack a){
        if(a==null)return;Prepare(a);var s=a.pressure;if(s==null||s.speed<=1)return;
        if(!a.Valid()||a.cancelled||a.motionFinished||(a.quickBasic&&a.quickContactPassed)||EnemyGroggy.IsStunned(a.debate,a.debate?a.debater.m_debateAttribute.Pointer:a.fighter.GetAttribute().Pointer)){Release(a);return;}
        int index=a.debate?a.speech.m_curAnimationIndex:a.fight.m_curAnimationIndex;
        if(index!=s.clipIndex){
            Release(a);s.clipIndex=index;s.lastContact=-1;
            if(a.debate){
                var events=a.speech.m_debateAnimation?.DGBOCHKBFJG;
                if(events!=null)for(int i=0;i<Math.Min(4096,events.Count);i++){var e=events[i];if(e!=null&&e.EBHFFNHELFB&&float.IsFinite(e.DHBBBOOGECL))s.lastContact=Math.Max(s.lastContact,e.DHBBBOOGECL);}
            }else{
                var events=index>=0&&index<(a.fight.m_listBattleAnimation?.Count??0)?a.fight.m_listBattleAnimation[index]?.DGBOCHKBFJG:null;
                if(events!=null)for(int i=0;i<Math.Min(4096,events.Count);i++){var e=events[i];if(e!=null&&(e.EBHFFNHELFB||e.GHEGCDIKHDI)&&float.IsFinite(e.DHBBBOOGECL))s.lastContact=Math.Max(s.lastContact,e.DHBBBOOGECL);}
            }
        }
        var animator=a.debate?a.debater.GetAnimator():a.fighter.GetAnimator();if(!animator||s.lastContact<0){Release(a);return;}
        string expected=a.debate||(index&1)==0?"Attack1":"Attack2";
        bool attack=false;
        for(int i=0;i<Math.Min(8,animator.layerCount);i++){var st=animator.GetCurrentAnimatorStateInfo(i);if(!animator.IsInTransition(i)&&st.IsName(expected)&&st.normalizedTime<s.lastContact){attack=true;break;}}
        if(!attack){Release(a);return;}
        if(!s.owns&&animator.speed>0){s.animator=animator;s.original=animator.speed;s.applied=s.original*s.speed;s.owns=true;animator.speed=s.applied;}
        // Never overwrite another controller's pause/knockback/groggy ownership.
    }
    internal static void Release(ImpactTiming.Attack a){var s=a?.pressure;if(s?.owns!=true)return;s.owns=false;if(s.animator&&Mathf.Approximately(s.animator.speed,s.applied))s.animator.speed=s.original;}
    internal static void Reset(){Release(ImpactTiming.Current(false));Release(ImpactTiming.Current(true));profiles.Clear();frozen.Clear();frozenEnemyOdds.Clear();fightFrozen=debateFrozen=false;fightWindow=debateWindow=0;logs=0;}
}
