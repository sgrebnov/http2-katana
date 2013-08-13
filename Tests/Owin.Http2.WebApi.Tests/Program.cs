using System;
using System.Net.Http;
using Microsoft.Owin.Hosting;

namespace Owin.Test.WebApiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://localhost:9000/";

            try
            {
                // Start OWIN host 
                using (WebApp.Start<Startup>(url: baseAddress))
                {
                    // Create HttpCient and make a request to api/values 
                    HttpClient client = new HttpClient();

                    var response = client.GetAsync(baseAddress + "api/values").Result;

                    Console.WriteLine(response);
                    Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                } 
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Error => {0} : {1}", 
                    ex.Message, 
                    (ex.InnerException != null) ? ex.InnerException.Message : String.Empty));
           
            }

            Console.ReadLine(); 
        } 
    }
}
