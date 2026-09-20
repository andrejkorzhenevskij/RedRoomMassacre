using System.Collections.Generic;

namespace RRM.Editor
{
    public static partial class CombatPrototypePlayCheck
    {
        public static void RunFeetAndExit() => RunStanceAndExit();
        private static IEnumerator<float> CheckFeet() => CheckStanceAndDragging();
    }
}
