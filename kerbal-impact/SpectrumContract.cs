using Contracts;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using static kerbal_impact.ImpactMonitor;


namespace kerbal_impact
{
    class SpectrumContract : ImpactContract
    {
        private static Dictionary<CelestialBody, Dictionary<String, int>> biomeDifficulties;
        private static String configFile = KSPUtil.ApplicationRootPath + "GameData/Impact/biomedifficulty.cfg";
        private static bool useBiomes;

        private const String titleBlurb = "#autoLOC_SpectrumContractBiome_Title";
        private const String descriptionBlurb = "#autoLOC_SpectrumContractBiome_Blurb";

        private const String titleLatBlurb = "#autoLOC_SpectrumContractLat_Blurb";
        private const String descriptionLatBlurb = "#autoLOC_SpectrumContractLat_Blurb";

        protected override bool Generate()
        {
            if (biomeDifficulties == null)
            {
                loadDifficulties();
            }
            return actuallyGenerate();
        }

        private static void loadDifficulties()
        {
            Log("Loading difficulties from " + configFile);
            ConfigNode node = ConfigNode.Load(configFile);

            if (node.HasValue("use_spectrum_biomes"))
            {
                useBiomes = bool.Parse(node.GetValue("use_spectrum_biomes"));
            }

            if (node.HasNode("BIOMES_LIST"))
            {
                biomeDifficulties = new Dictionary<CelestialBody, Dictionary<string, int>>();
                foreach (ConfigNode bodyNode in node.GetNodes())
                {
                    String bodyName = bodyNode.GetValue("body");
                    CelestialBody body = FlightGlobals.Bodies.Find(b => b.name == bodyName);
                    Dictionary<string, int> difficulties = new Dictionary<string, int>();
                    ConfigNode.ValueList values = bodyNode.values;
                    foreach (ConfigNode.Value s in values)
                    {
                        if (s.name == "body") continue;
                        difficulties.Add(s.name, int.Parse(s.value));

                    }
                    biomeDifficulties.Add(body, difficulties);

                }
            }
        }

        protected override List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies)
        {
            List<PossibleContract> possible = new List<PossibleContract>();
            double probSum = 0;
            foreach (CelestialBody body in bodies)
            {
                //Log("posible body=" + body.theName);
                IEnumerable<SpectrumContract> contracts = ContractSystem.Instance.GetCurrentContracts<SpectrumContract>()
                    .Where(contract => contract.pickedContract.body == body);
                if (contracts.Count() > 0) continue;//only 1 contract of a given type on a given body at once

                contracts = ContractSystem.Instance.GetCurrentContracts<SpectrumContract>()
                    .Where(contract => contract.prestige == prestige && contract.ContractState == State.Offered);
                if (contracts.Count() > 0) continue;//only 1 contract a given prestige offered at a time


                //Log("posible body="+body.theName); 
                // Moved check for body to prevent missing key exception in next line
                if (!biomeDifficulties.ContainsKey(body)) continue;
                Dictionary<string, int> biomes = biomeDifficulties[body];

                int stars = starRatings[prestige];
                //Log("Looking for contracs with stars" + stars);
                if (useBiomes)
                {

                    IEnumerable<KeyValuePair<String, int>> b = biomes.Where(bd => (int)(bd.Value / 3.4) == stars - 1);
                    foreach (KeyValuePair<String, int> biomeVal in b)
                    {
                        string biome = biomeVal.Key;
                        //Log("contract stars = " + stars + " possible biome =" + biome);
                        possible.Add(new PossibleContract(probSum++, body, biome, 0));
                    }
                }
                else
                {
                    float lat = 0;
                    switch (prestige)
                    {
                        case ContractPrestige.Trivial:
                            lat = 0;
                            break;
                        case ContractPrestige.Significant:
                            lat = 50;
                            break;
                        case ContractPrestige.Exceptional:
                            lat = 75;
                            break;
                    }
                    possible.Add(new PossibleContract(probSum++, body, null, lat));
                }

            }
            return possible;
        }

        protected override string GetTitle()
        {
            if (useBiomes)
            {
                return Localizer.Format(titleBlurb, pickedContract.biome, pickedContract.body.GetDisplayName());
            }
            else
            {
                return Localizer.Format(titleLatBlurb, pickedContract.body.GetDisplayName(), pickedContract.latitude);
            }
        }

        protected override string GetDescription()
        {
            if (useBiomes)
                return Localizer.Format(descriptionBlurb, pickedContract.biome, pickedContract.body.GetDisplayName());
            else
                return Localizer.Format(descriptionLatBlurb, pickedContract.body.GetDisplayName(), pickedContract.latitude);
        }

        protected override string GetSynopsys()
        {
            return GetTitle();
        }

        protected override string MessageCompleted()
        {
            return Localizer.Format("#autoLOC_SpectrumContract_Completed");
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
    }

}
