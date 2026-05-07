using Contracts;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{
    class AsteroidSpectrumContract : ImpactContract
    {
        private const String titleBlurb = "#autoLOC_AsteroidContract_Blurb";
        private const String descriptionBlurb = "#autoLOC_AsteroidContract_Blurb";

        protected override bool Generate()
        {
            bool result = actuallyGenerate();
            if (result)
            {
                GameEvents.onVesselDestroy.Add(OnVesselDestroy);
                //make the name of the targeted asteroid visible
                IEnumerable<Vessel> asteroids =
                FlightGlobals.Vessels.Where(v => v.GetName() == pickedContract.asteroid);
                Vessel asteroid = asteroids.First();
                asteroid.DiscoveryInfo.SetLevel(DiscoveryLevels.Name | DiscoveryLevels.Presence);
            }
            return result;
        }

        protected override List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies)
        {
            List<PossibleContract> possible = new List<PossibleContract>();
            double probSum = 0;
            //Log("Finding asteroids");
            IEnumerable<Vessel> asteroids = FlightGlobals.Vessels.Where(v => v.vesselType == VesselType.SpaceObject);
            foreach (Vessel asteroid in asteroids)
            {
                //Log("asteroid name = " + asteroid.GetName() + " asteroid discovery=" + asteroid.DiscoveryInfo.Level);
                IEnumerable<AsteroidSpectrumContract> contracts = ContractSystem.Instance.GetCurrentContracts<AsteroidSpectrumContract>()
                    .Where(contract => contract.pickedContract.asteroid == asteroid.GetName());
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given asteroid at once

                contracts = ContractSystem.Instance.GetCurrentContracts<AsteroidSpectrumContract>()
                    .Where(contract => contract.prestige == prestige && contract.ContractState == State.Offered);
                if (contracts.Count() > 0) continue;//only 1 contract a given prestige offered at a time

                //Does this asteroid match the correct presige?
                int stars = getAsteroidStars(asteroid);
                if (stars == starRatings[prestige])
                {
                    possible.Add(new PossibleContract(probSum++, asteroid.GetName(), asteroid.orbit.referenceBody));
                }

            }
            return possible;
        }

        private int getAsteroidStars(Vessel asteroid)
        {
            int stars = 2;
            //get size class  - a=3, b,c=2, d,e=1

            stars += orbitFactor(asteroid.orbit.referenceBody);

            stars = Math.Max(1, Math.Min(3, stars));
            return stars;


        }

        private int orbitFactor(CelestialBody celestialBody)
        {
            if (celestialBody.isHomeWorld) return -1;
            if (celestialBody.GetName() == "Sun") return 0;
            return orbitFactor(celestialBody.GetOrbit().referenceBody) + 1;
        }

        protected override string GetTitle()
        {
            return Localizer.Format(titleBlurb, pickedContract.asteroid);
        }

        protected override string GetDescription()
        {
            return Localizer.Format(descriptionBlurb, pickedContract.asteroid, pickedContract.body.GetDisplayName());
        }

        protected override string GetSynopsys()
        {
            return GetTitle();
        }

        protected override string MessageCompleted()
        {
            return Localizer.Format("#autoLOC_AsteroidContract_Completed");
        }

        public override bool MeetRequirements()
        {
            AvailablePart ap = PartLoader.getPartInfoByName("Impact Spectrometer");
            if (ap != null)
            {
                if (ResearchAndDevelopment.PartTechAvailable(ap))
                    return true;
            }
            return false;
        }

        private void OnVesselDestroy(Vessel vessel)
        {
            Log("In astContract onVesselDestroy");
            if (vessel.vesselType == VesselType.SpaceObject && pickedContract != null)
            {
                Log("vessel of type asteroid has been destroyed - checking for active contracts");
                Log("PC=" + pickedContract);
                Log("PC.ast=" + pickedContract.asteroid);
                Log("vesssle=" + vessel);

                if (pickedContract != null && pickedContract.asteroid != null && pickedContract.asteroid == vessel.GetName())
                {
                    Log("the asteroid is the one refered to by this contract");
                    this.Cancel();
                }
            }
        }

        protected override void OnFinished()
        {
            base.OnFinished();
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
        }

        protected override void OnAccepted()
        {
            base.OnAccepted();
            IEnumerable<Vessel> asteroids =
                FlightGlobals.Vessels.Where(v => v.GetName() == pickedContract.asteroid);
            Vessel asteroid = asteroids.First();
            asteroid.DiscoveryInfo.SetLevel(DiscoveryLevels.StateVectors | DiscoveryLevels.Name | DiscoveryLevels.Presence);

        }


    }

}
