using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Vow.Dodge;

[BepInPlugin("local.heroesvow.dodgelimit","Dodge limit and streak guard","1.1.0")]
public sealed class DodgeLimit:BasePlugin
{
    internal static DodgeLimit Mod;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<int> Ceiling,Consecutive;
    static readonly DodgeRules rules=new();
    [ThreadStatic] static Frame frame;
    static bool warned;
    public static string LoadError="";
    sealed class Frame{
        internal Fi_Controller.FMHMOHIGKCA routine;
        internal IntPtr defender;
        internal int accuracy,evade;
        internal bool ready,recorded;
    }
    public override void Load(){
        Mod=this;
        Enabled=Config.Bind("Dodge","Enabled",true,"무투 회피 상한과 연속 회피 제한 사용. 아군·적군·짐승 모두 적용.");
        Ceiling=Config.Bind("Dodge","MaxPercent",15,new ConfigDescription("최종 회피 확률 상한 (%)",new AcceptableValueRange<int>(0,70)));
        Consecutive=Config.Bind("Dodge","MaxConsecutiveDodges",4,new ConfigDescription("연속 회피 후 다음 공격은 회피 불가. 피격 시 초기화.",new AcceptableValueRange<int>(1,10)));
        Enabled.SettingChanged+=(_,_)=>Clear();
        Ceiling.SettingChanged+=(_,_)=>Clear();
        Consecutive.SettingChanged+=(_,_)=>Clear();
        var h=new Harmony("local.heroesvow.dodgelimit");
        AddComponent<DodgePanel>();
        try{
            h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller.FMHMOHIGKCA),"MoveNext"),prefix:P(nameof(Enter)),finalizer:P(nameof(Leave)));
            h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"OCANHOIMGLN"),postfix:P(nameof(Evade)));
            h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"DNMHDJJMIJA"),postfix:P(nameof(Decision)));
            h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),"TriggerBuff"),prefix:P(nameof(Outcome)));
            h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller.JFBIHELODLP),"MoveNext"),prefix:P(nameof(BeforeDamage)),postfix:P(nameof(HitResolved)));
            foreach(var name in new[]{"StartBattle","EndBattle","QuitBattle"})h.Patch(AccessTools.DeclaredMethod(typeof(Fi_Controller),name),prefix:P(nameof(Clear)));
            h.Patch(AccessTools.DeclaredMethod(typeof(NFIDHHGKMDC),"LEJICHHIEGH"),postfix:P(nameof(Joining)));
            Log.LogInfo($"회피 제한 준비 완료: 최대 {Ceiling.Value}%, 연속 {Consecutive.Value}회 후 회피 불가. F11에서 조절.");
        }catch(Exception ex){h.UnpatchSelf();LoadError="회피 제한 초기화 실패 · 게임 구조 확인 필요";Log.LogWarning(LoadError+": "+ex);}
    }
    static HarmonyMethod P(string name)=>new(typeof(DodgeLimit),name);
    static void Clear(){rules.Clear();}
    static void Joining(NFIDHHGKMDC __instance)=>rules.Forget(__instance.Pointer);
    static void Enter(Fi_Controller.FMHMOHIGKCA __instance,out Frame __state){
        __state=frame;frame=Enabled.Value?new Frame{routine=__instance}:null;
    }
    static void Leave(Frame __state){frame=__state;}
    static bool IsTarget(NFIDHHGKMDC a){
        var target=frame?.routine?._cellTarget_5__14;
        return Enabled.Value && target!=null && a!=null && target.GetAttribute()?.Pointer==a.Pointer;
    }
    static void Error(Exception e){if(!warned){warned=true;Mod.Log.LogWarning("회피 제한 판정 실패: "+e);}}
    static void Evade(NFIDHHGKMDC __instance,bool __0,ref int __result){
        try{
            if(__0 || !IsTarget(__instance) || Fi_Controller.m_curHero==null)return;
            var f=frame;
            f.accuracy=Fi_Controller.m_curHero.GetAttribute().OICDHNHGPHP(false);
            f.evade=__result;f.defender=__instance.Pointer;f.ready=true;f.recorded=false;
            // Force the native random branch to reach its final dodge check. Only this
            // synchronous target evaluation changes; UI values and saved stats do not.
            __result=unchecked(f.accuracy-100);
        }catch(Exception e){Error(e);}
    }
    static void Decision(NFIDHHGKMDC __instance,ref bool __result){
        try{
            var f=frame;
            if(f==null || !f.ready || f.defender!=__instance.Pointer || !IsTarget(__instance))return;
            int native=DodgeRules.NativePercent(f.accuracy,f.evade,GLIJMELMEHI.BOJHKIIJLLG,__instance.MCMCPMECLJH(),__result);
            int percent=DodgeRules.CappedPercent(native,Ceiling.Value,rules.MustHit(f.defender,Consecutive.Value));
            __result=percent>0 && DodgeRules.Roll(percent,AOHIBKLOAEI.FBBNHMFIKCM(0,100));
        }catch(Exception e){__result=false;Error(e);}
    }
    static void Outcome(GKHLLOOEEFN __1,LADIINNDGLB __2){
        try{
            var f=frame;
            if(f==null || !f.ready || f.recorded || __1==null || __1.GetAttribute()?.Pointer!=f.defender)return;
            if(__2!=LADIINNDGLB.t_be_dodge)return;
            f.recorded=true;rules.Record(f.defender,true);
        }catch(Exception e){Error(e);}
    }
    static void BeforeDamage(Fi_Controller.JFBIHELODLP __instance,out bool __state){__state=__instance.__1__state==0;}
    static void HitResolved(bool __state,bool __runOriginal){
        try{
            // A pre-animation "hit" can still be parried later. Reset only when the
            // actual damage iterator runs, never while the counter mod is holding it.
            if(!Enabled.Value || !__state || !__runOriginal)return;
            var card=Fi_Controller.m_tmpCard??Fi_Controller.m_curCard;
            if(card?.HFBAJIGHOAF!=KJFOPOCBHBM.CCENIDMDCCN.Attack)return;
            var targets=Fi_Controller.FPGNHJGOCGJ??Fi_Controller.m_listTargetHero;
            if(targets==null)return;
            foreach(var t in targets){var a=t?.GetAttribute();if(a!=null && !a.EDJIDCFHLOP)rules.Record(a.Pointer,false);}
        }catch(Exception e){Error(e);}
    }
}

public sealed class DodgePanel:MonoBehaviour
{
    public DodgePanel(IntPtr p):base(p){}
    bool open;
    public void Update(){if(Input.GetKeyDown(KeyCode.F11))open=!open;}
    public void OnGUI(){
        if(!open || AppDomain.CurrentDomain.GetData("HeroesVow.Parry.EmbeddedDodge") is true)return;
        float x=Mathf.Max(8,(Screen.width-540)*.5f),y=445;
        GUI.Box(new Rect(x,y,538,190),"Q · 회피 제한");
        GUILayout.BeginArea(new Rect(x+12,y+28,515,155));
        try{
            bool on=GUILayout.Toggle(DodgeLimit.Enabled.Value,"회피 제한 사용 (아군 · 적군 · 짐승)");
            if(on!=DodgeLimit.Enabled.Value)DodgeLimit.Enabled.Value=on;
            Slider("최대 회피 확률 (%)",DodgeLimit.Ceiling,0,70);
            Slider("연속 회피 허용 횟수 · 다음 공격은 회피 불가",DodgeLimit.Consecutive,1,10);
        }finally{GUILayout.EndArea();}
    }
    static void Slider(string label,ConfigEntry<int> entry,int min,int max){
        GUILayout.Label(label+"  "+entry.Value);
        int n=Mathf.Clamp(Mathf.RoundToInt(GUILayout.HorizontalSlider(entry.Value,min,max)),min,max);
        if(n!=entry.Value)entry.Value=n;
    }
}
