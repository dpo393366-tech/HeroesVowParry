using System;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
namespace Vow.Counters {
 internal static class CompactSettings {
  internal static ConfigEntry<bool> warningEffects;
  internal sealed class Control {
   internal readonly string module,type,field,label;internal readonly float min,max,step;
   internal ConfigEntry<float> entry;
   internal Control(string module,string type,string field,string label,float min,float max,float step){this.module=module;this.type=type;this.field=field;this.label=label;this.min=min;this.max=max;this.step=step;}
  }
  internal static readonly Control[] controls={
   new("DuelCounters","Vow.Counters.DuelCounters","quickBasicSpeed","단타 공격 모션 배속",.25f,1,.05f),
   new("DuelCounters","Vow.Counters.DuelCounters","cueWindow","섬광·소리 후 패링 입력 시간 (초)",.05f,2,.05f),
   new("DuelCounters","Vow.Counters.DuelCounters","earlyCueWindow","섬광 전 미리 가드 인정 (초)",0,1,.01f),
   new("DuelCounters","Vow.Counters.DuelCounters","guardWindow","가드 1회 연타 방어 유지 (초)",0,.6f,.01f),
   new("DuelCounters","Vow.Counters.DuelCounters","enemyCeiling","적 최대 자동 패링 확률 (%)",0,100,1),
   new("DuelCounters","Vow.Counters.EnemyGroggy","threshold","그로기까지 필요한 패링 횟수",1,50,1),
   new("DuelCounters","Vow.Counters.EnemyGroggy","damageMultiplier","그로기 중 받는 피해 배율",1,5,.1f),
   new("DuelCounters","Vow.Counters.StrengthPressure","maxSpeed","강적 최대 공격 배속 · 비례 적용 / 다음 전투",1,2,.05f),
   new("DuelCounters","Vow.Counters.StrengthPressure","minWindow","강적 최소 패링 시간 (초) · 다음 전투",.05f,2,.01f),
  };
  internal static void Connect(Assembly assembly){
   if(warningEffects==null&&assembly.GetName().Name=="DuelCounters")
    warningEffects=assembly.GetType("Vow.Counters.ParryVisuals",false)?.GetField("warningEffects",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null) as ConfigEntry<bool>;
   foreach(var c in controls)if(c.entry==null&&assembly.GetName().Name==c.module)
    c.entry=assembly.GetType(c.type,false)?.GetField(c.field,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null) as ConfigEntry<float>;
  }
  internal static void Draw(){
   if(warningEffects!=null){
    bool shown=GUILayout.Toggle(warningEffects.Value,"패링 이펙트 표시 · 금빛 섬광 / 붉은 예고");
    if(shown!=warningEffects.Value)warningEffects.Value=shown;
   }
   foreach(var c in controls){
    if(c.entry==null){GUILayout.Label(c.label+" · 해당 모드 로딩 대기");continue;}
    GUILayout.Label(c.label+"   "+c.entry.Value.ToString("0.##"));
    float current=c.entry.Value,raw=GUILayout.HorizontalSlider(current,c.min,c.max);
    // Opening the window must not round or reset existing custom values.
    if(raw==current)continue;
    float value=Mathf.Clamp(Mathf.Round(raw/c.step)*c.step,c.min,c.max);
    if(Math.Abs(value-current)>.00001f)c.entry.Value=value;
   }
  }
 }
}
