using System;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
#if DuelCounters
namespace Vow.Counters;
#elif FieldEncounters
namespace Vow.Encounters;
#elif ReserveWaves
namespace Vow.Waves;
#elif FramePacing
namespace Vow.Performance;
#else
namespace Vow.Governors;
#endif

public sealed class Panel : MonoBehaviour
{
    public Panel(IntPtr p):base(p){}
    internal static Action Draw;
    internal static Action Overlay;
    internal static string key,title;
    internal static int index;
    static bool opened;
    internal static bool InputAllowed=>!opened&&!StandaloneSettings.LegacyOpen;
#if DuelCounters
    static Panel runner;
    internal static void Run(System.Collections.IEnumerator routine){runner.StartCoroutine(BepInEx.Unity.IL2CPP.Utils.Collections.CollectionExtensions.WrapToIl2Cpp(routine));}
    public void Awake(){runner=this;}
#endif
    const string Selection="HeroesVow.Expansion.SettingsTab";
    internal static void Install(BasePlugin plugin,string k,string t,int i,Action draw)
    {
        key=k;title=t;index=i;Draw=draw;
        if(AppDomain.CurrentDomain.GetData(Selection)==null)AppDomain.CurrentDomain.SetData(Selection,k);
        plugin.AddComponent<Panel>();
    }
    public void Update(){
        ModControl.Tick();StandaloneSettings.TryAttach();
        if(!StandaloneSettings.Attached&&Input.GetKeyDown(KeyCode.F11))opened=!opened;
        if(!ModControl.Active)return;
#if DuelCounters
        long started=CombatCost.Start();
        try{ImpactTiming.Tick();if(InputAllowed)DuelCounters.PollInput();}
        finally{CombatCost.End(0,started);CombatCost.Frame();}
#elif FieldEncounters
        FieldEncounters.SyncAuras();
        FieldEncounters.TickRewards();
#endif
    }
#if DuelCounters
    public void LateUpdate(){if(!ModControl.Active)return;long started=CombatCost.Start();try{CounterGlow.RefreshAll();AnimalDodgeSync.Tick();CounterMotion.Tick();TwoHandBrace.Tick();EnemyGroggy.Tick();}finally{CombatCost.End(1,started);}}
#endif
    Vector2 scroll;
    public void OnGUI(){
        Overlay?.Invoke();
        if(!opened||StandaloneSettings.Attached)return;
        float width=Mathf.Min(650,Screen.width-24),height=Mathf.Min(670,Screen.height-40);
        var box=new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height);
        GUI.Box(box,"무투·설전 패링 · F11");
        if(GUI.Button(new Rect(box.xMax-68,box.y+5,58,25),"닫기"))opened=false;
        GUILayout.BeginArea(new Rect(box.x+12,box.y+36,width-24,height-48));
        try{scroll=GUILayout.BeginScrollView(scroll);try{Draw?.Invoke();}finally{GUILayout.EndScrollView();}}finally{GUILayout.EndArea();}
    }
    internal static void Switch(string label,ConfigEntry<bool> entry){
        var value=GUILayout.Toggle(entry.Value,label);if(value!=entry.Value)entry.Value=value;
    }
    internal static void Slider(string label,ConfigEntry<float> entry,float min,float max,float step){
        GUILayout.Label(label+"   "+entry.Value.ToString("0.##"));
        var value=Mathf.Clamp(Mathf.Round(GUILayout.HorizontalSlider(entry.Value,min,max)/step)*step,min,max);
        if(Mathf.Abs(value-entry.Value)>.001f)entry.Value=value;
    }
    internal static void Hook(Harmony h,Type target,string method,Type owner,string before=null,string after=null){
        var original=AccessTools.DeclaredMethod(target,method)??throw new MissingMethodException(target.FullName,method);
        h.Patch(original,before==null?null:new HarmonyMethod(owner,before),after==null?null:new HarmonyMethod(owner,after));
    }
}
