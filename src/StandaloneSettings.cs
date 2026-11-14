using UnityEngine;
namespace Vow.Counters;
internal static class StandaloneSettings {
 static float next;
 // F11 is always owned by this module, even when convenience F12 is installed.
 internal static void Refresh(){
  if(Time.unscaledTime<next)return;next=Time.unscaledTime+2;
  DodgeSettings.Connect();CompactSettings.Connect(typeof(DuelCounters).Assembly);
 }
 internal static void Draw(){
  bool value=GUILayout.Toggle(ModControl.Desired,"패링 모드 사용 · "+ModControl.Status);
  if(value!=ModControl.Desired)ModControl.Request(value);
  if(ModControl.Pending)GUILayout.Label("진행 중인 전투가 끝나면 적용됩니다.");
  CompactSettings.Connect(typeof(DuelCounters).Assembly);CompactSettings.Draw();
  DodgeSettings.Draw();
 }
}
