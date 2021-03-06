﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace RadiumRest.Core.ResourceRepo
{
    internal class ResourceRepository
    {

        internal static ResourceRepository Repo;

        private static Dictionary<string, Dictionary<string, PathExecutionInfo>> pathExecutionInfo = new Dictionary<string, Dictionary<string, PathExecutionInfo>>();

        internal PathExecutionParams this[string method, string reqUrl]
        {
            get
            {
                return GetExecutionParams(method, reqUrl);
            }
        }

        private PathExecutionParams GetExecutionParams(string method, string reqUrl)
        {
            PathExecutionParams executionParams = null;
            bool isFound = false;
            Dictionary<string, string> variables = null;
            PathExecutionInfo executionInfo = null;

            string[] urlSplit = reqUrl.Split('/');

            if (pathExecutionInfo.ContainsKey(method))
            {

                variables = new Dictionary<string, string>();

                foreach (KeyValuePair<string, PathExecutionInfo> onePath in pathExecutionInfo[method])
                {
                    string[] definedPathSplit = onePath.Key.Split('/');

                    if (definedPathSplit.Length == urlSplit.Length)
                    {
                        variables.Clear();
                        isFound = true;

                        for (int i = 0; i < definedPathSplit.Length; i++)
                        {
                            if (definedPathSplit[i].StartsWith("@"))
                                variables.Add(definedPathSplit[i].Substring(1), urlSplit[i]);
                            else
                            {
                                if (definedPathSplit[i] != urlSplit[i])
                                {
                                    isFound = false;
                                    break;
                                }
                            }
                        }

                    }

                    if (isFound)
                    {
                        executionInfo = onePath.Value;
                        break;
                    }
                }
            }

            if (isFound)
            {
                executionParams = new PathExecutionParams
                {
                    ExecutionInfo = executionInfo,
                    Parameters = variables
                };
            }

            return executionParams;
        }



        internal static void AddExecutionInfo(string method, string reqUrl, PathExecutionInfo value)
        {
            Dictionary<string, PathExecutionInfo> methodDic;

            if (!pathExecutionInfo.ContainsKey(method))
            {
                methodDic = new Dictionary<string, PathExecutionInfo>();
                pathExecutionInfo.Add(method, methodDic);
            }
            else methodDic = pathExecutionInfo[method];

            if (!methodDic.ContainsKey(reqUrl))
                methodDic.Add(reqUrl, value);
        }


        internal static void Initialize(Assembly callingAssembly)
        {
            Repo = new ResourceRepository();

            var ignoreAssemblies = new string[] {"RadiumRest", "RadiumRest.Core", "RadiumRest.Selfhost", "mscorlib"};
            var referencedAssemblies = callingAssembly.GetReferencedAssemblies();
            var currentAsm = Assembly.GetExecutingAssembly().GetName();

            var scanAssemblies = new List<AssemblyName>() { callingAssembly.GetName()};

            foreach (var asm in referencedAssemblies)
            {
                if (asm == currentAsm)
                    continue;

                if (!ignoreAssemblies.Contains(asm.Name))
                    scanAssemblies.Add(asm);
            }

            foreach (var refAsm in scanAssemblies)
            {
                try
                {
                    var asm = Assembly.Load(refAsm.FullName);


                    foreach (var typ in asm.GetTypes())
                    {
                        if (typ.IsSubclassOf(typeof(RestResourceHandler)))
                        {
                            var classAttribObj = typ.GetCustomAttributes(typeof(RestResource), false).FirstOrDefault();
                            string baseUrl;
                            if (classAttribObj != null)
                            {
                                var classAttrib = (RestResource)classAttribObj;
                                baseUrl = classAttrib.Path;
                                baseUrl = baseUrl.StartsWith("/") ? baseUrl : "/" + baseUrl;
                            }
                            else baseUrl = "";

                            var methods = typ.GetMethods();


                            foreach (var method in methods)
                            {
                                var methodAttribObject = method.GetCustomAttributes(typeof(RestPath), false).FirstOrDefault();

                                if (methodAttribObject != null)
                                {
                                    var methodAttrib = (RestPath)methodAttribObject;
                                    string finalUrl = baseUrl + (methodAttrib.Path ?? "");
                                    
                                    var finalMethod = methodAttrib.Method;

                                    PathExecutionInfo exeInfo = new PathExecutionInfo
                                    {
                                        Type = typ,
                                        Method = method
                                    };
                                    AddExecutionInfo(finalMethod, finalUrl, exeInfo);
                                }
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
