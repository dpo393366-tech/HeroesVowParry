using System;
using System.Collections.Generic;

namespace Vow.Counters;

internal static class ParryRecovery
{
    // Native AP is Int32. Credits survive turns, not battle exit or actor reuse.
    static readonly HashSet<(bool debate,IntPtr attr)> halves=new();
    internal static void Reset()=>halves.Clear();
    internal static void Forget(bool debate,IntPtr attr)=>halves.Remove((debate,attr));
    internal static int HalfPoint(bool debate,IntPtr attr,int current,int cap){
        var key=(debate,attr);
        if(attr==IntPtr.Zero||current<0||cap<=0||current>=cap){halves.Remove(key);return 0;}
        if(halves.Remove(key))return 1;
        halves.Add(key);return 0;
    }
    internal static void FightAP(GKHLLOOEEFN defender){
        var a=defender?.GetAttribute();if(a==null)return;
        if(a.MKEIPLCMANH<=0){Forget(false,a.Pointer);return;}
        if(HalfPoint(false,a.Pointer,a.IHOPPBMEAEN,a.NPEECFFDLAF(false))>0)a.LPPKHEFBOGI(1,true);
    }
    internal static void DebateAP(NPCCtrl defender){
        var a=defender?.m_debateAttribute;if(a==null)return;
        if(a.MKEIPLCMANH<=0){Forget(true,a.Pointer);return;}
        if(HalfPoint(true,a.Pointer,a.IHOPPBMEAEN,a.NPEECFFDLAF(false))>0)a.LPPKHEFBOGI(1,true);
    }
    // Integer resources: floor one percent, with one point for a positive cap.
    // Never lower an already-over-cap resource or resurrect a defeated defender.
    internal static int Amount(int current, int maximum)
    {
        if (maximum <= 0 || current < 0 || current >= maximum) return 0;
        return (int)Math.Min((long)maximum - current, Math.Max(1, maximum / 100));
    }

    internal static void Fight(GKHLLOOEEFN defender)
    {
        var a = defender?.GetAttribute();
        if (a == null || a.MKEIPLCMANH <= 0) return;
        // Match native IIBGEBHCBCJ: HP uses the current living stack count;
        // PJIFIBNIFHA is the battle MP cap, including scenario overrides.
        int hp = Amount(a.MKEIPLCMANH, defender.GetBattleHPLimit(a.FGHFOLKBHDL));
        int mp = Amount(a.BCAIBCEODLJ, a.PJIFIBNIFHA);
        if (hp > 0) { bool changed = false; a.OLFGKGOPDMB(hp, out changed, true); }
        if (mp > 0) a.BKBGPOHLNFB(mp, true);
    }

    internal static void Debate(NPCCtrl defender)
    {
        var a = defender?.m_debateAttribute;
        if (a == null || a.MKEIPLCMANH <= 0) return;
        int hp = Amount(a.MKEIPLCMANH, a.ONGKCKHBLIB);
        if (hp > 0) a.OLFGKGOPDMB(hp);
        // Debate has HP/AP/anger, not martial MP. Do not alter anger or AP.
    }
}

