using System;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vow.Counters;

// Only private clones of the two native visual prefabs are changed. No actor
// material, native pooled effect, attack clock or animator is modified.
internal sealed class ParryNativeEffects:IDisposable
{
    internal const string BuffPath="assets/resource/effect/instant/05buff/buff080.prefab";
    internal const string WavePath="assets/resource/effect/instant/06magic/音波04.prefab";
    sealed class Pair {
        internal GameObject root;
        internal ParticleSystem[] particles;
        internal readonly List<Material> materials=new();
        internal readonly List<(Transform transform,Vector3 scale)> sizes=new();
        internal ParticleSystemRenderer[] renderers;
        internal readonly List<(Material material,string key,Color color)> glow=new();
        internal bool busy;
    }
    static readonly List<Pair> pool=new();
    static GameObject buff,wave,parking;
    static double retryAt;
    readonly Transform anchor;
    Pair pair;
    bool disposed;
    bool visible=true;
    float lastSpeed=-1,lastScale=-1,lastGlow=-1;
    static readonly string[] colorProperties={"_Color","_MainColor","_TintColor","_EmissionColor","_BaseColor"};
    internal static void Warm(){
        if(buff&&wave || Time.unscaledTimeAsDouble<retryAt)return;
        retryAt=Time.unscaledTimeAsDouble+5;
        try{
            foreach(var bundle in AssetBundle.GetAllLoadedAssetBundles_Native()){
                if(!bundle)continue;
                if(!buff&&bundle.Contains(BuffPath))buff=bundle.LoadAsset<GameObject>(BuffPath);
                if(!wave&&bundle.Contains(WavePath))wave=bundle.LoadAsset<GameObject>(WavePath);
                if(buff&&wave)break;
            }
            if(buff&&wave&&pool.Count==0)pool.Add(Create());
        }catch(Exception e){ParryVisuals.Note("native warning prepare: "+e.Message);}
    }
    static Pair Create(){
        if(!parking){parking=new GameObject("HV warning effects cache");Object.DontDestroyOnLoad(parking);parking.SetActive(false);}
        var p=new Pair{root=new GameObject("HV buff080 + 音波04")};
        p.root.transform.SetParent(parking.transform,false);
        try{
            Configure(Object.Instantiate(buff,p.root.transform),new Color(1,.065f,.015f),p);
            Configure(Object.Instantiate(wave,p.root.transform),new Color(1,.65f,.06f),p);
            p.particles=p.root.GetComponentsInChildren<ParticleSystem>(true);
            p.renderers=p.root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            p.root.SetActive(false);
            return p;
        }catch{Destroy(p);throw;}
    }
    static Color Mono(Color c){float value=Mathf.Max(c.r,Mathf.Max(c.g,c.b));return new Color(value,value,value,c.a);}
    static Gradient Mono(Gradient g){
        if(g==null)return null;
        var clone=new Gradient();var keys=g.colorKeys;
        for(int i=0;i<keys.Length;i++){var key=keys[i];key.color=Mono(key.color);keys[i]=key;}
        clone.SetKeys(keys,g.alphaKeys);clone.mode=g.mode;return clone;
    }
    static ParticleSystem.MinMaxGradient Mono(ParticleSystem.MinMaxGradient g){
        var mode=g.m_Mode;g.m_ColorMin=Mono(g.m_ColorMin);g.m_ColorMax=Mono(g.m_ColorMax);
        if(g.m_GradientMin!=null)g.m_GradientMin=Mono(g.m_GradientMin);
        if(g.m_GradientMax!=null)g.m_GradientMax=Mono(g.m_GradientMax);
        g.mode=mode;return g;
    }
    static void Configure(GameObject go,Color hue,Pair pair){
        // Native prefabs were inspected: Transform + ParticleSystem + renderer
        // only. They contain no combat scripts or colliders.
        bool isBuff=go.name.StartsWith("buff080",StringComparison.Ordinal);
        // buff080 uses Local scaling, wave uses Hierarchy scaling in the native
        // asset. Scale local emitters individually without scaling parents twice.
        if(!isBuff)pair.sizes.Add((go.transform,go.transform.localScale));
        foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)){
            if(isBuff)pair.sizes.Add((ps.transform,ps.transform.localScale));
            ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;
            main.useUnscaledTime=true;main.loop=false;

            // Preserve the native alpha and intensity variation, remove blue/green hue.
            main.startColor=Mono(main.startColor);

        }
        NativeWarningProfile.Apply(go,isBuff);
        var copies=new Dictionary<IntPtr,Material>();
        foreach(var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true)){
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++){
                var original=materials[i];if(!original)continue;
                if(!copies.TryGetValue(original.Pointer,out var copy)){
                    copy=new Material(original);copies.Add(original.Pointer,copy);pair.materials.Add(copy);
                    foreach(var key in colorProperties)if(copy.HasProperty(key)){
                        var c=Mono(copy.GetColor(key));copy.SetColor(key,new Color(c.r*hue.r,c.g*hue.g,c.b*hue.b,c.a));
                    }
                    foreach(var key in new[]{"_MainColor","_TintColor","_Color"})if(copy.HasProperty(key)){pair.glow.Add((copy,key,copy.GetColor(key)));break;}
                }
                materials[i]=copy;
            }
            renderer.sharedMaterials=materials;
        }
    }
    internal ParryNativeEffects(Transform root){
      try{
        anchor=root;Warm();if(!root||!buff||!wave){ParryVisuals.Note("native warning prefabs not loaded yet");return;}
        foreach(var candidate in pool)if(!candidate.busy&&candidate.root){pair=candidate;break;}
        if(pair==null){if(pool.Count>=4){ParryVisuals.Note("native warning capacity reached");return;}pair=Create();pool.Add(pair);}
        pair.busy=true;
        pair.root.transform.SetParent(null,false);
        Object.DontDestroyOnLoad(pair.root);
        Position();
        UpdateOptions();
        foreach(var renderer in pair.renderers)if(renderer)renderer.enabled=true;
        pair.root.SetActive(true);
        foreach(var ps in pair.particles){ps.Clear(false);ps.Play(false);}
        ParryVisuals.Note("native warning buff080 + 音波04 started, speed="+ParryVisuals.EffectSpeed);
      }catch(Exception e){Dispose();ParryVisuals.Note("native warning stopped: "+e.Message);}
    }
    void Position(){
        if(!pair.root||!anchor)return;
        var t=pair.root.transform;t.position=anchor.position;t.rotation=anchor.rotation;
        t.localScale=Vector3.one;
    }
    void UpdateOptions(){
        float speed=ParryVisuals.EffectSpeed,scale=ParryVisuals.EffectScale;
        if(speed!=lastSpeed){foreach(var ps in pair.particles){var main=ps.main;main.simulationSpeed=speed;}lastSpeed=speed;}
        float glow=ParryVisuals.EffectGlow;
        if(glow!=lastGlow){foreach(var entry in pair.glow)if(entry.material){var c=entry.color;entry.material.SetColor(entry.key,new Color(c.r*glow,c.g*glow,c.b*glow,c.a));}lastGlow=glow;}
        if(scale!=lastScale){foreach(var size in pair.sizes)if(size.transform)size.transform.localScale=size.scale*scale;lastScale=scale;}
    }
    internal void Show(bool visible){
        if(disposed||pair==null)return;
        if(!anchor){Dispose();return;}
        Position();
        if(this.visible!=visible){
            foreach(var renderer in pair.renderers)if(renderer)renderer.enabled=visible;
            foreach(var ps in pair.particles)if(ps){if(visible)ps.Play(false);else ps.Pause(false);}
            this.visible=visible;
        }
        UpdateOptions();
    }
    public void Dispose(){
        if(disposed)return;disposed=true;
        if(pair!=null){
            if(pair.root){foreach(var ps in pair.particles)if(ps)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                pair.root.SetActive(false);pair.root.transform.SetParent(parking.transform,false);}
            pair.busy=false;pair=null;
        }
    }
    static void Destroy(Pair p){foreach(var m in p.materials)if(m)Object.Destroy(m);if(p.root)Object.Destroy(p.root);}
}
