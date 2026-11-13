using UnityEngine;
namespace Vow.Counters;
internal static class BattleModal {
#if DuelCounters
    internal static bool Visible=>
        (UI_MenuUI.IJGEDOMNOBK&&UI_MenuUI.IJGEDOMNOBK.gameObject.activeInHierarchy)||
        (UI_SettingUI.IJGEDOMNOBK&&UI_SettingUI.IJGEDOMNOBK.gameObject.activeInHierarchy);
#else
    internal static bool Visible=>false;
#endif
}
