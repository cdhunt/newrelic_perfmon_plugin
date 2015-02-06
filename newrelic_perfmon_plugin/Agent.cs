using System;
using System.Collections.Generic;
using NewRelic.Platform.Sdk;
using NewRelic.Platform.Sdk.Utils;
using System.Management;
using System.Configuration;

namespace newrelic_perfmon_plugin
{
    class PerfmonAgent : Agent
    {
        private static string DefaultGuid = "com.automatedops.perfmon_plugin";

        public override string Guid { get {
            if (ConfigurationManager.AppSettings.HasKeys())
            {
                if (! string.IsNullOrEmpty(ConfigurationManager.AppSettings["guid"].ToString()))
                {
                    return ConfigurationManager.AppSettings["guid"].ToString();
                }
            }
            return DefaultGuid;
        } }
        
        public override string Version { get 
            {
                string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                return version; 
            } 
        }

        private string Name { get; set; }
        private List<Object> Counters { get; set; }
        private ManagementScope Scope { get; set; }

        private static Logger logger = Logger.GetLogger("newrelic_perfmon_plugin");

        public PerfmonAgent(string name, List<Object> paths)
        {
            Name = name;
            Counters = paths;
            Scope = new ManagementScope("\\\\" + Name + "\\root\\cimv2");
        }

        public override string GetAgentName()
        {
            return Name;
        }

        public override void PollCycle()
        {
            try
            {
                Scope.Connect();            
                var metricNames = new Dictionary<string, int>();

                foreach (Dictionary<string, Object> counter in Counters)
                {

                    string providerName = counter["provider"].ToString();
                    string categoryName = counter["category"].ToString();
                    string counterName = counter["counter"].ToString();
                    string predicate = string.Empty;
                    if (counter.ContainsKey("instance"))
                    {
                        predicate = string.Format(" Where Name Like '{0}'", counter["instance"]);
                    }
                    string unitValue = counter["unit"].ToString();

                    string queryString = string.Format("Select Name, {2} from Win32_PerfFormattedData_{0}_{1}{3}", providerName, categoryName, counterName, predicate);
                
                    ManagementObjectSearcher search = new ManagementObjectSearcher(Scope, new ObjectQuery(queryString));

                    try
                    {
                        ManagementObjectCollection queryResults = search.Get();


                        foreach (ManagementObject result in queryResults)
                        {
                            try
                            {
                                float value = Convert.ToSingle(result[counterName]);
                                string instanceName = string.Empty;

                                if (result["Name"] != null)
                                {
                                    instanceName = string.Format("({0})", result["Name"]);
                                }

                                string metricName = string.Format("{0}{2}/{1}", categoryName, counterName, instanceName);

                                if (metricNames.ContainsKey(metricName))
                                {
                                    metricName = metricName + "#" + metricNames[metricName]++;
                                }
                                else
                                {
                                    metricNames.Add(metricName, 1);
                                }

                                logger.Debug("{0}/{1}: {2} {3}", Name, metricName, value, unitValue);

                                ReportMetric(metricName, unitValue, value);
                            }
                            catch (Exception e)
                            {
                                logger.Error("Exception occurred in processing results. {0}\r\n{1}", e.Message, e.StackTrace);
                            }
                        }
                    }
                    catch (ManagementException e)
                    {
                        logger.Error("Exception occurred in polling. {0}\r\n{1}", e.Message, queryString);
                    }
                    catch (Exception e)
                    {
                        logger.Error("Unable to connect to \"{0}\". {1}", Name, e.Message);
                    }     
                } 
         
            }    
            catch (Exception e)
            {
                logger.Error("Unable to connect to \"{0}\". {1}", Name, e.Message);
            }
        }
    }

    class PerfmonAgentFactory : AgentFactory
    {
        public override Agent CreateAgentWithConfiguration(IDictionary<string, object> properties)
        {
            string name = (string)properties["name"];
            List<Object> counterlist = (List<Object>)properties["counterlist"];

            if (counterlist.Count == 0)
            {
                throw new Exception("'counterlist' is empty. Do you have a 'config/plugin.json' file?");
            }

            return new PerfmonAgent(name, counterlist);
        }
    }
}
