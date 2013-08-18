using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace Owin.Test.WebApiTest
{
    public class ValuesController : ApiController
    {
        //static Dictionary<int, string> _store = new Dictionary<int, string>();

        //public ValuesController()
        //{
        //    if (_store == null)
        //    {
        //        _store = new Dictionary<int, string>();
        //        Put(0, "value#0");
        //        Put(1, "value#1");
        //    }
        //}

        // GET api/values 
        public IEnumerable<string> Get()
        {
            //return _store.Values.ToArray();

            return new string[]{"value", "value"};
        }

        // GET api/values/5 
        public string Get(int id)
        {
            //if (_store.ContainsKey(id))
            //{
            //    return _store[id];
            //}

            //return "Item does not exist";

            return "value";
        }

        // POST api/values 
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5 
        public void Put(int id, [FromBody]string value)
        {
            //if (_store.ContainsKey(id))
            //{
            //    _store[id] = value;
            //}
            //else
            //{
            //    _store.Add(id, value);
            //}
        }

        // DELETE api/values/5 
        public void Delete(int id)
        {
        }
    } 

}
