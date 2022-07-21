using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CiellosAzureDashboard.Data;
using CiellosAzureDashboard.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CiellosAzureDashboard.APIControllers
{
    [ApiKeyAuth]
    [Route("api/[controller]")]
    [ApiController]
    public class ManagementController : ControllerBase
    {
        private readonly CADContext _context;
        private readonly IAzureHelper _azureHelper;

        public ManagementController(CADContext context, IAzureHelper azureHelper)
        {
            _context = context;
            _azureHelper = azureHelper;
        }
        [HttpPost]
        public ActionResult<string> PostTodoItem([FromBody] StartStopItem startStopItem)
        {
            VM virtMachine = new VM();
            try
            {
                //GetHashCode VM from local DB
                virtMachine = _context.VMs.FirstOrDefault(vm => vm.VMName == startStopItem.VMName && vm.ResourceGroupName == startStopItem.ResourceGroup && vm.SubscriptionId == startStopItem.SubscriptionId);

                if (virtMachine != null)
                {
                    switch (startStopItem.Action)
                    {
                        case "Start":
                            {
                                _azureHelper.StartVM(virtMachine.VMId);
                                return Ok(new { VMName = virtMachine.VMName, ResourceGroup = virtMachine.ResourceGroupName, SubscriptionId = virtMachine.SubscriptionId, Result = "Is starting" });
                            }
                        case "Stop":
                            {
                                _azureHelper.StopVM(virtMachine.VMId);
                                return Ok(new { VMName = virtMachine.VMName, ResourceGroup = virtMachine.ResourceGroupName, SubscriptionId = virtMachine.SubscriptionId, Result = "Is stoping" });
                            }
                        case "Status":
                            {
                                VM vMachine = _azureHelper.GetVM(virtMachine.VMId);
                                return Ok(new { VMName = vMachine.VMName, ResourceGroup = vMachine.ResourceGroupName, SubscriptionId = vMachine.SubscriptionId, Result = vMachine.PowerState });
                            }
                        default:
                            {
                                return BadRequest(string.Format("Action value {0} not found", startStopItem.Action));
                            }

                    }

                }
                else
                {
                    return BadRequest("The virtual machine was not found in the dashboard database.");
                }
            }
            catch
            {
                
            }
            return BadRequest("Please, check your input data.");

        }
    }


    public class StartStopItem
    { 
        public string VMName { get; set; }
        public string ResourceGroup { get; set; }
        public string SubscriptionId { get; set; }
        public string Action { get; set; }
    }
}