using System;
namespace Vow.Counters;

internal static class GroggyPoseClock
{
    // Playback progress is independent of turn duration. Only the groggy state
    // owner decides when to release the animation and restore the controller.
    internal static float Normalized(bool beast,float elapsed,float length){
        if(!float.IsFinite(length)||length<=0)length=.1f;
        if(!float.IsFinite(elapsed)||elapsed<0)elapsed=0;
        float progress=elapsed/Math.Max(.1f,length);
        // Play the entry once, then HOLD. The old .35..80 ping-pong sampled
        // recovery frames forwards and backwards on some humanoid rigs,
        // repeatedly standing up and falling despite one continuous stun.
        return Math.Min(beast?.65f:.42f,progress);
    }
}
