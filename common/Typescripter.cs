using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;

namespace common
{
    public static class Collector {
        private static Options _options { get; set; }

        public static void Run(string optionsPath)
        {
            _options = JsonConvert.DeserializeObject<Options>(File.ReadAllText(optionsPath));

            var implementingModels = new HashSet<Type>(GetModelsFromSource(_options.Source).SelectMany(GetModelsFromImplementingType));
            Console.WriteLine("Found {0} Source Types", implementingModels.Count);

            var allModels = GetAllModelsToGenerate(implementingModels);
            Console.WriteLine("Generating {0} Models", allModels.Count);
            var generator = new JSchemaGenerator();
            var schema = new HashSet<JSchema>( allModels.Select(generator.Generate));

            File.WriteAllText(_options.Destination, JsonConvert.SerializeObject(schema));
        }

        public static Type GetPropertyType(this PropertyInfo pi)
        {
            if (pi.PropertyType.IsGenericType)
            {
                return pi.PropertyType.GetGenericArguments()[0];
            }
            return pi.PropertyType;
        }

        private static IEnumerable<Type> GetImplementingTypes(Assembly a)
        {
            return TryGetImplementingTypes(a).Where(t => t.BaseType != null && _options.CollectionTypeNames.Contains(t.BaseType.Name));
        }
        private static IEnumerable<Type> TryGetImplementingTypes(Assembly a)
        {
            try
            {
                Console.WriteLine($"Assembly {0} Models", a.FullName);
                Console.WriteLine(String.Join(", ", a.GetTypes().Select(t => t.Name).ToList()));
                return a.GetTypes();
            }
            catch { return Enumerable.Empty<Type>(); }
        }

        private static List<Type> GetModelsFromSource(string rootPath)
        {
            var dlls = _options.Dlls.SelectMany(f => Directory.GetFiles(rootPath, f));// Get dll files from options file list.
            Console.WriteLine($"dlls found {0}", dlls.Count());
            Console.WriteLine(String.Join(", ", dlls));
            Console.WriteLine(_options.Source);
            var assemblies = dlls.Select(Load).Where(a => a != null);// Load the assemblies so we can reflect on them
            Console.WriteLine(assemblies.Count());
            return assemblies.SelectMany(GetImplementingTypes).ToList();// Find all Types that inherit from Implementing types.
        }

        private static Assembly Load(string path)
        {
            try { return Assembly.LoadFile(path); }
            catch { return null; }
        }

        public static bool IsModelType(this Type t)
        {
            if (!t.IsClass || t.Namespace == null || t == typeof(string))
            {
                return false;
            }

            bool isModel = t.FullName != null && !t.FullName.StartsWith("System.") && !t.FullName.StartsWith("Microsoft.");
            return isModel;
        }

        private static HashSet<Type> GetAllModelsToGenerate(HashSet<Type> models)
        {
            var allModels = new HashSet<Type>(GetDerivedTypes(models));// Add derived types

            // Now recursively search the all models for child models
            foreach (var model in allModels.ToArray())
            {
                RecursivelySearchModels(model, allModels);
            }
            return allModels;
        }

        private static HashSet<Type> GetDerivedTypes(HashSet<Type> types)
        {
            var typesList = types;
            var allTypes = new HashSet<Type>(typesList);
            foreach (var type in typesList)
            {
                allTypes.UnionWith(type.Assembly.GetTypes().Where(t => t != type && type.IsAssignableFrom(t)));
            }
            return allTypes;
        }

        private static IEnumerable<Type> GetModelsFromImplementingType(Type controllerType)
        {
            var methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var models = methods.SelectMany(GetModelsFromMethod);
            return models;
        }

        private static IEnumerable<Type> GetModelsFromMethod(MethodInfo arg)
        {
            return GetModelTypes(arg.ReturnType)
                .Union(arg.GetParameters()
                    .Where(p => !HasAttributeNamed(p, "FromUriAttribute")) // All FromUri parameters will be expanded later in the DataServiceGenerator
                    .Select(p => p.ParameterType)
                    .SelectMany(GetModelTypes)
                );
        }

        private static bool HasAttributeNamed(ParameterInfo parameter, string attributeName)
        {
            var attribs = parameter.GetCustomAttributes(inherit: false);
            return attribs.Length > 0 && attribs.Any(a => a.GetType().Name == attributeName);
        }

        private static IEnumerable<Type> GetModelTypes(Type t)
        {
            if (t.GetCustomAttributes().Any(x => x.GetType().Name == "TypeScripterIgnoreAttribute")
                || t == typeof(Task))
            {
                yield break;
            }

            if (t.IsModelType() || t.IsEnum)
            {
                if (t.IsArray)
                {
                    yield return t.GetElementType();
                }
                else
                {
                    yield return t;
                }
            }
            else if (t.IsGenericType)
            {
                foreach (var a in t.GetGenericArguments().Where(a => a.IsModelType() || a.IsGenericType).SelectMany(GetModelTypes))
                {
                    yield return a;
                }
            }

            if (t.BaseType != null && t.BaseType.IsModelType())
            {
                yield return t.BaseType;
            }
        }

        private static void RecursivelySearchModels(Type model, HashSet<Type> visitedModels)
        {
            var props = model
                .GetProperties()
                .Select(p => p.GetPropertyType()).SelectMany(GetModelTypes).Where(t => !visitedModels.Contains(t) && t.IsModelType());
            foreach (var p in props)
            {
                visitedModels.Add(p);
                RecursivelySearchModels(p, visitedModels);
            }
        }
    }
}
