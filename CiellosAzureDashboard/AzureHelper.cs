using CiellosAzureDashboard.Data;
using CiellosAzureDashboard.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Cdn;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Clients;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Transactions;

namespace CiellosAzureDashboard
{
    public class AzureHelper : IAzureHelper
    {
        private List<IVirtualMachine> VirtualMachines { get; set; }
        public VMList VirtualMashinesList { get; set; }
        private TransactionScope TransactionScope { get; set; }
        private System.Data.Common.DbTransaction currentTransaction { get; set; }
        public X509Helper X509Helper { get; set; }
        public bool IsDBLocked { get; set; }

        public AzureHelper(IX509Helper _X509Helper)
        {
            X509Helper = _X509Helper as X509Helper;
            VirtualMashinesList = new VMList(this);
            X509Helper.RotateCertificate();
            Task.Run(() => RemoveAllVirtualMashinesAsync());
            Task.Run(() => UpdateAllVirtualMashinesAsync());
         //   Task.Run(() => StartVirtualMashinesAsync());
         //   Task.Run(() => StopVirtualMashinesAsync());
            AzureHelperService.AzureHelper = this;
        }

        /// <summary>
        /// GetAzureConnection
        /// </summary>
        /// <param name="_app">Application instance</param>
        /// <returns></returns>
        private IAzure GetAzureConnection(Application _app, bool _withoutSubscription = false)
        {
                if (!String.IsNullOrEmpty(_app.ClientSecret))
                {
                    return GetAzureConnection(_app.ClientId,
                                   _app.ClientSecret,
                                   _app.TenantId,
                                   _app.SubscriptionId, 
                                   _withoutSubscription);
                }
                else
                {
                    return GetAzureConnection(_app.ClientId,
                                              _app.TenantId,
                                              _app.SubscriptionId,
                                              _withoutSubscription);
            }
        }

        /// <summary>
        /// GetAzureConnection
        /// </summary>
        /// <param name="_clientId"></param>
        /// <param name="_tenantId"></param>
        /// <returns></returns>
        private IAzure GetAzureConnection(string _clientId, string _tenantId, string _subscriptionId, bool _withoutSubscription = false)
        {
            IAzure azure;
            var creds = new AzureCredentialsFactory().FromServicePrincipal(_clientId, X509Helper.GetRootCertificate(), _tenantId, AzureEnvironment.AzureGlobalCloud);
            if (_withoutSubscription)
            {
                azure = Azure
                        .Configure()
                        .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                        .Authenticate(creds)
                        .WithDefaultSubscription();
            }
            else
            {
                azure = Azure
                        .Configure()
                        .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                        .Authenticate(creds)
                        .WithSubscription(_subscriptionId);
            }

            return azure;
        }

        /// <summary>
        /// GetAzureConnection object
        /// </summary>
        /// <param name="_clientId">Application client identifier</param>
        /// <param name="_clientSecret">Application secret key</param>
        /// <param name="_tenantId">Tenant identifier</param>
        /// <param name="_subscriptionId">Subscription inentifier</param>
        /// <returns></returns>
        private IAzure GetAzureConnection(string _clientId, string _clientSecret, string _tenantId, string _subscriptionId, bool _withoutSubscription = false)
        {
            IAzure azure;
            var creds = new AzureCredentialsFactory().FromServicePrincipal(_clientId, _clientSecret, _tenantId, AzureEnvironment.AzureGlobalCloud);
            if (_withoutSubscription)
            {
                azure = Azure
                        .Configure()
                        .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                        .Authenticate(creds)
                        .WithDefaultSubscription();
            }
            else
            {
                azure = Azure
                        .Configure()
                        .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                        .Authenticate(creds)
                        .WithSubscription(_subscriptionId);
            }

            return azure;
        }

        public X509Certificate2 GetCertificate()
        {
            return X509Helper.GetPublicKeyCertificate();
        }

        public async Task RemoveAllVirtualMashinesAsync()
        {
            while (true)
            {
                await Task.Delay(86400000);
                IsDBLocked = true;
                RemoveAllVirtualMashines();
                IsDBLocked = false;
                GC.Collect();
            }

        }

        public async Task UpdateAllVirtualMashinesAsync()
        {
            while (true)
            {
                await Task.Delay(30000);
                IsDBLocked = true;
                UpdateAllVirtualMashines();
                IsDBLocked = false;
                GC.Collect();
            }

        }

        public async Task StartVirtualMashinesAsync()
        {
            while (true)
            {
                await Task.Delay(30000);
                IsDBLocked = true;
                using (CADContext context = new CADContext())
                {
                    TimeSpan timeNow = System.DateTime.Now.TimeOfDay;
                    TimeSpan time = new TimeSpan(timeNow.Hours, timeNow.Minutes, 0);
                    List<Schedules> VMs = context.Schedules.Include(s => s.ScheduleVMsList).Where(s => s.StartTime == time).ToList();
                    foreach(var res in VMs)
                    {
                        foreach (var vm in res.ScheduleVMsList)
                        {
                            VM virt = context.VMs.Find(vm.VMId);
                            this.StartVM(virt.VMId.ToString());
                        }
                    }
                }
                IsDBLocked = false;
                GC.Collect();
            }

        }

        public async Task StopVirtualMashinesAsync()
        {
            while (true)
            {
                await Task.Delay(30000);
                IsDBLocked = true;
                using (CADContext context = new CADContext())
                {
                    TimeSpan timeNow = System.DateTime.Now.TimeOfDay;
                    TimeSpan time = new TimeSpan(timeNow.Hours, timeNow.Minutes, 0);
                    List<Schedules> VMs = context.Schedules.Include(s => s.ScheduleVMsList).Where(s => s.StopTime == time).ToList();
                    foreach (var res in VMs)
                    {
                        foreach (var vm in res.ScheduleVMsList)
                        {
                            VM virt = context.VMs.Find(vm.VMId);
                            this.StopVM(virt.VMId.ToString());
                        }
                    }
                }
                IsDBLocked = false;
                GC.Collect();
            }

        }

        public void RemoveAllVirtualMashines()
        {
            using (CADContext context = new CADContext())
            {
                var vmList = context.VMs.AsNoTracking().ToList();
                context.VMs.RemoveRange(vmList);
                context.SaveChanges();
            }
        }
            public void UpdateAllVirtualMashines()
        {
            /*using (CADContext context = new CADContext())
            {
                var vmList = context.VMs.AsNoTracking().ToList();
                context.VMs.RemoveRange(vmList);
                context.SaveChanges();
            }*/
            using (CADContext context = new CADContext())
            {
                try
                {
                    IAzure azure;
                    foreach (var apps in context.Applications)
                    {
                        try
                        {
                            azure = GetAzureConnection(apps);
                            var curVMList = azure.VirtualMachines.List();
                            VirtualMashinesList.UpdateVMList(curVMList, apps.AppId);
                             azure = null;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }


            }
        }

        public VMList GetVirtualMashinesList()
        {
            return VirtualMashinesList;
        }

        public IVirtualMachine GetVMFromAzure(string _vmId)
        {
            IVirtualMachine machine = null;
            using (CADContext context = new CADContext())
            {
                try
                {
                    IAzure azure;
                    VM vm = context.VMs.FirstOrDefault(v => v.VMId == _vmId);
                    Application apps = context.Applications.FirstOrDefault(app => app.AppId == vm.ApplicationId);
                    azure = GetAzureConnection(apps);
                    machine = azure.VirtualMachines.List().FirstOrDefault(v => v.VMId == _vmId);
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return machine;
        }

        public void StartVM(string _vmId)
        {
            var vm = GetVMFromAzure(_vmId);
            if (vm != null)
                vm.StartAsync();
        }

        public void StopVM(string _vmId)
        {
            var vm = GetVMFromAzure(_vmId);
            if (vm != null)
                vm.DeallocateAsync();
        }


        /// <summary>
        /// GetJOVirtualMachineAsync
        /// </summary>
        /// <param name="jobject"></param>
        /// <returns></returns>
        public async Task<JsonResult> GetJOVirtualMachineAsync(JObject jobject)
        {
            return await Task.Run(() => GetJOVirtualMachine(jobject));
        }

        /// <summary>
        /// GetJOVirtualMachine
        /// </summary>
        /// <param name="jobject"></param>
        /// <returns></returns>
        public JsonResult GetJOVirtualMachine(JObject jobject)
        {
            VM vm = new VM();
            using (CADContext context = new CADContext())
            {
                try
                {
                    IVirtualMachine virtualMachine = GetVMFromAzure(jobject["vmid"].ToString());
                    if (virtualMachine!= null)
                    {
                        vm = VirtualMashinesList.FindOrCreateVM(createLocalVMFromAzureVM(virtualMachine));
                    }
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return new JsonResult(new { data = vm });
        }

        /// <summary>
        /// createLocalVMFromAzureVM
        /// </summary>
        /// <param name="_virtualMachine"></param>
        /// <returns></returns>
        public VM createLocalVMFromAzureVM(IVirtualMachine _virtualMachine, int _appId = 0)
        {
            VM vm = new VM();
            vm.ApplicationId = _appId;
            vm.VMName = _virtualMachine.Name;
            vm.ResourceGroupName = _virtualMachine.ResourceGroupName;
            vm.VMSize = _virtualMachine.Size.ToString();

            string vmPowerState = _virtualMachine.PowerState?.ToString();
            if (!String.IsNullOrEmpty(vmPowerState))
            {
                if (vmPowerState.StartsWith("PowerState/"))
                {
                    vmPowerState = vmPowerState.Remove(0, "PowerState/".Length);
                }
                vm.PowerState = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(vmPowerState);
            }
            vmPowerState = null;
            string tagRows = "";
            foreach (var tag in _virtualMachine.Tags)
            {
                tagRows += "<div class='zoom'><span class='TagRow'>";
                tagRows += "<span class='Tag'>" + tag.Key.ToString();
                tagRows += "</span>";
                tagRows += "<span class='Value'>" + tag.Value.ToString();
                tagRows += "</span>";
                tagRows += "</span></div>";
            }

            vm.Tags = tagRows;
            tagRows = null;
            vm.ProvisioningState = _virtualMachine.ProvisioningState;
            vm.VMId = _virtualMachine.VMId;
            vm.SubscriptionId = _virtualMachine.Manager.SubscriptionId;
            _virtualMachine = null;
            return vm;
        }
        public VM GetVM(string _vmguid)
        {
            var vm = this.GetVMFromAzure(_vmguid);
            try
            {
                return this.createLocalVMFromAzureVM(vm);
            }
            catch (Exception ex)
            {
                throw new Exception( ex.ToString() + " " + vm.Id.ToString());
            }

        }
        /// <summary>
        /// GetVirtualMachinesByUserAsync
        /// </summary>
        /// <param name="_currentUser"></param>
        /// <returns></returns>
        public async Task<JsonResult> GetVirtualMachinesByUserAsync(ClaimsPrincipal _currentUser)
        {
            return await Task.Run(() => GetVirtualMachinesByUser(_currentUser));
        }

        /// <summary>
        /// GetVirtualMachinesByUser
        /// </summary>
        /// <param name="_currentUser"></param>
        /// <returns></returns>
        public JsonResult GetVirtualMachinesByUser(ClaimsPrincipal _currentUser)
        {
            List<VM> vMashines = new List<VM>();
            using (var context = new CADContext())
            {
                try
                {
                    User currentUser = context.Users
                        .Include(u => u.Dashboard)
                           .ThenInclude(d => d.DashboardApplications)
                        .FirstOrDefault(u => u.UserName == _currentUser.Identity.Name);
                    if (currentUser?.Dashboard == null) return new JsonResult(new { data = "" });

                    var dashboard = context.Dashboards.Include(d => d.DashboardApplications).FirstOrDefault(d => d.DashboardId == currentUser.Dashboard.DashboardId);
                    if (currentUser.Dashboard.DashboardId != 0)
                    {
                        vMashines = VirtualMashinesList.GetVMs(currentUser.Dashboard.DashboardId);
                    }
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return new JsonResult(new { data = vMashines });
        }

        /// <summary>
        /// GetVirtualMachinesByAccessCode
        /// </summary>
        /// <param name="_accessCode"></param>
        /// <returns></returns>
        public List<VM> GetVirtualMachinesByAccessCode(string _accessCode)
        {
            Dashboard dashboard;
            List<VM> vMashines = new List<VM>();
            using (var context = new CADContext())
            {
                try
                {
                    dashboard = context.Dashboards.Include(d => d.DashboardApplications).FirstOrDefault(d => d.DashboardAnonAccessCode == _accessCode);
                    vMashines = VirtualMashinesList.GetVMs(dashboard.DashboardId);
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return vMashines;
        }

        /// <summary>
        /// GetDashboardByAccessCode
        /// </summary>
        /// <param name="_accessCode"> _accessCode</param>
        /// <returns></returns>
        public Dashboard GetDashboardByAccessCode(string _accessCode)
        {
            Dashboard dashboard = new Dashboard();
            using (var context = new CADContext())
            {
                try
                {
                    dashboard = context.Dashboards.Include(d => d.DashboardApplications).FirstOrDefault(d => d.DashboardAnonAccessCode == _accessCode);
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return dashboard;
        }
    }


    public class VMList
    {
        private AzureHelper azureHelper;
        public VMList(AzureHelper _azureHelper)
        {
            azureHelper = _azureHelper;
        }

        public void UpdateVMList(IEnumerable<IVirtualMachine> _list, int _appId)
        {
            using (var context = new CADContext())
            {
                try
                {
                    foreach (var _vm in _list)
                    {
                        VM vm = new VM();
                        vm = azureHelper.createLocalVMFromAzureVM(_vm, _appId);
                        FindOrCreateVM(vm);
                        vm = null;
                    }
                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }

        }


        public void UpdateActiveVMs(int _dashboardId, int[] _VMList)
        {
            using (var context = new CADContext())
            {
                var activeList = context.ActiveVMs.Where(v => v.DashboardId == _dashboardId);

                foreach (var vm in activeList)
                {
                    context.ActiveVMs.Remove(vm);
                    context.SaveChanges();
                }
    
                foreach (int vmId in _VMList)
                {
                    AddActiveVM(_dashboardId, vmId);
                }
                activeList = null;

            }

        }


        public List<VM> GetVMs(int _dashboardId)
        {
            List<VM> list = new List<VM>();
            using (var context = new CADContext())
            {
                Dashboard dashboard = context.Dashboards.Include(d => d.DashboardApplications).AsNoTracking().FirstOrDefault(d => d.DashboardId == _dashboardId);
                switch (dashboard.DisplayType)
                {
                    case DisplayType.ShowAll:
                        {
                            list = GetVMsByDash(dashboard);
                            break;
                        }
                    case DisplayType.ExcludeType:
                        {
                            list = GetVMsByDash(dashboard, true);
                            break;
                        }
                    case DisplayType.SelectType:
                        {
                            list = GetActiveVMs(dashboard.DashboardId);
                            break;
                        }
                }
            }
            return list;
        }

        public List<VM> GetActiveVMs(int _dashboardId)
        {
            List<VM> list = new List<VM>();
            using (var context = new CADContext())
            {
                foreach (var activeVM in context.ActiveVMs.Where(iv => iv.DashboardId == _dashboardId))
                {
                    var vm = context.VMs.FirstOrDefault(v => v.VMId == activeVM.VMId);
                    if(vm != null)
                        list.Add(vm);
                }
            }
            return list;
        }

        public void AddActiveVM(int _dashboardId, string _vmId)
        {
            using (var context = new CADContext())
            {
                ActiveVM vm = context.ActiveVMs.FirstOrDefault(iv => iv.DashboardId == _dashboardId && iv.VMId == _vmId);
                if (vm == null)
                {
                    vm = new ActiveVM();
                    vm.DashboardId = _dashboardId;
                    vm.VMId = _vmId;
                    context.ActiveVMs.Add(vm);
                    context.SaveChanges();
                    vm = null;
                }
            }

        }

        public void AddActiveVM(int _dashboardId, int _vmId)
        {
            using (var context = new CADContext())
            {
                VM vm = context.VMs.FirstOrDefault(v => v.Id == _vmId);
                if (vm != null)
                {
                    AddActiveVM(_dashboardId, vm.VMId);
                }
            }
        }

        public void RemoveActiveVM(int _dashboardId, int _vmId)
        {
            using (var context = new CADContext())
            {
                VM vm = context.VMs.FirstOrDefault(v => v.Id == _vmId);
                if (vm != null)
                {
                    RemoveActiveVM(_dashboardId, vm.VMId);
                }
            }
        }

        public void RemoveActiveVM(int _dashboardId, string _vmId)
        {
            using (var context = new CADContext())
            {
                ActiveVM vm = context.ActiveVMs.FirstOrDefault(iv => iv.DashboardId == _dashboardId && iv.VMId == _vmId);
                if (vm != null)
                {
                    context.ActiveVMs.Remove(vm);
                    context.SaveChanges();
                }
            }
        }

        public List<VM> GetVMsByAppID(int _appId)
        {
            List<VM> list = new List<VM>();
            using (var context = new CADContext())
            {
                try
                {
                    list = context.VMs.Where(vms => vms.ApplicationId == _appId).ToList();
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }

            return list;
        }

        public List<VM> GetVMsByDash(Dashboard _dashboard, bool _exceptSelected = false)
        {
            List<VM> list = new List<VM>();
            foreach (var apps in _dashboard.DashboardApplications)
            {
                list.AddRange(GetVMsByAppID(apps.ApplicationId));
            }
            if (_exceptSelected)
            {
                List<VM> excludeActiveList = new List<VM>();
                foreach (VM vm in list)
                {
                    if (GetActiveVMs(_dashboard.DashboardId)?.Find(v => v.VMId == vm.VMId) == null)
                    {
                        excludeActiveList.Add(vm);
                    }
                }
                list = excludeActiveList;
            }
            return list;
        }

        public List<VM> GetByDashboardId(int _dashboardId, bool _exceptInactive = false)
        {
            List<VM> list = new List<VM>();
            Dashboard dashboard = new Dashboard();
            using (var context = new CADContext())
            {
                try
                {
                    dashboard = context.Dashboards.Include(vm => vm.DashboardApplications).FirstOrDefault(d => d.DashboardId == _dashboardId);

                    foreach (var app in dashboard.DashboardApplications)
                    {
                        list.AddRange(GetVMsByAppID(app.ApplicationId));
                    }
                    if (_exceptInactive)
                    {
                        List<VM> excludeInactiveList = new List<VM>();
                        foreach (VM vm in list)
                        {
                            if (GetVMs(_dashboardId).Find(v => v.VMId == vm.VMId) == null)
                            {
                                excludeInactiveList.Add(vm);
                            }
                        }
                        list = excludeInactiveList;
                    }
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return list;
        }

        public VM FindOrCreateVM(VM _azureVm)
        {
            VM vm = SelectVMByVmId(_azureVm.VMId);
            if (vm != null)
            {
                UpdateByVM(vm, _azureVm);
                return vm;
            }
            else
            {
                return CreateVMByVM(_azureVm);
            }
        }

        private VM CreateVMByVM(VM _vm)
        {
            VM vm = new VM();
            using (var context = new CADContext())
            {
                try
                {
                    context.VMs.Add(_vm);
                    vm = context.VMs.FirstOrDefault(v => v.VMId == _vm.VMId);
                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return vm;
        }

        private VM SelectVMByVmId(string _vmId)
        {
            VM vm = new VM();
            using (var context = new CADContext())
            {
                try
                {
                    vm = context.VMs.FirstOrDefault(v => v.VMId == _vmId);
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            return vm;
        }

        private void UpdateByVM(VM _vm, VM _azureVm)
        {
            VM vm = new VM();
            using (var context = new CADContext())
            {
                try
                {
                    _vm.PowerState = _azureVm.PowerState;
                    _vm.ProvisioningState = _azureVm.ProvisioningState;
                    _vm.ResourceGroupName = _azureVm.ResourceGroupName;
                    _vm.SubscriptionId = _azureVm.SubscriptionId;
                    _vm.Tags = _azureVm.Tags;
                    _vm.VMId = _azureVm.VMId;
                    _vm.VMName = _azureVm.VMName;
                    _vm.VMSize = _azureVm.VMSize;

                    context.VMs.Update(_vm);
                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Log log = new Log();
                    log.name = ex.ToString();
                    log.timestamp = DateTime.UtcNow;
                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
        }
    }

    public static class AzureHelperService
    {
        public static AzureHelper AzureHelper { get; set; }
    }
}
