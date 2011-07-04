﻿using System.Configuration;
using System.IO;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Routing;
using Castle.Facilities.FactorySupport;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Releasers;
using Castle.Windsor;
using Castle.Windsor.Configuration.Interpreters;
using log4net.Config;
using ProductionProfiler.Core.Caching;
using ProductionProfiler.Core.Collectors;
using ProductionProfiler.Core.Configuration;
using ProductionProfiler.IoC.StructureMap;
using ProductionProfiler.IoC.Windsor;
using ProductionProfiler.Logging.Log4Net;
using ProductionProfiler.Persistence.Sql;
using ProductionProfiler.Web.Controllers;
using ProductionProfiler.Web.Models;
using ProductionProfiler.Web.Profilng;
using ProductionProfiler.Core.Extensions;
using StructureMap;

namespace ProductionProfiler.Web
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication, IContainerAccessor
    {
        private static IWindsorContainer _container;

        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{folder}/{*pathInfo}", new { folder = "profiler" });

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);

            XmlConfigurator.ConfigureAndWatch(new FileInfo(Server.MapPath("~/bin/Config/log4net.config")));

            Core.IoC.IContainer container;

            if (ConfigurationManager.AppSettings["IoCContainer"].ToLowerInvariant() == "castle")
            {
                try
                {
                    _container = new WindsorContainer(new XmlInterpreter());
                    _container.AddFacility<FactorySupportFacility>();
                    _container.Kernel.ReleasePolicy = new NoTrackingReleasePolicy();
                }
                catch (ConfigurationErrorsException ex)
                {
                    if (ex.Message == "Could not find section 'castle' in the configuration file associated with this domain.")
                        _container = new WindsorContainer();
                    else
                        throw;
                }

                ControllerBuilder.Current.SetControllerFactory(typeof(WindsorControllerFactory));
                RegisterCastleDependencies();
                container = new WindsorProfilerContainer(_container);
            }
            else
            {
                ControllerBuilder.Current.SetControllerFactory(typeof(StructureMapControllerFactory));
                RegisterStructureMapDependencies();
                container = new StructureMapProfilerContainer(ObjectFactory.Container);
            }

            //set up profiler
            Configure.With(container)
                .HandleExceptionsVia(e => System.Diagnostics.Trace.Write(e.Format()))
                .Logger(new Log4NetLogger())
                .DataProvider(new SqlPersistenceProvider(new SqlConfiguration("profiler-sqlite", "profiler", "Profiler")))
                .HttpRequestDataCollector<BasicHttpRequestDataCollector>()
                .HttpResponseDataCollector<BasicHttpResponseDataCollector>()
                .CollectInputOutputMethodDataForTypes(new[] { typeof(IWorkflow) })
                .AddMethodDataCollector<WorkflowMethodDataCollector>()
                    .ForTypesAssignableFrom(new []{typeof(IWorkflow)})
                .CacheEngine<HttpRuntimeCacheEngine>()
                .RequestFilter(req => Path.GetExtension(req.Url.AbsolutePath) == string.Empty)
                .CaptureExceptions()
                .CaptureResponse()
                //.EnableMonitoring(5000, 3000)
                .Initialise();
        }

        public IWindsorContainer Container
        {
            get { return _container; }
        }

        private static void RegisterStructureMapDependencies()
        {
            ObjectFactory.Configure(c => c.Scan(a =>
            {
                a.TheCallingAssembly();
                a.With(new DerivedOpenGenericInterfaceConnectionScanner(typeof(IWorkflow<,>)));
            }));

            ObjectFactory.Configure(c => c.Scan(a =>
            {
                a.TheCallingAssembly();
                a.AddAllTypesOf(typeof(IController));
            }));
        }

        private static void RegisterCastleDependencies()
        {
            _container.Register(
                AllTypes.FromAssembly(Assembly.GetExecutingAssembly())
                    .BasedOn<IController>()
                    .Configure(c => c.LifeStyle.Transient));

            _container.Register(AllTypes.FromAssembly(Assembly.GetExecutingAssembly())
                .BasedOn(typeof(IWorkflow<,>))
                    .WithService
                    .FromInterface(typeof(IWorkflow<,>))
                .If(t => true)
                .Configure(c => c.LifeStyle.Transient));
        }
    }
}