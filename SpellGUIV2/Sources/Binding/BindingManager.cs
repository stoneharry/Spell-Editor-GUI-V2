using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SpellEditor.Sources.Binding
{
    public class BindingManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static BindingManager _instance = new BindingManager();
        private List<Binding> _bindings;
        
        private BindingManager()
        {
            var bindingList = new List<Binding>();
            foreach (string fileName in Directory.GetFiles(Config.Config.BindingsDirectory + "\\", "*.txt"))
            {
                var bindingEntryList = new List<BindingEntry>();
                var orderOutput = true;
                foreach (string line in File.ReadAllLines(fileName))
                {
                    // Skip comments
                    if (line.StartsWith("#"))
                        continue;
                    string[] parts = line.Split(' ');
                    // try to read first two words
                    if (parts.Length < 2)
                        continue;
                    var entry = new BindingEntry(parts);
                    if (entry.Type == BindingType.IGNORE_ORDER)
                        orderOutput = false;
                    else if (entry.Type != BindingType.UNKNOWN)
                        bindingEntryList.Add(entry);
                }
                if (bindingEntryList.Count == 0)
                    continue;
                var binding = new Binding(fileName, bindingEntryList, orderOutput);
                bindingList.Add(binding);
                Logger.Info($"Loaded binding {fileName} with {bindingEntryList.Count} fields.");
            }
            _bindings = bindingList;
            Logger.Info($"Loaded {bindingList.Count} bindings.");
        }

        public Binding[] GetAllBindings()
        {
            return _bindings.ToArray();
        }

        public static BindingManager GetInstance()
        {
            return _instance;
        }

        public Binding FindBinding(string name) => _bindings.FirstOrDefault(entry => entry.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
    }
}
