﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TortoiseProc
{
    class Options
    {
        Dictionary<string, string> options;

        private Options() 
        {
            options = new Dictionary<string, string>();
        }

        public static Options ParseOptions(string[] args)
        {
            Options options = new Options();
            foreach (string arg in args)
            {
                if (arg.StartsWith("/"))
                {
                    string[] values = arg.Substring(1).Split(':');
                    options.options[values[0].ToLower()] = values.Length > 1 ? string.Join(":", values, 1, values.Length - 1) : null;
                }
            }
            return options;
        }
        public string GetValue(string name, string defaultValue)
        {
            string value;
            return options.TryGetValue(name, out value) ? value : defaultValue;
        }
        public bool IsPresent(string name) {
            return options.ContainsKey(name);
        }
        public string SetValue(string name, string value) {
            return options[name] = value;
        }
    }
}
