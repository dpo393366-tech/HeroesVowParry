using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
namespace Vow.Counters;
internal static class DodgeSettings {
 static ConfigEntry<bool> enabled;
 static ConfigEntry<int> ceiling,consecutive;
 static FieldInfo error;
 internal static void Connect(){
  if(enabled!=null)return;
  foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
   if(a.GetName().Name!="DodgeLimit")continue;
   var t=a.GetType("Vow.Dodge.DodgeLimit");if(t==null)continue;
   const BindingFlags f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
   var e=t.GetField("Enabled",f)?.GetValue(null) as ConfigEntry<bool>;
   var c=t.GetField("Ceiling",f)?.GetValue(null) as ConfigEntry<int>;
   var n=t.GetField("Consecutive",f)?.GetValue(null) as ConfigEntry<int>;
   if(e==null||c==null||n==null)continue;
   enabled=e;ceiling=c;consecutive=n;error=t.GetField("LoadError",f);
   AppDomain.CurrentDomain.SetData("HeroesVow.Parry.EmbeddedDodge",true);
  }
 }
 internal static void Draw(){
  GUILayout.Space(14);GUILayout.Label("회피 제한 · 패링 ON/OFF와 별도");
  if(enabled==null){GUILayout.Label("회피 제한 DLL을 불러오지 못했습니다. 설치 로그를 확인하세요.");return;}
  string message=error?.GetValue(null) as string;
  if(!string.IsNullOrEmpty(message)){GUILayout.Label(message);return;}
  bool value=GUILayout.Toggle(enabled.Value,"회피 제한 사용 (아군 · 적군 · 짐승)");if(value!=enabled.Value)enabled.Value=value;
  Slider("최대 회피 확률 (%)",ceiling,0,70);
  Slider("연속 회피 허용 횟수 · 다음 공격은 회피 불가",consecutive,1,10);
 }
 static void Slider(string label,ConfigEntry<int> entry,int min,int max){
  GUILayout.Label(label+"  "+entry.Value);
  int value=Mathf.Clamp(Mathf.RoundToInt(GUILayout.HorizontalSlider(entry.Value,min,max)),min,max);
  if(value!=entry.Value)entry.Value=value;
 }
}
