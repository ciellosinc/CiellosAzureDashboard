using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            StringBuilder sb = new StringBuilder();
            VM virtMachine = null;
            try
            {
                //GetHashCode VM from local DB

                if (_context.VMs.Where(vm => vm.VMName.ToUpper() == startStopItem.VMName.ToUpper()).ToList().Count == 1)
                {
                    virtMachine = _context.VMs.FirstOrDefault(vm => vm.VMName.ToUpper() == startStopItem.VMName.ToUpper());
                }

                if (virtMachine == null)
                {
                    if (_context.VMs.Where(vm => vm.VMName.ToUpper() == startStopItem.VMName.ToUpper() && vm.ResourceGroupName.ToUpper() == startStopItem.ResourceGroup.ToUpper()).ToList().Count == 1)
                    {
                        virtMachine = _context.VMs.FirstOrDefault(vm => vm.VMName.ToUpper() == startStopItem.VMName.ToUpper() && vm.ResourceGroupName.ToUpper() == startStopItem.ResourceGroup.ToUpper());
                    }
                    else
                    {
                        virtMachine = _context.VMs.FirstOrDefault(vm => vm.VMName.ToUpper() == startStopItem.VMName.ToUpper() && vm.ResourceGroupName.ToUpper() == startStopItem.ResourceGroup.ToUpper() && vm.SubscriptionId == startStopItem.SubscriptionId);
                    }
                }

                if (virtMachine.Id > 0)
                {
                    switch (startStopItem.Action)
                    {
                        case "Start":
                            {
                                _azureHelper.StartVM(virtMachine.VMId);
                                return Ok(new { virtMachine.VMName, ResourceGroup = virtMachine.ResourceGroupName, virtMachine.SubscriptionId, Result = "Is starting" });
                            }
                        case "Stop":
                            {
                                _azureHelper.StopVM(virtMachine.VMId);
                                return Ok(new { virtMachine.VMName, ResourceGroup = virtMachine.ResourceGroupName, virtMachine.SubscriptionId, Result = "Is stoping" });
                            }
                        case "Status":
                            {

                                VM vMachine = _azureHelper.GetVM(virtMachine.VMId);

                                return Ok(new { vMachine.VMName, ResourceGroup = vMachine.ResourceGroupName, vMachine.SubscriptionId, _azureHelper.GetVMFromAzure(virtMachine.VMId).Tags, Result = vMachine.PowerState });
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
            catch (Exception ex)
            {
                return BadRequest("Please, check your input data." + ex.ToString() + virtMachine.VMId);
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
