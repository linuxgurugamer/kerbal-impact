using Contracts;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using static kerbal_impact.ImpactMonitor;


namespace kerbal_impact
{
    class SeismicContract : ImpactContract
    {

        protected override bool Generate()
        {
            return actuallyGenerate();
        }

        protected override List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies)
        {
            List<PossibleContract> possible = new List<PossibleContract>();
            double probSum = 0;

            foreach (CelestialBody body in bodies)
            {
                IEnumerable<SeismicContract> contracts = ContractSystem.Instance.GetCurrentContracts<SeismicContract>()
                    .Where(contract => contract.pickedContract.body == body);
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given body at once
                contracts = ContractSystem.Instance.GetCurrentContracts<SeismicContract>()
                    .Where(contract => contract.prestige == prestige && contract.ContractState == State.Offered);
                if (contracts.Count() > 0) continue;//only 1 contract a given prestige offered at a time

                ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");

                ScienceSubject subject;
                ExperimentSituations sit = ExperimentSituations.SrfLanded;
                subject = ResearchAndDevelopment.GetExperimentSubject(experiment, sit, body, "surface", "");
                int stars = starRatings[prestige];
                double energy = pickKE(stars, subject, body);
                possible.Add(new PossibleContract(++probSum, body, energy));
            }
            return possible;
        }

        private double pickKE(double stars, ScienceSubject subject, CelestialBody body)
        {
            double scienceCap = subject.scienceCap;
            double minSci = (stars - 1) / 3 * scienceCap;
            double maxSci = stars / 3 * scienceCap;
            double goalScience = minSci + random.NextDouble() * (maxSci - minSci);
            double ke = ImpactMonitor.translateScienceToKE(goalScience, body, subject);
            return ke;
        }

        protected override string GetTitle()
        {
            return Localizer.Format("#autoLOC_SeismicContract_Title",
                ImpactMonitor.energyFormat(pickedContract.energy), pickedContract.body.GetDisplayName());
        }

        protected override string GetDescription()
        {
            return Localizer.Format("#autoLOC_SeismicContract_Blurb", pickedContract.body.GetDisplayName(), ImpactMonitor.energyFormat(pickedContract.energy));
        }

        protected override string GetSynopsys()
        {
            return GetTitle();
        }

        protected override string MessageCompleted()
        {
            return "#autoLOC_SeismicContract_Completed";
        }

        public override bool MeetRequirements()
        {
            AvailablePart ap = PartLoader.getPartInfoByName("Impact Seismometer");
            if (ap != null)
            {
                if (ResearchAndDevelopment.PartTechAvailable(ap))
                {
                    return true;
                }
            }
            return false;
        }
    }

}
