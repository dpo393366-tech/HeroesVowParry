using System;
using UnityEngine;
using BepInEx.Configuration;
namespace Vow.Counters;

// Presentation only: never changes clocks, transforms, HP, AI or input windows.
internal static class ParryVisuals
{
#if DuelCounters
    static ConfigEntry<bool> outline,success,extraImpact,warningEffects;
    internal static bool WarningEnabled=>warningEffects?.Value??true;
    static ConfigEntry<float> impactScale;
    static ConfigEntry<float> effectSpeed,effectScale,effectGlow,scale,redDuration,goldDuration;
    internal static float RedDuration=>redDuration?.Value??.85f;
    internal static float GoldDuration=>goldDuration?.Value??.45f;
    internal static bool OutlineEnabled=>outline?.Value==true;
    internal static float EffectSpeed=>Mathf.Clamp(effectSpeed?.Value??1f,.25f,3f);
    internal static float EffectScale=>Mathf.Clamp(effectScale?.Value??1.6f,.25f,3f);
    internal static float EffectGlow=>Mathf.Clamp(effectGlow?.Value??2f,1f,5f);
    internal static bool Visible=>Panel.InputAllowed && !BattleModal.Visible && !UI_LoadUI.m_isLoading && !FICFPEHAJNJ.ODIOKGGBKEI && Time.timeScale>0 &&
        (Fi_Controller.m_isInBattle&&!Fi_Controller.m_isEnd || De_Controller.m_isInDebate&&De_Controller.EBACHLGLNGD&&!De_Controller.EBACHLGLNGD.m_isEnd);
    struct Burst {internal IntPtr victim;internal Vector3 position,direction;internal double at;internal bool active,heavy;}
    static readonly Burst[] bursts=new Burst[6];
    static int cursor;
    static Texture2D core,streak;
    static float Duration(bool heavy)=>.30f;
    static int notes;static double noteAt;
    internal static void Note(string text){
        double now=Time.unscaledTimeAsDouble;if(now-noteAt>60){noteAt=now;notes=0;}
        if(notes++<20)DuelCounters.StabilityNote("visual " +text);
    }
    internal static int Cost(ImpactTiming.Attack a){
        try{return a==null?0:a.debate?a.debater.m_debateAttribute.HIMGMJJKFFL(a.debateCard,false):a.fighter.GetAttribute().HIMGMJJKFFL(a.fightCard,false);}
        catch(Exception ex){Note("cost fallback: "+ex.Message);return 0;}
    }
    internal static void Install(DuelCounters mod){
        warningEffects=mod.Config.Bind("Effects","WarningEffectsVisible",true,"Show both gold warning flash and heavy red/gold warning effects. Does not change audio, parry input or success sparks.");
        outline=mod.Config.Bind("Effects","HeavyNativeEffects",true,"AP cost 4 or more: simultaneous native buff080 and 音波04 red/gold effects. Replaces the old outline. All attack costs also show a half-size gold flash.");
        redDuration=mod.Config.Bind("Effects","RedWarningDuration",.85f,new ConfigDescription("Real seconds for the red warning visual; does not change parry input windows.",new AcceptableValueRange<float>(.35f,1.5f)));
        goldDuration=mod.Config.Bind("Effects","GoldWarningDuration",.45f,new ConfigDescription("Real seconds for the gold warning visual.",new AcceptableValueRange<float>(.2f,1f)));
        effectSpeed=mod.Config.Bind("Effects","HeavyEffectSpeed",1f,new ConfigDescription("Native warning particle playback speed, independent of combat speed and parry windows.",new AcceptableValueRange<float>(.25f,3f)));
        effectScale=mod.Config.Bind("Effects","HeavyEffectScale",1.6f,new ConfigDescription("Size of both native red/gold effects.",new AcceptableValueRange<float>(.25f,3f)));
        effectGlow=mod.Config.Bind("Effects","HeavyEffectGlow",2f,new ConfigDescription("Native additive HDR glow intensity; does not tint actor materials.",new AcceptableValueRange<float>(1f,5f)));
        var expanded=mod.Config.Bind("Effects","ExpandedWarningDefaultsApplied",false,"One-time migration for the enlarged native warning preset.");
        if(!expanded.Value){if(Math.Abs(effectScale.Value-1f)<.0001f)effectScale.Value=1.6f;expanded.Value=true;}
        success=mod.Config.Bind("Effects","SuccessBurst",true,"Weapon-contact white flash and asymmetric steel sparks. No circular wave, hit stop or camera shake.");
        scale=mod.Config.Bind("Effects","SuccessBurstScale",1f,new ConfigDescription("Size of the successful counter burst.",new AcceptableValueRange<float>(.5f,2)));
        extraImpact=mod.Config.Bind("Effects","ExtraSuccessImpact",true,"Additional white-hot spark filaments and short crossing contact glint. No rings. Bounded to six concurrent bursts.");
        impactScale=mod.Config.Bind("Effects","ExtraSuccessImpactScale",1.3f,new ConfigDescription("Size of the additional successful-parry impact only.",new AcceptableValueRange<float>(.5f,2)));
        var vivid=mod.Config.Bind("Effects","VividImpact120Applied",false,"One-time larger/brighter warning preset; never changes parry input windows.");
        var shortAgain=mod.Config.Bind("Effects","OriginalEffectDuration123Applied",false,"Restore the pre-enlargement visual durations once; preserve larger/brighter visuals and input windows.");
        if(!shortAgain.Value){
            if(vivid.Value){
                redDuration.Value=Math.Abs(redDuration.Value-1.1f)<.026f?.85f:Mathf.Clamp(redDuration.Value/1.3f,.35f,1.5f);
                goldDuration.Value=Math.Abs(goldDuration.Value-.6f)<.026f?.45f:Mathf.Clamp(goldDuration.Value/1.3f,.2f,1f);
            }
            shortAgain.Value=true;
        }
        if(!vivid.Value){
            effectScale.Value=Mathf.Clamp(effectScale.Value*1.25f,.25f,3f);
            effectGlow.Value=Mathf.Clamp(effectGlow.Value*1.5f,1f,5f);
            vivid.Value=true;
        }
        // Small immutable textures, once at load rather than during first combat.
        try{core=Make(64,64,0);streak=Make(64,16,2);}
        catch(Exception e){DuelCounters.Warn("패링 성공 효과 준비: "+e.Message);}
    }
    internal static void Settings(){
        Panel.Switch("행동력 4 이상 · buff080 + 音波04 붉은 금빛 효과",outline);
        Panel.Slider("붉은 금빛 효과 표시시간 (초)",redDuration,.35f,1.5f,.05f);
        Panel.Slider("공통 · 금빛 섬광 지속시간 (초)",goldDuration,.2f,1,.05f);
        Panel.Slider("붉은 금빛 효과 재생 속도",effectSpeed,.25f,3,.05f);
        Panel.Slider("붉은 금빛 효과 크기",effectScale,.25f,3,.05f);
        Panel.Slider("붉은 금빛 효과 발광 강도",effectGlow,1,5,.1f);
        Panel.Switch("성공 · 무기 충돌 지점 흰 섬광 / 금속 스파크",success);
        Panel.Slider("패링 성공 임팩트 크기",scale,.5f,2,.1f);
        Panel.Switch("성공 추가 효과 · 고휘도 불꽃 / 짧은 교차 섬광",extraImpact);
        Panel.Slider("추가 충돌 효과 크기",impactScale,.5f,2,.1f);
    }
    internal static void Success(ImpactTiming.Attack attack,IntPtr attr,GKHLLOOEEFN defender,NPCCtrl debateDefender){
        if(success?.Value!=true || !Visible || attack==null || !attack.Valid())return;
        Transform root=null,attacker=null;
        float height=1.1f;
        if(attack.debate){
            if(debateDefender==null && attack.speech?.m_listTarget!=null)
                foreach(var t in attack.speech.m_listTarget)if(t && t.m_debateAttribute.Pointer==attr){debateDefender=t;break;}
            if(!debateDefender)return;
            root=debateDefender.GetTransform();attacker=attack.debater?.GetTransform();
        }else{
            if(defender==null)return;
            root=defender.GetTransform();attacker=attack.fighter?.GetTransform();
            if(defender.TryCast<AnimalCtrl>()!=null)height=.7f;
        }
        if(!root)return;
        double now=Time.unscaledTimeAsDouble;
        // Merge duplicate callbacks only, not consecutive authored combo hits.
        for(int i=0;i<bursts.Length;i++)if(bursts[i].active && bursts[i].victim==attr && now-bursts[i].at<.025)return;
        Vector3 incoming=attacker?attacker.position-root.position:root.forward;
        if(incoming.sqrMagnitude<.001f)incoming=root.forward;
        incoming=incoming.normalized;
        Vector3 point=root.position+Vector3.up*height+incoming*.35f;
        if(attack.debate?debateDefender!=null:defender!=null){
            var right=attack.debate?debateDefender.GetRightWeaponCtrl():defender.GetRightWeaponCtrl();
            var left=attack.debate?debateDefender.GetLeftWeaponCtrl():defender.GetLeftWeaponCtrl();
            var offensive=attack.debate?attack.debater?.GetRightWeaponCtrl():attack.fighter?.GetRightWeaponCtrl();
            if(!offensive)offensive=attack.debate?attack.debater?.GetLeftWeaponCtrl():attack.fighter?.GetLeftWeaponCtrl();
            Vector3 reference=attacker?attacker.position+Vector3.up*height:point+incoming;
            if(WeaponPoint(offensive,point,out var tip))reference=tip;
            bool hasRight=WeaponPoint(right,reference,out var r),hasLeft=WeaponPoint(left,reference,out var l);
            if(hasRight||hasLeft){
                var candidate=hasRight&&(!hasLeft||(r-reference).sqrMagnitude<=(l-reference).sqrMagnitude)?r:l;
                // Do not place a ranged/magic parry halfway across the arena.
                if((candidate-root.position).sqrMagnitude<16f)point=candidate;
                if(WeaponPoint(offensive,point,out tip)&&(tip-point).sqrMagnitude<.36f)point=(point+tip)*.5f;
            }
        }
        // Freeze the contact point in world space: recoil/movement must not drag sparks.
        bursts[cursor]=new Burst{victim=attr,position=point,direction=incoming,at=now,active=true,heavy=Cost(attack)>=4};cursor=(cursor+1)%bursts.Length;
    }
    // Native weapon renderers provide the visible weapon bounds. This is a
    // bounded contact estimate (not a physics collision), no skeleton scan.
    static bool WeaponPoint(WeaponCtrl weapon,Vector3 toward,out Vector3 point){
        point=default;if(!weapon||!weapon.gameObject.activeInHierarchy)return false;
        var renderers=weapon.m_listRenderer;float best=float.MaxValue;bool found=false;
        if(renderers!=null)for(int i=0;i<Math.Min(16,renderers.Count);i++){
            var renderer=renderers[i];if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
            var candidate=renderer.bounds.ClosestPoint(toward);float distance=(candidate-toward).sqrMagnitude;
            if(float.IsFinite(distance)&&distance<best){best=distance;point=candidate;found=true;}
        }
        if(!found){point=weapon.transform.position;found=float.IsFinite(point.sqrMagnitude);}
        return found;
    }
    internal static void Draw(){
        if(!Visible || success?.Value!=true || Event.current==null || Event.current.type!=EventType.Repaint)return;
        double now=Time.unscaledTimeAsDouble;bool any=false;
        for(int i=0;i<bursts.Length;i++){
            if(bursts[i].active && now-bursts[i].at>=Duration(bursts[i].heavy))bursts[i].active=false;
            any|=bursts[i].active;
        }
        if(!any)return;
        var camera=Camera.main;if(!camera || !core || !streak)return;
        var old=GUI.color;var matrix=GUI.matrix;int depth=GUI.depth;
        try{
            GUI.depth=-950;
            for(int i=0;i<bursts.Length;i++){
                var b=bursts[i];if(!b.active)continue;
                var point=camera.WorldToScreenPoint(b.position);
                if(point.z<=0 || point.x<0 || point.x>Screen.width || point.y<0 || point.y>Screen.height)continue;
                var toward=camera.WorldToScreenPoint(b.position+b.direction);
                float axis=Mathf.Atan2(point.y-toward.y,toward.x-point.x)*Mathf.Rad2Deg;
                float t=Mathf.Clamp01((float)(now-b.at)/Duration(b.heavy)),fade=(1-t)*(1-t);
                float size=Mathf.Min(Screen.width,Screen.height)*.2125f*scale.Value*(b.heavy?1.2f:1f);
                float x=point.x,y=Screen.height-point.y;
                // Tight, white-hot contact flash, then fast irregular steel shards.
                GUI.matrix=matrix;
                GUI.color=new Color(1,.96f,.76f,Mathf.Clamp01(1-t*3.6f));
                float c=size*(.28f-.12f*t);GUI.DrawTexture(new Rect(x-c/2,y-c/2,c,c),core);
                int count=extraImpact.Value?18:12;
                float boost=extraImpact.Value?Mathf.Clamp(impactScale.Value,.5f,2):1;
                for(int j=0;j<count;j++){
                    GUI.matrix=matrix;
                    // Unequal lengths, launch angles and side lobes avoid a ring.
                    float angle=axis+SparkAngle(j,i);
                    GUIUtility.RotateAroundPivot(angle,new Vector2(x,y));
                    float speed=.28f+(j*7%11)*.045f;
                    float travel=size*(.012f+t*speed)*boost;
                    float length=size*(.09f+(j*5%7)*.025f)*(1-t*.8f)*boost;
                    float width=size*(.009f+(j%3)*.003f)*(1-t*.7f)*1.5f;
                    float fall=size*t*t*.13f;
                    GUI.color=new Color(1,(b.heavy?.40f:.68f)+.045f*(j%5),b.heavy?.12f:.32f,fade);
                    GUI.DrawTexture(new Rect(x+travel,y-width+fall,length,width*2),streak);
                    if(extraImpact.Value){
                        GUI.color=new Color(1,1,.94f,fade);
                        GUI.DrawTexture(new Rect(x+travel,y-width*.3f+fall,length*.82f,width*.85f),streak);
                    }
                }
                if(extraImpact.Value&&t<.3f){
                    for(int j=0;j<2;j++){
                        GUI.matrix=matrix;GUIUtility.RotateAroundPivot(axis+(j==0?-27:39),new Vector2(x,y));
                        float len=size*(j==0?.75f:.44f)*boost,width=size*.027f;
                        GUI.color=new Color(1,1,1,Mathf.Clamp01(1-t/.3f));
                        GUI.DrawTexture(new Rect(x-len/2,y-width/2,len,width),streak);
                    }
                }
            }
        }finally{GUI.color=old;GUI.matrix=matrix;GUI.depth=depth;}
    }
    internal static float SparkAngle(int shard,int burst){
        float spread=(shard*47+burst*17)%117-58;
        return shard%4==0?spread+155:spread;
    }
    static Texture2D Make(int w,int h,int kind){
        var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color32[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            float u=(2*x+1f)/w-1,v=(2*y+1f)/h-1,r=Mathf.Sqrt(u*u+v*v);
            float a=kind==0?Mathf.Exp(-r*r*8)*Mathf.Clamp01((1-r)*6):
                Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(v)),3)*Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(u)),.5f);
            pixels[y*w+x]=new Color(1,1,1,a);
        }
        tex.SetPixels32(pixels);tex.Apply(false,true);return tex;
    }
    internal static void Reset(){Array.Clear(bursts,0,bursts.Length);cursor=0;CounterGlow.Reset();}
#else
    internal static int Cost(ImpactTiming.Attack a)=>0;
    internal static void Install(DuelCounters m){}
    internal static void Settings(){}
    internal static void Success(ImpactTiming.Attack a,IntPtr p,GKHLLOOEEFN f,NPCCtrl d){}
    internal static void Reset(){}
    internal static void Draw(){}
#endif
}
