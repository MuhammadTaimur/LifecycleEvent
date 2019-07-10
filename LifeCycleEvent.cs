using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;

[assembly: Autodesk.Connectivity.Extensibility.Framework.ApiVersion("11.0")]
[assembly: Autodesk.Connectivity.Extensibility.Framework.ExtensionId("9D3D1F6A-9046-4C82-9774-4F2F522C9606")]

namespace LifeCycleEvent
{
    public class LifeCycleEvent : IWebServiceExtension
    {
        public void OnLoad()
        {
            DocumentServiceExtensions.UpdateFileLifecycleStateEvents.Pre += new EventHandler<UpdateFileLifeCycleStateCommandEventArgs>(PostLifeCycleEvent);
        }

        void PostLifeCycleEvent(object sender, UpdateFileLifeCycleStateCommandEventArgs EventArgs)
        {
            IWebService VaultSesession = (IWebService)sender;
            String VaultName = VaultSesession.WebServiceManager.WebServiceCredentials.VaultName;
            WebServiceCredentials VaultCrednetials = new WebServiceCredentials(VaultSesession);
            WebServiceManager VaultServiceManager = new WebServiceManager(VaultCrednetials);

            List<File> AllFiles = new List<File>();
            List<long> FileIds = new List<long>();
            List<long> LifeCycleDefninitions = new List<long>();
            List<long> FromLifecycleStates = new List<long>();

            File FileObject = new File();
            foreach (long EachMasterId in EventArgs.FileMasterIds)
            {
                FileObject = new File();
                FileObject = VaultServiceManager.DocumentService.GetLatestFileByMasterId(EachMasterId);
                FileLfCyc FromLifeCycle = FileObject.FileLfCyc;

                AllFiles.Add(FileObject);
                FileIds.Add(FileObject.Id);
                LifeCycleDefninitions.Add(FromLifeCycle.LfCycDefId);
                FromLifecycleStates.Add(FromLifeCycle.LfCycStateId);
            }

            IEnumerable<long> UniqueLifeCycleDefinitionIds = LifeCycleDefninitions.Distinct();
            IEnumerable<long> UniqueFromLifecycleStateIds = FromLifecycleStates.Distinct();
            IEnumerable<long> UniqueToLifecycleStateIds = EventArgs.ToStateIds.Distinct();

            LfCycDef[] AllFileLifeCycleDefs = VaultServiceManager.LifeCycleService.GetLifeCycleDefinitionsByIds(UniqueLifeCycleDefinitionIds.ToArray());

            List<PropDefCond> ConditionArray = new List<PropDefCond>();

            // Get the Condition Arrays for the Selected Files by matching the from and to lifercycle state Ids
            foreach (LfCycDef EachDef in AllFileLifeCycleDefs)
            {
                LfCycTrans[] LifeCycleTransitions = EachDef.TransArray;

                foreach(LfCycTrans EachTran in LifeCycleTransitions)
                {
                    if (!(EachTran.RuleSet is null))
                        {
                        foreach (long FromLifeCycleStateId in UniqueFromLifecycleStateIds)
                        {
                            foreach (long UniqueToLifecycleStateId in UniqueToLifecycleStateIds)
                            {
                                foreach(long UniqueFromLifecycleStateId in UniqueFromLifecycleStateIds)
                                {
                                    if ((EachTran.FromId == UniqueFromLifecycleStateId) && (EachTran.ToId == UniqueToLifecycleStateId))
                                    {
                                        foreach(PropDefCond EachCond in EachTran.RuleSet.CondArray)
                                        {
                                            ConditionArray.Add(EachCond);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Get the Porerpty Def Ids from the Condition Array
            List<long> NonCompliantPropDefIds = new List<long>();
            foreach(PropDefCond EachCond in ConditionArray)
            {
                NonCompliantPropDefIds.Add(EachCond.PropDefId);
            }

            //Shortlist the Compliance Falgged Proerpty Ids
            IEnumerable<long> UniqueNonCompliantPropDefIds = NonCompliantPropDefIds.Distinct();

            // Getting the Prorepty VALUES for Compliance Marked Properties from the Selected Files
            PropInst[] AllProeprtyInfo = VaultServiceManager.PropertyService.GetProperties("FILE", FileIds.ToArray(), UniqueNonCompliantPropDefIds.ToArray());
            List<long> ActualNonCompliantProperties = new List<long>();
            foreach (PropInst EachPropInfo in AllProeprtyInfo)
            {
                if (EachPropInfo.Val is null)
                {
                    ActualNonCompliantProperties.Add(EachPropInfo.PropDefId);
                }
            }

            // Shotlist the Array with Unqiue Property Ids
            IEnumerable<long> UniqueActualNonCompliantProperties = ActualNonCompliantProperties.Distinct();

            // Get the Proerpty Definition for these Ids
            PropDefInfo[] NonCompliantProperties = VaultServiceManager.PropertyService.GetPropertyDefinitionInfosByEntityClassId("FILE", UniqueActualNonCompliantProperties.ToArray());

            // Generate the Warning Message
            String PropertyNames = "";
            int i = 0;
            foreach (PropDefInfo EachInfo in NonCompliantProperties)
            {
                PropertyNames += EachInfo.PropDef.DispName + "\n";
                i++;
            }
            MessageBox.Show("Warning! The following Property(s) are Non-Compliant: \n\n" + PropertyNames, "Property Compliance Warning",MessageBoxButtons.OK, MessageBoxIcon.Warning );
                
        }

    }

}
