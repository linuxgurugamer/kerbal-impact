using Contracts;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{
    class ImpactParameter : ContractParameter
    {
        private const string keTitle = "#autoLOC_ImpactParam_KeTitle";
        private const string biomeTitle = "#autoLOC_ImpactParam_BiomeTitle";
        private const string latitudeTitle = "#autoLOC_ImpactParam_LatTitle";
        private const string asteroidTitle = "#autoLOC_ImpactParam_AstTitle";

        ImpactContract.PossibleContract contract;
        private bool isComplete = false;

        public ImpactParameter()
        {

        }

        public ImpactParameter(ImpactContract.PossibleContract contract)
        {
            this.contract = contract;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            ImpactCoordinator.getInstance().bangListeners.Add(OnBang);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            ImpactCoordinator.getInstance().bangListeners.Remove(OnBang);
        }

        private void OnBang(ImpactScienceData data)
        {
            Log("OnBang, isCompete: " + isComplete);
            if (isComplete)
            {
                ImpactCoordinator.getInstance().bangListeners.Remove(OnBang);
            }
            Log("bang received in " + contract.expectedDataType + " parameter " + data.datatype);
            if (data.datatype != contract.expectedDataType) return;
            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(data.subjectID);

            bool passed = false;
            switch (contract.expectedDataType)
            {
                case ImpactScienceData.DataTypes.Seismic:
                    //check this was the right body and the impact was high enough energy
                    passed = (subject.IsFromBody(contract.body) && data.kineticEnergy >= contract.energy);
                    break;
                case ImpactScienceData.DataTypes.Spectral:
                    //check it is the right body
                    if (!subject.IsFromBody(contract.body))
                    {
                        break;
                    }
                    //if a biome is specified  then check the biome matches
                    Log("Contract biome =" + contract.biome + " data biome =" + data.biome);
                    Log("Contract lat =" + contract.latitude + " data lat =" + data.latitude);
                    if (contract.biome != null)
                    {
                        passed = data.biome == contract.biome;
                    }
                    else
                    {
                        passed = contract.latitude <= Math.Abs(data.latitude);
                    }
                    break;

                    case ImpactScienceData.DataTypes.Density:
                    //check it is the right body
                    passed = subject.IsFromBody(contract.body);
                    break;

                case ImpactScienceData.DataTypes.Asteroid:
                    Log("Contract astreroid =" + contract.asteroid + " data asteroid ="
                    + data.asteroid + "data.datatype =" + data.datatype + " data asteroid =" + data.asteroid);
                    passed = contract.asteroid == data.asteroid;
                    break;
            }

            if (passed)
            {
                SetComplete();
                isComplete = true;
                ImpactCoordinator.getInstance().bangListeners.Remove(OnBang);
            }
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            contract = new ImpactContract.PossibleContract(node);
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            contract.save(node);
        }

        protected override string GetTitle()
        {
            if (contract.asteroid != null)
            {
                return Localizer.Format(asteroidTitle, contract.asteroid);
            }
            if (contract.biome == null)
            {
                if (contract.energy > 0)
                {
                    return Localizer.Format(keTitle, contract.body.GetDisplayName(), ImpactMonitor.energyFormat(contract.energy));
                }
                else
                {
                    return Localizer.Format(latitudeTitle, contract.body.GetDisplayName(), contract.latitude);
                }
            }
            else
                return Localizer.Format(biomeTitle, contract.biome, contract.body.GetDisplayName());
        }
    }
}
