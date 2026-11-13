using System;
using System.Collections.Generic;
using UnityEngine;
namespace Vow.Counters;

// An in-place guard clip supplies the wrist angles. This short two-bone correction brings
// the supporting hand to the grip even on weapons and retargeted bodies of different lengths.
internal sealed class TwoHandBrace
{
    struct Arm {
        internal Transform top,elbow,wrist;
        internal bool Valid=>top && elbow && wrist;
        internal void Reach(Vector3 target,Vector3 pole,float blend){
            if(!Valid)return;
            Vector3 a=top.position,b=elbow.position,c=wrist.position;
            float l1=Vector3.Distance(a,b),l2=Vector3.Distance(b,c);
            if(l1<.0001f || l2<.0001f)return;
            target=Vector3.Lerp(c,target,blend);
            Vector3 axis=(target-a).normalized;if(axis.sqrMagnitude<.5f)return;
            float reach=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
            float x=(l1*l1-l2*l2+reach*reach)/(2*reach);
            Vector3 bend=Vector3.ProjectOnPlane(pole,axis).normalized;
            var joint=a+axis*x+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-x*x));
            Quaternion wristAngle=wrist.rotation;
            top.rotation=Quaternion.FromToRotation(b-a,joint-a)*top.rotation;
            elbow.rotation=Quaternion.FromToRotation(wrist.position-elbow.position,a+axis*reach-elbow.position)*elbow.rotation;
            wrist.rotation=wristAngle;
        }
    }
    static readonly List<TwoHandBrace> shown=new();
    static readonly Dictionary<(int,int,bool),TwoHandBrace> cached=new();
    readonly Transform root;
    readonly Arm left,right;
    readonly bool bow;
    float elapsed;
    TwoHandBrace(GKHLLOOEEFN hero,Animator animator,bool leftHandWeapon){
        root=hero.GetTransform();bow=leftHandWeapon;
        var bones=animator && animator.isHuman?null:root.GetComponentsInChildren<Transform>(true);
        Transform Bone(HumanBodyBones b,params string[] names){
            if(bones==null)return animator.GetBoneTransform(b);
            foreach(var t in bones)if(t){
                string n=t.name.Replace(" ","").Replace("_","").ToLowerInvariant();
                foreach(string candidate in names)if(n.EndsWith(candidate,StringComparison.Ordinal))return t;
            }
            return null;
        }
        left=new Arm{top=Bone(HumanBodyBones.LeftUpperArm,"leftarm","leftupperarm","lupperarm","cfjarm00l"),elbow=Bone(HumanBodyBones.LeftLowerArm,"leftforearm","leftlowerarm","lforearm","cfjforearm01l"),wrist=Bone(HumanBodyBones.LeftHand,"lefthand","lhand","cfjhandl")};
        right=new Arm{top=Bone(HumanBodyBones.RightUpperArm,"rightarm","rightupperarm","rupperarm","cfjarm00r"),elbow=Bone(HumanBodyBones.RightLowerArm,"rightforearm","rightlowerarm","rforearm","cfjforearm01r"),wrist=Bone(HumanBodyBones.RightHand,"righthand","rhand","cfjhandr")};
    }
    internal static void Start(GKHLLOOEEFN hero,Animator animator,bool bow){
        var root=hero.GetTransform();if(!root)return;
        for(int i=shown.Count-1;i>=0;i--)if(shown[i].root==root)shown.RemoveAt(i);
        var key=(root.GetInstanceID(),animator?animator.GetInstanceID():0,bow);
        if(!cached.TryGetValue(key,out var pose)||!pose.root||!pose.left.Valid||!pose.right.Valid){
            if(cached.Count>=32)cached.Clear();
            cached[key]=pose=new TwoHandBrace(hero,animator,bow);
        }
        pose.elapsed=0;
        if(pose.left.Valid && pose.right.Valid)shown.Add(pose);
    }
    internal static void Clear(){shown.Clear();cached.Clear();}
    internal static void Tick(){
        if(!Fi_Controller.m_isInBattle){shown.Clear();cached.Clear();return;}
        for(int i=shown.Count-1;i>=0;i--){
            var pose=shown[i];if(!pose.root || !Fi_Controller.m_isInBattle || !pose.Apply())shown.RemoveAt(i);
        }
    }
    bool Apply(){
        if(!left.Valid||!right.Valid)return false;
        if(Time.timeScale>0)elapsed+=Time.unscaledDeltaTime;
        if(elapsed>=.42f)return false;
        float blend=Mathf.SmoothStep(0,1,Mathf.Min(elapsed/.065f,(.42f-elapsed)/.085f));
        Vector3 rightAxis=(right.top.position-left.top.position).normalized,up=root.up,fwd=root.forward;
        float width=Vector3.Distance(left.top.position,right.top.position);
        Vector3 centre=(left.top.position+right.top.position)*.5f;
        Vector3 lead=centre+fwd*width*.88f+up*width*.05f+rightAxis*width*(bow?-.12f:.12f);
        Vector3 support=lead-up*width*.17f+rightAxis*width*(bow?.09f:-.09f);
        left.Reach(bow?lead:support,-rightAxis-up*.65f,blend);
        right.Reach(bow?support:lead,rightAxis-up*.65f,blend);return true;
    }
}
