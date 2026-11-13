using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace Vow.Counters;
internal static class ModControl {
 const string Owner="local.heroesvow.counters";
 static ConfigEntry<bool> enabled;
 static OwnedPatches hooks;
 static SwitchState state;
 static int idleFrames;
 internal static bool Active=>state?.Active==true;
 internal static bool Desired=>state?.Desired==true;
 internal static bool Pending=>state?.Pending==true;
 internal static string Error{get;private set;}="";
 internal static void Initialize(DuelCounters mod,Harmony combat){
  enabled=mod.Config.Bind("Module","Enabled",true,"MOD manager parry ON/OFF. Changes during combat take effect after the battle ends.");
  hooks=new OwnedPatches(combat);state=new SwitchState(true);
  NativeModMenu.Install();
  Request(enabled.Value);
  if(!Battle())Apply();
  DuelCounters.StabilityNote($"Standalone mod-manager adapter ready; parry active={Active}; owned hooks={hooks.Count}");
 }
 static bool Battle()=>Fi_Controller.m_isInBattle||De_Controller.m_isInDebate||UI_LoadUI.m_isLoading;
 internal static void Request(bool value){if(state==null)return;state.Request(value);enabled.Value=value;idleFrames=0;Error="";}
 internal static void Tick(){
  if(state==null)return;
  if(enabled.Value!=state.Desired)Request(enabled.Value);
  if(!state.CanApply(Battle())){idleFrames=0;return;}
  if(++idleFrames>=3)Apply();
 }
 static void Apply(){
  if(state==null||!state.CanApply(Battle()))return;
  try{
   DuelCounters.Suspend();
   hooks.Set(state.Desired);state.Commit();idleFrames=0;
   DuelCounters.StabilityNote("MOD manager parry "+(Active?"ON":"OFF"));
  }catch(Exception ex){
   // Restore prior state on a partial hook operation. Never report success on failure.
   try{hooks.Set(state.Active);}catch(Exception recovery){DuelCounters.Warn("Parry toggle rollback: "+recovery.Message);}
   state.Fail();enabled.Value=state.Active;Error="전환 실패 · 로그를 확인하세요";DuelCounters.Warn("Parry toggle failed: "+ex.Message);
  }
 }
 internal static string Status=>Error.Length>0?Error:Pending?(Desired?"전투 종료 후 ON":"전투 종료 후 OFF"):(Active?"ON · 사용 중":"OFF · 사용 안 함");
}
