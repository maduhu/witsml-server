﻿//----------------------------------------------------------------------- 
// PDS.Witsml.Server, 2017.1
//
// Copyright 2017 Petrotechnical Data Systems
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//   
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using log4net.Config;
using Hangfire.Mongo;
using PDS.Framework;
using PDS.Framework.Web;
using PDS.Witsml.Server;
using PDS.Witsml.Server.Configuration;
using PDS.Witsml.Server.Data;
using PDS.Witsml.Server.Jobs.Configuration;
using PDS.Witsml.Web.Controllers;

namespace PDS.Witsml.Web
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            System.Web.Http.GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            XmlConfigurator.ConfigureAndWatch(new FileInfo(Server.MapPath("~/log4net.config")));
            ContainerConfiguration.Register(Server.MapPath("~/bin"));

            if (string.IsNullOrWhiteSpace(WitsmlSettings.OverrideServerVersion))
                WitsmlSettings.OverrideServerVersion = typeof(EtpController).GetAssemblyVersion();

            var container = (IContainer)System.Web.Http.GlobalConfiguration.Configuration.DependencyResolver.GetService(typeof(IContainer));
            var databaseProvider = container.Resolve<IDatabaseProvider>();

            // pre-init IWitsmlStore dependencies
            var store = container.Resolve<IWitsmlStore>();
            store.WMLS_GetCap(new WMLS_GetCapRequest(OptionsIn.DataVersion.Version141));

            Task.Run(async () =>
            {
                // Wait before initializing Hangfire to give server time to warm up
                await Task.Delay(WitsmlSettings.ChangeDetectionPeriod * 1000);

                // Configure and register Hangfire jobs
                Hangfire.GlobalConfiguration.Configuration.UseMongoStorage(databaseProvider.ConnectionString, databaseProvider.DatabaseName);
                HangfireConfig.Register(container);
            });
        }

        protected void Application_End(object sender, EventArgs e)
        {
            HangfireConfig.Unregister();
        }
    }
}