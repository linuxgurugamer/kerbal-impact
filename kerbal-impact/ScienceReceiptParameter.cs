using Contracts;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{
    class ScienceReceiptParameter : ContractParameter
    {
        private const string keTitle = "#autoLOC_SciParam_KeTitle";

        private Boolean isComplete = false;

        ImpactContract.PossibleContract contract;

        long randId;

        public ScienceReceiptParameter()
        {
            randId = (new System.Random()).Next();
        }

        public ScienceReceiptParameter(ImpactContract.PossibleContract contract)
        {

            this.contract = contract;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            ImpactCoordinator.getInstance().scienceListeners.Add(OnScience);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            ImpactCoordinator.getInstance().scienceListeners.Remove(OnScience);
        }

        private void OnScience(ImpactScienceData data)
        {
            if (isComplete)
            {
                ImpactCoordinator.getInstance().scienceListeners.Remove(OnScience);
            }
            Log("science received in " + contract.expectedDataType + " parameter " + randId);
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
                    if (contract.biome != null)
                    {
                        passed = data.biome == contract.biome;
                    }
                    else passed = contract.latitude <= Math.Abs(data.latitude);
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
                ImpactCoordinator.getInstance().scienceListeners.Remove(OnScience);
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
            return Localizer.Format(keTitle);
        }
    }

}
