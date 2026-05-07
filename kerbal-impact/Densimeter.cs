using KSP.Localization;
using KSP.UI.Screens.Flight.Dialogs;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{
    internal class Densimeter : PartModule, IScienceDataContainer
    {
        public Spectrometer observerPartModule = null;
        public Vessel observerVessel = null;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Densimeter Status", guiUnits = "", isPersistant = true)]
        public string statusText = "   No data";


        public override void OnLoad(ConfigNode node)
        {
            Log("Densimeter.OnLoad");
            if (node.HasNode("ScienceData"))
            {
                Log("Has ScienceData");
                ConfigNode storedDataNode = node.GetNode("ScienceData");
                ImpactScienceData data = new ImpactScienceData(storedDataNode);
                result = data;
            }
        }

        void Start()
        {
            Log("Densimeter.Start");
            if (result != null) Log("result is NOT null");
            if (HighLogic.LoadedSceneIsFlight)
            {
                observerVessel = vessel;
                List<Spectrometer> spectrometers = vessel.FindPartModulesImplementing<Spectrometer>();
                observerPartModule = spectrometers[0];
            }
        }

        protected ImpactScienceData result;

        public override void OnSave(ConfigNode node)
        {
            Log("Saving densimeter 1");
            OnSave(node, result);
        }

        public static void OnSave(ConfigNode node, ImpactScienceData data)
        {
            Log("Saving densimeter 2");
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
            Log("Densimeter.NewResult, dataAmount: " + newData.dataAmount +", datatype: " + newData.datatype + ", situationMask: " + newData.situationMask);
            
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
            Log("Densimeter.ReturnData");
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
            Log("addExperiment, deployed: " + observerPartModule.deployed + ", deployable: " + observerPartModule.deployable);
            if (observerPartModule.deployed || !observerPartModule.deployable)
            {
                //only replace if it is better than any existing results
                if (result == null || newData.dataAmount > result.dataAmount)
                {
                    Log("Densimeter.addExperiment, Trying to save impact");
                    result = newData;
                }
            }
        }


        protected ExperimentsResultDialog expDialog = null;

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
            if (result != null)
                return new ImpactScienceData[] { result };
            else
                return new ImpactScienceData[] { };
        }

        public ImpactScienceData[] GetDensitytData()
        {
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

        [KSPEvent(guiActive = true, guiName = "#autoLOC_Densimeter_Review", active = false)]
        public void reviewEvent()
        {
            ReviewData();
        }

    }
}
