﻿using System;
using System.Collections.Generic;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using ProductionProfiler.Core.IoC;

namespace ProductionProfiler.IoC.Windsor
{
    public class WindsorProfilerContainer : IContainer
    {
        private readonly IWindsorContainer _container;

        public WindsorProfilerContainer(IWindsorContainer container)
        {
            _container = container;
        }

        public void RegisterTransient<T>(Type implementation, string name = null) where T : class
        {
            _container.Register(Component.For<T>()
                .LifeStyle.Transient
                .ConditionalName(name)
                .ImplementedBy(implementation));
        }

        public void RegisterSingleton<T>(Type implementation, string name = null) where T : class
        {
            _container.Register(Component.For<T>()
                .LifeStyle.Singleton
                .ConditionalName(name)
                .ImplementedBy(implementation));
        }

        public void RegisterPerWebRequest<T>(Type implementation, string name = null) where T : class
        {
            _container.Register(Component.For<T>()
                .LifeStyle.PerWebRequest //GT: changed this from contrib Hybrid PerWebRequest/PerThread
                .ConditionalName(name)
                .ImplementedBy(implementation));
        }

        public void RegisterSingletonInstance<T>(T instance) where T : class
        {
            _container.Register(Component.For<T>()
                .LifeStyle.Singleton
                .Instance(instance));
        }

        public T Resolve<T>()
        {
            return _container.Resolve<T>();
        }

        public T Resolve<T>(string key)
        {
            return _container.Resolve<T>(key);
        }

        public T[] ResolveAll<T>()
        {
            return _container.ResolveAll<T>();
        }

        public bool HasObject(Type type)
        {
            return _container.Kernel.HasComponent(type);
        }

        public void InitialiseForProxyInterception(IEnumerable<Type> typesToIntercept, IEnumerable<Type> typesToIgnore)
        {
            RegisterTransient<RequestProfilingInterceptor>(typeof(RequestProfilingInterceptor));
            _container.Kernel.ProxyFactory.AddInterceptorSelector(new ProfilingInterceptorSelector(typesToIntercept, typesToIgnore));
        }
    }

    public static class ContainerExtensions
    {
        public static ComponentRegistration<T> ConditionalName<T>(this ComponentRegistration<T> registration, string name) where T : class
        {
            if (name != null)
                registration.Named(name);

            return registration;
        }
    }
}
