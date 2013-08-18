using System;
using Microsoft.Owin.Hosting;
using Owin.Test.WebApiTest;

namespace Http2.Server.Owin.WebApi.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            string serverAddress = "https://localhost:8443/";
            

            try
            {
                // Start OWIN host 
                using (WebApp.Start<Startup>(new StartOptions(serverAddress)))
                {
                    // Create HttpCient and make a request to api/values 
                    //HttpClient client = new HttpClient();

                    //var response = client.GetAsync(baseAddress + "api/values").Result;

                    //Console.WriteLine(response);
                    //Console.WriteLine(response.Content.ReadAsStringAsync().Result);
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
