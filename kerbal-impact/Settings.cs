namespace kerbal_impact
{
    public class ImpactSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Impact!"; } } // Localizer.GetStringByTag("LOC_RoverScience_GUI_DefaultSettings") ; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "Impact!"; } } // Localizer.GetStringByTag("LOC_RoverScience_GUI_RoverScience"); } }
        public override string DisplaySection { get { return "Impact!"; } } //  Localizer.GetStringByTag("LOC_RoverScience_GUI_RoverScience") ; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }

        [GameParameters.CustomParameterUI("Return to observer vessel after impact",
            toolTip = "Return to the observer vessel after impact, if disabled, will have to return to Tracking Station")]
        public bool returnToObserver = true;

        [GameParameters.CustomIntParameterUI("Time to wait before returning to observer vessel", minValue = 5, maxValue = 30,
            toolTip = "This is the delay after an impact before returning to the observer vessel")]
        public int timeDelayBeforeReturn = 5;

    }
}
