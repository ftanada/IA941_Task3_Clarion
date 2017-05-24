using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Clarion;
using Clarion.Framework;
using Clarion.Framework.Core;
using Clarion.Framework.Templates;
using WorldServerLibrary.Model;
using WorldServerLibrary;
using System.Threading;
using Gtk;
using System.Collections;

namespace ClarionDEMO
{
    /// <summary>
    /// Public enum that represents all possibilities of agent actions
    /// </summary>
    public enum CreatureActions
    {
        DO_NOTHING,
        ROTATE_CLOCKWISE,
        GO_AHEAD,
        GO_TO,
        EAT,
        GET,
        HIDE
    }

    public class ClarionAgent
    {
        #region Constants
        /// <summary>
        /// Constant that represents the Visual Sensor
        /// </summary>
        private String SENSOR_VISUAL_DIMENSION = "VisualSensor";
        /// <summary>
        /// Constant that represents that there is at least one wall ahead
        /// </summary>
        private String DIMENSION_WALL_AHEAD = "WallAhead";
        // FMT 29/04/2017
        private String DIMENSION_FOOD_AHEAD = "FoodAhead";
        private String DIMENSION_JEWEL_AHEAD = "JewelAhead";
        private String DIMENSION_JEWEL_HIDE = "JewelHide";
        private String DIMENSION_ENERGY_LOW = "EnergyLow";
        double prad = 0;
        #endregion

        #region Properties
		public Mind mind = null;
		String creatureId = String.Empty;
		String creatureName = String.Empty;
        #endregion

        #region Simulation
        /// <summary>
        /// If this value is greater than zero, the agent will have a finite number of cognitive cycle. Otherwise, it will have infinite cycles.
        /// </summary>
        public double MaxNumberOfCognitiveCycles = -1;
        /// <summary>
        /// Current cognitive cycle number
        /// </summary>
        private double CurrentCognitiveCycle = 0;
        /// <summary>
        /// Time between cognitive cycle in miliseconds
        /// </summary>
        public Int32 TimeBetweenCognitiveCycles = 0;
        /// <summary>
        /// A thread Class that will handle the simulation process
        /// </summary>
        private Thread runThread;
        #endregion

        #region Agent
		private WorldServer worldServer;
        /// <summary>
        /// The agent 
        /// </summary>
        private Clarion.Framework.Agent CurrentAgent;
        #endregion

        #region Perception Input
        /// <summary>
        /// Perception input to indicates a wall ahead
        /// </summary>
		private DimensionValuePair inputWallAhead;
        // FMT 29/04/2017 element to perceive a thing (food / jewel)
        private DimensionValuePair inputFoodAhead;
        private DimensionValuePair inputJewelAhead;
        private DimensionValuePair inputEnergyLow;
        private DimensionValuePair inputJewelHide;
        private String lastSeenFood = String.Empty;
        private String lastSeenJewel = String.Empty;
        private String lastSeenJewelColor = String.Empty;
        private Creature myCreature = null;
        private double thingVr = 1;
        private double thingVl = 1;
        private double thingDistance = 0;
        private double thingX = 0;
        private double thingY = 0;
        #endregion

        #region Action Output
        /// <summary>
        /// Output action that makes the agent to rotate clockwise
        /// </summary>
		private ExternalActionChunk outputRotateClockwise;
        /// <summary>
        /// Output action that makes the agent go ahead
        /// </summary>
		private ExternalActionChunk outputGoAhead;
        /// Output action that makes the agent go to
        /// FMT 13/05/2017
		private ExternalActionChunk outputGoTo;
        /// FMT 20170430
        /// Output action that makes the agent eat
        private ExternalActionChunk outputEat;
        /// <summary>
        ///  Output action that makes the agent get object
        private ExternalActionChunk outputGet;
        ///  Output action that makes the agent hide object
        private ExternalActionChunk outputHide;

        /// </summary>
        /// <param name="nws"></param>
        /// <param name="creature_ID"></param>
        /// <param name="creature_Name"></param>
        #endregion

        
        #region Constructor
        public ClarionAgent(WorldServer nws, String creature_ID, String creature_Name)
        {
			worldServer = nws;
			// Initialize the agent
            CurrentAgent = World.NewAgent("Current Agent");
			mind = new Mind();
			mind.Show ();
			creatureId = creature_ID;
			creatureName = creature_Name;

            // Initialize Input Information
            inputWallAhead = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_WALL_AHEAD);
            // FMT 29/04/2017
            inputFoodAhead = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_FOOD_AHEAD);
            inputJewelAhead = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_JEWEL_AHEAD);
            inputJewelHide = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_JEWEL_HIDE);
            inputEnergyLow = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_ENERGY_LOW);

            // Initialize Output actions
            outputRotateClockwise = World.NewExternalActionChunk(CreatureActions.ROTATE_CLOCKWISE.ToString());
            outputGoAhead = World.NewExternalActionChunk(CreatureActions.GO_AHEAD.ToString());
            // FMT 29/04/2017
            outputEat = World.NewExternalActionChunk(CreatureActions.EAT.ToString());
            outputGet = World.NewExternalActionChunk(CreatureActions.GET.ToString());
            outputGoTo = World.NewExternalActionChunk(CreatureActions.GO_TO.ToString());
            outputHide = World.NewExternalActionChunk(CreatureActions.HIDE.ToString());

            //Create thread to simulation
            runThread = new Thread(CognitiveCycle);
			Console.WriteLine("Agent started");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Run the Simulation in World Server 3d Environment
        /// </summary>
        public void Run()
        {                
			Console.WriteLine ("Running ...");
            // Setup Agent to run
            if (runThread != null && !runThread.IsAlive)
            {
                SetupAgentInfraStructure();
                // Start Simulation Thread                
                runThread.Start(null);
            }
        }

        /// <summary>
        /// Abort the current Simulation
        /// </summary>
        /// <param name="deleteAgent">If true beyond abort the current simulation it will die the agent.</param>
        public void Abort(Boolean deleteAgent)
        {   Console.WriteLine ("Aborting ...");
            if (runThread != null && runThread.IsAlive)
            {
                runThread.Abort();
            }

            if (CurrentAgent != null && deleteAgent)
            {
                CurrentAgent.Die();
            }
        }

		IList<Thing> processSensoryInformation()
		{
			IList<Thing> response = null;

			if (worldServer != null && worldServer.IsConnected)
			{
				response = worldServer.SendGetCreatureState(creatureName);
				prad = (Math.PI / 180) * response.First().Pitch;
				while (prad > Math.PI) prad -= 2 * Math.PI;
				while (prad < - Math.PI) prad += 2 * Math.PI;
				Sack s = worldServer.SendGetSack("0");
				mind.setBag(s);
			}

			return response;
		}

		void processSelectedAction(CreatureActions externalAction)
		{   Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
			if (worldServer != null && worldServer.IsConnected)
			{
                switch (externalAction)
                {
                    case CreatureActions.DO_NOTHING:
                        // Do nothing as the own value says
                        break;
                    case CreatureActions.ROTATE_CLOCKWISE:
                        worldServer.SendSetAngle(creatureId, 2, -2, 2);
                        break;
                    case CreatureActions.GO_AHEAD:
                        worldServer.SendSetAngle(creatureId, 1, 1, prad);
                        break;
                    // FMT 29/04/2017
                    case CreatureActions.EAT:
                        Console.WriteLine("Action: EAT");
                        if (lastSeenFood != null)
                        {
                            worldServer.SendEatIt(creatureId, lastSeenFood);
                            worldServer.GenerateFood(1, 1);
                            mind.update();
                            // FMT redirect creature
                            if (thingX > 0)
                            {
                                Console.WriteLine("Action (EAT): GO TO");
                                worldServer.SendSetGoTo(creatureId, thingVr, thingVl, thingX, thingY);
                            }
                        }
                        break;
                    case CreatureActions.GET:
                        Console.WriteLine("Action: GET");
                        if (lastSeenJewel != null)
                        {
                            worldServer.SendSackIt(creatureId, lastSeenJewel);
                            mind.setBag(worldServer.SendGetSack(creatureId));
                            mind.update();
                            // FMT 15/05/2017 forcing window refresh
                            //mind.HideAll();
                            //mind.ShowAll();
                            // FMT reseed more jewel
                            worldServer.GenerateJewel(1);
                            // FMT redirect creature
                            if (thingX > 0)
                            {
                                Console.WriteLine("Action (GET): GO TO");
                                worldServer.SendSetGoTo(creatureId, thingVr, thingVl, thingX, thingY);
                            }
                        }
                        break;
                    case CreatureActions.GO_TO:
                        Console.Write("Action: GO TO ");
                        Console.Write(thingX);
                        Console.WriteLine(thingY);
                        worldServer.SendSetGoTo(creatureId, thingVr, thingVl, thingX, thingY);
                        //worldServer.SendSetAngle(creatureId, 1, 1, prad);
                        break;
                    case CreatureActions.HIDE:
                        Console.WriteLine("Action: HIDE");
                        if (lastSeenJewel != null)
                        {
                            worldServer.SendHideIt(creatureId, lastSeenJewel);
                            mind.update();
                            // FMT reseed more jewel
                            worldServer.GenerateJewel(1);
                            // FMT redirect creature
                            if (thingX > 0)
                            {
                                Console.WriteLine("Action (HIDE): GO TO");
                                worldServer.SendSetGoTo(creatureId, thingVr, thingVl, thingX, thingY);
                            }
                        }
                        break;
                default:
					break;
				}
			}
		}

        #endregion

        #region Setup Agent Methods
        /// <summary>
        /// Setup agent infra structure (ACS, NACS, MS and MCS)
        /// </summary>
        private void SetupAgentInfraStructure()
        {
            // Setup the ACS Subsystem
            SetupACS();                    
        }

        private void SetupMS()
        {            
            //RichDrive
        }

        /// <summary>
        /// Setup the ACS subsystem
        /// </summary>
        private void SetupACS()
        {
            // Create Rule to avoid collision with wall
            SupportCalculator avoidCollisionWallSupportCalculator = FixedRuleToAvoidCollisionWall;
            FixedRule ruleAvoidCollisionWall = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputRotateClockwise, avoidCollisionWallSupportCalculator);

            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleAvoidCollisionWall);

            // Create Rule To Go Ahead
            SupportCalculator goAheadSupportCalculator = FixedRuleToGoAhead;
            FixedRule ruleGoAhead = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGoAhead, goAheadSupportCalculator);
            
            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleGoAhead);

            // FMT 29/04/2017
            // FMT Create Rule to Eat
            SupportCalculator eatSupportCalculator = FixedRuleToEat;
            FixedRule ruleEat = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputEat, eatSupportCalculator);
            CurrentAgent.ACS.Parameters.PERFORM_RER_REFINEMENT = true;
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_METHOD = ActionCenteredSubsystem.LevelSelectionMethods.COMBINED;
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_OPTION = ActionCenteredSubsystem.LevelSelectionOptions.FIXED;
            CurrentAgent.ACS.Parameters.FIXED_FR_LEVEL_SELECTION_MEASURE = 1;
            CurrentAgent.ACS.Parameters.FIXED_BL_LEVEL_SELECTION_MEASURE = 1;
            CurrentAgent.ACS.Parameters.FIXED_RER_LEVEL_SELECTION_MEASURE = 1;
            CurrentAgent.ACS.Parameters.WM_UPDATE_ACTION_PROBABILITY = 1;

            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleEat);

            // FMT Create Rule to Get
            SupportCalculator getSupportCalculator = FixedRuleToGet;
            FixedRule ruleGet = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGet, getSupportCalculator);

            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleGet);

            // FMT Create Rule to Hide
            SupportCalculator hideSupportCalculator = FixedRuleToHide;
            FixedRule ruleHide = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputHide, hideSupportCalculator);

            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleHide);

            // FMT Create Rule to Go To
            SupportCalculator gotoSupportCalculator = FixedRuleToGoTo;
            FixedRule ruleGoto = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGoTo, gotoSupportCalculator);

            // Commit this rule to Agent (in the ACS)
            //CurrentAgent.Commit(ruleGoto);

            // Disable Rule Refinement
            CurrentAgent.ACS.Parameters.PERFORM_RER_REFINEMENT = false;

            // The selection type will be probabilistic
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_METHOD = ActionCenteredSubsystem.LevelSelectionMethods.STOCHASTIC;

            // The action selection will be fixed (not variable) i.e. only the statement defined above.
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_OPTION = ActionCenteredSubsystem.LevelSelectionOptions.FIXED;

            // Define Probabilistic values
            CurrentAgent.ACS.Parameters.FIXED_FR_LEVEL_SELECTION_MEASURE = 1;
            CurrentAgent.ACS.Parameters.FIXED_IRL_LEVEL_SELECTION_MEASURE = 0;
            CurrentAgent.ACS.Parameters.FIXED_BL_LEVEL_SELECTION_MEASURE = 0;
            CurrentAgent.ACS.Parameters.FIXED_RER_LEVEL_SELECTION_MEASURE = 0;

            // FMT 13/05/2017 additonal setting for network
            SimplifiedQBPNetwork net = AgentInitializer.InitializeImplicitDecisionNetwork(CurrentAgent, SimplifiedQBPNetwork.Factory);
            net.Parameters.LEARNING_RATE = 1;
            CurrentAgent.ACS.Parameters.PERFORM_RER_REFINEMENT = false;
        }

        /// <summary>
        /// Make the agent perception. In other words, translate the information that came from sensors to a new type that the agent can understand
        /// </summary>
        /// <param name="sensorialInformation">The information that came from server</param>
        /// <returns>The perceived information</returns>
		private SensoryInformation prepareSensoryInformation(IList<Thing> listOfThings)
        {
            // FMT 30/04/2017 - debugging
            /*Console.WriteLine("Dumping listoFThings (input):");
            for (int iList = 0; iList < listOfThings.Count(); iList++)
            {
                Thing currThing = (Thing) listOfThings[iList];
                Console.Write("Thing ");
                Console.WriteLine(iList);
                Console.WriteLine(currThing);
            }*/

            // New sensory information
            SensoryInformation si = World.NewSensoryInformation(CurrentAgent);

            // FMT 29/04/2017 - handle food and jewel
            Boolean jewelAhead = false;
            Boolean foodAhead = false;
            Boolean wallAhead = false;
            double jewelAheadActivationValue = CurrentAgent.Parameters.MIN_ACTIVATION;
            foreach (Thing item in listOfThings)
            {
                switch (item.CategoryId)
                {
                    case Thing.CATEGORY_JEWEL:
                        if (item.DistanceToCreature <= 61)
                        {
                            jewelAhead = true;
                            //CurrentAgent.ReceiveFeedback(si, 1.0);
;                       }
                        else
                        {
                            if ((thingDistance > 0) && (thingDistance > item.DistanceToCreature))
                            {
                                thingX = item.comX;
                                thingY = item.comY;
                                thingDistance = item.DistanceToCreature;
                                //CurrentAgent.ReceiveFeedback(si, 0.0);
                            }
                        }
                        jewelAheadActivationValue = jewelAhead ? CurrentAgent.Parameters.MAX_ACTIVATION : CurrentAgent.Parameters.MIN_ACTIVATION;
                        si.Add(inputJewelAhead, jewelAheadActivationValue);
                        if (jewelAhead)
                        {
                            lastSeenJewel = item.Name;
                            lastSeenJewelColor = item.Material.Color;
                            Console.Write("Input: jewel ");
                            Console.WriteLine(lastSeenJewel);
                            //jewelAhead = false;
                        }
                        break;
                    case Thing.CATEGORY_FOOD:
                    case Thing.CATEGORY_PFOOD:
                    case Thing.CATEGORY_NPFOOD:
                        double foodAheadActivationValue = CurrentAgent.Parameters.MIN_ACTIVATION;
                        if (item.DistanceToCreature <= 61) foodAhead = true;
                        if ((foodAhead) && !(jewelAhead))
                        {
                            foodAheadActivationValue = CurrentAgent.Parameters.MAX_ACTIVATION;
                            lastSeenFood = item.Name;
                            Console.Write("Input: food ");
                            Console.WriteLine(lastSeenFood);
                        }
                        si.Add(inputFoodAhead, foodAheadActivationValue);
                        break;
                    case Thing.CATEGORY_BRICK:
                        if (item.DistanceToCreature <= 61) wallAhead = true;
                        // Detect if we have a wall ahead
                        double wallAheadActivationValue = wallAheadActivationValue = CurrentAgent.Parameters.MIN_ACTIVATION;
                        if ((wallAhead) && !(jewelAhead) && !(foodAhead))
                            wallAheadActivationValue = CurrentAgent.Parameters.MAX_ACTIVATION;
                        si.Add(inputWallAhead, wallAheadActivationValue);
                        break;
                }
            }

            //Console.WriteLine(sensorialInformation);
            myCreature = (Creature)listOfThings.Where(item => (item.CategoryId == Thing.CATEGORY_CREATURE)).First();
            int n = 0; 
            int isRequiredTotal = 0;
            foreach (Leaflet l in myCreature.getLeaflets())
            {
                mind.updateLeaflet(n, l);
                // FMT checking if we target a jewel if that jewel is in the leaflet
                if (jewelAheadActivationValue > CurrentAgent.Parameters.MIN_ACTIVATION)
                {
                    int isRequired = l.getRequired(lastSeenJewelColor);
                    int isCollected = l.getCollected(lastSeenJewelColor);
                    isRequiredTotal = isRequiredTotal + (isRequired - isCollected);
                }
                n++;
            }
            if ((jewelAheadActivationValue > CurrentAgent.Parameters.MIN_ACTIVATION) && (isRequiredTotal <= 0))
            {
                // not in leaflet, setup hide it
                double jewelHideActivationValue = CurrentAgent.Parameters.MAX_ACTIVATION;
                si.Add(inputJewelHide, jewelHideActivationValue);
                si.Add(inputJewelAhead, CurrentAgent.Parameters.MIN_ACTIVATION);
            }
            //System.Threading.Thread.Sleep(100);
            return si;
        }
        #endregion

        #region Fixed Rules
        private double FixedRuleToAvoidCollisionWall(ActivationCollection currentInput, Rule target)
        {
            // See partial match threshold to verify what are the rules available for action selection
            return ((currentInput.Contains(inputWallAhead, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
        }

        private double FixedRuleToGoAhead(ActivationCollection currentInput, Rule target)
        {
            // Here we will make the logic to go ahead
            return ((currentInput.Contains(inputWallAhead, CurrentAgent.Parameters.MIN_ACTIVATION))) ? 1.0 : 0.0;
        }

        // FMT 13/05/2017
        private double FixedRuleToGoTo(ActivationCollection currentInput, Rule target)
        {
            // Here we will make the logic to go ahead
            return ((currentInput.Contains(inputWallAhead, CurrentAgent.Parameters.MIN_ACTIVATION))) ? 1.0 : 0.0;
        }

        // FMT 29/04/2017
        private double FixedRuleToEat(ActivationCollection currentInput, Rule target)
        {
            // Here we will make the logic to eat
            return ((currentInput.Contains(inputFoodAhead, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
        }

        private double FixedRuleToGet(ActivationCollection currentInput, Rule target)
        {
            // Here we will make the logic to get thing
            return ((currentInput.Contains(inputJewelAhead, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
        }

        private double FixedRuleToHide(ActivationCollection currentInput, Rule target)
        {
            // Here we will make the logic to hide thing
            return ((currentInput.Contains(inputJewelHide, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
        }

        #endregion

        #region Run Thread Method
        private void CognitiveCycle(object obj)
        {

			Console.WriteLine("Starting Cognitive Cycle ... press CTRL-C to finish !");
   
            // Cognitive Cycle starts here getting sensorial information
            while (CurrentCognitiveCycle != MaxNumberOfCognitiveCycles)
            {   
				// Get current sensory information                    
				IList<Thing> currentSceneInWS3D = processSensoryInformation();

                // Make the perception
                SensoryInformation si = prepareSensoryInformation(currentSceneInWS3D);

                //Perceive the sensory information
                CurrentAgent.Perceive(si);

                //Choose an action
                ExternalActionChunk chosen = CurrentAgent.GetChosenExternalAction(si);

                // Get the selected action
                String actionLabel = chosen.LabelAsIComparable.ToString();
                CreatureActions actionType = (CreatureActions)Enum.Parse(typeof(CreatureActions), actionLabel, true);
                // FMT 02/05/2017
                if (actionLabel != null)
                {
                    //Console.Write("Output: ");
                    //Console.WriteLine(actionLabel);
                }

                // Call the output event handler
				processSelectedAction(actionType);

                // Increment the number of cognitive cycles
                CurrentCognitiveCycle++;

                //Wait to the agent accomplish his job
                if (TimeBetweenCognitiveCycles > 0)
                {
                    Thread.Sleep(TimeBetweenCognitiveCycles);
                }
			}
        }
        #endregion

    }
}
