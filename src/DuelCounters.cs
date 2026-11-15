using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using UnityEngine;
using NativeRoutine=Il2CppSystem.Collections.IEnumerator;
namespace Vow.Counters;

[BepInPlugin("local.heroesvow.counters","Heroes Vow Parry","2.0.0")]
public sealed class DuelCounters : BasePlugin
{
    static DuelCounters mod;
    static ConfigEntry<float> window,baseline,ceiling,playerCeiling,allyCeiling,enemyCeiling,cooldown,comboBudget,timingWindow,postWindow,guardWindow,attackLead,timingAdvance,fastAttackSpeed,quickBasicSpeed,minSingleLead,warningLead,successCooldown,cueWindow,earlyCueWindow;
    static ConfigEntry<bool> flash,sound,recoverHalfAP,perStrike,noDelay,visibilityTrace,quickBasic,antiSpam,cueInput;
    static ConfigEntry<float> rangedGrace;
    internal static float RangedGrace=>Mathf.Clamp(rangedGrace?.Value??.1f,0,.5f);
    internal static bool PerStrike=>perStrike?.Value==true;
    internal static bool GuardPressed=>Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Q);
    internal static bool NoDelay=>noDelay?.Value==true;
    internal static bool CueInput=>cueInput?.Value==true;
    internal static float EarlyCueWindow=>earlyCueWindow?.Value??.2f;
    internal static float CueWindow=>cueWindow?.Value??.35f;
    internal static float WarningLead=>warningLead?.Value??0f;
    internal static bool AntiSpam=>antiSpam?.Value==true;
    internal static bool QuickBasic=>quickBasic?.Value==true;
    internal static float MinSingleLead=>minSingleLead?.Value??.35f;
    internal static float QuickBasicSpeed=>quickBasicSpeed?.Value??.8f;
    internal static float FastAttackSpeed=>fastAttackSpeed?.Value??1.5f;
    internal static bool VisibilityTrace=>visibilityTrace?.Value==true;
    internal static float TimingAdvance=>timingAdvance?.Value??.2f;
    internal static float TimingWindow=>timingWindow?.Value??.25f;
    internal static float PostWindow=>postWindow?.Value??.25f;
    internal static float GuardWindow=>guardWindow?.Value??.25f;
    internal static float AttackLead=>attackLead?.Value??0f;
    internal static float FailedCooldown=>cooldown?.Value??.6f;
    internal static float SuccessCooldown=>successCooldown?.Value??0f;
    internal static bool FlashEnabled=>flash?.Value==true;
    internal static bool SoundEnabled=>sound?.Value==true;
    static ConfigEntry<bool> verboseTrace;
    internal static bool VerboseTrace=>verboseTrace?.Value==true;
    static int generation,openedFrame,winningFrame=-1;
    static double readyAt,contactUntil;
    static bool parryPress;
    static Action manualGuard;
    static Func<bool> live;
    static CounterGlow glow;
    internal static void Warn(string text){mod.Log.LogWarning(text);StabilityNote(text);}
    internal static void StabilityNote(string text){
#if DuelCounters
        // Keep recoveries available even when the user's general BepInEx log is off.
        try{
            string path=System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,"DuelCounters-stability.log");
            
            CombatLog.Write(path,524288,DateTime.Now.ToString("s")+" "+text+Environment.NewLine);
        }catch{/* Logging must never block combat recovery. */}
#endif
    }
    static void Effect(Action play){try{play();}catch(Exception e){Warn(e.Message);}}
    // An early press is a miss only during an attack actually aimed at the player.
    // Unrelated actions must not animate the distant player or consume their cooldown.
    static Action IncomingGuard(){
        if(Fi_Controller.m_isInBattle && !Fi_Controller.m_isEnd && Fi_Controller.m_isLaunchingAttack){
            var a=ImpactTiming.Current(false);var actor=a?.fighter??Fi_Controller.m_curHero;
            var card=a?.fightCard??Fi_Controller.m_curCard;
            if(actor==null || actor.GetAttribute().IBLKPJDLABN!=FMHEOPMKFJB.Enemy ||
               card==null || card.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack ||
               (a!=null && (!a.Valid()||(a.started&&!PerStrike)||a.cancelled)))return null;
            var targets=Fi_Controller.m_listTargetHero;if(targets==null)return null;
            foreach(var target in targets)if(target!=null && PlayerBattleRules.Manual(false,target.GetHero(),target.GetAttribute().IBLKPJDLABN) && Opposed(FMHEOPMKFJB.Enemy,target.GetAttribute().IBLKPJDLABN))
                return ()=>CounterEffects.Guard(target);
        }else if(De_Controller.m_isInDebate && De_Controller.m_isLaunchingAttack){
            var debate=De_Controller.EBACHLGLNGD;var a=ImpactTiming.Current(true);var actor=a?.debater??De_Controller.m_curHero;
            var card=a?.debateCard??De_Controller.m_curCard;
            if(!debate || debate.m_isEnd || !actor || actor.m_debateAttribute.IBLKPJDLABN!=FMHEOPMKFJB.Enemy ||
               card==null || card.HFBAJIGHOAF!=MMGEDKCCMHH.GANKBONOKGD.Attack ||
               (a!=null && (!a.Valid()||(a.started&&!PerStrike)||a.cancelled)))return null;
            var targets=debate.m_listTarget;if(targets==null)return null;
            foreach(var target in targets)if(target && PlayerBattleRules.Manual(true,target.GetHero(),target.m_debateAttribute.IBLKPJDLABN) && Opposed(FMHEOPMKFJB.Enemy,target.m_debateAttribute.IBLKPJDLABN))
                return ()=>CounterEffects.Rebut(target);
        }
        return null;
    }
    // A held button is not a new attempt.
    internal static void PollInput(){
        GuardInfoInput.Observe();
        if(NoDelay){RealtimeParry.PollInput();return;}
        if(!(Fi_Controller.m_isInBattle?PlayerBattleRules.ManualCombat(false):De_Controller.m_isInDebate&&PlayerBattleRules.ManualCombat(true)))return;
        if(!Panel.InputAllowed||!Application.isFocused||Time.timeScale<=0||!GuardPressed)return;
        if(!Fi_Controller.m_isInBattle && !De_Controller.m_isInDebate && manualGuard==null)return;
        bool windowLive=manualGuard!=null && live?.Invoke()==true;
        var early=windowLive?null:IncomingGuard();
        parryPress=windowLive;
        if(!windowLive && early==null)return;
        double now=Time.unscaledTimeAsDouble;
        if(now<readyAt||winningFrame>=0||(manualGuard==null && now<contactUntil))return;
        if(windowLive && Time.frameCount>openedFrame && winningFrame<0){
            winningFrame=Time.frameCount;Effect(manualGuard);return;
        }
        readyAt=now+cooldown.Value;
        Effect(windowLive?manualGuard:early);
    }
    static bool AllowInfo(){
        if(GuardInfoInput.Suppress())return false;
        if(NoDelay)return !RealtimeParry.SuppressInfo();
        // OpenUI is invoked on mouse-down in the world and also on release by portraits.
        bool active=manualGuard!=null && live?.Invoke()==true;
        if(Input.GetMouseButtonDown(1))parryPress=active;
        return !((active || parryPress) &&
            (Input.GetMouseButtonDown(1)||Input.GetMouseButton(1)||Input.GetMouseButtonUp(1)));
    }
    public override void Load(){
        mod=this;
        Panel.Install(this,"T","무투 · 설전",0,Settings);
        try{LoadCombat();}
        catch(Exception ex){
            new Harmony("local.heroesvow.counters").UnpatchSelf();
            Warn("패링 게임 연결 중단: "+ex);
            ModControl.InitializeUnavailable(this,ex);
        }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    void LoadCombat(){
        mod=this;
        // The simultaneous scheduler was removed. Retire its old saved flag
        // without resetting any active timing, balance or audio settings.
        Config.Bind("SimultaneousCombat","Enabled",false,"Retired: martial and debate use native sequential turns.").Value=false;
        noDelay=Config.Bind("Counter","RealtimeTiming",true,"Contact-based counters. Attack motions continue at the configured speed; only damage settlement waits for the adjustable late-input window.");
        fastAttackSpeed=Config.Bind("Counter","FastAttackSpeed",1.5f,new ConfigDescription("Attack playback cap while the game is accelerated; normal 1x playback stays 1x.",new AcceptableValueRange<float>(1f,2f)));
        quickBasic=Config.Bind("Counter","SlowQuickBasicAttacks",true,"Slow incoming single-contact attack motions until contact, regardless of card rarity or animation state. Restore the user game speed immediately for recovery. Only actual manual-parry targets; no per-hit waiting.");
        minSingleLead=Config.Bind("Counter","MinimumSingleWindupSeconds",.35f,new ConfigDescription("Minimum real playback time of a single-contact attack from its authored animation start to contact; no extra wait and no slower recovery. Zero disables the floor.",new AcceptableValueRange<float>(0,1f)));
        quickBasicSpeed=Config.Bind("Counter","QuickBasicAttackSpeed",.8f,new ConfigDescription("Absolute single-contact attack playback cap before contact, at both game speeds; recovery uses original game speed.",new AcceptableValueRange<float>(.25f,1f)));
        visibilityTrace=Config.Bind("Diagnostics","ModelVisibilityTrace",false,"Read-only bounded visibility, bounds and animation state samples during martial battles; no model visibility changes.");
        cueInput=Config.Bind("Counter","CueAnchoredInput",true,"Manual parry uses each audible/visible cue as its input-window start. If contact arrives first, damage/effects settle only after that window, without pausing attack animation.");
        cueWindow=Config.Bind("Counter","CueInputWindowSeconds",.35f,new ConfigDescription("Right-click accepted from each warning for this many real seconds. One strike keeps its own immutable cue deadline; a later flash cannot reopen an earlier hit.",new AcceptableValueRange<float>(.05f,2f)));
        earlyCueWindow=Config.Bind("Counter","PreCueBufferSeconds",.2f,new ConfigDescription("Accept a guard pressed this many real seconds before the same attack cue; zero disables early buffering. Cooldowns and anti-spam still apply.",new AcceptableValueRange<float>(0,1f)));
        rangedGrace=Config.Bind("Counter","RangedParryGraceSeconds",.1f,new ConfigDescription("Archery attacks only: extend the cue deadline and guard retention by this many real seconds. Does not move the cue, slow the projectile or extend failed-input cooldowns.",new AcceptableValueRange<float>(0,.5f)));
        warningLead=Config.Bind("Counter","WarningToFirstContactSeconds",0f,new ConfigDescription("Desired cue lead before contact, real seconds. Never changes motion speed. If authored motion is shorter, cue is emitted at the earliest known point; the full cue input window is still honored.",new AcceptableValueRange<float>(0,2f)));
        antiSpam=Config.Bind("Counter","RejectRepeatedPendingGuard",true,"Mouse/Q share cooldown. Invalid pending repeated inputs fail; accepted cue inputs are committed per strike and cannot be revoked by another press.");
        timingAdvance=Config.Bind("Counter","TimingAdvanceSeconds",0f,new ConfigDescription("Move only the manual INPUT reference before actual contact. Warning lead is a separate setting.",new AcceptableValueRange<float>(0,2f)));
        timingWindow=Config.Bind("Counter","RealtimeTimingSeconds",.25f,new ConfigDescription("Accepted right-click interval before the shifted parry reference, real seconds; no added animation wait.",new AcceptableValueRange<float>(.05f,.6f)));
        postWindow=Config.Bind("Counter","PostContactSeconds",.25f,new ConfigDescription("Input accepted after the shifted reference; only any remainder beyond real contact defers damage and harmful effects.",new AcceptableValueRange<float>(0,.6f)));
        guardWindow=Config.Bind("Counter","GuardHoldSeconds",.25f,new ConfigDescription("One successful right-click covers following contacts in the same card within this interval from the input; never extended by another hit.",new AcceptableValueRange<float>(0,.6f)));
        attackLead=Config.Bind("Counter","AttackStartDelaySeconds",0f,new ConfigDescription("Once before an attacking card begins, never once per hit; real seconds.",new AcceptableValueRange<float>(0,1f)));
        perStrike=Config.Bind("Counter","PerStrikeParry",true,"Separate counters per authored contact. Aggregate skills settle only unblocked integer damage shares at their native final impact; no skill replay.");
        window=Config.Bind("Counter","WindowSeconds",.5f,new ConfigDescription("Real seconds",new AcceptableValueRange<float>(.2f,3f)));
        comboBudget=Config.Bind("Counter","ComboInputBudgetSeconds",1.2f,new ConfigDescription("Maximum added manual input wait across all strikes of one card, real seconds. Native hit/recoil animation time is separate.",new AcceptableValueRange<float>(.2f,3f)));
        cooldown=Config.Bind("Counter","FailedCooldownSeconds",.6f,new ConfigDescription("Cooldown after a missed right-click, real seconds",new AcceptableValueRange<float>(.1f,2f)));
        successCooldown=Config.Bind("Counter","SuccessfulCooldownSeconds",0f,new ConfigDescription("Optional cooldown after successful manual parry. Zero permits immediate next guard. Repeated contacts from the same press never extend it.",new AcceptableValueRange<float>(0,2f)));
        baseline=Config.Bind("Counter","BasePercent",10f,new ConfigDescription("Equal strength",new AcceptableValueRange<float>(0,30)));
        ceiling=Config.Bind("Counter","MaxPercent",10f,new ConfigDescription("AI upper bound",new AcceptableValueRange<float>(0,30)));
        playerCeiling=Config.Bind("Counter","PlayerMaxPercent",10f,new ConfigDescription("Protagonist automatic parry ceiling while AUTO is enabled. Manual input is unaffected.",new AcceptableValueRange<float>(0,100)));
        allyCeiling=Config.Bind("Counter","AllyMaxPercent",10f,new ConfigDescription("Other friendly officers automatic parry ceiling. Independent of protagonist and enemy settings.",new AcceptableValueRange<float>(0,100)));
        enemyCeiling=Config.Bind("Counter","EnemyMaxPercent",60f,new ConfigDescription("Enemy automatic parry ceiling, adjustable from 0 to 100 percent. Defaults to 60%; protagonist and ally caps are configured separately.",new AcceptableValueRange<float>(0,100)));
        flash=Config.Bind("Counter","YellowHighlight",true,"Flash beside an enemy immediately before damage, after attack animation starts");
        sound=Config.Bind("Counter","PlaySound",true,"Play the incoming attack cue and successful counter sound");
        recoverHalfAP=Config.Bind("Counter","RestoreHalfAPOnSuccess",true,"Gain 0.5 AP per successful parried hit on all defender sides. Native integer AP increases by 1 every two successes; credit resets at battle end/actor initialization and is discarded at cap.");
        recoverHalfAP.SettingChanged+=(_,__)=>ParryRecovery.Reset();
        // User-selected fixed behavior: controls removed from F11, mechanics retained.
        noDelay.Value=true;quickBasic.Value=true;cueInput.Value=true;antiSpam.Value=true;
        warningLead.Value=0;attackLead.Value=0;cooldown.Value=.6f;successCooldown.Value=0;
        verboseTrace=Config.Bind("Diagnostics","VerboseCombatTrace",false,"Detailed per-action trace for diagnosis only; enabling requires a restart. Recovery warnings remain available when disabled.");
        var h=new Harmony("local.heroesvow.counters");
        try{
            Panel.Hook(h,typeof(Fi_InfoUI),"OpenUI",GetType(),before:nameof(AllowInfo));
            Panel.Hook(h,typeof(De_InfoUI),"OpenUI",GetType(),before:nameof(AllowInfo));
            DiscardDisplay.Install(h);
            ImpactTiming.Install(h);
            Panel.Hook(h,typeof(Fi_Controller),"Update",typeof(ManualCardRefresh),after:"Tick");
            CombatTrace.Install(h);
            Panel.Hook(h,typeof(Fi_Controller),"QuitBattle",GetType(),before:nameof(Reset));
            Panel.Hook(h,typeof(De_Controller),"QuitDebate",GetType(),before:nameof(Reset));
            Panel.Hook(h,typeof(Fi_Controller),"EndBattle",GetType(),before:nameof(Reset));
            Panel.Hook(h,typeof(De_Controller),"EndDebate",GetType(),before:nameof(Reset));
            CombatTargetRecovery.Install(h);
            PlayerBattleRules.Install(this,h);
            MartialTurnFrequency.Install(this,h);
#if DuelCounters
            StrengthPressure.Install(this,h);
#endif
            EnemyGroggy.Install(this,h);
            Effect(CounterEffects.PrepareAudio);
        ParryVisuals.Install(this);
            Panel.Overlay=()=>{if(ModControl.Active){glow?.Marker();RealtimeParry.Marker();ParryVisuals.Draw();}};
#if DuelCounters
            CardLatency.Install(h);CombatCost.Install(h);
#endif
            ModControl.Initialize(this,h);
            StabilityNote("Loaded standalone Parry 2.0.0 (native draw restored): parry, groggy, timing, effects and combat recovery. Native sequential martial/debate; configured enemy attack/turn multipliers; no companion limit override. Aggregate damage still settles at native final contact.");
        }catch(Exception ex){h.UnpatchSelf();Warn("패링 초기화 중단: "+ex);ModControl.InitializeUnavailable(this,ex);}
    }
    static void Settings()=>StandaloneSettings.Draw();
    internal static void Suspend(){Reset();CounterGlow.Reset();TwoHandBrace.Clear();CounterEffects.StopAudio();}
    static Action restorePending;
    internal static void Release(){
        var restore=restorePending;restorePending=null;
        manualGuard=null;live=null;winningFrame=-1;
        try{if(restore!=null)Effect(restore);}finally{var old=glow;glow=null;if(old!=null)Effect(old.Dispose);}
    }
    static void Reset(){ParryVisuals.Reset();ParryRecovery.Reset();EnemyGroggy.Reset();generation++;readyAt=contactUntil=0;parryPress=false;ImpactTiming.Reset();Release();}
    static void SetFast(bool debate,bool value){
        if(debate){
            De_DebateCtrlUI.m_isAccelerate=value;
            if(De_DebateCtrlUI.m_debateCtrl)De_DebateCtrlUI.m_debateCtrl.DJFHAHDMADH();
        }else{
            Fi_BattleCtrlUI.m_isAccelerate=value;
            if(Fi_BattleCtrlUI.m_battleFieldCtrl)Fi_BattleCtrlUI.m_battleFieldCtrl.RefreshAccelerate();
        }
    }
    static Action CounterSpeed(bool debate,bool playerTarget){
        bool locked=debate?De_Controller.m_isLaunchingAttack:Fi_Controller.m_isLaunchingAttack;
        bool fast=debate?De_DebateCtrlUI.m_isAccelerate:Fi_BattleCtrlUI.m_isAccelerate;
        float scale=Time.timeScale;
        if(debate)De_Controller.m_isLaunchingAttack=true;else Fi_Controller.m_isLaunchingAttack=true;
        if(playerTarget && fast){
            Effect(()=>SetFast(debate,false));
        }
        bool scaled=playerTarget && Mathf.Abs(scale-2f)<.01f;
        if(scaled)Time.timeScale=1;
        return ()=>{
            if(playerTarget && fast)Effect(()=>SetFast(debate,true));
            if(scaled && Mathf.Approximately(Time.timeScale,1))Time.timeScale=scale;
            if(debate)De_Controller.m_isLaunchingAttack=locked;else Fi_Controller.m_isLaunchingAttack=locked;
        };
    }
    internal static bool Opposed(FMHEOPMKFJB a,FMHEOPMKFJB b){
        return a!=FMHEOPMKFJB.Neutral && b!=FMHEOPMKFJB.Neutral && (a==FMHEOPMKFJB.Enemy)!=(b==FMHEOPMKFJB.Enemy);
    }
    static float Souls(CLAGKDECFID h){
        var points=h?.LMPMNLLGOEC?.GKAPBPDPDJI;
        if(points==null||points.Length<3)return 0;
        return (float)points[0]+points[1]+points[2];
    }
    internal static float NeutralAutoPercent=>baseline.Value;
    internal static float EnemyAutoCeiling=>enemyCeiling.Value;
    static float Relative(float a,float b)=> (a-b)/Mathf.Max(1,a+b);
    static float Odds(float atk,float def,float aSoul,float dSoul,CLAGKDECFID defender)=>Mathf.Clamp(baseline.Value+60*(.6f*Relative(def,atk)+.4f*Relative(dSoul,aSoul)),0,Mathf.Clamp(IsPlayer(defender)?playerCeiling.Value:allyCeiling.Value,0,100))*.01f;
    static float Stat(CLAGKDECFID h,int primary,int will)=>.5f*primary+.25f*(h?.GGOIKBIJNCJ()??primary)+.25f*will;
    internal static bool IsPlayer(CLAGKDECFID h)=>h!=null && h.OJPECMNINNA==NEINKOOBJIH.POMNMBGDDIN?.OJPECMNINNA;
    internal static void Fight(Fi_Controller __instance,ref NativeRoutine __result){
        try{
            var action=ImpactTiming.Current(false);var actor=action?.fighter??Fi_Controller.m_curHero;var card=action?.fightCard??Fi_Controller.m_curCard;var targets=Fi_Controller.m_listTargetHero;
            if(!__instance||actor==null||card==null||targets==null||card.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack)return;
            var h=actor.GetHero();var side=actor.GetAttribute().IBLKPJDLABN;float best=-1;
            GKHLLOOEEFN victim=null;bool manual=false,knockback=false;
            foreach(var t in targets){
                if(t==null||EnemyGroggy.IsStunned(false,t.GetAttribute().Pointer)||!Opposed(side,t.GetAttribute().IBLKPJDLABN))continue;
                var dh=t.GetHero();float chance=t.GetAttribute().IBLKPJDLABN==FMHEOPMKFJB.Enemy?StrengthPressure.EnemyOdds(false,actor.GetAttribute().Pointer,t.GetAttribute().Pointer):Odds(Stat(h,actor.GetForce(),actor.GetWill()),Stat(dh,t.GetForce(),t.GetWill()),Souls(h),Souls(dh),dh);
                manual=PlayerBattleRules.Manual(false,dh,t.GetAttribute().IBLKPJDLABN);
                if(manual||chance>best){victim=t;best=chance;}if(manual)break;
            }
            if(victim==null)return;
            int order=__instance.m_curOrder;
            var original=__result;
            __result=ImpactTiming.Safe(Intercept(__result,manual,PlayerBattleRules.AutoRoll(false,victim.GetHero()),false,best,side==FMHEOPMKFJB.Enemy,()=>new CounterGlow(actor.GetTransform(),actor.GetListRenderer(),actor.GetHighlighter(),ParryVisuals.Cost(action)),()=>CounterEffects.Guard(victim),"패링",()=>__instance && !Fi_Controller.m_isEnd && actor.Pointer==Fi_Controller.m_curHero?.Pointer && order==__instance.m_curOrder && (action==null || action.Valid()) && (!manual || Fi_Controller.m_listTargetHero?.Contains(victim)==true),
                ()=>{Effect(()=>ParryVisuals.Success(action,victim.GetAttribute().Pointer,victim,null));EnemyGroggy.Record(action);ImpactTiming.Cancel(false,true);knockback=ParryStagger.Record(__instance.Pointer,__instance.AHDOJFHBBFJ,order,actor.Pointer);actor.GetAttribute().EFDDHIBLEHF?.Remove(card);if(recoverHalfAP.Value)Effect(()=>ParryRecovery.FightAP(victim));CombatTrace.Event("parry success; HP/MP recovery removed");},
                ()=>{if(action!=null)ImpactTiming.Finish(false);else ImpactTiming.ResumeFight(__instance);},
                ()=>{var icons=Fi_TopIcon.m_listTopIcon;if(icons!=null)foreach(var icon in icons)
                        if(icon && icon.m_npcCtrl?.Pointer==victim.Pointer){icon.ShowDamage(0);break;}
                    if(knockback)CounterEffects.Recoil(__instance,actor,victim);return knockback;},defenderSide:victim.GetAttribute().IBLKPJDLABN),original,action).WrapToIl2Cpp();
        }catch(Exception e){mod.Log.LogWarning(e);}
    }
    internal static void Debate(De_Controller __instance,ref NativeRoutine __result){
        try{
            var action=ImpactTiming.Current(true);var actor=action?.debater??De_Controller.m_curHero;var card=action?.debateCard??De_Controller.m_curCard;var targets=__instance.m_listTarget;
            if(!__instance||!actor||card==null||targets==null||card.HFBAJIGHOAF!=MMGEDKCCMHH.GANKBONOKGD.Attack)return;
            var h=actor.GetHero();var side=actor.m_debateAttribute.IBLKPJDLABN;float best=-1;NPCCtrl victim=null;bool manual=false;
            foreach(var t in targets){
                if(!t||EnemyGroggy.IsStunned(true,t.m_debateAttribute.Pointer)||!Opposed(side,t.m_debateAttribute.IBLKPJDLABN))continue;
                var dh=t.GetHero();float chance=t.m_debateAttribute.IBLKPJDLABN==FMHEOPMKFJB.Enemy?StrengthPressure.EnemyOdds(true,actor.m_debateAttribute.Pointer,t.m_debateAttribute.Pointer):Odds(Stat(h,actor.GetWise(),actor.GetWill()),Stat(dh,t.GetWise(),t.GetWill()),Souls(h),Souls(dh),dh);
                manual=PlayerBattleRules.Manual(true,dh,t.m_debateAttribute.IBLKPJDLABN);
                if(manual||chance>best){victim=t;best=chance;}if(manual)break;
            }
            if(!victim)return;
            int order=__instance.m_curOrder;
            var original=__result;
            __result=ImpactTiming.Safe(Intercept(__result,manual,PlayerBattleRules.AutoRoll(true,victim.GetHero()),true,best,side==FMHEOPMKFJB.Enemy,()=>new CounterGlow(actor.GetTransform(),actor.GetListRenderer(),actor.GetHighlighter(),ParryVisuals.Cost(action)),()=>CounterEffects.Rebut(victim),"반박",()=>__instance && !__instance.m_isEnd && actor==De_Controller.m_curHero && order==__instance.m_curOrder && (action==null || action.Valid()) && (!manual || __instance.m_listTarget?.Contains(victim)==true),
                ()=>{Effect(()=>ParryVisuals.Success(action,victim.m_debateAttribute.Pointer,null,victim));EnemyGroggy.Record(action);ImpactTiming.Cancel(true,true);actor.m_debateAttribute.EFDDHIBLEHF?.Remove(card);if(recoverHalfAP.Value)Effect(()=>ParryRecovery.DebateAP(victim));},
                ()=>{if(action!=null)ImpactTiming.Finish(true);else ImpactTiming.ResumeDebate(__instance);},
                ()=>{actor.StopSpeaking();return false;},defenderSide:victim.m_debateAttribute.IBLKPJDLABN),original,action).WrapToIl2Cpp();
        }catch(Exception e){mod.Log.LogWarning(e);}
    }
    internal static IEnumerator Strike(PerHitParry.Plan plan,PerHitParry.Step step){
        var a=plan.attack;
        if(a.debate){
            var actor=a.debater;var targets=a.speech?.m_listTarget;
            if(!actor || targets==null || a.debateCard?.HFBAJIGHOAF!=MMGEDKCCMHH.GANKBONOKGD.Attack)yield break;
            var list=new List<NPCCtrl>();
            // Native debate removes a target from m_listTarget BEFORE creating
            // its damage iterator. Its captured targetHero remains authoritative.
            if(step.debateVictim)list.Add(step.debateVictim);
            else foreach(var t in targets)if(t)list.Add(t);
            list.Sort((x,y)=>IsPlayer(y.GetHero()).CompareTo(IsPlayer(x.GetHero())));
            foreach(var victim in list){
                if(!PerHitParry.Valid(plan))yield break;
                var side=actor.m_debateAttribute.IBLKPJDLABN;
                if(EnemyGroggy.IsStunned(true,victim.m_debateAttribute.Pointer)||!Opposed(side,victim.m_debateAttribute.IBLKPJDLABN))continue;
                bool manual=PlayerBattleRules.Manual(true,victim.GetHero(),victim.m_debateAttribute.IBLKPJDLABN);
                float chance=victim.m_debateAttribute.IBLKPJDLABN==FMHEOPMKFJB.Enemy?StrengthPressure.EnemyOdds(true,actor.m_debateAttribute.Pointer,victim.m_debateAttribute.Pointer):Odds(Stat(actor.GetHero(),actor.GetWise(),actor.GetWill()),Stat(victim.GetHero(),victim.GetWise(),victim.GetWill()),Souls(actor.GetHero()),Souls(victim.GetHero()),victim.GetHero());
                if(NoDelay){
                    Resolve(plan,step,victim.m_debateAttribute.Pointer,victim.m_debateAttribute.IBLKPJDLABN,manual,PlayerBattleRules.AutoRoll(true,victim.GetHero()),chance,()=>CounterEffects.Rebut(victim),()=>{
                        if(!PerHitParry.Block(plan,step,victim.m_debateAttribute.Pointer))return;
                        if(recoverHalfAP.Value)Effect(()=>ParryRecovery.DebateAP(victim));
                    });
                    continue;
                }
                yield return Intercept(null,manual,PlayerBattleRules.AutoRoll(true,victim.GetHero()),true,chance,side==FMHEOPMKFJB.Enemy,
                    ()=>new CounterGlow(actor.GetTransform(),actor.GetListRenderer(),actor.GetHighlighter(),ParryVisuals.Cost(a)),()=>CounterEffects.Rebut(victim),"반박",
                    ()=>PerHitParry.Valid(plan) && (step.debateVictim?.Pointer==victim.Pointer || a.speech.m_listTarget?.Contains(victim)==true),
                    ()=>{if(!PerHitParry.Block(plan,step,victim.m_debateAttribute.Pointer))return;if(recoverHalfAP.Value)Effect(()=>ParryRecovery.DebateAP(victim));},
                    ()=>{},()=>false,true,manual?PerHitParry.InputLimit(plan,step,window.Value,comboBudget.Value):-1,manual?spent=>PerHitParry.ChargeInput(plan,spent):null,defenderSide:victim.m_debateAttribute.IBLKPJDLABN);
            }
        }else{
            var actor=a.fighter;var targets=Fi_Controller.m_listTargetHero;
            if(actor==null || targets==null || a.fightCard?.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack)yield break;
            var list=new List<GKHLLOOEEFN>();foreach(var t in targets)if(t!=null)list.Add(t);
            list.Sort((x,y)=>IsPlayer(y.GetHero()).CompareTo(IsPlayer(x.GetHero())));
            foreach(var victim in list){
                if(!PerHitParry.Valid(plan))yield break;
                var side=actor.GetAttribute().IBLKPJDLABN;
                if(EnemyGroggy.IsStunned(false,victim.GetAttribute().Pointer)||!Opposed(side,victim.GetAttribute().IBLKPJDLABN))continue;
                bool manual=PlayerBattleRules.Manual(false,victim.GetHero(),victim.GetAttribute().IBLKPJDLABN);
                float chance=victim.GetAttribute().IBLKPJDLABN==FMHEOPMKFJB.Enemy?StrengthPressure.EnemyOdds(false,actor.GetAttribute().Pointer,victim.GetAttribute().Pointer):Odds(Stat(actor.GetHero(),actor.GetForce(),actor.GetWill()),Stat(victim.GetHero(),victim.GetForce(),victim.GetWill()),Souls(actor.GetHero()),Souls(victim.GetHero()),victim.GetHero());
                if(NoDelay){
                    Resolve(plan,step,victim.GetAttribute().Pointer,victim.GetAttribute().IBLKPJDLABN,manual,PlayerBattleRules.AutoRoll(false,victim.GetHero()),chance,()=>CounterEffects.Guard(victim),()=>{
                        if(!PerHitParry.Block(plan,step,victim.GetAttribute().Pointer,victim))return;
                        if(recoverHalfAP.Value)Effect(()=>ParryRecovery.FightAP(victim));
                    });
                    continue;
                }
                yield return Intercept(null,manual,PlayerBattleRules.AutoRoll(false,victim.GetHero()),false,chance,side==FMHEOPMKFJB.Enemy,
                    ()=>new CounterGlow(actor.GetTransform(),actor.GetListRenderer(),actor.GetHighlighter(),ParryVisuals.Cost(a)),()=>CounterEffects.Guard(victim),"패링",
                    ()=>PerHitParry.Valid(plan) && Fi_Controller.m_listTargetHero?.Contains(victim)==true,
                    ()=>{if(!PerHitParry.Block(plan,step,victim.GetAttribute().Pointer,victim))return;if(recoverHalfAP.Value)Effect(()=>ParryRecovery.FightAP(victim));},
                    ()=>{},()=>false,true,manual?PerHitParry.InputLimit(plan,step,window.Value,comboBudget.Value):-1,manual?spent=>PerHitParry.ChargeInput(plan,spent):null,defenderSide:victim.GetAttribute().IBLKPJDLABN);
            }
        }
    }
    static void Resolve(PerHitParry.Plan p,PerHitParry.Step s,IntPtr attribute,FMHEOPMKFJB defenderSide,bool manual,bool autoRoll,float chance,Action guard,Action success){
        if(EnemyGroggy.IsStunned(p.attack.debate,attribute))return;
        if(manual && CueInput){
            RealtimeParry.BindCue(p.attack,s);
            if(RealtimeParry.ConsumeCue(p.attack,attribute,s.cueStart,s.cueDeadline,Time.unscaledTimeAsDouble)){
                RealtimeParry.Audit(p.attack,"cue-success",$"step={s.index} victim={attribute}");
                RealtimeParry.Success(p.attack,attribute);SuccessSound(p.attack,s,defenderSide==FMHEOPMKFJB.Enemy);success();return;
            }
            if(Instant(p.attack,attribute,false,autoRoll,chance,guard,defenderSide==FMHEOPMKFJB.Enemy,s)){success();return;}
        }else if(Instant(p.attack,attribute,manual,autoRoll,chance,guard,defenderSide==FMHEOPMKFJB.Enemy,s)){success();return;}
        if(manual)LateParry.Add(p,s,attribute,guard,()=>{SuccessSound(p.attack,s,defenderSide==FMHEOPMKFJB.Enemy);success();});
    }
    static void SuccessSound(ImpactTiming.Attack a,PerHitParry.Step s,bool enemy){
        if(!sound.Value)return;
        if(s!=null){
            if(enemy?s.enemySoundPlayed:s.friendlySoundPlayed)return;
            if(enemy)s.enemySoundPlayed=true;else s.friendlySoundPlayed=true;
        }
        Effect(()=>CounterEffects.Clang(ParryVisuals.Cost(a),enemy));
    }
    static bool Instant(ImpactTiming.Attack a,IntPtr victim,bool manual,bool autoRoll,float chance,Action guard,bool enemyDefender=false,PerHitParry.Step step=null){
        if(EnemyGroggy.IsStunned(a.debate,victim))return false;
        bool byHand=manual && RealtimeParry.Consume(a,victim);
        bool won=byHand || (autoRoll && UnityEngine.Random.value<chance);
        RealtimeParry.Audit(a,"initial-check",$"manual={byHand} automatic={won&&!byHand} victim={victim}");
        if(!won)return false;
        if(byHand)RealtimeParry.Success(a,victim);
        if(!byHand)Effect(guard);
        SuccessSound(a,step,enemyDefender);
        return true;
    }
    static IEnumerator Intercept(NativeRoutine original,bool manual,bool autoRoll,bool debate,float chance,bool enemyAttack,Func<CounterGlow> illuminate,Action guard,string name,Func<bool> valid,Action discard,Action finish,Func<bool> reaction,bool continueAttack=false,float inputLimit=-1,Action<float> chargeInput=null,FMHEOPMKFJB defenderSide=FMHEOPMKFJB.MySide){
        if(restorePending!=null){if(original!=null)yield return original;yield break;}
        int soundCost=ParryVisuals.Cost(ImpactTiming.Current(debate));
        int token=++generation;
        bool playerWindow=enemyAttack && manual && inputLimit!=0;
        bool won=false,byHand=false;
        float elapsed=0;
        try{
            if(playerWindow){
                restorePending=CounterSpeed(debate,manual);
                if(flash.Value)Effect(()=>glow=illuminate());
                if(sound.Value && valid())Effect(CounterEffects.AttackWarning);
                openedFrame=Time.frameCount;
                if(manual){manualGuard=guard;live=valid;}

                float limit=inputLimit<0?window.Value:Math.Min(window.Value,inputLimit);
                while(elapsed<limit && valid() && generation==token){
                    yield return null;
                    if(!Application.isFocused||Time.timeScale<=0)continue;
                    elapsed+=Time.unscaledDeltaTime;
                    if(glow!=null)Effect(()=>glow.Pulse(elapsed));
                    if(manual && winningFrame>=0){
                        if(elapsed<=limit)byHand=won=true;
                        else readyAt=Time.unscaledTimeAsDouble+cooldown.Value;
                        break;
                    }
                }
            }
            if(!won && autoRoll)won=UnityEngine.Random.value<chance;
        }finally{
            chargeInput?.Invoke(elapsed);
            if(token==generation)Release();
        }
        if(token!=generation||!valid())yield break;
        if(!won){if(original!=null)yield return original;yield break;}
        if(manual){readyAt=0;contactUntil=Time.unscaledTimeAsDouble+(continueAttack?0:.34);}
        discard();
        if(!byHand)Effect(guard);
        if(sound.Value)Effect(()=>CounterEffects.Clang(soundCost,defenderSide==FMHEOPMKFJB.Enemy));
        bool recoil=false;Effect(()=>recoil=reaction());
        if(continueAttack){if(token==generation && valid())finish();yield break;}
        if(!playerWindow && !recoil){if(token==generation && valid())finish();yield break;}
        // Keep the native decision paused only while the contact/recoil is visible.
        // Non-player targets retain their current speed and never get an input window.
        if(debate)De_Controller.m_isLaunchingAttack=true;else Fi_Controller.m_isLaunchingAttack=true;
        float shown=0;
        try{
            while(shown<.34f && token==generation && valid()){
                yield return null;
                if(Application.isFocused && Time.timeScale>0)shown+=Time.unscaledDeltaTime;
            }
        }finally{
            if(token==generation){
                if(debate)De_Controller.m_isLaunchingAttack=false;else Fi_Controller.m_isLaunchingAttack=false;
            }
        }
        if(token==generation && valid())finish();
    }
}



