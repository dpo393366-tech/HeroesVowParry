using System;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Vow.Counters;

internal static class CounterEffects
{
    static readonly Dictionary<string,AnimationClip> clips=new(StringComparer.Ordinal);
    static AssetBundle animations;
    static AudioClip clang,clang2,clang3,enemyClang,heavyClang,attackWarning;
    static readonly System.Random soundRandom=new();
    static AudioSource warningSource,clangSource;
    internal static void StopAudio(){if(warningSource)warningSource.Stop();if(clangSource)clangSource.Stop();}
    static readonly HashSet<string> reported=new();
    static void Missing(string key){if(reported.Add(key))DuelCounters.Warn("패링 연출을 찾을 수 없음: "+key);}
    internal static AnimationClip Clip(string path){
        if(clips.TryGetValue(path,out var clip)&&clip)return clip;
        string asset=("Assets/Resource/"+path).ToLowerInvariant();
        if(!animations || !animations.Contains(asset)){
            foreach(var bundle in AssetBundle.GetAllLoadedAssetBundles_Native()){
                if(!bundle || !bundle.Contains(asset))continue;
                animations=bundle;break;
            }
        }
        if(animations && animations.Contains(asset)){
            clip=animations.LoadAsset<AnimationClip>(asset);
            if(clip){clips[path]=clip;return clip;}
        }
        return null;
    }
    const string TwoHandBlock="Animation/单手巨斧/Frank_RPG_2Hand_Block_in_Place@@Unreal Take.anim";
    const string WeaponGuard="Animation/11Skill/格挡_Take 001.anim";
    // Once is the game's one-shot override slot. Some controllers expose it on two body layers.
    static bool Pose(Animator animator,AnimatorOverrideController controller,AnimationClip clip,string slot){
        if(!animator || !controller || !clip)return false;
        bool played=false;
        for(int layer=0;layer<animator.layerCount;layer++){
            int hash=Animator.StringToHash(animator.GetLayerName(layer)+"."+slot);
            if(!animator.HasState(layer,hash)){
                hash=Animator.StringToHash(slot);
                if(!animator.HasState(layer,hash))continue;
            }
            if(!played)controller[slot]=clip;
            animator.CrossFadeInFixedTime(hash,.025f,layer,Mathf.Min(.08f,clip.length*.10f));played=true;
        }
        return played;
    }
    static string Stance(GKHLLOOEEFN hero){
        string key=hero.GetBattleAnimationName()??"";
        return key.EndsWith(".anim",StringComparison.OrdinalIgnoreCase)?key:key+".anim";
    }
    internal static void Guard(GKHLLOOEEFN hero){
        var animator=hero.GetAnimator();var controller=hero.GetOverrideController();
        var clip=Clip(TwoHandBlock);
        if(!clip)clip=Clip(WeaponGuard);
        Pose(animator,controller,clip,"Once");
        TwoHandBrace.Start(hero,animator,Stance(hero).StartsWith("远程",StringComparison.Ordinal));
    }
    internal static void Rebut(NPCCtrl hero){
        const string path="DebateAnimation/01Attack/A出手-停止-示意.anim";
        if(!Pose(hero.GetAnimator(),hero.GetOverrideController(),Clip(path),"Once"))Missing(path);
    }
    internal static void Recoil(Fi_Controller fight,GKHLLOOEEFN attacker,GKHLLOOEEFN defender){
        CounterMotion.Begin(attacker,defender);
        string stance=Stance(attacker);
        if(attacker.TryCast<AnimalCtrl>()!=null){
            attacker.SetDodgeAnimation(attacker.GetBattleAnimationName());
            var ctrl=attacker.GetOverrideController();var anim=attacker.GetAnimator();
            var clip=Clip("AnimationDodgeStart/"+stance);
            if(!clip && ctrl)clip=ctrl["DodgeStart"];
            if(Pose(anim,ctrl,clip,"DodgeStart"))
                fight.StartCoroutine(RecoverAnimal(anim,ctrl).WrapToIl2Cpp());
            else if(!Pose(anim,ctrl,clip,"Once"))Missing("동물 피격 "+stance);
        }else fight.PlayBeHitAnimation(attacker,stance,.30f,0);
    }
    static System.Collections.IEnumerator RecoverAnimal(Animator anim,AnimatorOverrideController ctrl){
        float elapsed=0;
        while(elapsed<.23f && anim && Fi_Controller.m_isInBattle){
            yield return null;if(Time.timeScale>0)elapsed+=Time.unscaledDeltaTime;
        }
        if(!anim || !ctrl || !Fi_Controller.m_isInBattle)yield break;
        int start=Animator.StringToHash("DodgeStart"),loop=Animator.StringToHash("DodgeLoop");
        for(int i=0;i<anim.layerCount;i++){
            var state=anim.IsInTransition(i)?anim.GetNextAnimatorStateInfo(i):anim.GetCurrentAnimatorStateInfo(i);
            if(state.shortNameHash==start || state.shortNameHash==loop){
                Pose(anim,ctrl,ctrl["DodgeEnd"],"DodgeEnd");yield break;
            }
        }
    }
    internal static void Clang(int actionCost=0,bool enemyDefender=false){
        // Duplicate callbacks are coalesced by contact identity in Resolve.
        // Distinct hits must not share a wall-clock mute window (even in one frame).
        // Defender side takes precedence. Cosmetic RNG never consumes combat rolls.
        if(enemyDefender)PlayCue(ref enemyClang,"EnemyBladeContact.pcm","Vow_Enemy_Parry");
        else if(actionCost>=4)PlayCue(ref heavyClang,"HeavyBladeContact.pcm","Vow_Heavy_Steel_Parry");
        else switch(soundRandom.Next(3)){
            case 1:PlayCue(ref clang2,"BladeContact2.pcm","Vow_Steel_Parry_2");break;
            case 2:PlayCue(ref clang3,"BladeContact3.pcm","Vow_Steel_Parry_3");break;
            default:PlayCue(ref clang,"BladeContact.pcm","Vow_Steel_Parry");break;
        }
    }
    internal static void AttackWarning()=>PlayCue(ref attackWarning,"AttackFlash.pcm","Vow_Attack_Flash",true);
    internal static void PrepareAudio(){
        LoadCue(ref clang,"BladeContact.pcm","Vow_Steel_Parry");
        LoadCue(ref clang2,"BladeContact2.pcm","Vow_Steel_Parry_2");
        LoadCue(ref clang3,"BladeContact3.pcm","Vow_Steel_Parry_3");
        LoadCue(ref enemyClang,"EnemyBladeContact.pcm","Vow_Enemy_Parry");
        LoadCue(ref heavyClang,"HeavyBladeContact.pcm","Vow_Heavy_Steel_Parry");
        LoadCue(ref attackWarning,"AttackFlash.pcm","Vow_Attack_Flash");
    }
    static void LoadCue(ref AudioClip clip,string resource,string name){
        if(!clip){
            using var stream=typeof(CounterEffects).Assembly.GetManifestResourceStream(resource);
            using var reader=new System.IO.BinaryReader(stream);
            const int rate=48000,channels=2;var samples=new float[checked((int)(stream.Length/2))];
            for(int i=0;i<samples.Length;i++)samples[i]=reader.ReadInt16()/32768f;
            clip=AudioClip.Create(name,samples.Length/channels,channels,rate,false);
            if(!clip.SetData(samples,0)){Object.Destroy(clip);clip=null;Missing(name);return;}
        }
    }
    static void PlayCue(ref AudioClip clip,string resource,string name,bool dedicated=false){
        LoadCue(ref clip,resource,name);if(!clip)return;
        // Native attacks also reuse SoundManager sources. Keep the warning on a
        // single private 2D source so a skill sound cannot recycle/stop it.
        ref AudioSource owned=ref (dedicated?ref warningSource:ref clangSource);
        if(!owned){
            var host=new GameObject(dedicated?"Vow_ParryWarningAudio":"Vow_ParrySuccessAudio");
            host.hideFlags=HideFlags.HideAndDontSave;Object.DontDestroyOnLoad(host);
            owned=host.AddComponent<AudioSource>();owned.playOnAwake=false;
            owned.spatialBlend=0;owned.pitch=1;owned.priority=32;
        }
        AudioSource src=owned;
        if(src)src.PlayOneShot(clip,Mathf.Clamp01(GLIJMELMEHI.HCELAMFAEBG*GLIJMELMEHI.GGDOBCKGNJD));
    }
}

// A single camera-facing glint at the leading edge of the attack. The actor is rendered normally.
internal sealed class CounterGlow:IDisposable
{
    readonly Transform anchor;
    readonly ParryNativeEffects effects;
    readonly bool heavy;
    internal readonly float Duration;
    static readonly System.Collections.Generic.List<CounterGlow> active=new();
    bool disposed;
    readonly SkinnedMeshRenderer body;
    static Texture2D star,whiteCore;
    float age;
    internal CounterGlow(Transform root,Il2CppSystem.Collections.Generic.List<SkinnedMeshRenderer> model,HighlightingSystem.Highlighter nativeOutline=null,int actionCost=0){
        anchor=root;heavy=actionCost>=4;Duration=heavy?ParryVisuals.RedDuration:ParryVisuals.GoldDuration;
        if(heavy&&ParryVisuals.WarningEnabled)effects=new ParryNativeEffects(root);
        active.Add(this);
        if(model!=null)foreach(var r in model)
            if(r && r.enabled && r.gameObject.activeInHierarchy && (!body||r.bounds.size.sqrMagnitude>body.bounds.size.sqrMagnitude))body=r;
        Pulse(0);
    }
    internal void Pulse(float elapsed){age=elapsed;Refresh();}
    // Follow the attacker after native movement, before camera rendering.
    internal static void RefreshAll(){
        if(ParryVisuals.Visible && ParryVisuals.WarningEnabled)ParryNativeEffects.Warm();
        for(int i=active.Count-1;i>=0;i--)try{active[i].Refresh();}
        catch(Exception ex){var cue=active[i];try{cue.Dispose();}catch{}ParryVisuals.Note("outline stopped after render error: "+ex.Message);}
    }
    void Refresh(){
        if(disposed||effects==null)return;
        effects.Show(age<Duration && ParryVisuals.Visible && ParryVisuals.WarningEnabled);
    }
    internal static void Reset(){while(active.Count>0)active[active.Count-1].Dispose();}
    internal void Marker(){
        if(disposed || age>=ParryVisuals.GoldDuration || !ParryVisuals.Visible || !ParryVisuals.WarningEnabled || Event.current.type!=EventType.Repaint)return;
        var cam=Camera.main;var at=Vector3.zero;
        if(cam && anchor){
            Vector3 world=body?body.bounds.center+Vector3.up*body.bounds.extents.y*.88f:anchor.position+Vector3.up*1.6f;
            at=cam.WorldToScreenPoint(world);
        }
        if(!star)star=CreateStar();
        float t=Mathf.Clamp01(age/ParryVisuals.GoldDuration);
        float w=.625f*Mathf.Min(Screen.width*.58f,Screen.height*Mathf.Lerp(.48f,.64f,Mathf.SmoothStep(0,1,t*4)));
        float h=w*.43f;
        float x=at.z>0?Mathf.Clamp(at.x,w*.28f,Screen.width-w*.28f):Screen.width*.5f;
        float y=at.z>0?Mathf.Clamp(Screen.height-at.y,h*.40f,Screen.height-h*.40f):Screen.height*.38f;
        float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,1,t));
        var prev=GUI.color;int depth=GUI.depth;
        try{
            GUI.depth=-950;GUI.color=new Color(1,1,1,alpha);
            GUI.DrawTexture(new Rect(x-w*.5f,y-h*.5f,w,h),star);
            const float coreDuration=.18f;
            if(age<coreDuration){
                if(!whiteCore)whiteCore=CreateWhiteCore();
                float pulse=Mathf.Clamp01(age/coreDuration);
                float size=h*Mathf.Lerp(.90f,1.10f,Mathf.SmoothStep(0,1,pulse));
                GUI.color=new Color(1,1,1,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,1,pulse)));
                GUI.DrawTexture(new Rect(x-size*.5f,y-size*.5f,size,size),whiteCore);
            }
        }finally{GUI.color=prev;GUI.depth=depth;}
    }
    static Texture2D CreateWhiteCore(){
        const int size=128;
        var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float u=(2*x+1f)/size-1,v=(2*y+1f)/size-1;
            float radius=Mathf.Sqrt(u*u+v*v);
            float core=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.61f,radius));
            float halo=.55f*Mathf.Exp(-3.7f*radius*radius);
            float edge=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,radius));
            pixels[y*size+x]=new Color(1,1,1,(core+(1-core)*halo)*edge);
        }
        tex.SetPixels32(pixels);tex.Apply(false,true);return tex;
    }
    static Texture2D CreateStar(){
        const int w=384,h=168;var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color32[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            float u=(2*x+1f)/w-1,v=(2*y+1f)/h-1,p=Mathf.Abs(u),q=Mathf.Abs(v);
            float radial=u*u+v*v*.34f;
            float horizontal=Mathf.Exp(-q*15)*Mathf.Pow(1-p,.62f);
            float vertical=Mathf.Exp(-p*23)*Mathf.Pow(1-q,1.1f);
            float diagonal=Mathf.Exp(-Mathf.Abs(q-p*1.85f)*15)*Mathf.Exp(-p*5-q*1.7f);
            float ray=horizontal+vertical+diagonal*.90f;
            float bloom=Mathf.Exp(-radial*13);
            float light=Mathf.Clamp01(ray*.88f+bloom*.5f);
            var c=Color.Lerp(new Color(.58f,.18f,.015f),new Color(1,.82f,.11f),Mathf.Clamp01(ray+bloom*.8f));
            c=Color.Lerp(c,new Color(1,.98f,.86f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.90f,light)));
            c.a=Mathf.Clamp01(ray*1.6f+bloom*.74f)*Mathf.Clamp01((1-p)*14)*Mathf.Clamp01((1-q)*14);
            pixels[y*w+x]=c;
        }
        tex.SetPixels32(pixels);tex.Apply(false,true);return tex;
    }
    public void Dispose(){if(disposed)return;disposed=true;active.Remove(this);effects?.Dispose();}
}
