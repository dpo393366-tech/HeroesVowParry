using System;
using System.Reflection;
using UnityEngine;
namespace Vow.Counters;
internal static class StandaloneSettings {
 static object legacy;
 static FieldInfo open;
 static float next;
 internal static bool Attached=>legacy!=null;
 internal static bool LegacyOpen=>legacy!=null&&(bool)open.GetValue(legacy);
 internal static void TryAttach(){
  if(Attached||Time.unscaledTime<next)return;next=Time.unscaledTime+2;
  CompactSettings.Connect(typeof(DuelCounters).Assembly);
  foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
   if(a.GetName().Name!="HeroesVowSettings")continue;
   var type=a.GetType("Vow.Settings.SettingsPanel");const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
   try{
    var menu=type?.GetField("menu",flags)?.GetValue(null);if(menu==null)continue;
    var add=menu.GetType().GetMethod("Add",flags);var opening=menu.GetType().GetField("open",flags);
    if(add==null||opening==null)continue;
    add.Invoke(menu,new object[]{"T","무투·설전 패링",(Action)Draw});
    var tabs=menu.GetType().GetField("tabs",flags)?.GetValue(menu) as System.Collections.IEnumerable;
    if(tabs==null)continue;
    foreach(var tab in tabs)if((string)tab.GetType().GetField("key",flags).GetValue(tab)=="T")
     tab.GetType().GetField("draw",flags).SetValue(tab,(Action)Draw);
    legacy=menu;open=opening;
   }catch(Exception ex){DuelCounters.Warn("F11 optional integration: "+ex.Message);}
  }
 }
 internal static void Draw(){
  bool value=GUILayout.Toggle(ModControl.Desired,"패링 모드 사용 · "+ModControl.Status);
  if(value!=ModControl.Desired)ModControl.Request(value);
  if(ModControl.Pending)GUILayout.Label("진행 중인 전투가 끝나면 적용됩니다.");
  CompactSettings.Connect(typeof(DuelCounters).Assembly);CompactSettings.Draw();
 }
}
