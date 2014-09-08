using System;
using System.Collections.Generic;
using NewRelic.Platform.Sdk;
using System.Management;

namespace newrelic_perfmon_plugin
{
    class PerfmonAgent : Agent
    {
        public override string Guid { get { return "com.automatedops.perfmom_plugin"; } }
        public override string Version { get { return "0.0.1"; } }

        private string Name { get; set; }
        private List<Object> Counters { get; set; }
        private ManagementScope ManagementScopt { get; set; }

        public PerfmonAgent(string name, List<Object> paths)
        {
            Name = name;
            Counters = paths;
            ManagementScopt = new ManagementScope("\\\\" + Name + "\\root\\cimv2");      
        }

        public override string GetAgentName()
        {
            return Name;
        }

        public override void PollCycle()
        {
            foreach (Dictionary<string, Object> counter in Counters)
            {

                string providerName = counter["provider"].ToString();
                string categoryName = counter["category"].ToString();
                string counterName = counter["counter"].ToString();
                string predicate = string.Empty;
                if (counter.ContainsKey("instance"))
                {
                    predicate = string.Format(" Where Name = '{0}'", counter["instance"]);
                }
                string unitValue = counter["unit"].ToString();

                string queryString = string.Format("Select Name, {2} from Win32_PerfFormattedData_{0}_{1}{3}", providerName, categoryName, counterName, predicate);
                
                ManagementObjectSearcher search = new ManagementObjectSearcher(ManagementScopt, new ObjectQuery(queryString));

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

                            string metricName = string.Format("{0}/{1}{3}/{2}", providerName, categoryName, counterName, instanceName);

                            Console.WriteLine("{0}/{1}: {2} {3}", Name, metricName, value, unitValue);

                            ReportMetric(metricName, unitValue, value);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Exception occurred in processing results.\n", e.Message, e.StackTrace);

                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception occurred in polling.\n", e.Message, e.StackTrace);
                }              
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
