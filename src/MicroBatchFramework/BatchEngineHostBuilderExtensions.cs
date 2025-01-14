﻿using System.Linq;
using MicroBatchFramework.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.Text;

namespace MicroBatchFramework
{
    public static class BatchEngineHostBuilderExtensions
    {
        const string ListCommand = "list";
        const string HelpCommand = "help";

        public static IHostBuilder UseBatchEngine(this IHostBuilder hostBuilder, string[] args, IBatchInterceptor interceptor = null)
        {
            if (args.Length == 0 || (args.Length == 1 && args[0].Equals(ListCommand, StringComparison.OrdinalIgnoreCase)))
            {
                ShowMethodList();
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddOptions<ConsoleLifetimeOptions>().Configure(x => x.SuppressStatusMessages = true);
                    services.AddSingleton<IHostedService, EmptyHostedService>();
                });
                return hostBuilder;
            }
            if (args.Length == 2 && args[0].Equals(HelpCommand, StringComparison.OrdinalIgnoreCase))
            {
                var (t, mi) = GetTypeFromAssemblies(args[1]);
                if (mi != null)
                {
                    Console.WriteLine(BatchEngine.BuildHelpParameter(new[] { mi }));
                }
                else
                {
                    Console.WriteLine("Method not found , please check \"list\" command.");
                }
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddOptions<ConsoleLifetimeOptions>().Configure(x => x.SuppressStatusMessages = true);
                    services.AddSingleton<IHostedService, EmptyHostedService>();
                });
                return hostBuilder;
            }

            Type type = null;
            MethodInfo methodInfo = null;
            if (args.Length >= 1)
            {
                (type, methodInfo) = GetTypeFromAssemblies(args[0]);
            }

            hostBuilder = hostBuilder
                .ConfigureServices(services =>
                {
                    services.AddOptions<ConsoleLifetimeOptions>().Configure(x => x.SuppressStatusMessages = true);
                    services.AddSingleton<string[]>(args);
                    services.AddSingleton<IHostedService, BatchEngineService>();
                    services.AddSingleton<IBatchInterceptor>(interceptor ?? NullBatchInerceptor.Default);
                    if (type != null)
                    {
                        services.AddSingleton<Type>(type);
                        services.AddTransient(type);
                    }
                    else
                    {
                        services.AddSingleton<Type>(typeof(void));
                    }

                    if (methodInfo != null)
                    {
                        services.AddSingleton<MethodInfo>(methodInfo);
                    }
                });

            return hostBuilder.UseConsoleLifetime();
        }

        public static Task RunBatchEngineAsync(this IHostBuilder hostBuilder, string[] args, IBatchInterceptor interceptor = null)
        {
            return UseBatchEngine(hostBuilder, args, interceptor).Build().RunAsync();
        }

        public static IHostBuilder UseBatchEngine<T>(this IHostBuilder hostBuilder, string[] args, IBatchInterceptor interceptor = null)
            where T : BatchBase
        {
            var method = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var defaultMethod = method.FirstOrDefault(x => x.GetCustomAttribute<CommandAttribute>() == null);
            var hasList = method.Any(x => x.GetCustomAttribute<CommandAttribute>()?.EqualsAny(ListCommand) ?? false);
            var hasHelp = method.Any(x => x.GetCustomAttribute<CommandAttribute>()?.EqualsAny(HelpCommand) ?? false);

            if (args.Length == 0)
            {
                if (defaultMethod == null || (defaultMethod.GetParameters().Length != 0 && !defaultMethod.GetParameters().All(x => x.HasDefaultValue)))
                {
                    if (!hasHelp)
                    {
                        Console.WriteLine(BatchEngine.BuildHelpParameter(method));
                        hostBuilder.ConfigureServices(services =>
                        {
                            services.AddOptions<ConsoleLifetimeOptions>().Configure(x => x.SuppressStatusMessages = true);
                            services.AddSingleton<IHostedService, EmptyHostedService>();
                        });
                        return hostBuilder;
                    }
                    else
                    {
                        // override default Help
                        args = new string[] { "help" };
                    }
                }
            }

            if (!hasList && args.Length == 1 && args[0].Equals(ListCommand, StringComparison.OrdinalIgnoreCase))
            {
                ShowMethodList();
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddOptions<ConsoleLifetimeOptions>().Configure(x => x.SuppressStatusMessages = true);
                    services.AddSingleton<IHostedService, EmptyHostedService>();
                });
                return hostBuilder;
            }

            if (!hasHelp && args.Length == 1 && args[0].Equals(HelpCommand, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(BatchEngine.BuildHelpParameter(method));
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddOptions<ConsoleLifetimeOptions>().Configure(x => x.SuppressStatusMessages = true);
                    services.AddSingleton<IHostedService, EmptyHostedService>();
                });
                return hostBuilder;
            }

            hostBuilder = hostBuilder.ConfigureServices(services =>
            {
                services.AddOptions<ConsoleLifetimeOptions>().Configure(x => x.SuppressStatusMessages = true);
                services.AddSingleton<string[]>(args);
                services.AddSingleton<Type>(typeof(T));
                services.AddSingleton<IHostedService, BatchEngineService>();
                services.AddSingleton<IBatchInterceptor>(interceptor ?? NullBatchInerceptor.Default);
                services.AddTransient<T>();
            });

            return hostBuilder.UseConsoleLifetime();
        }

        public static Task RunBatchEngineAsync<T>(this IHostBuilder hostBuilder, string[] args, IBatchInterceptor interceptor = null)
            where T : BatchBase
        {
            return UseBatchEngine<T>(hostBuilder, args, interceptor).Build().RunAsync();
        }

        static void ShowMethodList()
        {
            Console.WriteLine("list of methods:");
            var list = GetBatchTypes();
            foreach (var item in list)
            {
                foreach (var item2 in item.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    Console.WriteLine(item.Name + "." + item2.Name);
                }
            }
        }

        static List<Type> GetBatchTypes()
        {
            List<Type> batchBaseTypes = new List<Type>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName.StartsWith("System") || asm.FullName.StartsWith("Microsoft.Extensions")) continue;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (var item in types)
                {
                    if (typeof(BatchBase).IsAssignableFrom(item) && item != typeof(BatchBase))
                    {
                        batchBaseTypes.Add(item);
                    }
                }
            }

            return batchBaseTypes;
        }

        static (Type, MethodInfo) GetTypeFromAssemblies(string arg0)
        {
            var batchBaseTypes = GetBatchTypes();
            if (batchBaseTypes == null)
            {
                return (null, null);
            }

            var split = arg0.Split('.');
            Type foundType = null;
            MethodInfo foundMethod = null;
            foreach (var baseType in batchBaseTypes)
            {
                bool isFound = false;
                foreach (var (method, cmdattr) in baseType.GetMethods().
                    Select(m => (MethodInfo: m, Attr: m.GetCustomAttribute<CommandAttribute>())).Where(x => x.Attr != null))
                {
                    if (cmdattr.CommandNames.Any(x => arg0.Equals(x, StringComparison.OrdinalIgnoreCase)))
                    {
                        if(foundType != null && foundMethod != null)
                        {
                            throw new InvalidOperationException($"Duplicate BatchBase Command name is not allowed, {foundType.FullName}.{foundMethod.Name} and {baseType.FullName}.{method.Name}");
                        }
                        foundType = baseType;
                        foundMethod = method;
                        isFound = true;
                    }
                }
                if (!isFound && split.Length == 2)
                {
                    if (baseType.Name.Equals(split[0], StringComparison.OrdinalIgnoreCase))
                    {
                        if (foundType != null)
                        {
                            throw new InvalidOperationException("Duplicate BatchBase TypeName is not allowed, " + foundType.FullName + " and " + baseType.FullName);
                        }
                        foundType = baseType;
                        foundMethod = baseType.GetMethod(split[1]);
                    }
                }
            }
            if(foundType != null && foundMethod != null)
            {
                return (foundType, foundMethod);
            }
            return (null, null);

        }
    }
}