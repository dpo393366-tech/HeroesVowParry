using System;
#if DuelCounters
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
#endif
namespace Vow.Counters;

internal static class EnemyGroggy
{
#if DuelCounters
    sealed class Enemy {
        internal readonly GroggyMeter meter=new();
        internal GKHLLOOEEFN fighter;internal NPCCtrl debater;internal IntPtr attr;
        internal bool debate,ending,posed,poseComplete;internal int endRound,endOrder;
        internal Animator animator;internal AnimatorOverrideController controller;
        internal AnimationClip savedClip,clip;internal float speed;internal bool rootMotion;internal AnimatorCullingMode culling;
        internal double poseAt,nextPose,nextBuffClean,nextPoseSample;internal float sampledPose=-1;
        internal readonly List<(int layer,int hash)> layers=new();internal string poseFailure;
        internal RectTransform anchor,hudRoot;internal Image healthTemplate;
        internal GameObject bar;internal Image fill;internal RectTransform rect;
        internal GameObject stunBadge;
        internal readonly RectTransform[] stunStars=new RectTransform[3];
        internal int shownCount=-1,shownThreshold=-1,shownSkips=-1;internal bool shownStunned;
        internal float shownMultiplier=-1,shownScale=-1,shownWidth=-1;
        internal Transform Root=>debate?debater.GetTransform():fighter.GetTransform();
        internal bool Alive=>debate?debater&&debater.m_debateAttribute.MKEIPLCMANH>0:fighter!=null&&fighter.GetAttribute().MKEIPLCMANH>0;
    }
    static readonly Dictionary<(bool,IntPtr),Enemy> enemies=new();
    static readonly List<Enemy> frameEnemies=new();
    static ConfigEntry<bool> enabled;
    static ConfigEntry<float> threshold,damageMultiplier;
    static int Threshold=>Mathf.Clamp(Mathf.RoundToInt(threshold?.Value??10f),1,50);
    static float DamageMultiplier=>Mathf.Clamp(damageMultiplier?.Value??1.5f,1f,5f);
    static double nextHud,nextError,nextPresentation;
    static Texture2D starTexture;static Sprite starSprite;
    internal static bool IsStunned(bool debate,IntPtr attr)=>enabled?.Value==true&&enemies.TryGetValue((debate,attr),out var e)&&e.meter.stunned;
    static void Report(string text){if(Time.unscaledTimeAsDouble<nextError)return;nextError=Time.unscaledTimeAsDouble+5;DuelCounters.Warn("그로기: "+text);}
    internal static void Install(DuelCounters mod,Harmony h){
        GroggyBuffs.Install(h);
        enabled=mod.Config.Bind("EnemyGroggy","Enabled",true,"Enemy only: parried hit count causes one skipped turn and configurable incoming damage until the next actionable turn. Each combo hit counts; multiple defenders at the same hit count once. Reset after recovery or battle exit.");
        threshold=mod.Config.Bind("EnemyGroggy","ParriedAttacksRequired",10f,new ConfigDescription("Parried hits required, including individual combo hits (whole number). Changes apply at the next successful parry; existing groggy recovers normally.",new AcceptableValueRange<float>(1f,50f)));
        damageMultiplier=mod.Config.Bind("EnemyGroggy","DamageTakenMultiplier",1.5f,new ConfigDescription("Incoming damage multiplier during groggy. Changes apply to subsequent damage immediately.",new AcceptableValueRange<float>(1f,5f)));
        h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"SetPhase"),prefix:new HarmonyMethod(typeof(EnemyGroggy),nameof(FightPhase)){priority=Priority.First});
        h.Patch(AccessTools.DeclaredMethod(typeof(De_Controller),"SetPhase"),prefix:new HarmonyMethod(typeof(EnemyGroggy),nameof(DebatePhase)){priority=Priority.First});
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"GOGFJJDCOOF"),prefix:new HarmonyMethod(typeof(EnemyGroggy),nameof(FightDamage)){priority=Priority.Last},postfix:new HarmonyMethod(typeof(EnemyGroggy),nameof(FightHit)));
        h.Patch(AccessTools.DeclaredMethod(typeof(LEDJAIBAAJC),"GOGFJJDCOOF"),prefix:new HarmonyMethod(typeof(EnemyGroggy),nameof(DebateDamage)){priority=Priority.Last},postfix:new HarmonyMethod(typeof(EnemyGroggy),nameof(DebateHit)));
        Panel.Hook(h,typeof(NFIDHHGKMDC),"LEJICHHIEGH",typeof(EnemyGroggy),before:nameof(FighterJoining));
        Panel.Hook(h,typeof(LEDJAIBAAJC),"LEJICHHIEGH",typeof(EnemyGroggy),before:nameof(DebaterJoining));
        // Native launch checks cannot-dodge BEFORE RNG, then guaranteed-dodge.
        // Override both, including DodgeLimit's final roll, only while groggy.
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"MCMCPMECLJH"),postfix:new HarmonyMethod(typeof(EnemyGroggy),nameof(CannotDodge)){priority=Priority.Last,after=new[]{"local.heroesvow.dodgelimit"}});
        h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"DNMHDJJMIJA"),postfix:new HarmonyMethod(typeof(EnemyGroggy),nameof(CannotForceDodge)){priority=Priority.Last,after=new[]{"local.heroesvow.dodgelimit"}});
    }
    static void CannotDodge(NFIDHHGKMDC __instance,ref bool __result){if(IsStunned(false,__instance.Pointer))__result=true;}
    static void CannotForceDodge(NFIDHHGKMDC __instance,ref bool __result){if(IsStunned(false,__instance.Pointer))__result=false;}
    internal static void Settings(){
        Panel.Switch("적 그로기 · 1턴 휴식 · 적의 공격 명중 시 누적 -1",enabled);
        Panel.Slider("그로기 발동 패링 횟수 (연타 각 타격 포함)",threshold,1,50,1);
        Panel.Slider("그로기 중 받는 피해 배율",damageMultiplier,1,5,.1f);
    }
    static void Forget(bool debate,IntPtr id){if(enemies.Remove((debate,id),out var e))Dispose(e);}
    static void FighterJoining(NFIDHHGKMDC __instance){Forget(false,__instance.Pointer);ParryRecovery.Forget(false,__instance.Pointer);}
    static void DebaterJoining(LEDJAIBAAJC __instance){Forget(true,__instance.Pointer);ParryRecovery.Forget(true,__instance.Pointer);}
    internal static void Record(ImpactTiming.Attack a,PerHitParry.Step step=null){
        if(enabled?.Value!=true||a==null||!a.Valid()||!PlayerBattleRules.Participating(a.debate))return;
        bool counted=step==null?a.groggyCounted:step.groggyCounted;
        if(counted)return;
        var attr=a.debate?a.debater?.m_debateAttribute?.Pointer:a.fighter?.GetAttribute()?.Pointer;
        var side=a.debate?a.debater.m_debateAttribute.IBLKPJDLABN:a.fighter.GetAttribute().IBLKPJDLABN;
        if(attr==null||side!=FMHEOPMKFJB.Enemy)return;
        var e=Get(a.debate,attr.Value,a.fighter,a.debater);
        bool began=e.meter.RecordAttack(ref counted,a.round,a.order,Threshold);
        if(step==null)a.groggyCounted=counted;else step.groggyCounted=counted;
        if(began){e.nextBuffClean=0;e.poseComplete=false;e.ending=false;DuelCounters.StabilityNote($"groggy begin enemy={attr} debate={a.debate} round={a.round} order={a.order} skips=1 threshold={Threshold} damage={DamageMultiplier:F1}");}
    }
    static Enemy Get(bool debate,IntPtr attr,GKHLLOOEEFN fighter,NPCCtrl debater){
        if(!enemies.TryGetValue((debate,attr),out var e))enemies[(debate,attr)]=e=new Enemy{debate=debate,attr=attr,fighter=fighter,debater=debater};
        return e;
    }
    static bool Skip(bool debate,IntPtr attr,int round,int order){
        if(enabled?.Value!=true||!enemies.TryGetValue((debate,attr),out var e)||!e.meter.stunned)return false;
        bool skip=e.meter.Turn(round,order);
        if(!skip){e.ending=false;e.poseComplete=true;if(e.stunBadge)e.stunBadge.SetActive(false);ReleasePose(e);DuelCounters.StabilityNote($"groggy recovered enemy={attr} round={round} order={order} damage=1.0 count=0");}
        return skip;
    }
    static void FightPhase(Fi_Controller __instance,ref Fi_Controller.NHCOMCJKABH __0){
        if(Fi_Controller.m_isSpecialAttack||!Fi_Controller.m_isInBattle||Fi_Controller.m_isEnd||__0==Fi_Controller.NHCOMCJKABH.end)return;
        var actor=Fi_Controller.m_curHero;if(actor==null)return;
        // Native SetRoundAndHero requests begin after selecting the next actor.
        bool entry=__0==Fi_Controller.NHCOMCJKABH.begin||__0==Fi_Controller.NHCOMCJKABH.draw||__0==Fi_Controller.NHCOMCJKABH.play;
        // Recovery/automatic-mode paths may re-enter draw/play without begin.
        // Use the same turn key in every entry path, so repeated phase callbacks
        // cannot consume multiple skipped turns or leave the multiplier latched.
        if(entry&&!Fi_Controller.m_isLaunchingAttack&&!PerHitParry.PendingSettlement(ImpactTiming.Current(false))&&
            Skip(false,actor.GetAttribute().Pointer,__instance.AHDOJFHBBFJ,__instance.m_curOrder))__0=Fi_Controller.NHCOMCJKABH.end;
    }
    static void DebatePhase(De_Controller __instance,ref De_Controller.MOLBMLDLJEP __0){
        if(!De_Controller.m_isInDebate||__instance.m_isEnd)return;
        var actor=De_Controller.m_curHero;if(!actor)return;
        // Debate selects directly into draw; repeated begin/draw calls share key.
        if((__0==De_Controller.MOLBMLDLJEP.begin||__0==De_Controller.MOLBMLDLJEP.draw||__0==De_Controller.MOLBMLDLJEP.play)&&
            !De_Controller.m_isLaunchingAttack&&!PerHitParry.PendingSettlement(ImpactTiming.Current(true))&&
            Skip(true,actor.m_debateAttribute.Pointer,__instance.m_curRound,__instance.m_curOrder))__0=De_Controller.MOLBMLDLJEP.end;
    }
    struct HitState {internal Enemy victim,attacker;internal int hp;internal ImpactTiming.Attack attack;internal PerHitParry.Step step;}
    static HitState Capture(bool debate,IntPtr victim,FMHEOPMKFJB side,int hp,ref int damage){
        if(enabled?.Value!=true||damage<=0)return default;
        var state=new HitState{hp=hp};
        if(side==FMHEOPMKFJB.Enemy&&enemies.TryGetValue((debate,victim),out var target)){
            state.victim=target;damage=target.meter.Damage(damage,DamageMultiplier);
        }
        if(side!=FMHEOPMKFJB.MySide&&side!=FMHEOPMKFJB.Ally)return state;
        var a=PerHitParry.DamageOwner;
        if(a==null||a.debate!=debate||!a.Valid()||!PlayerBattleRules.Participating(debate))return state;
        var id=debate?a.debater?.m_debateAttribute?.Pointer:a.fighter?.GetAttribute()?.Pointer;
        if(id==null)return state;
        var attackerSide=debate?a.debater.m_debateAttribute.IBLKPJDLABN:a.fighter.GetAttribute().IBLKPJDLABN;
        if(attackerSide!=FMHEOPMKFJB.Enemy||!enemies.TryGetValue((debate,id.Value),out var attacker))return state;
        state.attacker=attacker;state.attack=a;state.step=PerHitParry.running?.step;return state;
    }
    static void FightDamage(NFIDHHGKMDC __instance,ref int __0,out HitState __state)=>__state=Capture(false,__instance.Pointer,__instance.IBLKPJDLABN,__instance.MKEIPLCMANH,ref __0);
    static void DebateDamage(LEDJAIBAAJC __instance,ref int __0,out HitState __state)=>__state=Capture(true,__instance.Pointer,__instance.IBLKPJDLABN,__instance.MKEIPLCMANH,ref __0);
    static void Hit(HitState state,int after){
        if(state.hp<=after)return;
        // Incoming damage must not release the ongoing groggy presentation or drain
        // the victim's meter. Only its own successful outgoing attack does.
        var victim=state.victim;
        if(victim!=null&&victim.meter.stunned)victim.poseComplete=false;
        if(state.attacker==null)return;
        if(state.step!=null){if(state.step.groggyRelieved)return;state.step.groggyRelieved=true;}
        else{if(state.attack.groggyRelieved)return;state.attack.groggyRelieved=true;}
        state.attacker.meter.Hit(state.hp,after);
    }
    static void FightHit(NFIDHHGKMDC __instance,HitState __state)=>Hit(__state,__instance.MKEIPLCMANH);
    static void DebateHit(LEDJAIBAAJC __instance,HitState __state)=>Hit(__state,__instance.MKEIPLCMANH);
    // Battle flags and actors exist before the loading curtain disappears.
    // The independent overlay must follow the actual foreground battle UI.
    static bool PresentationReady(bool debate){
        if(UI_LoadUI.m_isLoading||!Panel.InputAllowed||Time.timeScale<=0||BattleModal.Visible)return false;
        if(debate){
            if(!De_Controller.m_isInDebate||De_Controller.m_isQuiting)return false;
            var f=De_Controller.EBACHLGLNGD;return f&&!f.m_isEnd;}
        // The controller's battle UI can be inactive while native per-actor HUDs
        // are visible. Bind to those HUDs, not the card/selection panel's lifetime.
        return Fi_Controller.m_isInBattle&&!Fi_Controller.m_isEnd;
    }
    internal static void Tick(){
        if(enabled?.Value!=true){if(enemies.Count>0)Reset();return;}
        if(!Fi_Controller.m_isInBattle&&!De_Controller.m_isInDebate){if(enemies.Count>0)Reset();return;}
        bool visible=PresentationReady(false)||PresentationReady(true);
        if(!visible)foreach(var hidden in enemies.Values)if(hidden.bar&&hidden.bar.activeSelf)hidden.bar.SetActive(false);
        // Hide before the pause/focus early return: loading commonly sets timeScale=0.
        if(!visible||Time.timeScale<=0)return;
        // Presentation only; hit/parry/turn rules remain synchronous native hooks.
        bool updateHud=Time.unscaledTimeAsDouble>=nextPresentation;
        if(updateHud)nextPresentation=Time.unscaledTimeAsDouble+1d/30;
        if(Time.unscaledTimeAsDouble>=nextHud){nextHud=Time.unscaledTimeAsDouble+.5;try{Hud();}catch(Exception ex){Report("HUD 검색 "+ex);}}
        frameEnemies.Clear();frameEnemies.AddRange(enemies.Values);
        foreach(var e in frameEnemies)try{
            if(!e.Alive){Forget(e.debate,e.attr);continue;}
            try{if(updateHud)PositionBar(e);}catch(Exception ex){Report("바 위치 "+ex.Message);}
            if(!e.meter.stunned){if(e.posed)ReleasePose(e);continue;}
            if(Time.unscaledTimeAsDouble>=e.nextBuffClean){
                e.nextBuffClean=Time.unscaledTimeAsDouble+.25;
                if(e.debate)GroggyBuffs.Purge(e.debater.m_debateAttribute);else GroggyBuffs.Purge(e.fighter.GetAttribute());
            }
            bool launching=e.debate?De_Controller.m_isLaunchingAttack:Fi_Controller.m_isLaunchingAttack;
            var a=ImpactTiming.Current(e.debate);
            bool acting=e.debate?De_Controller.m_curHero?.m_debateAttribute?.Pointer==e.attr:Fi_Controller.m_curHero?.GetAttribute()?.Pointer==e.attr;
            if(acting&&a!=null&&a.Valid()&&(PerHitParry.PendingSettlement(a)||!a.motionFinished))continue;
            if(acting&&launching)continue;
            // Wait for authored attack/late damage to finish before touching pose
            // or requesting native end; never abort an in-flight damage iterator.
            if(e.debate){var f=De_Controller.EBACHLGLNGD;if(!f||f.m_isEnd)continue;
                if(De_Controller.m_curHero?.m_debateAttribute?.Pointer==e.attr&&De_Controller.m_curPhase!=De_Controller.MOLBMLDLJEP.end&&(!e.ending||e.endRound!=f.m_curRound||e.endOrder!=f.m_curOrder)){
                    e.ending=true;e.endRound=f.m_curRound;e.endOrder=f.m_curOrder;f.SetPhase(De_Controller.MOLBMLDLJEP.end,13);
                }
            }else{var f=Fi_Controller.EBACHLGLNGD;if(!f||Fi_Controller.m_isEnd)continue;
                if(Fi_Controller.m_curHero?.GetAttribute()?.Pointer==e.attr&&Fi_Controller.m_curPhase!=Fi_Controller.NHCOMCJKABH.end&&TurnHandoff.BattleReady(f)&&(!e.ending||e.endRound!=f.AHDOJFHBBFJ||e.endOrder!=f.m_curOrder)){
                    e.ending=true;e.endRound=f.AHDOJFHBBFJ;e.endOrder=f.m_curOrder;f.SetPhase(Fi_Controller.NHCOMCJKABH.end,300,true);
                }
            }
            // SetPhase may synchronously complete a turn and recover the unit.
            // Do not reacquire the pose after recovery in the same callback.
            if(e.meter.stunned&&e.Alive)Pose(e);else if(e.posed)ReleasePose(e);
        }catch(Exception ex){Report("표시/진행 "+ex);}
    }
    static void Pose(Enemy e){
        if(!e.posed){
            if(Time.unscaledTimeAsDouble<e.nextPose)return;e.nextPose=Time.unscaledTimeAsDouble+1;
            if(!e.debate){AnimalDodgeSync.Cancel(e.fighter);CounterMotion.Complete(e.fighter);}
            e.animator=e.debate?e.debater.GetAnimator():e.fighter.GetAnimator();
            e.controller=e.debate?e.debater.GetOverrideController():e.fighter.GetOverrideController();
            if(!e.animator||!e.controller){PoseFailure(e,"Animator/controller unavailable");return;}
            e.layers.Clear();
            for(int layer=0;layer<e.animator.layerCount;layer++){
                int hash=Animator.StringToHash(e.animator.GetLayerName(layer)+".Once");
                if(!e.animator.HasState(layer,hash)){hash=Animator.StringToHash("Once");if(!e.animator.HasState(layer,hash))continue;}
                e.layers.Add((layer,hash));
            }
            bool animal=!e.debate&&e.fighter.TryCast<AnimalCtrl>()!=null;
            AnimationClip source=null;
            if(animal){foreach(string slot in new[]{"death","Death","BeHit"}){try{source=e.controller[slot];}catch{}if(source)break;}
                if(!source&&e.animator.runtimeAnimatorController)foreach(var clip in e.animator.runtimeAnimatorController.animationClips)if(clip&&(clip.name.IndexOf("death",StringComparison.OrdinalIgnoreCase)>=0||clip.name.IndexOf("die",StringComparison.OrdinalIgnoreCase)>=0)){source=clip;break;}}
            else source=CounterEffects.Clip("Animation/12Hurt/状态-眩晕01.anim")??CounterEffects.Clip("Animation/12Hurt/stunned@@Take 001.anim");
            if(!source){PoseFailure(e,"groggy clip unavailable; animal="+animal);return;}
            // Use only pose curves. Never fire death/attack animation events.
            e.clip=Object.Instantiate(source);e.clip.events=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<AnimationEvent>(0);
            if(e.layers.Count>0)e.savedClip=e.controller["Once"];e.speed=e.animator.speed;e.rootMotion=e.animator.applyRootMotion;e.culling=e.animator.cullingMode;e.poseAt=Time.unscaledTimeAsDouble;
            if(e.layers.Count>0)e.controller["Once"]=e.clip;e.animator.applyRootMotion=false;e.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;e.posed=true;e.sampledPose=-1;
            DuelCounters.StabilityNote($"groggy pose enemy={e.attr} clip={source.name} layers={e.layers.Count} mode={(e.layers.Count>0?"Animator":"sample")} ");
        }
        if(!e.animator||!e.clip)return;
        bool beast=!e.debate&&e.fighter.TryCast<AnimalCtrl>()!=null;
        float elapsed=(float)(Time.unscaledTimeAsDouble-e.poseAt);
        float t=GroggyPoseClock.Normalized(beast,elapsed,e.clip.length);
        bool displaced=false;
        foreach(var layer in e.layers){
            var state=e.animator.GetCurrentAnimatorStateInfo(layer.layer);
            if(e.animator.IsInTransition(layer.layer)||(state.fullPathHash!=layer.hash&&state.shortNameHash!=layer.hash)){displaced=true;break;}
        }
        // Enforce ownership every frame, but sample undisturbed motion at 30Hz.
        // A completed animation is not a completed stun. Beasts hold the down
        // pose instead of replaying their falling/death clip indefinitely.
        e.animator.speed=0;
        if(!displaced&&(e.sampledPose==t||Time.unscaledTimeAsDouble<e.nextPoseSample))return;
        e.nextPoseSample=Time.unscaledTimeAsDouble+1d/30;
        // LateUpdate Play alone is overwritten by native state changes before the
        // next Animator evaluation. Evaluate the pose now, after native movement.
        var root=e.Root;if(!root)return;var pos=root.position;var rot=root.rotation;var scale=root.localScale;
        var visual=e.animator.transform;var localPos=visual.localPosition;var localRot=visual.localRotation;var localScale=visual.localScale;
        e.animator.speed=0;
        try{
            if(e.layers.Count>0){foreach(var layer in e.layers)e.animator.Play(layer.hash,layer.layer,t);e.animator.Update(0);}
            else e.clip.SampleAnimation(e.animator.gameObject,t*e.clip.length);
            e.sampledPose=t;
        }finally{root.position=pos;root.rotation=rot;root.localScale=scale;visual.localPosition=localPos;visual.localRotation=localRot;visual.localScale=localScale;}
    }
    static void PoseFailure(Enemy e,string text){if(e.poseFailure==text)return;e.poseFailure=text;DuelCounters.Warn("그로기 모션 enemy="+e.attr+" "+text);
    }
    static void ReleasePose(Enemy e,bool idle=true){
        if(!e.posed)return;e.posed=false;
        if(e.layers.Count>0&&e.controller&&e.controller["Once"]==e.clip)e.controller["Once"]=e.savedClip;
        if(e.animator){e.animator.speed=e.speed;e.animator.applyRootMotion=e.rootMotion;e.animator.cullingMode=e.culling;
            if(idle&&e.Alive)for(int layer=0;layer<e.animator.layerCount;layer++)
                foreach(string state in new[]{"Idle","Default"}){int h=Animator.StringToHash(e.animator.GetLayerName(layer)+"."+state);
                    if(!e.animator.HasState(layer,h))h=Animator.StringToHash(state);
                    if(e.animator.HasState(layer,h)){e.animator.CrossFadeInFixedTime(h,.15f,layer);break;}}}
        if(e.clip)Object.Destroy(e.clip);e.clip=null;
    }
    static void Hud(){
        // m_listHUD is the native RECYCLE POOL. GetOne removes the visible HUD
        // from it, so enumerating that list cannot find active enemy health bars.
        // Resolve ownership from the actual battle roster instead.
        if(Fi_Controller.m_isInBattle&&!Fi_Controller.m_isEnd&&PlayerBattleRules.Participating(false)&&Fi_Controller.m_listHero!=null)
            foreach(var actor in Fi_Controller.m_listHero)try{
                var attr=actor?.GetAttribute();
                if(attr==null||attr.IBLKPJDLABN!=FMHEOPMKFJB.Enemy||attr.MKEIPLCMANH<=0)continue;
                var e=Get(false,attr.Pointer,actor,null);var hud=actor.GetHUD();
                if(!hud||hud.m_battleUnit?.Pointer!=actor.Pointer){BindBar(e,null,null,null);continue;}
                BindBar(e,HealthTrack(hud.m_followingHpSlider,hud.m_healthImage),hud.m_rectTransform?hud.m_rectTransform:hud.GetComponent<RectTransform>(),hud.m_healthImage);
            }catch(Exception ex){Report("무투 HUD 연결 "+ex.Message);}
        if(De_Controller.m_isInDebate&&PlayerBattleRules.Participating(true)&&De_Controller.m_listHero!=null)
            foreach(var actor in De_Controller.m_listHero)try{
                var attr=actor?.m_debateAttribute;
                if(attr==null||attr.IBLKPJDLABN!=FMHEOPMKFJB.Enemy||attr.MKEIPLCMANH<=0)continue;
                var e=Get(true,attr.Pointer,null,actor);var hud=actor.m_hudDebate;
                if(!hud||hud.m_npcCtrl?.Pointer!=actor.Pointer){BindBar(e,null,null,null);continue;}
                BindBar(e,HealthTrack(hud.m_sliderHp,hud.m_imageDebateHP),hud.GetComponent<RectTransform>(),hud.m_imageDebateHP);
            }catch(Exception ex){Report("설전 HUD 연결 "+ex.Message);}
    }
    static GameObject RectObject(string name)=>new GameObject(name,new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(new[]{Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>()}));
    // Slider fill width changes with HP. Its fill container is the full track.
    static RectTransform HealthTrack(Slider slider,Image fallback){
        if(slider&&slider.gameObject.activeInHierarchy){var f=slider.fillRect;if(f&&f.parent){var track=f.parent.GetComponent<RectTransform>();if(track)return track;}
            var trackRoot=slider.GetComponent<RectTransform>();if(trackRoot)return trackRoot;}
        return fallback?fallback.rectTransform:null;
    }
    static void BindBar(Enemy e,RectTransform anchor,RectTransform root,Image template){
        e.anchor=anchor;e.hudRoot=root;e.healthTemplate=template;
    }
    static void CopyHealthAppearance(Image target,Image source){
        if(!source)return;
        // Copy the native health art/material, not its health scripts or events.
        target.sprite=source.overrideSprite?source.overrideSprite:source.sprite;
        target.material=source.material;target.preserveAspect=source.preserveAspect;
        target.type=source.type==Image.Type.Filled?Image.Type.Simple:source.type;
        target.raycastTarget=false;
    }
    static void EnsureBar(Enemy e){
        if(!e.hudRoot||!e.anchor)return;
        if(!e.bar){
            e.shownCount=-1;
            e.bar=RectObject("Vow_GroggyMeter");e.bar.layer=e.hudRoot.gameObject.layer;
            var rect=e.rect=e.bar.GetComponent<RectTransform>();rect.pivot=new(.5f,1);
            var bg=e.bar.AddComponent<Image>();CopyHealthAppearance(bg,e.healthTemplate);bg.color=new(.025f,.07f,.16f,1);bg.raycastTarget=false;
            var fill=RectObject("Fill");fill.layer=e.bar.layer;fill.transform.SetParent(rect,false);e.fill=fill.AddComponent<Image>();CopyHealthAppearance(e.fill,e.healthTemplate);e.fill.raycastTarget=false;e.fill.color=new(.12f,.48f,1,1);
            var fr=e.fill.rectTransform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
            DuelCounters.StabilityNote("groggy HUD created enemy="+e.attr+" actor-owned-health-blue-depletion");
        }
        if(e.rect.parent!=e.hudRoot){e.rect.SetParent(e.hudRoot,false);e.rect.anchorMin=e.rect.anchorMax=e.hudRoot.pivot;e.rect.SetAsLastSibling();}
    }
    // A persistent HUD cue, independent of the animator pose and warning toggle.
    // One shared tiny texture; no particles, lights, coroutines or per-frame scans.
    static Sprite StunStar(){
        if(starSprite)return starSprite;
        const int size=48;
        starTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float dx=(x+.5f-size*.5f)/(size*.5f),dy=(y+.5f-size*.5f)/(size*.5f);
            float angle=Mathf.Atan2(dy,dx)-Mathf.PI*.5f;
            float radius=.57f+.27f*Mathf.Cos(5*angle);
            float alpha=Mathf.Clamp01((radius-Mathf.Sqrt(dx*dx+dy*dy))*size*.5f);
            starTexture.SetPixel(x,y,new Color(1,1,1,alpha));
        }
        starTexture.Apply(false,true);
        starSprite=Sprite.Create(starTexture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
        return starSprite;
    }
    static void StunMarker(Enemy e){
        if(!e.meter.stunned){if(e.stunBadge&&e.stunBadge.activeSelf)e.stunBadge.SetActive(false);return;}
        if(!e.stunBadge){
            e.stunBadge=RectObject("Vow_GroggyStatus");e.stunBadge.layer=e.bar.layer;
            var badge=e.stunBadge.GetComponent<RectTransform>();badge.SetParent(e.rect,false);
            badge.anchorMin=badge.anchorMax=new(.5f,1);badge.pivot=new(.5f,.5f);
            badge.anchoredPosition=new(0,48);badge.sizeDelta=new(90,24);
            for(int i=0;i<3;i++){
                var obj=RectObject("StunStar"+i);obj.layer=e.bar.layer;obj.transform.SetParent(badge,false);
                var img=obj.AddComponent<Image>();img.sprite=StunStar();img.raycastTarget=false;img.color=new(1,.84f,.16f,1);
                e.stunStars[i]=img.rectTransform;e.stunStars[i].anchorMin=e.stunStars[i].anchorMax=new(.5f,.5f);
            }
        }
        if(!e.stunBadge.activeSelf)e.stunBadge.SetActive(true);
        float clock=Time.unscaledTime*2.6f;
        for(int i=0;i<3;i++){
            float phase=clock+i*Mathf.PI*2/3;float depth=Mathf.Sin(phase);
            var star=e.stunStars[i];star.anchoredPosition=new(Mathf.Cos(phase)*34,depth*9);
            float size=(15+depth*2)*1.5f;star.sizeDelta=new(size,size);
            star.localRotation=Quaternion.Euler(0,0,-clock*30+i*25);
        }
    }
    static void PositionBar(Enemy e){
        bool visible=PresentationReady(e.debate)&&e.anchor&&e.hudRoot&&e.anchor.gameObject.activeInHierarchy&&e.hudRoot.gameObject.activeInHierarchy;
        if(!visible){if(e.bar&&e.bar.activeSelf)e.bar.SetActive(false);return;}
        EnsureBar(e);
        var r=e.anchor.rect;
        // Stay in the health HUD's coordinate system: no main-camera projection,
        // overlay sorting, screen scaling or expiring half-second anchor lease.
        var left=e.hudRoot.InverseTransformPoint(e.anchor.TransformPoint(new Vector3(r.xMin,r.yMin,0)));
        var right=e.hudRoot.InverseTransformPoint(e.anchor.TransformPoint(new Vector3(r.xMax,r.yMin,0)));
        var top=e.hudRoot.InverseTransformPoint(e.anchor.TransformPoint(new Vector3(r.xMin,r.yMax,0)));
        float width=Mathf.Abs(right.x-left.x);
        if(width<=.01f){e.bar.SetActive(false);return;}
        Vector2 position=new((left.x+right.x)*.5f,Mathf.Min(left.y,right.y)-3);
        if(e.rect.anchoredPosition!=position)e.rect.anchoredPosition=position;
        Vector2 barSize=new(width,Mathf.Max(3,Mathf.Abs(top.y-left.y)));
        if(e.rect.sizeDelta!=barSize){e.rect.sizeDelta=barSize;e.shownWidth=width;}
        if(!e.bar.activeSelf)e.bar.SetActive(true);
        StunMarker(e);
        int limit=Threshold;float multiplier=DamageMultiplier;
        if(e.shownCount==e.meter.count&&e.shownThreshold==limit&&e.shownSkips==e.meter.skips&&e.shownStunned==e.meter.stunned&&e.shownMultiplier==multiplier)return;
        e.shownCount=e.meter.count;e.shownThreshold=limit;e.shownSkips=e.meter.skips;e.shownStunned=e.meter.stunned;e.shownMultiplier=multiplier;
        float remaining=GroggyBarMath.Remaining(e.meter.count,Threshold,e.meter.stunned);
        e.fill.rectTransform.anchorMax=new(remaining,1);
        e.fill.enabled=remaining>0;
    }
    static void Dispose(Enemy e){e.meter.Recover();ReleasePose(e,false);if(e.bar)Object.Destroy(e.bar);}
    internal static void Reset(){foreach(var e in enemies.Values)try{Dispose(e);}catch{}enemies.Clear();nextHud=nextPresentation=0;
        if(starSprite)Object.Destroy(starSprite);if(starTexture)Object.Destroy(starTexture);starSprite=null;starTexture=null;}
#else
    internal static bool IsStunned(bool debate,IntPtr attr)=>false;
    internal static void Install(DuelCounters mod,HarmonyLib.Harmony h){}
    internal static void Settings(){}
    internal static void Record(ImpactTiming.Attack a,PerHitParry.Step step=null){}
    internal static void Tick(){}
    internal static void Reset(){}
#endif
}

