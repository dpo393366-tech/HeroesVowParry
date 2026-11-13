using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Vow.Counters;
internal static class NativeModMenu {
 internal const string AdapterOwner="local.heroesvow.parry.modmenu";
 static UI_ModUI owner;
 static UI_ModRowUI row;
 static int settle;
 static bool busy;
 static string lastStatus;
 internal static void Install(){
  var h=new Harmony(AdapterOwner);
  try{
   foreach(var n in new[]{"Start","InitListMod","RefreshListMod","OpenUI"})
    h.Patch(AccessTools.DeclaredMethod(typeof(UI_ModUI),n),postfix:new HarmonyMethod(typeof(NativeModMenu),nameof(Ensure)){priority=Priority.Last});
   h.Patch(AccessTools.DeclaredMethod(typeof(UI_ModUI),"Update"),postfix:new HarmonyMethod(typeof(NativeModMenu),nameof(Tick)){priority=Priority.Last});
   foreach(var n in new[]{"ToggleChange","ButtonPress"})h.Patch(AccessTools.DeclaredMethod(typeof(UI_ModRowUI),n),prefix:new HarmonyMethod(typeof(NativeModMenu),n=="ToggleChange"?nameof(Toggle):nameof(Press)));
  }catch{h.UnpatchSelf();throw;}
 }
 static bool Same(UI_ModRowUI r)=>row&&r&&row.Pointer==r.Pointer;
 static void Ensure(UI_ModUI __instance){
  if(busy||!__instance||!__instance.m_parentTransform||!__instance.m_rowPrefab)return;
  busy=true;
  try{
   if(!row||!owner||owner.Pointer!=__instance.Pointer){
    if(row)Object.Destroy(row.gameObject);
    owner=__instance;
    GameObject template=owner.m_rowPrefab;
    for(int i=0;i<owner.m_parentTransform.childCount;i++){
     var candidate=owner.m_parentTransform.GetChild(i).GetComponent<UI_ModRowUI>();
     if(candidate&&candidate.gameObject.activeSelf){template=candidate.gameObject;break;}
    }
    var obj=Object.Instantiate(template,owner.m_parentTransform,false);
    row=obj.GetComponent<UI_ModRowUI>();
    if(!row){Object.Destroy(obj);throw new InvalidOperationException("Native MOD row component missing");}
    obj.name="HeroesVowParry_ModToggle";row.m_index=-2070;row.m_baseUI=owner;
    // Native rows bind runtime listeners to their original instance. A clone must
    // own fresh events, otherwise its checkbox can operate on the source MOD.
    row.m_toggle.onValueChanged=new Toggle.ToggleEvent();
    row.m_toggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)(value=>{ModControl.Request(value);lastStatus=null;Refresh();}));
    foreach(var button in obj.GetComponentsInChildren<Button>(true)){
     button.onClick=new Button.ButtonClickedEvent();
     button.onClick.AddListener((UnityEngine.Events.UnityAction)(()=>{ModControl.Request(!ModControl.Desired);lastStatus=null;Refresh();}));
    }
    obj.SetActive(true);lastStatus=null;
    DuelCounters.StabilityNote("MOD menu parry row created");
   }
   Refresh();Layout();settle=3;
  }catch(Exception ex){DuelCounters.Warn("Parry MOD menu: "+ex.Message);}finally{busy=false;}
 }
 static void Refresh(){
  if(!row)return;
  string status=ModControl.Status;
  if(status==lastStatus)return;lastStatus=status;
  row.m_textName.text="무투·설전 패링";
  row.m_textType.text="독립 모드";
  row.m_textDescription.text=status+" · 체크로 즉시 저장 / F11 세부 설정";
  row.m_toggle.SetIsOnWithoutNotify(ModControl.Desired);row.m_toggle.interactable=true;
  if(row.m_imageHighlight)row.m_imageHighlight.enabled=false;
 }
 static void Tick(UI_ModUI __instance){
  if(!owner||!row||owner.Pointer!=__instance.Pointer||!owner.gameObject.activeInHierarchy)return;
  Refresh();if(settle>0){settle--;Layout();if(settle==0)DuelCounters.StabilityNote("MOD parry layout: active="+row.gameObject.activeInHierarchy+" position="+row.GetComponent<RectTransform>().anchoredPosition+" scale="+row.transform.localScale+" content="+owner.m_parentTransform.rect);}
 }
 static void Layout(){
  if(!row||!owner)return;
  var parent=owner.m_parentTransform;
  var rect=row.GetComponent<RectTransform>();
  if(!rect)return;
  row.gameObject.SetActive(true);rect.SetAsLastSibling();
  for(int i=0;i<parent.childCount;i++){
   var child=parent.GetChild(i);if(child.Pointer==rect.Pointer||!child.gameObject.activeSelf)continue;
   if(!child.GetComponent<UI_ModRowUI>())continue;
   var template=child.GetComponent<RectTransform>();if(!template)continue;
   rect.anchorMin=template.anchorMin;rect.anchorMax=template.anchorMax;rect.pivot=template.pivot;
   rect.sizeDelta=template.sizeDelta;rect.localScale=template.localScale;rect.localRotation=template.localRotation;break;
  }
  var group=parent.GetComponent<LayoutGroup>();
  if(group&&group.enabled)LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
  // Append after actual native AND other plugin rows. Never reposition another plugin's entry.
  float height=Mathf.Max(24,Mathf.Abs(rect.rect.height)),bottom=0,x=rect.anchoredPosition.x;bool found=false;
  for(int i=0;i<parent.childCount;i++){
   var child=parent.GetChild(i);if(child.Pointer==rect.Pointer||!child.gameObject.activeSelf)continue;
   var r=child.GetComponent<UI_ModRowUI>();var rt=child.GetComponent<RectTransform>();
   if(!r||!rt)continue;
   if(!found||rt.anchoredPosition.y<bottom){bottom=rt.anchoredPosition.y;x=rt.anchoredPosition.x;}
   height=Mathf.Max(height,Mathf.Abs(rt.rect.height));found=true;
  }
  rect.anchoredPosition=new Vector2(x,found?bottom-height:0);
  float required=Mathf.Abs(rect.anchoredPosition.y)+height+12;
  if(parent.rect.height<required)parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,required);
 }
 static bool Toggle(UI_ModRowUI __instance){if(!Same(__instance))return true;ModControl.Request(row.m_toggle.isOn);lastStatus=null;Refresh();return false;}
 static bool Press(UI_ModRowUI __instance){if(!Same(__instance))return true;ModControl.Request(!ModControl.Desired);lastStatus=null;Refresh();return false;}
}
