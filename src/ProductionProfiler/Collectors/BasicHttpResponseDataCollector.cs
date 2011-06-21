﻿
using System.Collections.Generic;
using System.Web;
using ProductionProfiler.Core.Profiling;
using ProductionProfiler.Core.Profiling.Entities;

namespace ProductionProfiler.Core.Collectors
{
    public class BasicHttpResponseDataCollector : IHttpResponseDataCollector
    {
        public List<DataCollection> Collect(HttpResponse response)
        {
            var data = new List<DataCollection>();

            var headers = new DataCollection("Response Headers", response.Headers);
            headers.Data.Add(new DataCollectionItem("StatusCode", response.StatusCode.ToString()));
            headers.Data.Add(new DataCollectionItem("Buffer", response.Buffer.ToString()));
            headers.Data.Add(new DataCollectionItem("Charset", response.Charset));

            if (HttpRuntime.UsingIntegratedPipeline && response.Headers.Count > 0)
            {
                foreach (string header in response.Headers.AllKeys)
                {
                    headers.Data.Add(new DataCollectionItem(header, response.Headers.Get(header)));
                }
            }

            if (HttpRuntime.UsingIntegratedPipeline && response.Cookies.Count > 0)
            {
                var cookies = new DataCollection("Response Cookies");
                foreach (HttpCookie cookie in response.Cookies)
                {
                    cookies.Data.Add(new DataCollectionItem(cookie.Name, cookie.Value));
                }
                data.Add(cookies);
            }

            data.Add(headers);
            return data;
        }
    }
}
