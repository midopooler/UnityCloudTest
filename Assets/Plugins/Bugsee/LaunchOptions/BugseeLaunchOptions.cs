using System;
using System.Collections.Generic;

namespace BugseePlugin
{
    /// <summary>
    /// Base class for launch options. Must not be used
    /// directly. Use either AndroidLaunchOptions or
    /// IOSLaunchOptions instead.
    /// </summary>
    public class BugseeLaunchOptions
    {
        private IDictionary<string, object> options;

        internal BugseeLaunchOptions()
        {
            this.options = new Dictionary<string, object>();
        }

        internal IDictionary<string, object> SerializeOptions()
        {
            return options;
        }

        public virtual void Reset()
        {
            this.options.Clear();
        }

        public void SetCustomOption(string optionKey, object optionValue)
        {
            // Add special handling for custom options here
            if (options.ContainsKey(optionKey))
            {
                options[optionKey] = optionValue;
            }
            else
            {
                options.Add(optionKey, optionValue);
            }
        }

        internal object this[string key]
        {
            get
            {
                if (options.ContainsKey(key))
                {
                    return options[key];
                }

                return null;
            }
            set
            {
                if (options.ContainsKey(key))
                {
                    options[key] = value;
                }
                else
                {
                    options.Add(key, value);
                }
            }
        }

        protected T GetValueAs<T>(string key)
        {
            if (options.ContainsKey(key))
            {
                try
                {
                    return (T)options[key];
                }
                catch (Exception) { }
            }

            return default(T);
        }
    }
}
