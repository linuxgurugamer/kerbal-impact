using System.Collections;
using System.Linq.Expressions;
using UnityEngine;
using static kerbal_impact.ImpactMonitor;


namespace kerbal_impact
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class ReturnToObserver : MonoBehaviour
    {
        public static ReturnToObserver instance;
        public Vessel ObserverVessel { get; set; }
        Vessel WatchedVessel { get; set; }

        bool active = false;
        void Start()
        {
            Log("ReturnToObserver.Start");
            instance = this;
        }

        public void Initiate(Vessel v)
        {
            Log($"ReturnToObserver.Initiate, vessel: {v.name}");
            ObserverVessel = v;
            if (!active && HighLogic.CurrentGame.Parameters.CustomParams<ImpactSettings>().returnToObserver)
                StartCoroutine(DelayAndSwitchToObserver());
        }

        public void WaitForDestruct(Vessel watchedVessel, Vessel v)
        {
            Log($"WaitForDestruct, watchedVessel: {watchedVessel.vesselName}    v: {v.vesselName}");
            WatchedVessel = watchedVessel;
            ObserverVessel = v;
            StartCoroutine(WaitForVesselDestruction());

        }

        IEnumerator WaitForVesselDestruction()
        {
            Log($"WaitForVesselDestruction, vessel: {WatchedVessel.vesselName}");
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (WatchedVessel.state == Vessel.State.DEAD)
                {
                    StartCoroutine(DelayAndSwitchToObserver());
                    yield break;
                }
                Log($"WaitForVesselDestruction, vessel: {WatchedVessel.vesselName}");
            }
       }
        IEnumerator DelayAndSwitchToObserver()
        {
            Log($"DelayAndSwitchToObserver, waiting for: {HighLogic.CurrentGame.Parameters.CustomParams<ImpactSettings>().timeDelayBeforeReturn} seconds");
            active = true;
            if (ObserverVessel == null)
            {
                Log("ObserverVessel is null");
                active = false;
                yield break;
            }
            yield return new WaitForEndOfFrame();
            for (int i = 0; i < HighLogic.CurrentGame.Parameters.CustomParams<ImpactSettings>().timeDelayBeforeReturn; i++)
            {
                Log($"i: {i}");
                yield return new WaitForSeconds(1f);
            }
            //yield return new WaitForSeconds(HighLogic.CurrentGame.Parameters.CustomParams<ImpactSettings>().timeDelayBeforeReturn);
            Log("DelayAndSwitchToObserver, delay is done");
            if (ObserverVessel != null)
            {
                Log($"Returning to ObserverVessel: {ObserverVessel}");
                FlightGlobals.ForceSetActiveVessel(ObserverVessel);
                FlightInputHandler.ResumeVesselCtrlState(ObserverVessel);
            }
            active = false;
            yield break;
        }
    }
}
