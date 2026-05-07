using Contracts;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{

    class ImpactContract : Contract
    {
        const String valuesNode = "ContractValues";

        // Linuxgurugamer reformatted for readability
        protected static Dictionary<ContractPrestige, int> starRatings = new Dictionary<ContractPrestige, int>
        {
            { ContractPrestige.Trivial, 1 },
            { ContractPrestige.Significant, 2},
            { ContractPrestige.Exceptional, 3}
        };

        protected PossibleContract pickedContract;
        protected readonly System.Random random = new System.Random();

        public int randId = new System.Random().Next();


        protected override bool Generate()
        {
            return false;
        }

        protected bool actuallyGenerate()
        {
            Log("Trying to generate an impact contract");
            IEnumerable<CelestialBody> bodies = Contract.GetBodies_Reached(false, false);

            bodies = bodies.Where(body => !body.atmosphere);
            //generate a weighted list of possible contracts (different bodsies and biomes where appropriate)
            List<PossibleContract> contracts = pickContracts(bodies);
            if (contracts.Count == 0) return false;
            double totalProb = contracts.Last().probability;
            double picked = random.NextDouble() * totalProb;
            IComparer<PossibleContract> comp = new PossibleContract.ProbComparer();
            int contractIndex = contracts.BinarySearch(new PossibleContract(picked, null, 0), comp);
            if (contractIndex < 0) contractIndex = ~contractIndex;
            //Log("pickedindex=" + contractIndex);
            pickedContract = contracts[contractIndex];
            Log("picked one " + pickedContract);

            SetExpiry();
            SetScience(1.5f, pickedContract.body);
            SetDeadlineYears(0.5f, pickedContract.body);
            SetReputation(3, 4, pickedContract.body);
            SetFunds(20000, 80000, 10000, pickedContract.body);

            generateParameters();
            Log("Generated parameters");

            return true;
        }

        protected void generateParameters()
        {
            AddParameter(new ImpactParameter(pickedContract));
            AddParameter(new ScienceReceiptParameter(pickedContract));
        }
        protected virtual List<PossibleContract> pickContracts(IEnumerable<CelestialBody> bodies) { return null; }

        public override bool CanBeCancelled()
        {
            return true;
        }

        public override bool CanBeDeclined()
        {
            return true;
        }

        protected override string GetHashString()
        {
            return pickedContract.getHashString();
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            pickedContract = new PossibleContract(node.GetNode(valuesNode));
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            ConfigNode paramNode = new ConfigNode(valuesNode);
            pickedContract.save(paramNode);
            node.AddNode(paramNode);
        }

        protected override void OnCompleted()
        {
            Log("Completed contract with id " + randId);
            base.OnCompleted();

        }

        public class PossibleContract
        {
            public double probability;
            public CelestialBody body;
            public double energy;
            public String biome;
            public double latitude;
            public string asteroid;
            public ImpactScienceData.DataTypes expectedDataType;

            public PossibleContract(double prob, CelestialBody bod, double energy)
            {
                probability = prob;
                body = bod;
                this.energy = energy;
                expectedDataType = ImpactScienceData.DataTypes.Seismic;
            }

            public PossibleContract(double prob, CelestialBody bod, string biome, float latitude)
            {
                probability = prob;
                body = bod;
                this.biome = biome;
                this.latitude = latitude;
                expectedDataType = ImpactScienceData.DataTypes.Spectral;
            }

            public PossibleContract(double prob, string asteroid, CelestialBody orbiting)
            {
                probability = prob;
                this.asteroid = asteroid;
                this.body = orbiting;
                expectedDataType = ImpactScienceData.DataTypes.Asteroid;
            }

            public override String ToString()
            {
                if (body != null) { return body.name + "-" + ImpactMonitor.energyFormat(energy) + "-" + biome + "-" + latitude; }
                else return asteroid;

            }


            public PossibleContract(ConfigNode node)
            {
                if (node.HasValue("BodyName"))
                {
                    String bodyName = node.GetValue("BodyName");
                    body = FlightGlobals.Bodies.Find(b => b.name == bodyName);
                }
                if (node.HasValue("Energy"))
                {
                    energy = Double.Parse(node.GetValue("Energy"));
                }
                if (node.HasValue("Biome"))
                {
                    biome = node.GetValue("Biome");
                }
                if (node.HasValue("Latitude"))
                {
                    latitude = float.Parse(node.GetValue("Latitude"));
                }
                if (node.HasValue("Asteroid"))
                {
                    asteroid = node.GetValue("Asteroid");
                    if (body == null)
                    {
                        //legacy contract without asteroid body specified
                        Vessel ast = FlightGlobals.Vessels.Where(v => v.GetName() == asteroid).Single();
                        body = ast.orbit.referenceBody;
                    }
                }
                if (node.HasValue(ImpactScienceData.DataTypeName))
                {
                    expectedDataType = (ImpactScienceData.DataTypes)
                        Enum.Parse(typeof(ImpactScienceData.DataTypes),
                        node.GetValue(ImpactScienceData.DataTypeName));
                }
                else
                {
                    //load up legacy contracts which didn't save datatype
                    if (biome != null || latitude > 0) expectedDataType = ImpactScienceData.DataTypes.Spectral;
                    else if (asteroid != null) expectedDataType = ImpactScienceData.DataTypes.Asteroid;
                    else expectedDataType = ImpactScienceData.DataTypes.Seismic;
                }
            }

            public void save(ConfigNode node)
            {
                if (body != null)
                {
                    node.AddValue("BodyName", body.name);
                }
                if (energy != 0)
                {
                    node.AddValue("Energy", energy);
                }
                if (biome != null)
                {
                    node.AddValue("Biome", biome);
                }
                if (latitude != 0)
                {
                    node.AddValue("Latitude", latitude);
                }
                if (asteroid != null)
                {
                    node.AddValue("Asteroid", asteroid);
                }
                node.AddValue(ImpactScienceData.DataTypeName, expectedDataType);
            }

            public class ProbComparer : IComparer<PossibleContract>
            {
                public int Compare(PossibleContract p1, PossibleContract p2)
                {
                    return p1.probability.CompareTo(p2.probability);
                }
            }

            internal string getHashString()
            {
                if (body != null)
                {
                    return body.name + energy + biome;
                }
                else return asteroid;
            }
        }
    }
}
