using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;

namespace GADataFetcherAppClass
{
    class Program
    {
        public class fullRequestBodyClass
        {
            public string email;
            public reportClass report;
        }

        public class reportClass
        {
            public reportRequestsClass[] reportRequests = new reportRequestsClass[1];
        }

        public class reportRequestsClass
        {
            public string viewId;
            public dateRangesClass[] dateRanges = new dateRangesClass[1];
            public List<metricsClass> metrics = new List<metricsClass>();
            public List<dimensionsClass> dimensions = new List<dimensionsClass>();
        }

        public class dateRangesClass
        {
            public string startDate { get; set; }
            public string endDate { get; set; }
        }
        public class metricsClass
        {
            public string expression { get; set; }
        }
        public class dimensionsClass
        {
            public string name { get; set; }
        }

        public class MetricHeaderEntry
        {
            public string name { get; set; }
            public string type { get; set; }
        }

        public class MetricHeader
        {
            public List<MetricHeaderEntry> metricHeaderEntries { get; set; }
        }

        public class ColumnHeader
        {
            public List<string> dimensions { get; set; }
            public MetricHeader metricHeader { get; set; }
        }

        public class Metric
        {
            public List<string> values { get; set; }
        }

        public class Row
        {
            public List<string> dimensions { get; set; }
            public List<Metric> metrics { get; set; }
        }

        public class Total
        {
            public List<string> values { get; set; }
        }

        public class Minimum
        {
            public List<string> values { get; set; }
        }

        public class Maximum
        {
            public List<string> values { get; set; }
        }

        public class Data
        {
            public List<Row> rows { get; set; }
            public List<Total> totals { get; set; }
            public int rowCount { get; set; }
            public List<Minimum> minimums { get; set; }
            public List<Maximum> maximums { get; set; }
        }

        public class Report
        {
            public ColumnHeader columnHeader { get; set; }
            public Data data { get; set; }
        }

        public class RootObject
        {
            public List<Report> reports { get; set; }
        }

        static void Main(string[] args)
        {

            {
                List<string> dimVal = composeDimVal();
                List<string> metricsVal = composeMetricsVal();

                List<string> emailIds = new List<string>();
                List<string> viewIds = new List<string>();

                String connectionString = ConfigurationManager.ConnectionStrings["DBConnection"].ConnectionString;
                string queryJson = "SELECT email,JSON_VALUE(ViewsInfo,'$[0].viewId') AS viewId FROM Integrations where Integrationtype='Google Analytics'";

                //opening the connection to Database
                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    //executing the sql query 
                    using (SqlCommand cmd = new SqlCommand(queryJson, con))
                    {
                        con.Open();
                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            // get the results of each column
                            emailIds.Add((string)reader["email"]);
                            viewIds.Add((string)reader["viewId"]);

                        }
                    }
                }

                for (int i = 0; i < emailIds.Count; i++)
                {
                    Console.WriteLine("========================================================================================");
                    Console.WriteLine(" Getiing Data for Email Id As " + emailIds[i]);
                    FilltheDatainObject(emailIds[i], viewIds[i], "60daysAgo", "yesterday", dimVal, metricsVal);
                    Console.WriteLine("===========================================================================================");
                }
            }


        }

        public static List<string> composeDimVal()
        {
            List<string> dimVal = new List<string>();
            dimVal.Add("ga:userType");
            dimVal.Add("ga:longitude");
            dimVal.Add("ga:latitude");
            dimVal.Add("ga:source");
            dimVal.Add("ga:date");
            dimVal.Add("ga:daysSinceLastSession");
            dimVal.Add("ga:deviceCategory");
            dimVal.Add("ga:language");
            dimVal.Add("ga:browser");

            return dimVal;
        }

        public static List<string> composeMetricsVal()
        {
            List<string> metricsVal = new List<string>();
            metricsVal.Add("ga:users");
            return metricsVal;
        }

        public static fullRequestBodyClass FilltheDatainObject(string emailId, string viewId, string startDate, string endDate,
                                                    List<string> dimVal, List<string> metricsVal)
        {
            reportRequestsClass reportRequestTempObject = new reportRequestsClass();

            for (int i = 0; i < dimVal.Count; ++i)
            {
                dimensionsClass dimTempobject = new dimensionsClass();
                dimTempobject.name = dimVal[i];
                reportRequestTempObject.dimensions.Add(dimTempobject);

            }

            for (int i = 0; i < metricsVal.Count; ++i)
            {
                metricsClass metricsTempobject = new metricsClass();
                metricsTempobject.expression = metricsVal[i];
                reportRequestTempObject.metrics.Add(metricsTempobject);
            }


            for (int i = 0; i < 1; ++i)
            {
                dateRangesClass dateRangesTempobject = new dateRangesClass();
                dateRangesTempobject.startDate = startDate;
                dateRangesTempobject.endDate = endDate;
                reportRequestTempObject.dateRanges[i] = dateRangesTempobject;
            }

            reportRequestTempObject.viewId = viewId;
            reportClass reportTempObject = new reportClass();
            reportTempObject.reportRequests[0] = reportRequestTempObject;

            fullRequestBodyClass fullRequestTempObject = new fullRequestBodyClass();
            fullRequestTempObject.email = emailId;
            fullRequestTempObject.report = reportTempObject;

            BuildRequestNInsertIntoDB(fullRequestTempObject);
            return fullRequestTempObject;
        }

        public static void InsertResponseToDB(string content)
        {
            try
            {
                RootObject items = JsonConvert.DeserializeObject<RootObject>(content);

                for (int reportCount = 0; reportCount < items.reports.Count; ++reportCount)
                {
                    for (int rowCount = 0; rowCount < items.reports[reportCount].data.rows.Count; ++rowCount)
                    {
                        for (int dimCount = 0; dimCount < items.reports[reportCount].data.rows[rowCount].dimensions.Count; ++dimCount)
                        {
                            Console.WriteLine(items.reports[reportCount].data.rows[rowCount].dimensions[dimCount]);

                        }
                    }

                }
            }
            catch ( Exception ex)
            {
                Console.WriteLine(" Something is wrong " + ex.Message);
            }
        }

        public static void BuildRequestNInsertIntoDB(fullRequestBodyClass fullRequest)
        {
            var client = new RestSharp.RestClient("https://apps.getpy.biz/googleanalytics");
            var request = new RestSharp.RestRequest("/", RestSharp.Method.POST);
            request.AddHeader("Content-type", "application/json");

            var json = JsonConvert.SerializeObject(fullRequest);
            request.AddParameter("application/json; charset=utf-8", json, RestSharp.ParameterType.RequestBody);
            //execute the request
            RestSharp.IRestResponse response = client.Execute(request);
            var content = response.Content; // raw content as string

            // Insert Received Data User Table
            InsertResponseToDB(content);
        }
    }
}
