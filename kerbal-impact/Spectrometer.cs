using KSP.Localization;
using KSP.UI.Screens.Flight.Dialogs;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{
    class Spectrometer : PartModule, IScienceDataContainer
    {
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Spectrometer Status", guiUnits = "", isPersistant = true)]
        public string statusText = "   No data";

        [KSPField(isPersistant = true)]
        public int situationMask = 16; // Default to low orbit

        [KSPField]
        public bool deployable = false; // Default to low orbit

        [KSPField(isPersistant = true)]
        public bool deployed = false;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Deployment Status", guiUnits = "", isPersistant = true)]
        public string deploymentStatus = ":   Retracted";

        [KSPField]
        public string animationName = "";

        [KSPField]
        public float customAnimationSpeed = 1f;

        [KSPField]
        public string startEventGUIName = "Deploy Spectrometer";

        [KSPField]
        public string endEventGUIName = "Retract Spectrometer";

        [KSPField]
        public string actionGUIName = "Toggle Spectrometer";

        private Animation anim;


        [KSPEvent(guiName = "Deploy Spectrometer", guiActive = true, guiActiveEditor = true, active = true)]
        public void DeployEvent()
        {
            if (!deployed)
                Deploy();
            else
                Retract();
        }

        [KSPAction("Toggle Spectrometer")]
        public void ToggleAction(KSPActionParam param)
        {
            if (!deployed)
                Deploy();
            else
                Retract();
        }

        void Deploy()
        {
            SetDeploying();
            if (animationName != "")
            {
                anim[animationName].speed = 1f * customAnimationSpeed;
                //if (anim[animationName].normalizedTime == 0f || anim[animationName].normalizedTime == 1f)
                //    anim[animationName].normalizedTime = 0f;
                anim.Play(animationName);
                StartCoroutine("SlowUpdate");
            }
        }

        void SetDeploying()
        { 
            deployed = true;
            deploymentStatus = ":   Deploying";
            finalDeploymentStatus = ":   Deployed";
            Actions["ToggleAction"].guiName = actionGUIName;
            Events["DeployEvent"].guiName = endEventGUIName;
        }
        void SetDeployed()
        {
            deploymentStatus = finalDeploymentStatus;
            finalDeploymentStatus = "";
            Events["DeployEvent"].active =
                Events["DeployEvent"].guiActive = true;
        }

        string finalDeploymentStatus = "";
        void Retract()
        {
            deployed = false;
            deploymentStatus = ":   Retracting";
            finalDeploymentStatus = ":   Retracted";
            Actions["ToggleAction"].guiName = actionGUIName;
            Events["DeployEvent"].guiName = startEventGUIName;
            if (animationName != "")
            {
                anim[animationName].speed = -1f * customAnimationSpeed;
                //if (anim[animationName].normalizedTime == 0f || anim[animationName].normalizedTime == 1f)
                //    anim[animationName].normalizedTime = 1f;
                if (!anim.isPlaying)
                    anim[animationName].normalizedTime = 1f;
                anim.Play(animationName);
                StartCoroutine("SlowUpdate");
            }
        }
        IEnumerator SlowUpdate()
        {
            Events["DeployEvent"].active = Events["DeployEvent"].guiActive = false;

            while (anim.isPlaying)
            {
                yield return new WaitForSeconds(0.25f);
            }
            SetDeployed();
            yield return null;
        }

        protected ImpactScienceData result;
        //TODO I think this should be a list

        protected ExperimentsResultDialog expDialog = null;

        public override void OnLoad(ConfigNode node)
        {
            if (node.HasNode("ScienceData"))
            {
                ConfigNode storedDataNode = node.GetNode("ScienceData");
                ImpactScienceData data = new ImpactScienceData(storedDataNode);
                result = data;
            }
            if (!deployable)
            {
                Actions["ToggleAction"].active = false;
                Events["DeployEvent"].active = Events["DeployEvent"].guiActive = false;
                Fields["deploymentStatus"].guiActive = false;
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (animationName != "")
            {
                anim = part.FindModelAnimators(animationName).FirstOrDefault();
                if (anim == null)
                {
                    animationName = "";
                }

                if (deployed)
                {
                    SetDeploying();
                    SetDeployed();

                    anim[animationName].speed =  customAnimationSpeed;
                    anim[animationName].normalizedTime = 1f;
                    anim.Play(animationName);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            Log("Saving spectrometer 1");
            OnSave(node, result);
        }

        public static void OnSave(ConfigNode node, ImpactScienceData data)
        {
            Log("Saving spectrometer 2");
            DumpNode(node);
            node.RemoveNodes("ScienceData"); //** Prevent duplicates            
            if (data != null)
            {
                ConfigNode storedDataNode = node.AddNode("ScienceData");
                data.SaveImpact(storedDataNode);
            }
        }


        internal static void NewResult(ConfigNode node, ImpactScienceData newData)
        {
            Log("Spectrometer.NewResult, kineticEnergy: " + newData.kineticEnergy + ", dataAmount: " + newData.dataAmount+ ", biome: " + newData.biome + ", latitude: " + newData.latitude +
                ", datatype: " + newData.datatype + ", situationMask: " + newData.situationMask);

            //only replace if it is better than any existing results
            if (node.HasNode("ScienceData"))
            {
                ConfigNode storedDataNode = node.GetNode("ScienceData");
                ImpactScienceData data = new ImpactScienceData(storedDataNode);
                if (newData.dataAmount <= data.dataAmount)
                {
                    Log("Discarding because better data is already stored");
                    return;
                }
            }
            OnSave(node, newData);
        }

        public override void OnUpdate()
        {
            if (result != null)
            {
                Events["reviewEvent"].active = true;
                statusText = "   Data recorded";
            }
        }


        public void ReturnData(ScienceData data)
        {
            Log("Spectrometer.ReturnData");
            if (data != null)
            {

                if (result == null || data.dataAmount > result.dataAmount)
                {
                    result = data as ImpactScienceData;
                }
#if false
                else if (data.dataAmount > result.dataAmount)
                {
                    result = data as ImpactScienceData;
                }
#endif
            }

            return;
        }

        internal void addExperiment(ImpactScienceData newData)
        {
            //only replace if it is better than any existing results
            if (result == null || newData.dataAmount > result.dataAmount)
            {
                Log("Trying to save impact");
                result = newData;
            }
        }

        public bool IsRerunnable()
        {
            return true;
        }

        public int GetScienceCount()
        {
            return result != null ? 1 : 0;
        }

        public void ReviewDataItem(ScienceData sd)
        {
            ScienceLabSearch labSearch = new ScienceLabSearch(part.vessel, sd);
            expDialog = ExperimentsResultDialog.DisplayResult(new ExperimentResultDialogPage(part, sd, 1f, 0f, false, "", true, labSearch, DumpData, KeepData, TransmitData, null));
        }

        public void ReviewData()
        {
            Log("ReviewData, GetScienceCount(): " + GetScienceCount());

            if (GetScienceCount() < 1)
                return;
            if (expDialog != null)
                DestroyImmediate(expDialog);
            ScienceData sd = result;
            ReviewDataItem(sd);
        }

        public ScienceData[] GetData()
        {
            Log("Spectrometer.GetData");
            if (result != null)
                return new ImpactScienceData[] { result };
            else
                return new ImpactScienceData[] { };
        }

        public ImpactScienceData[] GetImpactData()
        {
            Log("Spectrometer.GetImpactData");
            if (result != null)
                return new ImpactScienceData[] { result };
            else
                return new ImpactScienceData[] { };
        }

        public void DumpData(ScienceData data)
        {
            Log("DumpData");
            expDialog = null;
            result = null;
        }

        public void KeepData(ScienceData data)
        {
            expDialog = null;
        }
        public void TransmitData(ScienceData data)
        {
            Log("TransmitData");
            expDialog = null;
            List<IScienceDataTransmitter> tranList = vessel.FindPartModulesImplementing<IScienceDataTransmitter>();
            if (tranList.Count > 0 && result != null)
            {
                List<ScienceData> list2 = new List<ScienceData>();
                list2.Add(result);
                tranList.OrderBy(ScienceUtil.GetTransmitterScore).First().TransmitData(list2);
                ImpactMonitor.getInstance().scienceToKSC(result);
                DumpData(result);
            }
            else ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_Screen_NoTrans"), 4f, ScreenMessageStyle.UPPER_LEFT);
        }
        
        [KSPEvent(guiActive = true, guiName = "#autoLOC_Spectrometer_Review", active = false)]
        public void reviewEvent()
        {
            ReviewData();
        }
    }
}
