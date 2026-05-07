using System.Collections;
using UnityEngine;

#if false
namespace kerbal_impact
{

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LoadedVesselSwitcher : MonoBehaviour
    {
        public static LoadedVesselSwitcher Instance;
        private double lastCameraSwitch = 0;
        private Vessel lastActiveVessel = null;

        // Extracted method, so we dont have to call these two lines everywhere
        public void ForceSwitchVessel(Vessel v)
        {
            if (v == null || !v.loaded)
                return;
            lastCameraSwitch = Time.time;
            lastActiveVessel = v;
            var camHeading = FlightCamera.CamHdg;
            var camPitch = FlightCamera.CamPitch;
            FlightGlobals.ForceSetActiveVessel(v);
            FlightInputHandler.ResumeVesselCtrlState(v);
            FlightCamera.CamHdg = camHeading;
            FlightCamera.CamPitch = camPitch;
        }

        public IEnumerator SwitchToVesselWhenPossible(Vessel vessel, float distance = 0)
        {
            var wait = new WaitForFixedUpdate();
            while (vessel != null && (!vessel.loaded || vessel.packed)) yield return wait;
            while (vessel != null && vessel.loaded && vessel != FlightGlobals.ActiveVessel) { ForceSwitchVessel(vessel); yield return wait; }
            if (vessel != null && vessel.loaded && !vessel.packed)
            {
                var flightCam = FlightCamera.fetch;
                if (flightCam != null && distance > 0) flightCam.SetDistance(distance);
            }
        }


    }
}
#endif